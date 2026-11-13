using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TriMode
{
    // HSV color wheel: hue by angle, saturation by radius. Value is provided
    // by the parent dialog so the marker always reflects the active color.
    public class ColorWheel : Control
    {
        Color _color = Color.FromArgb(255, 79, 163);
        float _value = 1f;
        bool _tracking;
        public string CenterText = "";
        public Color HoleColor = Color.Empty;
        public Color CenterColor = Color.Empty;

        public event EventHandler ColorChanged;
        public event EventHandler CenterClicked;

        public ColorWheel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Default;
            Size = new Size(156, 156);
        }

        public Color SelectedColor
        {
            get { return _color; }
        }

        public void SetColor(Color c)
        {
            _color = c;
            CenterColor = c;
            float h, s, v;
            ColorToHsv(c, out h, out s, out v);
            if (v > 0.02f) _value = v;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(3, 3, Width - 6, Height - 6);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float rad = Math.Min(r.Width, r.Height) / 2f;
            for (int i = 0; i < 360; i += 1)
            {
                Color c = HsvToColor(i, 1f, _value);
                using (SolidBrush b = new SolidBrush(c))
                    g.FillPie(b, r.X, r.Y, r.Width, r.Height, i, 1.4f);
            }
            float inner = rad * 0.56f;
            Color holeColor = HoleColor;
            if (holeColor == Color.Empty)
                holeColor = Parent == null ? Color.Black : Parent.BackColor;
            using (SolidBrush hole = new SolidBrush(holeColor))
                g.FillEllipse(hole, cx - inner, cy - inner, inner * 2, inner * 2);
            using (Pen edge = new Pen(Color.FromArgb(120, 255, 255, 255), 1f))
                g.DrawEllipse(edge, cx - inner, cy - inner, inner * 2, inner * 2);
            using (Pen edge = new Pen(Color.FromArgb(70, 0, 0, 0), 1f))
                g.DrawEllipse(edge, r.X, r.Y, r.Width, r.Height);

            float h2, s2, v2;
            ColorToHsv(_color, out h2, out s2, out v2);
            float angle = h2 * (float)Math.PI / 180f;
            float mr = (rad + inner) / 2f;
            float mx = cx + (float)Math.Cos(angle) * mr;
            float my = cy + (float)Math.Sin(angle) * mr;
            using (SolidBrush black = new SolidBrush(Color.FromArgb(240, 0, 0, 0)))
                g.FillEllipse(black, mx - 7f, my - 7f, 14f, 14f);
            using (SolidBrush white = new SolidBrush(_color))
            using (Pen p = new Pen(Color.White, 2f))
            {
                g.FillEllipse(white, mx - 5f, my - 5f, 10f, 10f);
                g.DrawEllipse(p, mx - 5f, my - 5f, 10f, 10f);
            }
            if (!string.IsNullOrEmpty(CenterText))
            {
                using (Font f = new Font(UiFonts.Family, 28f, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    Color centerInk = CenterColor == Color.Empty
                        ? (GlassStyle.LightMode ? Color.FromArgb(45, 45, 52) : Color.FromArgb(235, 235, 242))
                        : CenterColor;
                    if (CenterColor != Color.Empty)
                    {
                        float lum = centerInk.R * 0.299f + centerInk.G * 0.587f + centerInk.B * 0.114f;
                        if (lum < 110f)
                            centerInk = Color.FromArgb((centerInk.R + 255) / 2,
                                (centerInk.G + 255) / 2, (centerInk.B + 255) / 2);
                    }
                    using (SolidBrush ink = new SolidBrush(centerInk))
                        g.DrawString(CenterText, f, ink, new RectangleF(0, 0, Width, Height), sf);
                }
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            float dx = e.X - Width / 2f;
            float dy = e.Y - Height / 2f;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float rad = Math.Min(Width, Height) / 2f - 3f;
            if (dist < rad * 0.56f)
            {
                if (CenterClicked != null) CenterClicked(this, EventArgs.Empty);
                base.OnMouseDown(e);
                return;
            }
            _tracking = true;
            Pick(e.Location);
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_tracking) Pick(e.Location);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _tracking = false;
            base.OnMouseUp(e);
        }

        void Pick(Point p)
        {
            float cx = Width / 2f, cy = Height / 2f;
            float dx = p.X - cx, dy = p.Y - cy;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
            float rad = Math.Min(Width, Height) / 2f - 3f;
            if (dist > rad) { dx *= rad / dist; dy *= rad / dist; dist = rad; }
            float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            if (angle < 0) angle += 360f;
            _value = 1f;
            _color = HsvToColor(angle, 1f, 1f);
            CenterColor = _color;
            Invalidate();
            if (ColorChanged != null) ColorChanged(this, EventArgs.Empty);
        }

        public static Color HsvToColor(float h, float s, float v)
        {
            h = ((h % 360f) + 360f) % 360f;
            int i = (int)(h / 60f);
            float f = h / 60f - i;
            float p = v * (1f - s);
            float q = v * (1f - f * s);
            float t = v * (1f - (1f - f) * s);
            float r, g, b;
            switch (i)
            {
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
                default: r = v; g = t; b = p; break;
            }
            return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        public static void ColorToHsv(Color c, out float h, out float s, out float v)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            v = max;
            float delta = max - min;
            s = max <= 0.0001f ? 0f : delta / max;
            if (delta <= 0.0001f) { h = 0f; return; }
            if (max == r) h = 60f * ((g - b) / delta);
            else if (max == g) h = 60f * (2f + (b - r) / delta);
            else h = 60f * (4f + (r - g) / delta);
            if (h < 0) h += 360f;
        }
    }
}
