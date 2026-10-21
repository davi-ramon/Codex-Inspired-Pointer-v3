using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace CodexInspiredPointer
{
    // Changes only the installed copy. The ZIP and its render sources remain
    // untouched, so applying a different glow always starts from clean art.
    public static class CursorTheme
    {
        private static readonly int[] Sizes = { 32, 48, 64, 96, 128, 256 };
        private static readonly string[] StaticNames = {
            "arrow", "help", "crosshair", "ibeam", "pen", "no", "sizens",
            "sizewe", "sizenwse", "sizenesw", "sizeall", "uparrow",
            "hand", "pin", "person", "grab", "grabbing"
        };

        private sealed class Entry
        {
            public int Size, HotX, HotY;
            public byte[] Png;
        }

        private static Bitmap Resize(Bitmap source, Rectangle crop, int size)
        {
            var output = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(output))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, size, size), crop,
                            GraphicsUnit.Pixel);
            }
            return output;
        }

        private static void FadeEdge(Bitmap image)
        {
            int n = image.Width, width = Math.Max(3, n / 8);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int distance = Math.Min(Math.Min(x, y), Math.Min(n - 1 - x, n - 1 - y));
                    double t = Math.Min(1.0, (double)distance / width);
                    double fade = t * t * (3 - 2 * t);
                    Color pixel = image.GetPixel(x, y);
                    if (pixel.A != 0 && fade < 1)
                        image.SetPixel(x, y, Color.FromArgb((int)Math.Round(pixel.A * fade),
                                                           pixel.R, pixel.G, pixel.B));
                }
        }

        private static byte[] EncodeCur(Bitmap source, int fullX, int fullY)
        {
            var entries = new List<Entry>();
            foreach (int n in Sizes)
            {
                int cropSize = n < 128 ? n * 2 : 256;
                int left = (256 - cropSize) / 2, top = left;
                if (fullX < left + 4) left = Math.Max(0, fullX - 4);
                else if (fullX > left + cropSize - 4)
                    left = Math.Min(256 - cropSize, fullX - cropSize + 4);
                if (fullY < top + 4) top = Math.Max(0, fullY - 4);
                else if (fullY > top + cropSize - 4)
                    top = Math.Min(256 - cropSize, fullY - cropSize + 4);
                using (Bitmap image = Resize(source,
                    new Rectangle(left, top, cropSize, cropSize), n))
                {
                    if (n < 128) FadeEdge(image);
                    using (var buffer = new MemoryStream())
                    {
                        image.Save(buffer, ImageFormat.Png);
                        entries.Add(new Entry {
                            Size = n, HotX = n < 128 ? (int)Math.Round((fullX - left) / 2.0) :
                                (int)Math.Round(fullX * n / 256.0),
                            HotY = n < 128 ? (int)Math.Round((fullY - top) / 2.0) :
                                (int)Math.Round(fullY * n / 256.0),
                            Png = buffer.ToArray()
                        });
                    }
                }
            }
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write((ushort)0); writer.Write((ushort)2);
                writer.Write((ushort)entries.Count);
                int offset = 6 + entries.Count * 16;
                foreach (Entry entry in entries)
                {
                    writer.Write((byte)(entry.Size == 256 ? 0 : entry.Size));
                    writer.Write((byte)(entry.Size == 256 ? 0 : entry.Size));
                    writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((ushort)entry.HotX); writer.Write((ushort)entry.HotY);
                    writer.Write((uint)entry.Png.Length); writer.Write((uint)offset);
                    offset += entry.Png.Length;
                }
                foreach (Entry entry in entries) writer.Write(entry.Png);
                return buffer.ToArray();
            }
        }

        private static byte[] Chunk(string type, byte[] data)
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes(type));
                writer.Write((uint)data.Length); writer.Write(data);
                if ((data.Length & 1) != 0) writer.Write((byte)0);
                return buffer.ToArray();
            }
        }

        private static byte[] Animate(List<byte[]> frames)
        {
            using (var header = new MemoryStream())
            using (var writer = new BinaryWriter(header))
            {
                foreach (int value in new [] {36, frames.Count, frames.Count,
                         128, 128, 32, 1, 2, 1}) writer.Write(value);
                byte[] anih = Chunk("anih", header.ToArray());
                using (var rates = new MemoryStream())
                using (var rateWriter = new BinaryWriter(rates))
                {
                    foreach (byte[] frame in frames) rateWriter.Write(2);
                    byte[] rate = Chunk("rate", rates.ToArray());
                    using (var framed = new MemoryStream())
                    {
                        framed.Write(System.Text.Encoding.ASCII.GetBytes("fram"), 0, 4);
                        foreach (byte[] frame in frames)
                        {
                            byte[] icon = Chunk("icon", frame);
                            framed.Write(icon, 0, icon.Length);
                        }
                        byte[] list = Chunk("LIST", framed.ToArray());
                        using (var body = new MemoryStream())
                        {
                            body.Write(System.Text.Encoding.ASCII.GetBytes("ACON"), 0, 4);
                            body.Write(anih, 0, anih.Length);
                            body.Write(rate, 0, rate.Length);
                            body.Write(list, 0, list.Length);
                            using (var output = new MemoryStream())
                            using (var outWriter = new BinaryWriter(output))
                            {
                                outWriter.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                                outWriter.Write((uint)body.Length);
                                outWriter.Write(body.ToArray());
                                return output.ToArray();
                            }
                        }
                    }
                }
            }
        }

        private static int[] Hotspot(string file)
        {
            string[] parts = File.ReadAllText(file).Trim().Split(',');
            return new [] {int.Parse(parts[0]), int.Parse(parts[1])};
        }

        public static int Main(string[] args)
        {
            try
            {
                if (args.Length != 4) throw new ArgumentException(
                    "Usage: CursorTheme.exe <assetsDir> <color> <radius> <intensity>");
                string assets = Path.GetFullPath(args[0]);
                string render = Path.Combine(assets, "render");
                var glow = new GlowSettings { ColorName = args[1],
                    Radius = int.Parse(args[2]), Intensity = int.Parse(args[3]) };
                if (glow.ColorName != "Blue" && glow.ColorName != "Cyan" &&
                    glow.ColorName != "Purple" && glow.ColorName != "White")
                    throw new ArgumentException("Invalid glow color");
                if (glow.Radius < 75 || glow.Radius > 140 ||
                    glow.Intensity < 60 || glow.Intensity > 180)
                    throw new ArgumentException("Invalid glow setting");
                foreach (string name in StaticNames)
                {
                    int[] hot = Hotspot(Path.Combine(render, name + ".hot"));
                    using (Bitmap raw = new Bitmap(Path.Combine(render, name + ".png")))
                    {
                        Bitmap themed = glow.Apply(raw, name == "no");
                        try { File.WriteAllBytes(Path.Combine(assets, name + ".cur"),
                            EncodeCur(themed, hot[0], hot[1])); }
                        finally { if (!Object.ReferenceEquals(raw, themed)) themed.Dispose(); }
                    }
                }
                foreach (string name in new [] {"working", "busy"})
                {
                    int[] hot = Hotspot(Path.Combine(render, name + ".hot"));
                    var frames = new List<byte[]>();
                    string[] paths = Directory.GetFiles(Path.Combine(render, name), "*.png");
                    Array.Sort(paths, StringComparer.Ordinal);
                    foreach (string path in paths)
                    {
                        using (Bitmap raw = new Bitmap(path))
                        {
                            Bitmap themed = glow.Apply(raw, false);
                            try { frames.Add(EncodeCur(themed, hot[0], hot[1])); }
                            finally { if (!Object.ReferenceEquals(raw, themed)) themed.Dispose(); }
                        }
                    }
                    if (frames.Count != 30) throw new InvalidDataException("Expected 30 wait frames");
                    File.WriteAllBytes(Path.Combine(assets, name + ".ani"), Animate(frames));
                }
                Console.WriteLine("Installed cursor glow updated.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
