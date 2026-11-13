using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

namespace TriMode
{
    public static class UiFonts
    {
        public const string DefaultFamily = "Noto Serif SC SemiBold";
        public static readonly string[] Families = { "Noto Serif SC SemiBold", "楷体", "Noto Sans SC Black", "Noto Sans SC Medium" };
        public static readonly string[] Labels = { "粗宋", "楷书", "粗黑", "清晰" };
        public static string Family = DefaultFamily;

        public static Font Create(string family, float pixels, FontStyle style)
        {
            return new Font(family, pixels, style, GraphicsUnit.Pixel);
        }

        public static int Index(string family)
        {
            for (int i = 0; i < Families.Length; i++)
                if (string.Equals(Families[i], family, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        public static string Normalize(string family)
        {
            return Families[Index(family)];
        }

        public static string Label(string family)
        {
            return Labels[Index(family)];
        }
    }

    public enum GlassMaterial
    {
        Solid = 0,
        Gradient = 1
    }

    public static class GlassStyle
    {
        public static string Name(GlassMaterial material)
        {
            return material == GlassMaterial.Gradient ? "渐变" : "纯色";
        }

        public static string Description(GlassMaterial material)
        {
            return material == GlassMaterial.Gradient ? "主题色渐变 · 层次柔和" : "单色深底 · 安静干净";
        }

        public static bool LightMode;
        public static bool PureBlack;
        public static Color Secondary = Color.Empty;

        public static Color Ink
        {
            get { return LightMode ? Color.FromArgb(32, 32, 38) : Color.FromArgb(250, 250, 252); }
        }

        public static Color Muted
        {
            get { return LightMode ? Color.FromArgb(92, 92, 104) : Color.FromArgb(178, 178, 190); }
        }

        public static Color Tile
        {
            get { return LightMode ? Color.FromArgb(20, 0, 0, 0) : Color.FromArgb(42, 255, 255, 255); }
        }

        public static Color Track
        {
            get { return LightMode ? Color.FromArgb(232, 232, 238) : Color.FromArgb(52, 52, 62); }
        }

        public static Color Hairline
        {
            get { return LightMode ? Color.FromArgb(36, 0, 0, 0) : Color.FromArgb(56, 255, 255, 255); }
        }

        public static Color OnAccent
        {
            get
            {
                Color c = ThemeColor(ThemeIndex);
                return (c.R * 0.299f + c.G * 0.587f + c.B * 0.114f) > 165f
                    ? Color.FromArgb(24, 24, 28) : Color.White;
            }
        }

        public static int ThemeIndex;

        public static void Configure(int theme, Color accent, Color secondary)
        {
            ThemeIndex = theme;
            LightMode = theme == 6;
            PureBlack = theme == 7;
            Secondary = secondary;
        }

        static Color ThemeColor(int index)
        {
            return AppSettings.ThemeColor(index);
        }

        public static Color SolidBackground(Color accent)
        {
            if (LightMode) return Color.FromArgb(255, 246, 246, 249);
            if (PureBlack) return Color.FromArgb(255, 8, 8, 10);
            if (ThemeIndex == 8 && Secondary != Color.Empty)
                return Blend(Blend(Color.FromArgb(255, 28, 26, 34), accent, 0.16f), Secondary, 0.12f);
            return Blend(Color.FromArgb(255, 28, 26, 34), accent, 0.13f);
        }

        public static Color GradientTop(Color accent)
        {
            if (LightMode) return Color.FromArgb(255, 252, 252, 254);
            if (PureBlack) return Color.FromArgb(255, 17, 17, 21);
            if (ThemeIndex == 8 && Secondary != Color.Empty)
                return Blend(Blend(Color.FromArgb(255, 40, 36, 48), accent, 0.24f), Secondary, 0.12f);
            return Blend(Color.FromArgb(255, 40, 36, 48), accent, 0.24f);
        }

        public static Color GradientBottom(Color accent)
        {
            if (LightMode) return Color.FromArgb(255, 234, 234, 241);
            if (PureBlack) return Color.FromArgb(255, 3, 3, 5);
            if (ThemeIndex == 8 && Secondary != Color.Empty)
                return Blend(Blend(Color.FromArgb(255, 16, 16, 22), accent, 0.08f), Secondary, 0.18f);
            return Blend(Color.FromArgb(255, 16, 16, 22), accent, 0.06f);
        }

        static Color Blend(Color baseColor, Color accent, float amount)
        {
            if (amount < 0f) amount = 0f;
            if (amount > 1f) amount = 1f;
            return Color.FromArgb(255,
                (int)(baseColor.R + (accent.R - baseColor.R) * amount),
                (int)(baseColor.G + (accent.G - baseColor.G) * amount),
                (int)(baseColor.B + (accent.B - baseColor.B) * amount));
        }

        public static readonly Color LowBattery = Color.FromArgb(255, 45, 67);

        public static void DrawRefreshIcon(Graphics g, RectangleF r, Color color)
        {
            using (Font f = new Font("Segoe UI Symbol", r.Height * 0.9f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (SolidBrush b = new SolidBrush(color))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                GraphicsState state = g.Save();
                // Flip vertically around the icon center.
                float cx = r.X + r.Width / 2f;
                float cy = r.Y + r.Height / 2f;
                g.TranslateTransform(cx, cy);
                g.ScaleTransform(-1f, -1f);
                g.TranslateTransform(-cx, -cy);
                g.DrawString("\u21BA", f, b, r, sf);
                g.Restore(state);
            }
        }

        public static GraphicsPath RoundedRect(RectangleF r, float tl, float tr, float br, float bl)
        {
            float max = Math.Min(r.Width, r.Height) / 2f;
            tl = Math.Max(0, Math.Min(max, tl));
            tr = Math.Max(0, Math.Min(max, tr));
            br = Math.Max(0, Math.Min(max, br));
            bl = Math.Max(0, Math.Min(max, bl));
            GraphicsPath p = new GraphicsPath();
            p.StartFigure();
            p.AddLine(r.X + tl, r.Y, r.Right - tr, r.Y);
            if (tr > 0) p.AddArc(r.Right - tr * 2, r.Y, tr * 2, tr * 2, 270, 90);
            p.AddLine(r.Right, r.Y + tr, r.Right, r.Bottom - br);
            if (br > 0) p.AddArc(r.Right - br * 2, r.Bottom - br * 2, br * 2, br * 2, 0, 90);
            p.AddLine(r.Right - br, r.Bottom, r.X + bl, r.Bottom);
            if (bl > 0) p.AddArc(r.X, r.Bottom - bl * 2, bl * 2, bl * 2, 90, 90);
            p.AddLine(r.X, r.Bottom - bl, r.X, r.Y + tl);
            if (tl > 0) p.AddArc(r.X, r.Y, tl * 2, tl * 2, 180, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawDropShadow(Graphics g, RectangleF r, float radius, int layers, int alpha, float spread)
        {
            if (LightMode) return;
            for (int i = layers; i >= 1; i--)
            {
                float d = spread * i;
                RectangleF rr = new RectangleF(r.X - d, r.Y - d + 1f, r.Width + d * 2f, r.Height + d * 2f);
                using (GraphicsPath path = Round(rr, radius + d))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(LightMode ? Math.Max(3, alpha / 7) : alpha, 0, 0, 0)))
                    g.FillPath(b, path);
            }
        }

        static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawChargingBolt(Graphics g, RectangleF ring, float phase)
        {
            float pulse = 0.5f + 0.5f * (float)Math.Sin(phase * Math.PI * 2.0);
            float cx = ring.X + ring.Width / 2f;
            float cy = ring.Y + ring.Height / 2f;
            float innerR = Math.Min(ring.Width, ring.Height) / 2f - 2.6f;

            GraphicsState st = g.Save();
            using (GraphicsPath clip = new GraphicsPath())
            {
                clip.AddEllipse(cx - innerR, cy - innerR, innerR * 2f, innerR * 2f);
                g.SetClip(clip, CombineMode.Intersect);

                DrawRadial(g, new RectangleF(cx - innerR, cy - innerR, innerR * 2f, innerR * 2f),
                    Color.FromArgb(38 + (int)(34 * pulse), 255, 203, 72),
                    Color.FromArgb(0, 255, 203, 72));

                float scale = 1.02f + 0.08f * pulse;
                float bw = 6.6f * scale;
                float bh = 9.6f * scale;
                RectangleF bolt = new RectangleF(cx - bw / 2f, cy - bh / 2f, bw, bh);
                DrawBolt(g, bolt, Color.FromArgb(255, 211, 86));

                using (Pen shine = new Pen(Color.FromArgb(170, 255, 248, 206), 0.65f))
                {
                    PointF[] p = BoltPoints(bolt);
                    g.DrawPolygon(shine, p);
                }
            }
            g.Restore(st);
        }

        public static void DrawBolt(Graphics g, RectangleF r, Color color)
        {
            using (SolidBrush b = new SolidBrush(color))
                g.FillPolygon(b, BoltPoints(r));
        }

        static PointF[] BoltPoints(RectangleF r)
        {
            return new PointF[]
            {
                new PointF(r.X + r.Width * 0.58f, r.Y),
                new PointF(r.X + r.Width * 0.08f, r.Y + r.Height * 0.56f),
                new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.56f),
                new PointF(r.X + r.Width * 0.30f, r.Y + r.Height),
                new PointF(r.X + r.Width * 0.92f, r.Y + r.Height * 0.38f),
                new PointF(r.X + r.Width * 0.55f, r.Y + r.Height * 0.38f)
            };
        }

        static void DrawRadial(Graphics g, RectangleF r, Color center, Color edge)
        {
            if (r.Width <= 0f || r.Height <= 0f) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(r);
                using (PathGradientBrush brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = center;
                    brush.SurroundColors = new Color[] { edge };
                    g.FillPath(brush, path);
                }
            }
        }
    }
}
