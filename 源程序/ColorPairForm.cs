using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace TriMode
{
    // 自定义主题：独立非模态窗口，600x700。
    // 两个空心色环、RGB 输入、常用颜色、最近使用、收藏主色 / 辅色 / 搭配。
    public class ColorPairForm : Form
    {
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);
        public Color AccentColor { get; private set; }
        public Color SecondaryColor { get; private set; }
        public string History { get; private set; }
        public string Favorites { get; private set; }
        public string SecondaryFavorites { get; private set; }
        public string FavoritePairs { get; private set; }
        public bool Applied;
        public event EventHandler PreviewChanged;
        public event EventHandler ApplyRequested;

        ScrollLite _historyHScroll, _mainHScroll, _secondaryHScroll, _pairHScroll;
        readonly RgbField _rA, _gA, _bA, _rB, _gB, _bB;
        readonly ColorWheel _wheelAccent;
        readonly ColorWheel _wheelSecondary;
        readonly FlowLayoutPanel _historyFlow;
        readonly FlowLayoutPanel _mainFavFlow;
        readonly FlowLayoutPanel _secondaryFavFlow;
        readonly FlowLayoutPanel _pairFavFlow;
        Color _ink;
        Color _muted;
        Color _field;
        Color _accent;
        Color _bg;
        readonly List<Panel> _paletteSwatches = new List<Panel>();
        readonly List<Color[]> _history = new List<Color[]>();
        readonly List<Color> _mainFavorites = new List<Color>();
        readonly List<Color> _secondaryFavorites = new List<Color>();
        readonly List<Color[]> _favoritePairs = new List<Color[]>();
        int _activeField;
        Control _armedChip;
        bool _syncing;
        bool _dragging;
        Point _dragStart;
        Point _dragOrigin;

        static readonly Color[] Palette =
        {
            Color.FromArgb(255, 0, 0), Color.FromArgb(0, 200, 0),
            Color.FromArgb(0, 110, 255), Color.FromArgb(245, 245, 248),
            Color.FromArgb(20, 20, 24), Color.FromArgb(255, 79, 163),
            Color.FromArgb(74, 158, 255), Color.FromArgb(52, 199, 89),
            Color.FromArgb(126, 124, 255), Color.FromArgb(255, 184, 46),
            Color.FromArgb(44, 188, 196), Color.FromArgb(236, 214, 122)
        };

        public ColorPairForm(Color accent, Color secondary, Color uiAccent,
            string history, string favorites, string secondaryFavorites, string favoritePairs)
        {
            AccentColor = accent;
            SecondaryColor = secondary;
            History = history ?? "";
            Favorites = favorites ?? "";
            SecondaryFavorites = secondaryFavorites ?? "";
            FavoritePairs = favoritePairs ?? "";
            _accent = uiAccent;
            _ink = GlassStyle.LightMode ? Color.FromArgb(34, 34, 40) : Color.FromArgb(244, 244, 248);
            _muted = GlassStyle.LightMode ? Color.FromArgb(100, 100, 112) : Color.FromArgb(168, 168, 180);
            _field = GlassStyle.LightMode ? Color.FromArgb(238, 238, 243) : Color.FromArgb(42, 42, 50);
            _bg = DialogBackground(accent, secondary);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = _bg;
            ForeColor = _ink;
            Font = MakeFont(20f, FontStyle.Regular);
            ClientSize = new Size(600, 860);
            DoubleBuffered = true;
            using (GraphicsPath gp = RoundedRect(new RectangleF(0, 0, Width, Height), 18f))
                Region = new Region(gp);
            ParseHistory(History);
            ParseFavorites(Favorites, _mainFavorites);
            ParseFavorites(SecondaryFavorites, _secondaryFavorites);
            ParseFavoritePairs(FavoritePairs);

            Label title = new Label();
            title.Text = "自定义主题";
            title.Font = new Font(UiFonts.Family, 15.5f, FontStyle.Bold);
            title.Tag = "title";
            title.ForeColor = _ink;
            title.SetBounds(20, 6, 340, 44);
            title.BackColor = Color.Transparent;
            Controls.Add(title);

            Label close = new Label();
            close.Text = "×";
            close.Font = MakeFont(24f, FontStyle.Bold);
            close.ForeColor = _muted;
            close.BackColor = Color.Transparent;
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.SetBounds(550, 4, 36, 36);
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Applied = false; Close(); };
            Controls.Add(close);

            // 色环加大 30%，左右边界与中间间距一致：70 / 195 / 70。
            _wheelAccent = new ColorWheel();
            _wheelAccent.BackColor = Color.Transparent;
            _wheelAccent.SetBounds(70, 52, 195, 195);
            _wheelAccent.CenterText = "主";
            _wheelAccent.SetColor(accent);
            _wheelAccent.CenterClicked += delegate { _activeField = 0; _rA.Focus(); };
            _wheelAccent.ColorChanged += delegate
            {
                _activeField = 0;
                if (_syncing) return;
                SetRgb(_rA, _gA, _bA, _wheelAccent.SelectedColor);
                RaisePreview();
            };
            Controls.Add(_wheelAccent);

            _wheelSecondary = new ColorWheel();
            _wheelSecondary.BackColor = Color.Transparent;
            _wheelSecondary.SetBounds(335, 52, 195, 195);
            _wheelAccent.HoleColor = _bg;
            _wheelSecondary.HoleColor = _bg;
            _wheelSecondary.CenterText = "辅";
            _wheelSecondary.SetColor(secondary);
            _wheelSecondary.CenterClicked += delegate { _activeField = 1; _rB.Focus(); };
            _wheelSecondary.ColorChanged += delegate
            {
                _activeField = 1;
                if (_syncing) return;
                SetRgb(_rB, _gB, _bB, _wheelSecondary.SelectedColor);
                RaisePreview();
            };
            Controls.Add(_wheelSecondary);

            MakeRgbGroup(70, 256, out _rA, out _gA, out _bA, accent, 0);
            MakeRgbGroup(335, 256, out _rB, out _gB, out _bB, secondary, 1);

            Label paletteLabel = MakeLabel("常用颜色", 20, 310, 560, 32, ContentAlignment.MiddleCenter);
            Controls.Add(paletteLabel);
            for (int i = 0; i < Palette.Length; i++)
            {
                Panel sw = new Panel();
                sw.SetBounds(132 + (i % 6) * 56, 348 + (i / 6) * 56, 48, 48);
                sw.BackColor = Color.Transparent;
                sw.Cursor = Cursors.Hand;
                _paletteSwatches.Add(sw);
                Color pc = Palette[i];
                sw.Paint += delegate(object s, PaintEventArgs e)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush b = new SolidBrush(pc))
                        e.Graphics.FillEllipse(b, 6, 6, 36, 36);
                    using (Pen p = new Pen(GlassStyle.Hairline, 1f))
                        e.Graphics.DrawEllipse(p, 6, 6, 36, 36);
                    if (AppSettings.ActiveThemeIndex == 8 &&
                        (pc.ToArgb() == AccentColor.ToArgb() ||
                         pc.ToArgb() == SecondaryColor.ToArgb()))
                    {
                        using (Pen white = new Pen(MarkFor(pc), 2f))
                        {
                            e.Graphics.DrawEllipse(white, 6, 6, 36, 36);
                            e.Graphics.DrawLines(white, new PointF[] {
                                new PointF(18f, 24f), new PointF(22f, 28f), new PointF(31f, 19f) });
                        }
                    }
                };
                sw.Click += delegate { ApplyColor(pc); };
                Controls.Add(sw);
            }

            Label historyLabel = MakeLabel("最近使用", 20, 456, 560, 24, ContentAlignment.MiddleCenter);
            Controls.Add(historyLabel);

            _historyFlow = new FlowLayoutPanel();
            _historyFlow.SetBounds(138, 486, 324, 60);
            _historyFlow.BackColor = Color.Transparent;
            _historyFlow.WrapContents = false;
            _historyFlow.AutoScroll = false;
            Controls.Add(_historyFlow);
            RefreshHistoryChips();

            Label favoriteLabel = MakeLabel("收藏颜色", 20, 552, 140, 28, ContentAlignment.MiddleLeft);
            favoriteLabel.ForeColor = _ink;
            Controls.Add(favoriteLabel);

            Label mainLabel = MakeLabel("主色", 20, 584, 60, 60, ContentAlignment.MiddleLeft);
            mainLabel.ForeColor = _ink;
            Controls.Add(mainLabel);
            _mainFavFlow = MakeFlow(100, 584, 324, 60);
            _mainFavFlow.WrapContents = false;
            Controls.Add(_mainFavFlow);
            RefreshMainFavoriteChips();
            Button favMain = MakeButton("收藏主色", 460, 599, 120, 30);
            favMain.Click += delegate
            {
                Color c = ReadRgb(_rA, _gA, _bA, AccentColor);
                AddFavorite(c, true);
            };
            Controls.Add(favMain);

            Label secondaryLabel = MakeLabel("辅色", 20, 648, 60, 60, ContentAlignment.MiddleLeft);
            secondaryLabel.ForeColor = _ink;
            Controls.Add(secondaryLabel);
            _secondaryFavFlow = MakeFlow(100, 648, 324, 60);
            _secondaryFavFlow.WrapContents = false;
            Controls.Add(_secondaryFavFlow);
            RefreshSecondaryFavoriteChips();
            Button favSecondary = MakeButton("收藏辅色", 460, 663, 120, 30);
            favSecondary.Click += delegate
            {
                Color c = ReadRgb(_rB, _gB, _bB, SecondaryColor);
                AddFavorite(c, false);
            };
            Controls.Add(favSecondary);

            Label pairLabel = MakeLabel("配套", 20, 712, 60, 60, ContentAlignment.MiddleLeft);
            pairLabel.ForeColor = _ink;
            Controls.Add(pairLabel);
            _pairFavFlow = MakeFlow(100, 712, 324, 60);
            _pairFavFlow.WrapContents = false;
            Controls.Add(_pairFavFlow);
            RefreshPairFavoriteChips();
            Button favPair = MakeButton("收藏搭配", 460, 727, 120, 30);
            favPair.Click += delegate
            {
                Color a = ReadRgb(_rA, _gA, _bA, AccentColor);
                Color b = ReadRgb(_rB, _gB, _bB, SecondaryColor);
                AddFavoritePair(a, b);
                RefreshPairFavoriteChips();
            };
            Controls.Add(favPair);

            Button cancel = MakeButton("取消", 400, 800, 82, 36);
            cancel.Click += delegate { Applied = false; Close(); };
            Controls.Add(cancel);

            Button ok = MakeButton("应用", 490, 800, 88, 36);
            ok.Click += delegate { Accept(); };
            Controls.Add(ok);

            AcceptButton = ok;
            CancelButton = cancel;

            MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left && (e.Y < 46 || e.X <= 18 || e.X >= Width - 18 || e.Y >= Height - 18))
                {
                    _dragging = true;
                    _dragStart = Cursor.Position;
                    _dragOrigin = Location;
                }
            };
            MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (_dragging)
                {
                    Point p = Cursor.Position;
                    Location = new Point(_dragOrigin.X + p.X - _dragStart.X, _dragOrigin.Y + p.Y - _dragStart.Y);
                }
            };
            MouseUp += delegate { _dragging = false; };
            ShiftLayout();
            SetupFavoriteScroll();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateFavoriteScrolls();
        }

        void ShiftLayout()
        {
            for (int i = 0; i < Controls.Count; i++)
            {
                Control c = Controls[i];
                if (object.Equals(c.Tag, "title")) c.Top += 2;
                else if (c.Top >= 250 && c.Top < 300) c.Top += 6;
                else c.Top += 4;
            }
        }

        void SetupFavoriteScroll()
        {
            _historyHScroll = MakeHScroll(_historyFlow);
            _mainHScroll = MakeHScroll(_mainFavFlow);
            _secondaryHScroll = MakeHScroll(_secondaryFavFlow);
            _pairHScroll = MakeHScroll(_pairFavFlow);
            UpdateFavoriteScrolls();
            _mainFavFlow.Top = 1;
            _secondaryFavFlow.Top = 1;
            _pairFavFlow.Top = 1;
        }

        ScrollLite MakeHScroll(FlowLayoutPanel flow)
        {
            Panel clip = new Panel();
            clip.SetBounds(flow.Left, flow.Top, flow.Width, flow.Height);
            clip.BackColor = Color.Transparent;
            Controls.Remove(flow);
            flow.SetBounds(0, 0, flow.Width, flow.Height);
            flow.AutoScroll = false;
            flow.WrapContents = false;
            clip.Controls.Add(flow);
            Controls.Add(clip);

            ScrollLite bar = new ScrollLite();
            bar.Visible = false;
            bar.Height = 8;
            bar.Tag = clip;
            bar.ValueChanged += delegate { flow.Left = -bar.Value; };
            clip.MouseWheel += delegate(object s, MouseEventArgs e) { WheelScroll(bar, e.Delta); };
            flow.MouseWheel += delegate(object s, MouseEventArgs e) { WheelScroll(bar, e.Delta); };
            clip.Controls.Add(bar);
            bar.BringToFront();
            return bar;
        }

        void WheelScroll(ScrollLite bar, int delta)
        {
            if (bar == null || !bar.Visible) return;
            bar.Value = Math.Max(bar.Minimum, Math.Min(bar.Maximum,
                bar.Value - Math.Sign(delta) * bar.SmallChange));
        }

        void UpdateFavoriteScrolls()
        {
            if (_mainHScroll != null) _mainHScroll.ThumbColor = AccentColor;
            if (_secondaryHScroll != null) _secondaryHScroll.ThumbColor = SecondaryColor;
            if (_pairHScroll != null) _pairHScroll.ThumbColor = AccentColor;
            if (_historyHScroll != null) _historyHScroll.ThumbColor = AccentColor;
            UpdateFavoriteScroll(_historyFlow, _historyHScroll);
            UpdateFavoriteScroll(_mainFavFlow, _mainHScroll);
            UpdateFavoriteScroll(_secondaryFavFlow, _secondaryHScroll);
            UpdateFavoriteScroll(_pairFavFlow, _pairHScroll);
        }

        void UpdateFavoriteScroll(FlowLayoutPanel flow, ScrollLite bar)
        {
            if (flow == null || bar == null) return;
            Panel clip = bar.Tag as Panel;
            if (clip == null) return;
            int content = 0;
            for (int i = 0; i < flow.Controls.Count; i++)
                content += flow.Controls[i].Width + flow.Controls[i].Margin.Horizontal;
            int max = Math.Max(0, content - clip.Width);
            flow.Width = Math.Max(content, clip.Width);
            flow.Height = Math.Max(48, clip.Height - 8);
            flow.Left = 0;
            bar.Visible = flow.Controls.Count > 6;
            bar.Minimum = 0;
            bar.LargeChange = Math.Max(1, clip.Width);
            bar.SmallChange = 28;
            bar.Maximum = max;
            bar.Value = 0;
            bar.SetBounds(0, clip.Height - 8, clip.Width, 8);
            bar.BringToFront();
        }

        void MakeRgbGroup(int x, int y, out RgbField r, out RgbField g, out RgbField b, Color c, int field)
        {
            r = MakeRgbBox(x + 22, y, field);
            g = MakeRgbBox(x + 92, y, field);
            b = MakeRgbBox(x + 162, y, field);
            Controls.Add(MakeRgbLabel("R", x, y));
            Controls.Add(MakeRgbLabel("G", x + 70, y));
            Controls.Add(MakeRgbLabel("B", x + 140, y));
            Controls.Add(r); Controls.Add(g); Controls.Add(b);
            SetRgb(r, g, b, c);
        }

        Label MakeRgbLabel(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = _muted;
            l.SetBounds(x, y, 20, 36);
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.BackColor = Color.Transparent;
            return l;
        }

        RgbField MakeRgbBox(int x, int y, int field)
        {
            RgbField box = new RgbField();
            box.SetBounds(x, y, 48, 36);
            box.BackColor = Color.Transparent;
            box.ForeColor = _ink;
            box.Font = MakeFont(20f, FontStyle.Regular);
            box.MaxLength = 3;
            box.Enter += delegate { _activeField = field; };
            box.TextChanged += delegate { if (!_syncing) OnRgbChanged(); };
            box.Leave += delegate { Normalize(box); OnRgbChanged(); };
            return box;
        }

        void OnRgbChanged()
        {
            _syncing = true;
            _wheelAccent.SetColor(ReadRgb(_rA, _gA, _bA, AccentColor));
            _wheelSecondary.SetColor(ReadRgb(_rB, _gB, _bB, SecondaryColor));
            _syncing = false;
            RaisePreview();
        }

        void SetRgb(RgbField r, RgbField g, RgbField b, Color c)
        {
            _syncing = true;
            r.Text = c.R.ToString();
            g.Text = c.G.ToString();
            b.Text = c.B.ToString();
            _syncing = false;
        }

        Color ReadRgb(RgbField r, RgbField g, RgbField b, Color fallback)
        {
            int rr, gg, bb;
            if (!int.TryParse(r.Text, out rr) || !int.TryParse(g.Text, out gg) || !int.TryParse(b.Text, out bb))
                return fallback;
            return Color.FromArgb(255,
                Math.Max(0, Math.Min(255, rr)),
                Math.Max(0, Math.Min(255, gg)),
                Math.Max(0, Math.Min(255, bb)));
        }

        void Normalize(RgbField box)
        {
            int v;
            if (!int.TryParse(box.Text, out v)) v = 0;
            box.Text = Math.Max(0, Math.Min(255, v)).ToString();
        }

        Label MakeLabel(string text, int x, int y, int w, int h, ContentAlignment align)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = _muted;
            l.TextAlign = align;
            l.SetBounds(x, y, w, h);
            l.BackColor = Color.Transparent;
            return l;
        }

        FlowLayoutPanel MakeFlow(int x, int y, int w, int h)
        {
            FlowLayoutPanel p = new FlowLayoutPanel();
            p.SetBounds(x, y, w, h);
            p.BackColor = Color.Transparent;
            p.WrapContents = false;
            p.AutoScroll = false;
            return p;
        }

        Button MakeButton(string text, int x, int y, int w, int h)
        {
            Button b = new PillButton();
            b.Font = MakeFont(20f, FontStyle.Bold);
            b.Text = text;
            b.SetBounds(x, y, w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = PillTrack();
            b.ForeColor = GlassStyle.Ink;
            b.UseVisualStyleBackColor = false;
            using (GraphicsPath gp = RoundedRect(new RectangleF(0, 0, w, h), h / 2f))
                b.Region = new Region(gp);
            b.MouseEnter += delegate
            {
                b.BackColor = _accent;
                b.ForeColor = GlassStyle.OnAccent;
            };
            b.MouseLeave += delegate
            {
                b.BackColor = PillTrack();
                b.ForeColor = GlassStyle.Ink;
            };
            return b;
        }

        public void RefreshThemeVisuals()
        {
            SuspendLayout();
            SendMessage(Handle, 0x000B, IntPtr.Zero, IntPtr.Zero);
            bool light = GlassStyle.LightMode;
            _ink = light ? Color.FromArgb(34, 34, 40) : Color.FromArgb(244, 244, 248);
            _muted = light ? Color.FromArgb(100, 100, 112) : Color.FromArgb(168, 168, 180);
            _field = light ? Color.FromArgb(238, 238, 243) : Color.FromArgb(42, 42, 50);
            int theme = AppSettings.ActiveThemeIndex;
            _accent = AppSettings.ThemeColor(theme);
            Color a = theme == 8 ? AccentColor : _accent;
            Color b = theme == 8 ? SecondaryColor : Color.Empty;
            _bg = DialogBackground(a, b);
            BackColor = _bg;
            _wheelAccent.HoleColor = _bg;
            _wheelSecondary.HoleColor = _bg;
            ApplyVisuals(this);
            _wheelAccent.BackColor = Color.Transparent;
            _wheelSecondary.BackColor = Color.Transparent;
            Font = MakeFont(20f, FontStyle.Regular);
            ResumeLayout();
            Invalidate(true);
            SendMessage(Handle, 0x000B, new IntPtr(1), IntPtr.Zero);
            Refresh();
        }

        void ApplyVisuals(Control parent)
        {
            for (int i = 0; i < parent.Controls.Count; i++)
            {
                Control c = parent.Controls[i];
                Label label = c as Label;
                RgbField box = c as RgbField;
                Button button = c as Button;
                ColorWheel wheel = c as ColorWheel;
                if (label != null)
                {
                    label.ForeColor = _ink;
                    if (object.Equals(label.Tag, "title"))
                        label.Font = new Font(UiFonts.Family, 15.5f, FontStyle.Bold);
                    else
                        label.Font = MakeFont(20f, FontStyle.Regular);
                }
                else if (box != null)
                {
                    box.BackColor = Color.Transparent; box.ForeColor = _ink;
                    box.Font = MakeFont(20f, FontStyle.Regular);
                }
                else if (button != null)
                {
                    button.BackColor = PillTrack();
                    button.ForeColor = GlassStyle.Ink;
                    button.Font = MakeFont(20f, FontStyle.Bold);
                }
                else if (wheel != null) wheel.BackColor = Color.Transparent;
                if (c.Controls.Count > 0) ApplyVisuals(c);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (AppSettings.ActiveMaterial == GlassMaterial.Gradient)
            {
                Color top = Blend(_bg, Color.White, 0.18f);
                Color bottom = Blend(_bg, Color.Black, 0.14f);
                using (LinearGradientBrush lg = new LinearGradientBrush(
                    new RectangleF(0, 0, Width, Height), top, bottom, 90f))
                    e.Graphics.FillRectangle(lg, ClientRectangle);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(_bg))
                    e.Graphics.FillRectangle(b, ClientRectangle);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = RoundedRect(new RectangleF(0, 0, Width, Height), 18f))
            {
                if (AppSettings.ActiveMaterial == GlassMaterial.Gradient)
                {
                    Color top = Blend(_bg, Color.White, 0.18f);
                    Color bottom = Blend(_bg, Color.Black, 0.14f);
                    using (LinearGradientBrush lg = new LinearGradientBrush(
                        new RectangleF(0, 0, Width, Height), top, bottom, 90f))
                        e.Graphics.FillPath(lg, gp);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(_bg))
                        e.Graphics.FillPath(b, gp);
                }
                GraphicsState glowState = e.Graphics.Save();
                e.Graphics.SetClip(gp);
                using (GraphicsPath glow = new GraphicsPath())
                {
                    glow.AddEllipse(Width - 300, -210, 430, 390);
                    using (PathGradientBrush pg = new PathGradientBrush(glow))
                    {
                        pg.CenterColor = Color.FromArgb(38, _accent);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, _accent) };
                        e.Graphics.FillPath(pg, glow);
                    }
                }
                e.Graphics.Restore(glowState);
                using (Pen border = new Pen(GlassStyle.Hairline, 1f))
                    e.Graphics.DrawPath(border, gp);
            }
            base.OnPaint(e);
        }

        static GraphicsPath RoundedRect(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        Color DialogBackground(Color a, Color b)
        {
            if (GlassStyle.LightMode)
                return Blend(Color.FromArgb(248, 248, 251), a, 0.14f);
            if (GlassStyle.PureBlack)
                return Color.FromArgb(255, 9, 9, 11);
            return Blend(Blend(Color.FromArgb(26, 26, 33), a, 0.20f), b, 0.08f);
        }

        static Color Blend(Color x, Color y, float amount)
        {
            return Color.FromArgb(255,
                (int)(x.R + (y.R - x.R) * amount),
                (int)(x.G + (y.G - x.G) * amount),
                (int)(x.B + (y.B - x.B) * amount));
        }

        Color CurrentAccent()
        {
            return AppSettings.ThemeColor(AppSettings.ActiveThemeIndex);
        }

        Color PillTrack()
        {
            return GlassStyle.Track;
        }

        void ApplyColor(Color c)
        {
            if (_activeField == 0) SetRgb(_rA, _gA, _bA, c);
            else SetRgb(_rB, _gB, _bB, c);
            RaisePreview();
        }

        void RaisePreview()
        {
            AccentColor = ReadRgb(_rA, _gA, _bA, AccentColor);
            SecondaryColor = ReadRgb(_rB, _gB, _bB, SecondaryColor);
            _bg = DialogBackground(AccentColor, SecondaryColor);
            BackColor = _bg;
            _wheelAccent.HoleColor = _bg;
            _wheelSecondary.HoleColor = _bg;
            _wheelAccent.BackColor = Color.Transparent;
            _wheelSecondary.BackColor = Color.Transparent;
            ApplyVisuals(this);
            Invalidate(true);
            if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
        }

        void Accept()
        {
            AccentColor = ReadRgb(_rA, _gA, _bA, AccentColor);
            SecondaryColor = ReadRgb(_rB, _gB, _bB, SecondaryColor);
            NormalizeAll();
            Applied = true;
            AddHistory(AccentColor, SecondaryColor);
            if (ApplyRequested != null) ApplyRequested(this, EventArgs.Empty);
        }

        void NormalizeAll()
        {
            Normalize(_rA); Normalize(_gA); Normalize(_bA);
            Normalize(_rB); Normalize(_gB); Normalize(_bB);
        }

        void AddHistory(Color a, Color b)
        {
            List<Color[]> next = new List<Color[]>();
            next.Add(new Color[] { a, b });
            for (int i = 0; i < _history.Count && next.Count < 6; i++)
            {
                Color[] p = _history[i];
                if (p[0].ToArgb() == a.ToArgb() && p[1].ToArgb() == b.ToArgb()) continue;
                next.Add(p);
            }
            _history.Clear();
            _history.AddRange(next);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _history.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(ToHex(_history[i][0])).Append('|').Append(ToHex(_history[i][1]));
            }
            History = sb.ToString();
            RefreshHistoryChips();
        }

        void AddFavorite(Color c, bool main)
        {
            List<Color> list = main ? _mainFavorites : _secondaryFavorites;
            List<Color> next = new List<Color>();
            next.Add(c);
            for (int i = 0; i < list.Count && next.Count < 12; i++)
                if (list[i].ToArgb() != c.ToArgb()) next.Add(list[i]);
            list.Clear();
            list.AddRange(next);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(ToHex(list[i]));
            }
            if (main) { Favorites = sb.ToString(); RefreshMainFavoriteChips(); }
            else { SecondaryFavorites = sb.ToString(); RefreshSecondaryFavoriteChips(); }
        }

        void AddFavoritePair(Color a, Color b)
        {
            List<Color[]> next = new List<Color[]>();
            next.Add(new Color[] { a, b });
            for (int i = 0; i < _favoritePairs.Count && next.Count < 6; i++)
            {
                Color[] p = _favoritePairs[i];
                if (p[0].ToArgb() == a.ToArgb() && p[1].ToArgb() == b.ToArgb()) continue;
                next.Add(p);
            }
            _favoritePairs.Clear();
            _favoritePairs.AddRange(next);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _favoritePairs.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(ToHex(_favoritePairs[i][0])).Append('|').Append(ToHex(_favoritePairs[i][1]));
            }
            FavoritePairs = sb.ToString();
        }

        void RefreshHistoryChips()
        {
            _historyFlow.Controls.Clear();
            if (_history.Count == 0)
            {
                _historyFlow.Controls.Add(MakeEmpty("暂无", _historyFlow.Width, 56, ContentAlignment.MiddleCenter)); UpdateFavoriteScrolls();
                return;
            }
            int total = 0;
            for (int i = 0; i < _history.Count; i++)
            {
                _historyFlow.Controls.Add(MakePairChip(_history[i], 48, 48, 2));
                total += 54;
            }
            UpdateFavoriteScrolls();
        }

        Panel MakePairChip(Color[] pair, int w, int h, int kind)
        {
            Panel chip = new Panel();
            chip.Size = new Size(w, h);
            chip.Margin = new Padding(0, 0, 6, 0);
            chip.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush lg = new LinearGradientBrush(
                    new RectangleF(0, 0, chip.Width, chip.Height), pair[0], pair[1], 45f))
                    e.Graphics.FillEllipse(lg, 3, 3, 42, 42);
                using (Pen p = new Pen(GlassStyle.Hairline, 1f))
                    e.Graphics.DrawEllipse(p, 3, 3, 42, 42);
                DrawChipDecor(e.Graphics, chip, kind, pair[0], pair[1], true);
            };
            chip.Cursor = Cursors.Hand;
            Color a = pair[0], b = pair[1];
            chip.Click += delegate
            {
                if (_armedChip == chip) { RemoveChip(kind, a, b); return; }
                SetRgb(_rA, _gA, _bA, a);
                SetRgb(_rB, _gB, _bB, b);
                RaisePreview();
                ArmChip(chip);
            };
            return chip;
        }

        Panel MakeColorChip(Color c, int kind)
        {
            Panel chip = new Panel();
            chip.Size = new Size(48, 48);
            chip.Margin = new Padding(0, 0, 6, 0);
            chip.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush b = new SolidBrush(c))
                    e.Graphics.FillEllipse(b, 3, 3, 42, 42);
                using (Pen p = new Pen(GlassStyle.Hairline, 1f))
                    e.Graphics.DrawEllipse(p, 3, 3, 42, 42);
                DrawChipDecor(e.Graphics, chip, kind, c, Color.Empty, false);
            };
            chip.Cursor = Cursors.Hand;
            chip.Click += delegate
            {
                if (_armedChip == chip) { RemoveChip(kind, c, Color.Empty); return; }
                ApplyColor(c);
                ArmChip(chip);
            };
            return chip;
        }

        static Color MarkFor(Color c)
        {
            return (c.R > 232 && c.G > 232 && c.B > 232) ? Color.Black : Color.White;
        }

        void ArmChip(Control chip)
        {
            _armedChip = chip;
            chip.Invalidate();
        }

        void DrawChipDecor(Graphics g, Control chip, int kind, Color a, Color b, bool pair)
        {
            bool selected = pair
                ? (a.ToArgb() == AccentColor.ToArgb() && b.ToArgb() == SecondaryColor.ToArgb())
                : (kind == 0
                    ? a.ToArgb() == AccentColor.ToArgb()
                    : a.ToArgb() == SecondaryColor.ToArgb());
            if (selected && AppSettings.ActiveThemeIndex == 8)
            {
                using (Pen white = new Pen(MarkFor(a), 2f))
                {
                    g.DrawEllipse(white, 1.5f, 1.5f, 45f, 45f);
                    g.DrawLines(white, new PointF[] {
                        new PointF(18f, 25f), new PointF(22f, 29f), new PointF(31f, 20f) });
                }
            }
            if (_armedChip == chip)
            {
                using (SolidBrush dark = new SolidBrush(Color.FromArgb(225, 20, 20, 24)))
                    g.FillEllipse(dark, 32, 0, 16, 16);
                using (Pen white = new Pen(Color.White, 1.7f))
                {
                    g.DrawLine(white, 36, 4, 44, 12);
                    g.DrawLine(white, 44, 4, 36, 12);
                }
            }
        }

        void RemoveChip(int kind, Color a, Color b)
        {
            if (kind == 0) _mainFavorites.RemoveAll(delegate(Color c) { return c.ToArgb() == a.ToArgb(); });
            else if (kind == 1) _secondaryFavorites.RemoveAll(delegate(Color c) { return c.ToArgb() == a.ToArgb(); });
            else if (kind == 2) _history.RemoveAll(delegate(Color[] p) { return p[0].ToArgb() == a.ToArgb() && p[1].ToArgb() == b.ToArgb(); });
            else _favoritePairs.RemoveAll(delegate(Color[] p) { return p[0].ToArgb() == a.ToArgb() && p[1].ToArgb() == b.ToArgb(); });
            if (kind == 0) Favorites = SerializeColors(_mainFavorites);
            else if (kind == 1) SecondaryFavorites = SerializeColors(_secondaryFavorites);
            else if (kind == 2) History = SerializePairs(_history);
            else FavoritePairs = SerializePairs(_favoritePairs);
            _armedChip = null;
            if (kind == 0) RefreshMainFavoriteChips();
            else if (kind == 1) RefreshSecondaryFavoriteChips();
            else if (kind == 2) RefreshHistoryChips();
            else RefreshPairFavoriteChips();
            UpdateFavoriteScrolls();
        }

        static Font MakeFont(float px, FontStyle style)
        {
            if (UiFonts.Family == "楷体") px += 1.8f;
            return new Font(UiFonts.Family, px, style, GraphicsUnit.Pixel);
        }

        static string SerializeColors(List<Color> list)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(ToHex(list[i]));
            }
            return sb.ToString();
        }

        static string SerializePairs(List<Color[]> list)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(ToHex(list[i][0])).Append('|').Append(ToHex(list[i][1]));
            }
            return sb.ToString();
        }

        Label MakeEmpty(string text, int w, int h, ContentAlignment align)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = _muted;
            l.TextAlign = align;
            l.AutoSize = false;
            l.Size = new Size(w, h);
            l.Margin = new Padding(0);
            l.BackColor = Color.Transparent;
            return l;
        }

        void RefreshMainFavoriteChips()
        {
            _mainFavFlow.Controls.Clear();
            if (_mainFavorites.Count == 0) { _mainFavFlow.Controls.Add(MakeEmpty("暂无主色", _mainFavFlow.Width, 60, ContentAlignment.MiddleLeft)); return; }
            for (int i = 0; i < _mainFavorites.Count; i++) _mainFavFlow.Controls.Add(MakeColorChip(_mainFavorites[i], 0));
            UpdateFavoriteScrolls();
        }

        void RefreshSecondaryFavoriteChips()
        {
            _secondaryFavFlow.Controls.Clear();
            if (_secondaryFavorites.Count == 0) { _secondaryFavFlow.Controls.Add(MakeEmpty("暂无辅色", _secondaryFavFlow.Width, 60, ContentAlignment.MiddleLeft)); return; }
            for (int i = 0; i < _secondaryFavorites.Count; i++) _secondaryFavFlow.Controls.Add(MakeColorChip(_secondaryFavorites[i], 1));
            UpdateFavoriteScrolls();
        }

        void RefreshPairFavoriteChips()
        {
            _pairFavFlow.Controls.Clear();
            if (_favoritePairs.Count == 0) { _pairFavFlow.Controls.Add(MakeEmpty("暂无搭配", _pairFavFlow.Width, 60, ContentAlignment.MiddleLeft)); return; }
            for (int i = 0; i < _favoritePairs.Count; i++)
            {
                Panel pairChip = MakePairChip(_favoritePairs[i], 48, 48, 3);
                pairChip.Margin = new Padding(0, 2, 6, 0);
                _pairFavFlow.Controls.Add(pairChip);
            }
        }

        void ParseHistory(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] parts = text.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length && _history.Count < 6; i++)
            {
                string[] pair = parts[i].Split('|');
                if (pair.Length != 2) continue;
                Color a, b;
                if (TryColor(pair[0], out a) && TryColor(pair[1], out b))
                    _history.Add(new Color[] { a, b });
            }
        }

        void ParseFavorites(string text, List<Color> list)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] parts = text.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length && list.Count < 12; i++)
            {
                Color c;
                if (TryColor(parts[i], out c)) list.Add(c);
            }
        }

        void ParseFavoritePairs(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] parts = text.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length && _favoritePairs.Count < 6; i++)
            {
                string[] pair = parts[i].Split('|');
                if (pair.Length != 2) continue;
                Color a, b;
                if (TryColor(pair[0], out a) && TryColor(pair[1], out b))
                    _favoritePairs.Add(new Color[] { a, b });
            }
        }

        static string ToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        class PillButton : Button
        {
            public PillButton()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer, true);
                FlatStyle = FlatStyle.Flat;
                UseVisualStyleBackColor = false;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(BackColor))
                    e.Graphics.FillRectangle(b, ClientRectangle);
                Color textColor = ForeColor;
                if (BackColor.GetBrightness() < 0.45f) textColor = Color.White;
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        class RgbField : Control
        {
            public int MaxLength = 3;

            public RgbField()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor |
                    ControlStyles.Selectable, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.IBeam;
                TabStop = true;
            }

            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                Focus();
                base.OnMouseDown(e);
            }

            protected override void OnKeyPress(KeyPressEventArgs e)
            {
                if (char.IsDigit(e.KeyChar) && Text.Length < MaxLength)
                {
                    Text += e.KeyChar;
                    e.Handled = true;
                }
                base.OnKeyPress(e);
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Back && Text.Length > 0)
                {
                    Text = Text.Substring(0, Text.Length - 1);
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Delete)
                {
                    Text = "";
                    e.Handled = true;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    Parent.SelectNextControl(this, true, true, true, true);
                    e.Handled = true;
                }
                base.OnKeyDown(e);
            }
        }

        class ScrollLite : Control
        {
            public int Minimum, Maximum, LargeChange = 1, SmallChange = 28;
            public Color ThumbColor = Color.Empty;
            int _value;
            bool _dragging;
            int _dragX, _dragValue;
            public event EventHandler ValueChanged;

            public int Value
            {
                get { return _value; }
                set
                {
                    int v = Math.Max(Minimum, Math.Min(Maximum, value));
                    if (v == _value) return;
                    _value = v;
                    Invalidate();
                    if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                }
            }

            public ScrollLite()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
                Height = 8;
            }

            Rectangle Thumb()
            {
                float span = Math.Max(1, Maximum);
                int track = Math.Max(1, Width - 4);
                int thumbW = Math.Max(24, (int)(track * (float)LargeChange / (span + LargeChange)));
                int x = 2 + (Maximum <= 0 ? 0 : (int)((track - thumbW) * (float)Value / Maximum));
                return new Rectangle(x, 1, thumbW, Height - 2);
            }

            static GraphicsPath Round(RectangleF r, float rad)
            {
                GraphicsPath gp = new GraphicsPath();
                float d = rad * 2f;
                gp.AddArc(r.X, r.Y, d, d, 180, 90);
                gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                gp.CloseFigure();
                return gp;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color track = GlassStyle.LightMode
                    ? Color.FromArgb(28, 0, 0, 0) : Color.FromArgb(48, 255, 255, 255);
                Color accent = ThumbColor == Color.Empty
                    ? AppSettings.ThemeColor(AppSettings.ActiveThemeIndex) : ThumbColor;
                using (GraphicsPath gp = Round(new RectangleF(0, 1, Width, Height - 2), 3f))
                using (SolidBrush b = new SolidBrush(track))
                    e.Graphics.FillPath(b, gp);
                Rectangle t = Thumb();
                using (GraphicsPath gp = Round(new RectangleF(t.X, t.Y, t.Width, t.Height), 3f))
                using (SolidBrush b = new SolidBrush(accent))
                    e.Graphics.FillPath(b, gp);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (Thumb().Contains(e.Location))
                    {
                        _dragging = true;
                        _dragX = e.X;
                        _dragValue = Value;
                    }
                    else
                    {
                        Value = Maximum <= 0 ? 0 : (int)(Maximum * (float)e.X / Math.Max(1, Width));
                    }
                }
                base.OnMouseDown(e);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                if (_dragging)
                {
                    int track = Math.Max(1, Width - 4 - Thumb().Width);
                    int delta = (int)((e.X - _dragX) * (float)Maximum / Math.Max(1, track));
                    Value = _dragValue + delta;
                }
                base.OnMouseMove(e);
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                _dragging = false;
                base.OnMouseUp(e);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                Value = Value - Math.Sign(e.Delta) * SmallChange;
                base.OnMouseWheel(e);
            }
        }

        static bool TryColor(string text, out Color c)
        {
            c = Color.Empty;
            if (string.IsNullOrEmpty(text)) return false;
            string s = text.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            if (s.Length != 6) return false;
            int value;
            if (!int.TryParse(s, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value)) return false;
            c = Color.FromArgb(255, (value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
            return true;
        }
    }
}
