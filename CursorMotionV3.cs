using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexInspiredPointer
{
    internal struct NativePoint { public int X, Y; }
    internal struct CursorInfo
    {
        public int Size;
        public int Flags;
        public IntPtr Cursor;
        public NativePoint Position;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)] public bool IsIcon;
        public int HotX, HotY;
        public IntPtr Mask, Color;
    }

    internal static class Native
    {
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll")] internal static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetSystemCursor(IntPtr cursor, uint id);
        [DllImport("user32.dll")] internal static extern IntPtr LoadCursor(IntPtr instance, IntPtr id);
        [DllImport("user32.dll", SetLastError=true)] internal static extern IntPtr CreateIconIndirect(ref IconInfo info);
        [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr handle);
        [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] internal static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] internal static extern bool SystemParametersInfo(uint action, uint parameter, IntPtr value, uint flags);
    }

    internal sealed class GlowSettings
    {
        public string ColorName = "Blue";
        public int Radius = 120;
        public int Intensity = 130;

        public static GlowSettings Read(string root)
        {
            var result = new GlowSettings();
            string file = Path.Combine(root, "settings.ini");
            if (!File.Exists(file)) return result;
            foreach (string line in File.ReadAllLines(file))
            {
                string[] pair = line.Split(new [] {'='}, 2);
                if (pair.Length != 2) continue;
                int number;
                if (pair[0] == "GlowColor" &&
                    (pair[1] == "Blue" || pair[1] == "Cyan" ||
                     pair[1] == "Purple" || pair[1] == "White"))
                    result.ColorName = pair[1];
                if (pair[0] == "GlowRadius" && int.TryParse(pair[1], out number))
                    result.Radius = Math.Max(75, Math.Min(140, number));
                if (pair[0] == "GlowIntensity" && int.TryParse(pair[1], out number))
                    result.Intensity = Math.Max(60, Math.Min(180, number));
            }
            return result;
        }

        public Bitmap Apply(Bitmap original, bool keepRed)
        {
            if (Radius == 100 && Intensity == 100 && (ColorName == "Blue" || keepRed))
                return original;
            int width = original.Width, height = original.Height;
            var rect = new Rectangle(0, 0, width, height);
            using (var source = original.Clone(rect, PixelFormat.Format32bppArgb))
            {
                BitmapData data = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var pixels = new byte[data.Stride * height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                source.UnlockBits(data);
                var bodyBytes = (byte[])pixels.Clone();
                var glowBytes = new byte[pixels.Length];
                Color color = keepRed ? Color.FromArgb(245, 52, 62) :
                    ColorName == "Cyan" ? Color.FromArgb(35, 220, 255) :
                    ColorName == "Purple" ? Color.FromArgb(160, 95, 255) :
                    ColorName == "White" ? Color.FromArgb(225, 240, 255) :
                    Color.FromArgb(38, 125, 255);
                for (int y=0; y<height; y++)
                    for (int x=0; x<width; x++)
                    {
                        int i=y*data.Stride+x*4;
                        int b=pixels[i], g=pixels[i+1], r=pixels[i+2], a=pixels[i+3];
                        bool light=keepRed ? r>g+25 && r>b+25 : b>r+25 && b>g+12;
                        if (!light || a==0) continue;
                        bodyBytes[i+3]=0;
                        glowBytes[i]=color.B; glowBytes[i+1]=color.G;
                        glowBytes[i+2]=color.R;
                        glowBytes[i+3]=(byte)Math.Min(255, a*Intensity/100);
                    }
                using (Bitmap body=FromBytes(bodyBytes,width,height))
                using (Bitmap glow=FromBytes(glowBytes,width,height))
                {
                    var result=new Bitmap(width,height,PixelFormat.Format32bppArgb);
                    using (Graphics g=Graphics.FromImage(result))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                        float factor=Radius/100f;
                        float w=width*factor, h=height*factor;
                        g.DrawImage(glow,(width-w)/2f,(height-h)/2f,w,h);
                    }
                    FadeEdges(result);
                    using (Graphics g=Graphics.FromImage(result))
                        g.DrawImageUnscaled(body,0,0);
                    return result;
                }
            }
        }

        private static void FadeEdges(Bitmap image)
        {
            int width=image.Width, height=image.Height;
            var rect=new Rectangle(0,0,width,height);
            BitmapData data=image.LockBits(rect,ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
            var bytes=new byte[data.Stride*height];
            Marshal.Copy(data.Scan0,bytes,0,bytes.Length);
            for (int y=0;y<height;y++)
                for (int x=0;x<width;x++)
                {
                    int distance=Math.Min(Math.Min(x,y),Math.Min(width-1-x,height-1-y));
                    if (distance>=18) continue;
                    double t=Math.Max(0,distance/18.0);
                    double fade=t*t*(3-2*t);
                    int i=y*data.Stride+x*4+3;
                    bytes[i]=(byte)Math.Round(bytes[i]*fade);
                }
            Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
            image.UnlockBits(data);
        }

        private static Bitmap FromBytes(byte[] bytes,int width,int height)
        {
            var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb);
            var rect=new Rectangle(0,0,width,height);
            BitmapData data=bitmap.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
            bitmap.UnlockBits(data);
            return bitmap;
        }
    }

    internal sealed class Role : IDisposable
    {
        public readonly string Name;
        public readonly uint Id;
        public readonly Bitmap[] Frames;
        public readonly int HotX, HotY;
        public string LastKey = "";
        public Role(string name, uint id, string renderRoot, string cursorRoot, GlowSettings glow)
        {
            Name = name; Id = id;
            string cur = Path.Combine(cursorRoot, name + ".cur");
            if (name == "working" || name == "busy") cur = Path.Combine(cursorRoot, name + ".ani");
            string png = Path.Combine(renderRoot, name + ".png");
            string frameDir = Path.Combine(renderRoot, name);
            if (Directory.Exists(frameDir))
            {
                string[] files = Directory.GetFiles(frameDir, "*.png");
                Array.Sort(files, StringComparer.Ordinal);
                Frames = new Bitmap[files.Length];
                for (int i = 0; i < files.Length; i++) {
                    Bitmap raw=new Bitmap(files[i]);
                    Frames[i]=glow.Apply(raw,name=="no");
                    if (!Object.ReferenceEquals(raw,Frames[i])) raw.Dispose();
                }
            }
            else {
                Bitmap raw=new Bitmap(png);
                Bitmap frame=glow.Apply(raw,name=="no");
                if (!Object.ReferenceEquals(raw,frame)) raw.Dispose();
                Frames = new Bitmap[] { frame };
            }
            if (Frames.Length == 0) throw new FileNotFoundException("No rendered cursor frames", frameDir);
            string hotFile = Path.Combine(renderRoot, name + ".hot");
            string[] hot = File.ReadAllText(hotFile).Trim().Split(',');
            HotX = int.Parse(hot[0]); HotY = int.Parse(hot[1]);
        }
        public void Dispose() { foreach (Bitmap frame in Frames) frame.Dispose(); }
    }

    public sealed class MotionV3 : ApplicationContext
    {
        private readonly string root;
        private readonly Mutex mutex;
        private readonly List<Role> roles = new List<Role>();
        private readonly System.Windows.Forms.Timer timer;
        private readonly NotifyIcon tray;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private NativePoint lastPoint;
        private double lastSeconds, lastSchemeCheck = -1;
        private double angle, angleVelocity, scale = 1, scaleVelocity;
        private int failures, lastSize;
        private double lastVisualUpdate;
        private bool active, paused;
        private double lastHeartbeat=-1;

        private MotionV3(string root, Mutex mutex)
        {
            this.root = root; this.mutex = mutex;
            string art = Path.Combine(root, "assets_active");
            string render = Path.Combine(art, "render");
            string[] names = { "arrow", "ibeam", "busy", "crosshair", "uparrow",
                "sizenwse", "sizenesw", "sizewe", "sizens", "sizeall", "no", "hand",
                "working", "help", "pen", "pin", "person" };
            uint[] ids = { 32512,32513,32514,32515,32516,32642,32643,32644,32645,
                32646,32648,32649,32650,32651,32631,32671,32672 };
            GlowSettings glow=GlowSettings.Read(root);
            for (int i=0;i<names.Length;i++) roles.Add(new Role(names[i], ids[i], render, art, glow));
            Native.GetCursorPos(out lastPoint);
            var menu = new ContextMenuStrip();
            var pause = new ToolStripMenuItem("Pausar movimento");
            pause.Click += delegate {
                paused = !paused;
                pause.Text = paused ? "Retomar movimento" : "Pausar movimento";
                if (paused) Restore(); else { ClearKeys(); ApplyBase(GetTargetSize()); }
            };
            menu.Items.Add(pause);
            var exit = new ToolStripMenuItem("Sair");
            exit.Click += delegate {
                File.WriteAllText(Path.Combine(root,"stop.flag"),"stop");
                ExitThread();
            };
            menu.Items.Add(exit);
            tray = new NotifyIcon();
            tray.Icon = SystemIcons.Application;
            tray.Text = "Codex-inspired Pointer 3";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 16;
            timer.Tick += Tick;
            if (IsOurScheme()) { active=true; ApplyBase(GetTargetSize()); }
            timer.Start();
            Log("started");
        }

        public static void Run(string root)
        {
            bool created;
            var mutex = new Mutex(true, @"Local\CodexInspiredPointerMotionV3", out created);
            if (!created) { mutex.Dispose(); return; }
            try { Application.Run(new MotionV3(root, mutex)); }
            catch (Exception e) { LogAt(root, "fatal " + e); throw; }
        }

        private static void LogAt(string root, string message)
        {
            try { File.AppendAllText(Path.Combine(root,"motion.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine); }
            catch { }
        }
        private void Log(string message) { LogAt(root,message); }
        private static double Clamp(double x,double lo,double hi) { return Math.Max(lo,Math.Min(hi,x)); }
        private static void Restore() { Native.SystemParametersInfo(0x57,0,IntPtr.Zero,0); }
        private void ClearKeys() { foreach (Role role in roles) role.LastKey=""; }

        private bool IsOurScheme()
        {
            using (var key=Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors"))
            {
                if (key==null) return false;
                return Convert.ToString(key.GetValue("",""))=="Codex-inspired Blue Glow 2" &&
                    string.Equals(Convert.ToString(key.GetValue("Arrow","")),
                    Path.Combine(root,"assets_active","arrow.cur"),StringComparison.OrdinalIgnoreCase);
            }
        }

        private static int GetTargetSize()
        {
            uint dpi=96;
            try { dpi=Native.GetDpiForSystem(); } catch (EntryPointNotFoundException) { }
            int metric=Native.GetSystemMetrics(13);
            double factor=Math.Max(dpi/96.0,Math.Max(1.0,metric/32.0));
            return (int)Clamp(Math.Round(128.0*factor/8.0)*8.0,128,256);
        }

        private void Tick(object sender, EventArgs e)
        {
            try { DoTick(); }
            catch (Exception ex) {
                if (++failures==1 || failures%60==0) Log("tick error " + ex);
                if (failures>180) { Restore(); ExitThread(); }
            }
        }

        private void DoTick()
        {
            if (File.Exists(Path.Combine(root,"stop.flag"))) { ExitThread(); return; }
            if (paused) return;
            double now=clock.Elapsed.TotalSeconds;
            if (now-lastSchemeCheck>.4)
            {
                lastSchemeCheck=now;
                bool scheme=IsOurScheme();
                if (scheme && !active) { active=true; ApplyBase(GetTargetSize()); }
                if (!scheme && active) { active=false; Restore(); ClearKeys(); }
            }
            if (!active) return;
            if (now-lastHeartbeat>15) {
                lastHeartbeat=now;
                try { File.WriteAllText(Path.Combine(root,"motion.status"),DateTime.UtcNow.ToString("O")); }
                catch { }
            }
            int targetSize=GetTargetSize();
            if (targetSize!=lastSize) ApplyBase(targetSize);
            NativePoint point;
            if (!Native.GetCursorPos(out point)) return;
            double dt=Clamp(now-lastSeconds,.008,.05);
            lastSeconds=now;
            int dx=point.X-lastPoint.X, dy=point.Y-lastPoint.Y;
            lastPoint=point;
            angleVelocity+=-dx*18.0+dy*4.0;
            angleVelocity+=(-125.0*angle-15.0*angleVelocity)*dt;
            angle=Clamp(angle+angleVelocity*dt,-18,18);
            bool pressed=(Native.GetAsyncKeyState(1)&0x8000)!=0 ||
                         (Native.GetAsyncKeyState(2)&0x8000)!=0;
            double targetScale=pressed?.88:1.0;
            scaleVelocity+=(630.0*(targetScale-scale)-25.0*scaleVelocity)*dt;
            scale=Clamp(scale+scaleVelocity*dt,.88,1.04);
            int scaleStep=(int)Clamp(Math.Round((scale-1.0)/.02),-6,2);
            // Replacing several system cursors on every tick briefly exposes
            // the small file-backed cursor between updates (the visible flash).
            // Only the cursor currently on screen needs a motion frame. The
            // native ANI files handle waiting states without any replacement.
            if (now-lastVisualUpdate>=1.0/30.0)
            {
                lastVisualUpdate=now;
                Role current=CurrentRole();
                if (current!=null && current.Name!="busy" && current.Name!="working")
                {
                    int limit=current.Name=="arrow"?18:current.Name=="hand"?12:8;
                    int step=current.Name=="arrow"?2:4;
                    int a=(int)Clamp(Math.Round(angle/step)*step,-limit,limit);
                    UpdateRole(current,targetSize,a,scaleStep,0);
                }
            }
            failures=0;
        }

        private Role CurrentRole()
        {
            var info=new CursorInfo(); info.Size=Marshal.SizeOf(typeof(CursorInfo));
            if (!Native.GetCursorInfo(ref info)) return null;
            foreach (Role role in roles)
                if (Native.LoadCursor(IntPtr.Zero,(IntPtr)role.Id)==info.Cursor) return role;
            return null;
        }

        private void ApplyBase(int targetSize)
        {
            foreach (Role role in roles) {
                role.LastKey="";
                if (role.Name!="busy" && role.Name!="working")
                    UpdateRole(role,targetSize,0,0,0);
            }
            lastSize=targetSize;
            Log("base applied size="+targetSize);
        }

        private void UpdateRole(Role role,int size,int angleStep,int scaleStep,int frame)
        {
            string key=size+":"+angleStep+":"+scaleStep+":"+frame;
            if (key==role.LastKey) return;
            Bitmap source=role.Frames[frame];
            using (var target=new Bitmap(size,size,PixelFormat.Format32bppPArgb))
            {
                using (Graphics g=Graphics.FromImage(target))
                {
                    g.Clear(Color.Transparent);
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode=SmoothingMode.HighQuality;
                    g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    float factor=size/256f;
                    g.TranslateTransform(role.HotX*factor,role.HotY*factor);
                    g.RotateTransform(angleStep);
                    g.ScaleTransform(1+scaleStep*.02f,1+scaleStep*.02f);
                    g.TranslateTransform(-role.HotX*factor,-role.HotY*factor);
                    g.DrawImage(source,new Rectangle(0,0,size,size));
                }
                IntPtr color=target.GetHbitmap(Color.Transparent);
                IntPtr mask=IntPtr.Zero;
                try
                {
                    using (var blank=new Bitmap(size,size,PixelFormat.Format1bppIndexed))
                        mask=blank.GetHbitmap();
                    var icon=new IconInfo { IsIcon=false, HotX=(int)Math.Round(role.HotX*size/256.0),
                        HotY=(int)Math.Round(role.HotY*size/256.0), Color=color, Mask=mask };
                    IntPtr cursor=Native.CreateIconIndirect(ref icon);
                    if (cursor==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    if (!Native.SetSystemCursor(cursor,role.Id))
                    {
                        int error=Marshal.GetLastWin32Error();
                        Native.DestroyIcon(cursor);
                        throw new System.ComponentModel.Win32Exception(error,"SetSystemCursor "+role.Name);
                    }
                    role.LastKey=key;
                }
                finally { if (color!=IntPtr.Zero) Native.DeleteObject(color);
                          if (mask!=IntPtr.Zero) Native.DeleteObject(mask); }
            }
        }

        protected override void ExitThreadCore()
        {
            timer.Stop(); timer.Dispose();
            Restore();
            tray.Visible=false; tray.ContextMenuStrip.Dispose(); tray.Dispose();
            foreach (Role role in roles) role.Dispose();
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            mutex.Dispose();
            Log("stopped");
            base.ExitThreadCore();
        }
    }
}
