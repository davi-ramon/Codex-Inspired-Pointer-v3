using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexInspiredPointer
{
    internal struct PointNative { public int X; public int Y; }

    internal static class Win32
    {
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out PointNative point);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadImage(IntPtr instance, string file, uint type,
                                               int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern bool SetSystemCursor(IntPtr cursor, uint id);
        [DllImport("user32.dll")] internal static extern bool DestroyCursor(IntPtr cursor);
        [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] internal static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] internal static extern bool SystemParametersInfo(uint action,
                                              uint parameter, IntPtr value, uint flags);
    }

    public sealed class Motion : ApplicationContext
    {
        private readonly string root;
        private readonly System.Windows.Forms.Timer timer;
        private readonly NotifyIcon tray;
        private readonly ToolStripMenuItem pauseItem;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private PointNative lastPoint;
        private double lastSeconds;
        private double angle;
        private double angleVelocity;
        private double scale = 1.0;
        private double scaleVelocity;
        private string lastArrowFile = "";
        private string lastHandFile = "";
        private bool paused;
        private bool schemeActive;
        private double lastSchemeCheck = -1;
        private int failures;
        private readonly Mutex mutex;

        private Motion(string root, Mutex mutex)
        {
            this.root = root;
            this.mutex = mutex;
            Win32.GetCursorPos(out lastPoint);
            var menu = new ContextMenuStrip();
            pauseItem = new ToolStripMenuItem("Pausar movimento");
            pauseItem.Click += delegate {
                paused = !paused;
                pauseItem.Text = paused ? "Retomar movimento" : "Pausar movimento";
                if (paused) RestoreAssignedCursors();
                else { lastArrowFile = ""; lastHandFile = ""; }
            };
            var exitItem = new ToolStripMenuItem("Sair");
            exitItem.Click += delegate { ExitThread(); };
            menu.Items.Add(pauseItem); menu.Items.Add(exitItem);
            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "Codex-inspired Pointer 2";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 16;
            timer.Tick += OnTick;
            timer.Start();
        }

        public static void Run(string root)
        {
            bool created;
            var mutex = new Mutex(true, @"Local\CodexInspiredPointerMotionV2", out created);
            if (!created) { mutex.Dispose(); return; }
            try { Application.Run(new Motion(root, mutex)); }
            finally { /* Motion.ExitThreadCore owns the mutex on normal exit. */ }
        }

        private static double Clamp(double x, double lo, double hi)
        {
            return Math.Max(lo, Math.Min(hi, x));
        }

        private static void RestoreAssignedCursors()
        {
            Win32.SystemParametersInfo(0x57, 0, IntPtr.Zero, 0);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (File.Exists(Path.Combine(root, "stop.flag"))) { ExitThread(); return; }
            if (paused) return;
            double now = clock.Elapsed.TotalSeconds;
            if (now - lastSchemeCheck > .4)
            {
                lastSchemeCheck = now;
                bool active = IsOurScheme();
                if (schemeActive && !active)
                {
                    RestoreAssignedCursors();
                    lastArrowFile = "";
                    lastHandFile = "";
                }
                schemeActive = active;
            }
            if (!schemeActive) return;
            PointNative point;
            if (!Win32.GetCursorPos(out point)) return;
            double dt = Clamp(now - lastSeconds, .008, .05);
            lastSeconds = now;
            int dx = point.X - lastPoint.X;
            int dy = point.Y - lastPoint.Y;
            lastPoint = point;

            // A damped spring keeps a small trailing bend and natural rebound.
            angleVelocity += -dx * 18.0 + dy * 4.0;
            angleVelocity += (-125.0 * angle - 15.0 * angleVelocity) * dt;
            angle = Clamp(angle + angleVelocity * dt, -18.0, 18.0);

            bool pressed = (Win32.GetAsyncKeyState(1) & 0x8000) != 0 ||
                           (Win32.GetAsyncKeyState(2) & 0x8000) != 0;
            double targetScale = pressed ? .88 : 1.0;
            scaleVelocity += (630.0 * (targetScale - scale) - 25.0 * scaleVelocity) * dt;
            scale = Clamp(scale + scaleVelocity * dt, .88, 1.04);

            int angleStep = (int)Clamp(Math.Round(angle / 2.0) * 2, -18, 18);
            int scaleStep = (int)Clamp(Math.Round((scale - 1.0) / .02), -6, 2);
            int handAngleStep = (int)Clamp(Math.Round(angle / 4.0) * 4, -12, 12);
            int targetSize = GetTargetCursorSize();
            string arrowFile = string.Format("a{0:+00;-00;+00}_s{1:+00;-00;+00}.cur", angleStep, scaleStep);
            string handFile = string.Format("h{0:+00;-00;+00}_s{1:+00;-00;+00}.cur", handAngleStep, scaleStep);
            if (!ApplyCursor(arrowFile, 32512, targetSize, ref lastArrowFile)) { Fail(); return; }
            if (!ApplyCursor(handFile, 32649, targetSize, ref lastHandFile)) { Fail(); return; }
            failures = 0;
        }

        private bool IsOurScheme()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors"))
            {
                if (key == null) return false;
                var name = Convert.ToString(key.GetValue("", ""));
                var arrow = Convert.ToString(key.GetValue("Arrow", ""));
                return string.Equals(name, "Codex-inspired Blue Glow 2", StringComparison.Ordinal) &&
                       string.Equals(arrow, Path.Combine(root, "assets_active", "arrow.cur"),
                                     StringComparison.OrdinalIgnoreCase);
            }
        }

        private static int GetTargetCursorSize()
        {
            uint dpi = 96;
            try { dpi = Win32.GetDpiForSystem(); }
            catch (EntryPointNotFoundException) { }
            int metric = Win32.GetSystemMetrics(13); // SM_CXCURSOR
            double factor = Math.Max(dpi / 96.0, Math.Max(1.0, metric / 32.0));
            return (int)Clamp(Math.Round(128.0 * factor / 8.0) * 8.0, 128, 256);
        }

        private bool ApplyCursor(string file, uint role, int targetSize, ref string previous)
        {
            string key = file + "@" + targetSize;
            if (key == previous) return true;
            string path = Path.Combine(root, "assets_active", "motion", file);
            IntPtr handle = Win32.LoadImage(IntPtr.Zero, path, 2, targetSize, targetSize, 0x10);
            if (handle == IntPtr.Zero) return false;
            if (!Win32.SetSystemCursor(handle, role)) {
                Win32.DestroyCursor(handle); return false;
            }
            previous = key;
            return true;
        }

        private void Fail()
        {
            if (++failures > 3) { RestoreAssignedCursors(); ExitThread(); }
        }

        protected override void ExitThreadCore()
        {
            timer.Stop(); timer.Dispose();
            RestoreAssignedCursors();
            tray.Visible = false;
            tray.ContextMenuStrip.Dispose();
            tray.Dispose();
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            mutex.Dispose();
            base.ExitThreadCore();
        }
    }
}
