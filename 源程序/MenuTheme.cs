using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TriMode
{
    // ContextMenuStrip still carries CS_DROPSHADOW; that rectangular shadow
    // shows up as a black outline around the custom rounded Region.
    public class ThemedContextMenu : ContextMenuStrip
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle &= ~0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }
    }

    // Shared dark-glass look for both the panel menu and the tray menu.
    // The native ContextMenuStrip renderer is replaced so the popup no longer
    // falls back to a light system menu when the rest of the app is themed.
    public static class MenuTheme
    {
        public static void Apply(ContextMenuStrip menu, Color accent)
        {
            Apply(menu, accent, true);
        }

        public static Color CurrentAccent = Color.FromArgb(255, 79, 163);

        public static void Apply(ContextMenuStrip menu, Color accent, bool dangerLast)
        {
            if (menu == null) return;
            CurrentAccent = accent;
            menu.Renderer = new ThemedMenuRenderer(accent);
            menu.BackColor = Color.FromArgb(29, 29, 36);
            menu.ForeColor = Color.FromArgb(232, 232, 240);
            menu.Font = new Font("Microsoft YaHei UI", 9.4f, FontStyle.Regular, GraphicsUnit.Point);
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.DropShadowEnabled = false;   // the OS shadow is rectangular and shows at the rounded corners
            menu.Padding = new Padding(0, 6, 0, 6);

            int maxText = 0;
            foreach (ToolStripItem item in menu.Items)
            {
                item.Padding = new Padding(0, 4, 0, 4);
                item.TextAlign = ContentAlignment.MiddleCenter;
                if (dangerLast && item == menu.Items[menu.Items.Count - 1]) item.Tag = "danger";
                item.Paint -= ItemPaint;
                item.Paint += ItemPaint;
                if (item.Text == "退出") item.Tag = "danger";
                int w = TextRenderer.MeasureText(item.Text, menu.Font).Width;
                if (w > maxText) maxText = w;
            }

            // ToolStripDropDown's auto width leaves a large empty column for short
            // labels. Size the popup to its longest label instead.
            int targetW = Math.Max(56, maxText + 24);
            int targetH = Math.Max(0, menu.GetPreferredSize(Size.Empty).Height);
            menu.AutoSize = false;
            menu.Size = new Size(targetW, targetH);
            foreach (ToolStripItem item in menu.Items)
            {
                item.AutoSize = false;
                item.Width = targetW;
                item.Height = 30;
            }

            menu.Opened -= MenuOpened;
            menu.Opened += MenuOpened;
            menu.Resize -= MenuResized;
            menu.Resize += MenuResized;
            RoundMenu(menu);
        }

        static void ItemPaint(object sender, PaintEventArgs e)
        {
            ToolStripItem item = sender as ToolStripItem;
            if (item == null || item.Text.Length == 0) return;
            Color color;
            if (!item.Enabled) color = Color.FromArgb(105, 255, 255, 255);
            else if (item.Selected || item.Pressed)
            {
                Color bg = GlassStyle.LightMode ? Color.FromArgb(246, 246, 249) : CurrentAccent;
                color = (bg.R * 0.299f + bg.G * 0.587f + bg.B * 0.114f) > 150f
                    ? Color.FromArgb(24, 24, 30) : Color.FromArgb(250, 250, 252);
            }
            else if (item.Tag is Color) color = (Color)item.Tag;
            else color = Color.FromArgb(224, 224, 232);

            using (Font font = item.Font)
            {
                // 1px right bias compensates for GDI side bearings; 1px down
                // lands the glyph body on the optical vertical center.
                TextRenderer.DrawText(e.Graphics, item.Text, font,
                    new Rectangle(1, 3, item.Width, item.Height), color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        static void MenuOpened(object sender, EventArgs e)
        {
            ContextMenuStrip menu = sender as ContextMenuStrip;
            RoundMenu(menu);
            if (menu != null)
            {
                menu.BeginInvoke(new Action(delegate { RoundMenu(menu); }));
            }
        }

        static void MenuResized(object sender, EventArgs e)
        {
            RoundMenu(sender as ContextMenuStrip);
        }

        static void RoundMenu(ContextMenuStrip menu)
        {
            if (menu == null || menu.Width <= 0 || menu.Height <= 0) return;
            float dpi = 1f;
            try
            {
                using (Graphics g = menu.CreateGraphics()) dpi = g.DpiX / 96f;
            }
            catch { }
            if (dpi < 1f) dpi = 1f;
            float radius = Math.Min(12f * dpi, Math.Min(menu.Width, menu.Height) / 2f);
            using (GraphicsPath gp = Round(new RectangleF(0, 0, menu.Width, menu.Height), radius))
            {
                if (menu.Region != null) menu.Region.Dispose();
                menu.Region = new Region(gp);
            }
        }

        internal static GraphicsPath Round(RectangleF r, float rad)
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
    }

    class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        readonly Color _accent;

        public ThemedMenuRenderer(Color accent) : base(new ThemedMenuColorTable())
        {
            _accent = accent;
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float dpi = g.DpiX / 96f;
            if (dpi < 1f) dpi = 1f;
            RectangleF r = new RectangleF(0, 0, e.ToolStrip.Width, e.ToolStrip.Height);
            using (GraphicsPath gp = MenuTheme.Round(r, 12f * dpi))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(r,
                    Color.FromArgb(248, 34, 34, 43), Color.FromArgb(250, 19, 19, 25),
                    LinearGradientMode.Vertical))
                {
                    g.FillPath(lg, gp);
                }

                GraphicsState st = g.Save();
                g.SetClip(gp);
                using (GraphicsPath glow = new GraphicsPath())
                {
                    glow.AddEllipse(r.Width - 170, -100, 230, 190);
                    using (PathGradientBrush pg = new PathGradientBrush(glow))
                    {
                        pg.CenterColor = Color.FromArgb(55, _accent);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, _accent) };
                        g.FillPath(pg, glow);
                    }
                }
                g.Restore(st);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Enabled) return;
            if (!e.Item.Selected && !e.Item.Pressed) return;
            float dpi = e.Graphics.DpiX / 96f;
            if (dpi < 1f) dpi = 1f;
            // Keep the hover fill away from the outer rounded corners so the
            // accent color never overlaps the popup edge.
            Rectangle r = new Rectangle(8, 7, e.Item.Width - 16, e.Item.Height - 8);
            using (GraphicsPath gp = MenuTheme.Round(r, 8f * dpi))
            {
                Color fill = GlassStyle.LightMode
                    ? Color.FromArgb(245, 245, 248) : Color.FromArgb(238, _accent);
                using (SolidBrush b = new SolidBrush(fill))
                    e.Graphics.FillPath(b, gp);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // Text is painted in ToolStripItem.Paint so it can be centered
            // against the full menu width, independent of item margins.
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            float dpi = e.Graphics.DpiX / 96f;
            if (dpi < 1f) dpi = 1f;
            int pad = (int)(16f * dpi);
            using (Pen p = new Pen(Color.FromArgb(28, 255, 255, 255), 1f))
                e.Graphics.DrawLine(p, pad, y, e.Item.Width - pad, y);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (Pen p = new Pen(e.Item.Selected ? Color.FromArgb(255, 255, 255) : _accent, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLine(p, cx - 4f, cy, cx - 1f, cy + 3.4f);
                g.DrawLine(p, cx - 1f, cy + 3.4f, cx + 4.6f, cy - 3.8f);
            }
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // No image column; keep the popup compact.
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Selected ? Color.White : Color.FromArgb(185, 185, 198);
            base.OnRenderArrow(e);
        }
    }

    class ThemedMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(29, 29, 36); } }
        public override Color MenuBorder { get { return Color.FromArgb(29, 29, 36); } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(36, 36, 45); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(36, 36, 45); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(36, 36, 45); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(42, 42, 52); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(42, 42, 52); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(29, 29, 36); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(29, 29, 36); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(29, 29, 36); } }
        public override Color SeparatorDark { get { return Color.FromArgb(30, 255, 255, 255); } }
        public override Color SeparatorLight { get { return Color.FromArgb(30, 255, 255, 255); } }
    }
}
