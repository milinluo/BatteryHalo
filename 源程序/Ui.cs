using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TriMode
{
    public class MainForm : Form
    {
        const int W = 276;                 // logical width
        const int PadX = 10;
        const int CardH = 48;
        const int CardGap = 5;
        const int HeaderH = 38;
        const int FooterH = 0;
        const int CollapsedW = 78;
        const int BottomPad = 8;
        const float Radius = 11f;   // fallback radius used by card helpers
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TRANSPARENT = 0x00000020;
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        float _cornerRadius = 11f;

        ScanResult _data;
        AppSettings _settings;
        readonly object _sync = new object();
        Thread _worker;
        volatile bool _stop;
        float _scale = 1f;
        float _dpiScale = 1f;
        float _userScale = 1f;
        Color _theme = Color.FromArgb(255, 79, 163);
        GlassMaterial _material = GlassMaterial.Gradient;
        bool _showMode = true;
        float _chargePhase;
        System.Windows.Forms.Timer _liquidTimer;
        bool _dragging;
        Point _dragOffset;
        Point _dragStartLocation;
        int _scroll;
        RectangleF _closeRect, _gearRect, _chevronRect;
        NotifyIcon _tray;
        TaskbarBadge _taskbarBadge;
        ToolStripMenuItem _taskbarItem;
        ThemedContextMenu _menu;
        ThemedContextMenu _panelMenu;
        ToolStripMenuItem _panelToggleItem;

        DateTime _lastUpdate = DateTime.MinValue;
        string _hotButton = "";
        int _hotCard = -1;

        System.Windows.Forms.Timer _skeletonTimer;
        float _shimmer;
        int _trayLevel = int.MinValue;
        int _trayTheme = -1;
        string _trayText = "";
        bool _trayMenuOpen;
        bool _captureMode;

        bool _collapsed;
        SettingsForm _settingsForm;
        Dictionary<string, DateTime> _lastLowNotify = new Dictionary<string, DateTime>();
        readonly Dictionary<string, Device> _history = new Dictionary<string, Device>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<ulong, int> _manualBattery = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, Device> _manualDevices = new Dictionary<ulong, Device>();
        readonly Dictionary<ulong, DateTime> _manualUntil = new Dictionary<ulong, DateTime>();
        readonly Dictionary<ulong, DateTime> _lastManualRefresh = new Dictionary<ulong, DateTime>();
        readonly Dictionary<ulong, DateTime> _batteryErrorAt = new Dictionary<ulong, DateTime>();
        readonly Dictionary<ulong, string> _manualErrorText = new Dictionary<ulong, string>();
        readonly List<Device> _refreshDevices = new List<Device>();
        readonly List<RectangleF> _refreshHits = new List<RectangleF>();
        readonly List<string> _deviceOrder = new List<string>();
        bool _deviceOrderLoaded;
        int _dragCardIndex = -1;
        PointF _dragCardStart;
        bool _cardDragging;
        ulong _hotRefresh;
        System.Windows.Forms.Timer _headsetTimer;
        bool _historyLoaded;
        string _historySavedText = "";
        readonly HashSet<string> _lastPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<ulong> _autoHeadsetPending = new HashSet<ulong>();
        readonly Dictionary<ulong, int> _btBatteryMemory = new Dictionary<ulong, int>();
        readonly Dictionary<ulong, DateTime> _autoHeadsetNext = new Dictionary<ulong, DateTime>();
        bool _ignoredLoaded;
        volatile bool _headsetReading;
        readonly List<Device> _deleteDevices = new List<Device>();
        readonly List<RectangleF> _deleteRects = new List<RectangleF>();

        static string HistoryPath
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设备记录.txt"); }
        }

        static string IgnorePath
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "忽略设备.txt"); }
        }

        static string OrderPath
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设备排序.txt"); }
        }

        public MainForm(EventWaitHandle showEvent) : this(showEvent, false) { }

        public MainForm(EventWaitHandle showEvent, bool skeletonOnly)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoScaleMode = AutoScaleMode.None;      // we scale coordinates ourselves; WinForms must not scale mouse input again
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AllowTransparency = true;            // layered window -> acrylic blur shows through per-pixel alpha
            BackColor = Color.FromArgb(28, 28, 32);
            ShowInTaskbar = false;
            Text = "电环";
            try { _dpiScale = CreateGraphics().DpiX / 96f; }
            catch { _dpiScale = 1f; }
            if (_dpiScale < 1f) _dpiScale = 1f;

            _settings = AppSettings.Load();
            AppSettings.Activate(_settings);
            _cornerRadius = _settings.CornerRadius;
            UiFonts.Family = _settings.Font;
            _userScale = _settings.UiScale;
            _theme = AppSettings.ThemeColor(_settings.Theme);
            _material = _settings.Material;
            _showMode = _settings.ShowMode;
            _scale = _dpiScale * _userScale;

            BuildTray();
            _collapsed = true;
            ApplyUserScale();
            ApplySize(2);                 // two skeleton rows while the first scan is running
            // default position: top-right
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width, wa.Top);

            _panelMenu = new ThemedContextMenu();
            _panelToggleItem = new ToolStripMenuItem(_collapsed ? "展开" : "收起");
            _panelToggleItem.Click += delegate { ToggleCollapsed(); };
            _panelMenu.Items.Add(_panelToggleItem);
            _panelMenu.Items.Add("设置", null, delegate { ShowSettingsDialog(); });
            _panelMenu.Items.Add("退出", null, delegate { Quit(); });
            _panelMenu.Closed += delegate
            {
                // Force a full repaint after the popup disappears; otherwise
                // layered-window pixels behind the menu can be left as ghosts.
                BeginInvoke(new Action(delegate { Invalidate(); Update(); }));
            };
            MenuTheme.Apply(_panelMenu, _theme);

            _skeletonTimer = new System.Windows.Forms.Timer();
            _skeletonTimer.Interval = 40;
            _skeletonTimer.Tick += delegate
            {
                if (_data != null) { _skeletonTimer.Stop(); return; }
                _shimmer += 0.028f;
                if (_shimmer > 1f) _shimmer -= 1f;
                Invalidate();
            };

            _liquidTimer = new System.Windows.Forms.Timer();
            _liquidTimer.Interval = 100;
            _liquidTimer.Tick += delegate
            {
                if (!Visible) { _liquidTimer.Stop(); return; }
                if (!HasChargingDevice()) { _liquidTimer.Stop(); return; }
                _chargePhase += 0.07f;
                if (_chargePhase > 1f) _chargePhase -= 1f;
                Invalidate();
            };

            _headsetTimer = new System.Windows.Forms.Timer();
            _headsetTimer.Interval = 60000;
            _headsetTimer.Tick += delegate { AutoRefreshHeadset(); };
            _headsetTimer.Start();

            AirPodsBridge.Start();
            AirPodsBridge.SetEarControl(_settings.AirPodsEarControl);
            if (!skeletonOnly) StartWorker();

            if (showEvent != null)
            {
                Thread showThread = new Thread(delegate ()
                {
                    while (!_stop)
                    {
                        bool signaled = showEvent.WaitOne(2000, false);
                        if (!signaled) continue;    // timeout must NOT reopen the panel
                        if (_stop) break;
                        try { BeginInvoke(new Action(delegate { ShowPanel(); })); }
                        catch { }
                    }
                });
                showThread.IsBackground = true;
                showThread.Start();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // Desktop widget: keep the main panel out of Alt+Tab.
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        // ---------- geometry ----------
        void EdgeRadii(float radius, out float tl, out float tr, out float br, out float bl)
        {
            if (_settings != null && !_settings.AutoFlattenCorners)
            {
                tl = tr = br = bl = radius;
                return;
            }
            Rectangle wa;
            try { wa = Screen.FromControl(this).WorkingArea; }
            catch { wa = Screen.PrimaryScreen.WorkingArea; }
            bool left = Left <= wa.Left + 1;
            bool right = Right >= wa.Right - 1;
            bool top = Top <= wa.Top + 1;
            bool bottom = Bottom >= wa.Bottom - 1;
            tl = (left || top) ? 0f : radius;
            tr = (right || top) ? 0f : radius;
            br = (right || bottom) ? 0f : radius;
            bl = (left || bottom) ? 0f : radius;
        }

        void UpdateShape()
        {
            if (Width <= 0 || Height <= 0) return;
            float r = _cornerRadius * _scale;
            float tl, tr, br, bl;
            EdgeRadii(r, out tl, out tr, out br, out bl);
            using (GraphicsPath gp = GlassStyle.RoundedRect(new RectangleF(0, 0, Width, Height), tl, tr, br, bl))
            {
                if (Region != null) Region.Dispose();
                Region = new Region(gp);
            }
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (IsHandleCreated)
            {
                UpdateShape();
                Invalidate();
            }
        }

        void ApplySize(int count)
        {
            Rectangle old = new Rectangle(Location, Size);
            int n = Math.Max(count, 1);
            int active = ActiveDeviceCount();
            int logicalW = _collapsed ? (14 + Math.Max(1, active) * 52) : W;
            int logicalH = _collapsed ? 34 : (HeaderH + n * CardH + (n - 1) * CardGap + FooterH + BottomPad);
            Size = new Size((int)(logicalW * _scale), (int)(logicalH * _scale));
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (old.Width > 0 && old.Height > 0)
            {
                // anchor top-right so shrinking collapses toward the corner
                Location = new Point(old.Right - Width, Location.Y);
            }
            ClampToScreen();
            UpdateShape();
            Invalidate();
        }

        public event EventHandler PanelStateChanged;
        public event EventHandler PreviewChanged;

        public bool IsPanelCollapsed
        {
            get { return _collapsed; }
        }

        bool ShowOfflineDevices
        {
            get { return !_collapsed && _settings != null && _settings.ShowOffline; }
        }

        public void SetPanelExpanded(bool expanded)
        {
            if (_collapsed == !expanded) return;
            ToggleCollapsed();
        }

        void ApplyUserScale()
        {
            int idx = AppSettings.ScaleIndex(_settings.UiScale);
            float expanded = idx == 0 ? 1f : (idx == 1 ? 1.1f : 1.2f);
            float collapsed = idx == 0 ? 1f : (idx == 1 ? 1.2f : 1.4f);
            _userScale = _collapsed ? collapsed : expanded;
            _scale = _dpiScale * _userScale;
        }

        public void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            ApplyUserScale();
            ApplySize(DataRowCount());
            Refresh();   // clear any stale expanded-card pixels after resizing
            if (PanelStateChanged != null) PanelStateChanged(this, EventArgs.Empty);
        }

        int DataRowCount()
        {
            lock (_sync)
            {
                if (_data == null) return 2;
                if (ShowOfflineDevices) return Math.Max(1, _data.Devices.Count);
                int n = 0;
                for (int i = 0; i < _data.Devices.Count; i++)
                    if (!_data.Devices[i].Offline) n++;
                return Math.Max(1, n);
            }
        }

        int ActiveDeviceCount()
        {
            lock (_sync)
            {
                if (_data == null) return 0;
                int n = 0;
                for (int i = 0; i < _data.Devices.Count; i++)
                    if (!_data.Devices[i].Offline) n++;
                return n;
            }
        }

        bool ChargingVisual(Device d)
        {
            if (d == null || !d.Charging) return false;
            if (d.IsApple && d.AppleLeft >= 100 && d.AppleRight >= 100 && !d.AppleCaseCharging)
                return false;
            return true;
        }

        bool HasChargingDevice()
        {
            lock (_sync)
            {
                if (_data == null) return false;
                for (int i = 0; i < _data.Devices.Count; i++)
                    if (!_data.Devices[i].Offline && _data.Devices[i].Charging) return true;
                return false;
            }
        }

        void UpdateMotionTimer()
        {
            if (_liquidTimer == null || _captureMode) return;
            if (Visible && HasChargingDevice())
                _liquidTimer.Start();
            else
                _liquidTimer.Stop();
        }

        void ClampToScreen()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int x = Location.X, y = Location.Y;
            if (x + Width > wa.Right) x = wa.Right - Width;
            if (y + Height > wa.Bottom) y = wa.Bottom - Height;
            if (x < wa.Left) x = wa.Left;
            if (y < wa.Top) y = wa.Top;
            Location = new Point(x, y);
        }

        static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
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

        // points -> logical pixels, so text scales with the same transform as coordinates
        static Font F(string family, float pt, FontStyle style)
        {
            string use = family == "Microsoft YaHei UI" ? UiFonts.Family : family;
            float px = pt * 4f / 3f;
            if (use == "楷体") px += 1f;
            return UiFonts.Create(use, (float)Math.Round(px), style);
        }

        static float KaiShift
        {
            get { return UiFonts.Family == "楷体" ? 2f : 0f; }
        }

        // ---------- painting ----------
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.TextContrast = 4;
            g.ScaleTransform(_scale, _scale);

            List<Device> devs;
            DateTime stamp;
            bool loading;
            lock (_sync)
            {
                loading = _data == null;
                devs = loading ? new List<Device>() : _data.Devices;
                if (!ShowOfflineDevices) devs = devs.FindAll(delegate(Device d) { return !d.Offline; });
                stamp = _lastUpdate;
            }

            _deleteDevices.Clear();
            _deleteRects.Clear();
            _refreshDevices.Clear();
            _refreshHits.Clear();
            DrawBackground(g);
            DrawHeader(g, devs.Count, stamp, loading);
            if (loading) DrawSkeletonCards(g);
            else DrawCards(g, devs);
            DrawBorder(g);
        }


        void DrawBackground(Graphics g)
        {
            float w = Width / _scale, h = Height / _scale;
            RectangleF full = new RectangleF(0, 0, w, h);
            float tl, tr, br, bl;
            EdgeRadii(_cornerRadius, out tl, out tr, out br, out bl);
            using (GraphicsPath gp = GlassStyle.RoundedRect(full, tl, tr, br, bl))
            {
                if (_material == GlassMaterial.Solid)
                {
                    using (SolidBrush b = new SolidBrush(GlassStyle.SolidBackground(_theme)))
                        g.FillPath(b, gp);
                    return;
                }

                using (LinearGradientBrush lg = new LinearGradientBrush(
                    full,
                    GlassStyle.GradientTop(_theme),
                    GlassStyle.GradientBottom(_theme),
                    LinearGradientMode.Vertical))
                {
                    g.FillPath(lg, gp);
                }
                using (LinearGradientBrush shine = new LinearGradientBrush(
                    full,
                    Color.FromArgb(18, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    LinearGradientMode.Vertical))
                {
                    g.FillPath(shine, gp);
                }
            }
        }

        void DrawBorder(Graphics g)
        {
            // no border
        }

        void DrawHeader(Graphics g, int count, DateTime stamp, bool loading)
        {
            float w = Width / _scale;
            List<Device> devs;
            lock (_sync) devs = _data == null ? new List<Device>() : _data.Devices;
            if (!ShowOfflineDevices) devs = devs.FindAll(delegate (Device d) { return !d.Offline; });
            int? lowest = null;
            foreach (Device d in devs) if (d.Battery >= 0) lowest = (lowest == null ? d.Battery : Math.Min(lowest.Value, d.Battery));

            if (_collapsed)
            {
                if (loading)
                {
                    int alpha = 34 + (int)(_shimmer * 28f);
                    using (Pen p = new Pen(Color.FromArgb(alpha + 20, 255, 255, 255), 1.6f))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                    {
                        float cx = 17f, cy = 17f;
                        for (int i = 0; i < 2; i++)
                        {
                            g.DrawEllipse(p, cx - 6f, cy - 6f, 12f, 12f);
                            using (GraphicsPath gp = Round(new RectangleF(cx + 10f, cy - 2f, 20f, 4f), 2f))
                                g.FillPath(b, gp);
                            cx += 44f;
                        }
                    }
                }
                else
                {
                    // one mini cell per device: kind icon + ring with % inside
                    float cell = 8f, cy = 17f, rad = 14f;
                    for (int i = 0; i < devs.Count; i++)
                    {
                        Device d = devs[i];
                        RectangleF icon = new RectangleF(cell, 9f, 16f, 16f);
                        using (GraphicsPath tile = Round(icon, 5f))
                        using (SolidBrush tb = new SolidBrush(GlassStyle.Tile))
                            g.FillPath(tb, tile);
                        DrawGlyph(g, d.Kind, new RectangleF(icon.X + 1f, icon.Y + 1f, 14f, 14f));

                        float cx = cell + 33f;
                        RectangleF ring = new RectangleF(cx - rad, cy - rad, rad * 2f, rad * 2f);
                        Color lvl = GlassStyle.Muted;
                        if (d.Battery >= 0 && d.Battery < 20) lvl = GlassStyle.LowBattery;
                        else if (d.Battery >= 0 && !d.Cached) lvl = _theme;

                        Color chargingColor = Color.FromArgb(255, 212, 175, 55); // metallic gold, distinct from theme yellow

                        using (Pen back = new Pen(GlassStyle.Hairline, 2.2f))
                            g.DrawEllipse(back, ring);
                        float sweep = 3.6f * Math.Max(0, Math.Min(100, d.Battery));
                        if (ChargingVisual(d) && d.Battery >= 0)
                        {
                            for (int gl = 0; gl < 2; gl++)
                            using (Pen glow = new Pen(Color.FromArgb(34 + gl * 20, chargingColor), 5.2f - gl * 1.8f))
                            {
                                glow.StartCap = LineCap.Round; glow.EndCap = LineCap.Round;
                                g.DrawArc(glow, ring, -90f, sweep);
                            }
                            using (Pen fg = new Pen(chargingColor, 2.4f))
                            {
                                fg.StartCap = LineCap.Round; fg.EndCap = LineCap.Round;
                                g.DrawArc(fg, ring, -90f, sweep);
                            }
                        }
                        else if (d.Battery >= 0)
                        using (Pen fg = new Pen(lvl, 2.2f))
                        {
                            fg.StartCap = LineCap.Round; fg.EndCap = LineCap.Round;
                            g.DrawArc(fg, ring, -90f, sweep);
                        }

                        string s = d.Battery >= 0 ? d.Battery.ToString() : "--";
                        Color textColor = ChargingVisual(d)
                            ? chargingColor
                            : (d.Battery >= 0 && d.Battery < 20
                                ? GlassStyle.LowBattery
                                : (d.Battery >= 0 && !d.Cached ? _theme : GlassStyle.Muted));
                        bool manual = d.ManualRefresh && d.BtAddress != 0;
                        bool refreshHover = manual && (_hotRefresh == d.BtAddress || _headsetReading);
                        if (refreshHover)
                        {
                            GlassStyle.DrawRefreshIcon(g, new RectangleF(cx - 7f, cy - 7f, 14f, 14f), GlassStyle.Ink);
                        }
                        else
                        {
                            using (Font fPct = F(UiFonts.Family, s.Length >= 3 ? 8.0f : 8.8f, FontStyle.Bold))
                            using (SolidBrush b = new SolidBrush(textColor))
                            {
                                StringFormat sf = new StringFormat();
                                sf.Alignment = StringAlignment.Center;
                                sf.LineAlignment = StringAlignment.Center;
                                RectangleF textRing = ring;
                                textRing.Y += KaiShift;
                                g.DrawString(s, fPct, b, textRing, sf);
                            }
                        }
                        if (manual)
                        {
                            _refreshHits.Add(ring);
                            _refreshDevices.Add(d);
                        }
                        cell += 52f;
                    }
                }
                _chevronRect = RectangleF.Empty;
                _closeRect = RectangleF.Empty;
                _gearRect = RectangleF.Empty;
            }
            else
            {
                using (Font fTitle = F("Microsoft YaHei UI", 11.5f, FontStyle.Bold))
                using (Font fMeta = F("Microsoft YaHei UI", 7.0f, FontStyle.Regular))
                using (SolidBrush bTitle = new SolidBrush(GlassStyle.Ink))
                using (SolidBrush bMeta = new SolidBrush(GlassStyle.Ink))
                {
                    string title = "设备电量";
                    float tw = g.MeasureString(title, fTitle, 1000, StringFormat.GenericTypographic).Width;
                    g.DrawString(title, fTitle, bTitle, PadX, 8 + KaiShift, StringFormat.GenericTypographic);
                    string meta = loading ? "正在读取" : (count == 0 ? "未发现设备" : count + " 台");
                    g.DrawString(meta, fMeta, bMeta, PadX + tw + 6, 13 + KaiShift, StringFormat.GenericTypographic);
                }
                _closeRect = new RectangleF(W - PadX - 18, 9, 18, 18);
                _chevronRect = new RectangleF(W - PadX - 38, 9, 18, 18);
                _gearRect = new RectangleF(W - PadX - 58, 9, 18, 18);
            }
            if (!_collapsed)
            {
                DrawIconButton(g, _gearRect, "gear");
                DrawIconButton(g, _chevronRect, "collapse");
                DrawIconButton(g, _closeRect, "close");
            }
        }

        void DrawIconButton(Graphics g, RectangleF r, string kind)
        {
            bool hot = _hotButton == kind;
            if (hot)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(36, 255, 255, 255)))
                using (GraphicsPath gp = Round(r, 6f))
                    g.FillPath(b, gp);
            }

            Color ink = hot ? Color.FromArgb(240, 240, 245) : Color.FromArgb(160, 160, 168);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            using (Pen p = new Pen(ink, 1.25f))
            using (SolidBrush bb = new SolidBrush(ink))
            {
                if (kind == "close")
                {
                    g.DrawLine(p, cx - 3f, cy - 3f, cx + 3f, cy + 3f);
                    g.DrawLine(p, cx + 3f, cy - 3f, cx - 3f, cy + 3f);
                }
                else if (kind == "collapse")
                {
                    g.DrawLine(p, cx - 4f, cy - 1f, cx, cy + 2.5f);
                    g.DrawLine(p, cx + 4f, cy - 1f, cx, cy + 2.5f);
                }
                else if (kind == "expand")
                {
                    g.DrawLine(p, cx - 4f, cy + 1.5f, cx, cy - 2f);
                    g.DrawLine(p, cx + 4f, cy + 1.5f, cx, cy - 2f);
                }
                else if (kind == "gear")
                {
                    using (Pen gp = new Pen(ink, 1.35f))
                    {
                        gp.StartCap = LineCap.Round;
                        gp.EndCap = LineCap.Round;
                        for (int i = 0; i < 6; i++)
                        {
                            double a = i * Math.PI / 3.0;
                            float x1 = cx + (float)Math.Cos(a) * 3.9f;
                            float y1 = cy + (float)Math.Sin(a) * 3.9f;
                            float x2 = cx + (float)Math.Cos(a) * 5.4f;
                            float y2 = cy + (float)Math.Sin(a) * 5.4f;
                            g.DrawLine(gp, x1, y1, x2, y2);
                        }
                        g.DrawEllipse(gp, cx - 3.9f, cy - 3.9f, 7.8f, 7.8f);
                    }
                    g.FillEllipse(bb, cx - 1.15f, cy - 1.15f, 2.3f, 2.3f);
                }
                else
                {
                    g.DrawArc(p, cx - 4.4f, cy - 4.4f, 8.8f, 8.8f, 45, 270);
                    g.FillEllipse(bb, cx + 1.5f, cy - 6.4f, 3.3f, 3.3f);
                }
            }
        }

        void DrawSkeletonCards(Graphics g)
        {
            for (int i = 0; i < 2; i++)
            {
                float y = HeaderH + i * (CardH + CardGap);
                if (y > Height / _scale) break;
                DrawSkeletonCard(g, new RectangleF(PadX, y, W - PadX * 2, CardH));
            }
        }

        void DrawSkeletonCard(Graphics g, RectangleF r)
        {
            GlassStyle.DrawDropShadow(g, r, 10f, 3, 8, 1.2f);
            using (GraphicsPath gp = Round(r, 10f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(72, 30, 30, 38)))
                g.FillPath(b, gp);

            RectangleF tile = new RectangleF(r.X + 8, r.Y + (r.Height - 30f) / 2f, 30, 30);
            Color block = Color.FromArgb(40, 255, 255, 255);
            using (SolidBrush b = new SolidBrush(block))
            {
                using (GraphicsPath gp = Round(tile, 8f))
                    g.FillPath(b, gp);
                using (GraphicsPath gp = Round(new RectangleF(tile.Right + 9, r.Y + 12, 78, 7), 3.5f))
                    g.FillPath(b, gp);
                using (GraphicsPath gp = Round(new RectangleF(tile.Right + 9, r.Y + 26, 112, 5), 2.5f))
                    g.FillPath(b, gp);
                using (GraphicsPath gp = Round(new RectangleF(r.Right - 56, r.Y + 20, 34, 8), 4f))
                    g.FillPath(b, gp);
            }

            using (Pen p = new Pen(Color.FromArgb(48, 255, 255, 255), 3f))
                g.DrawEllipse(p, r.Right - 86f, r.Y + 13f, 22f, 22f);

            GraphicsState st = g.Save();
            using (GraphicsPath clip = Round(r, 10f))
            {
                g.SetClip(clip, CombineMode.Intersect);
                float band = 110f;
                float x = r.X - band + _shimmer * (r.Width + band * 2f);
                RectangleF bandRect = new RectangleF(x, r.Y, band, r.Height);
                using (LinearGradientBrush lg = new LinearGradientBrush(bandRect,
                    Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                {
                    ColorBlend cb = new ColorBlend(3);
                    cb.Colors = new Color[] {
                        Color.FromArgb(0, 255, 255, 255),
                        Color.FromArgb(18, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255)
                    };
                    cb.Positions = new float[] { 0f, 0.5f, 1f };
                    lg.InterpolationColors = cb;
                    g.FillRectangle(lg, bandRect);
                }
            }
            g.Restore(st);
        }

        void DrawCards(Graphics g, List<Device> devs)
        {
            if (!ShowOfflineDevices) devs = devs.FindAll(delegate(Device d) { return !d.Offline; });
            if (devs.Count == 0)
            {
                RectangleF area = new RectangleF(PadX, HeaderH + 2, W - PadX * 2, CardH);
                using (GraphicsPath gp = Round(area, 12f))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(38, 38, 42)))
                    g.FillPath(sb, gp);
                using (Font f = F("Microsoft YaHei UI", 8.4f, FontStyle.Regular))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(132, 132, 140)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("未检测到已连接的鼠标 / 键盘 / 耳机", f, b, area, sf);
                }
                return;
            }

            for (int i = 0; i < devs.Count; i++)
            {
                float y = HeaderH + i * (CardH + CardGap) - _scroll;
                if (y > Height / _scale || y + CardH < 0) continue;
                DrawCard(g, devs[i], new RectangleF(PadX, y, W - PadX * 2, CardH), i);
            }
        }

        void DrawCard(Graphics g, Device d, RectangleF r, int index)
        {
            bool hover = _hotCard == index;
            GlassStyle.DrawDropShadow(g, r, 10f, 3, 9, 1.2f);
            using (GraphicsPath gp = Round(r, 10f))
            {
                Color cardFill = GlassStyle.LightMode
                    ? (hover ? Color.FromArgb(52, 38, 38, 48) : Color.FromArgb(28, 38, 38, 48))
                    : (hover ? Color.FromArgb(120, 34, 34, 42) : Color.FromArgb(90, 24, 24, 30));
                using (SolidBrush b = new SolidBrush(cardFill))
                    g.FillPath(b, gp);
                using (LinearGradientBrush lg = new LinearGradientBrush(
                    r,
                    Color.FromArgb(20, 255, 255, 255),
                    Color.FromArgb(5, 255, 255, 255),
                    System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                {
                    g.FillPath(lg, gp);
                }
            }

            // left icon tile
            float tileX = r.X + 8;
            RectangleF tile = new RectangleF(tileX, r.Y + (r.Height - 30f) / 2f, 30, 30);
            using (GraphicsPath gp = Round(tile, 8f))
            using (SolidBrush sb = new SolidBrush(GlassStyle.Hairline))
                g.FillPath(sb, gp);
            DrawGlyph(g, d.Kind, new RectangleF(tile.X + 5f, tile.Y + 5f, 20, 20));

            if (d.Offline)
            {
                RectangleF del = new RectangleF(r.Right - 34f, r.Y + (r.Height - 22f) / 2f, 22f, 22f);
                using (GraphicsPath gp = Round(del, 7f))
                using (SolidBrush b = new SolidBrush(GlassStyle.Tile))
                    g.FillPath(b, gp);
                using (Pen p = new Pen(Color.FromArgb(190, 255, 130, 140), 1.6f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    g.DrawLine(p, del.X + 7f, del.Y + 7f, del.Right - 7f, del.Bottom - 7f);
                    g.DrawLine(p, del.Right - 7f, del.Y + 7f, del.X + 7f, del.Bottom - 7f);
                }
                _deleteRects.Add(del);
                _deleteDevices.Add(d);
            }
            else
            {
                // right side: small ring + big % number (same layout for all)
                string big = d.Battery >= 0 ? d.Battery + "%" : "—";
                bool manual = d.ManualRefresh && d.BtAddress != 0;
                bool refreshHover = manual && (_hotRefresh == d.BtAddress || _headsetReading);

                using (Font fBig = F(UiFonts.Family, 12f, FontStyle.Bold))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                    RectangleF numArea = new RectangleF(r.Right - 58, r.Y + 4, 54, r.Height - 8);
                    Color numColor = ChargingVisual(d)
                        ? Color.FromArgb(255, 212, 175, 55)
                        : (d.Battery >= 0 && d.Battery < 20
                            ? GlassStyle.LowBattery
                            : (d.Cached ? GlassStyle.Muted : _theme));
                    using (SolidBrush numBrush = new SolidBrush(numColor))
                    {
                        if (refreshHover)
                        {
                            GlassStyle.DrawRefreshIcon(g, new RectangleF(r.Right - 44f, r.Y + (r.Height - 16f) / 2f, 16f, 16f),
                                GlassStyle.Ink);
                        }
                        else
                        {
                            numArea.Y += KaiShift;
                            g.DrawString(big, fBig, numBrush, numArea, sf);
                        }
                    }
                    if (manual)
                    {
                        _refreshHits.Add(numArea);
                        _refreshDevices.Add(d);
                    }
                }

                // small ring to the left of the number
                DrawBattery(g, d, r.Right - 70f, r.Y + r.Height / 2f, 11f);
            }

            // middle text
            float nameX = tile.Right + 9;
            float nameMax = (r.Right - 88) - nameX;
            using (Font fName = F("Microsoft YaHei UI", 8.9f, FontStyle.Bold))
            using (Font fSub = F("Microsoft YaHei UI", 6.7f, FontStyle.Regular))
            using (SolidBrush bName = new SolidBrush(GlassStyle.Ink))
            using (SolidBrush bSub = new SolidBrush(d.Battery < 0 ? GlassStyle.Muted : GlassStyle.Ink))
            {
                g.DrawString(Trim(g, d.Name, fName, nameMax), fName, bName, nameX, r.Y + 8 + KaiShift, StringFormat.GenericTypographic);

                float subX = nameX;
                if (_showMode)
                {
                    Color dotColor = ModeColor(d);
                    using (SolidBrush dot = new SolidBrush(dotColor))
                        g.FillEllipse(dot, nameX, r.Y + 25f, 3f, 3f);
                    subX = nameX + 8f;
                }
                g.DrawString(Trim(g, StatusText(d), fSub, nameMax + 2f), fSub, bSub, subX, r.Y + 22.5f + KaiShift, StringFormat.GenericTypographic);
            }
        }

        string StatusText(Device d)
        {
            if (d.Offline)
                return "已断开";
            if (d.IsApple)
            {
                List<string> ap = new List<string>();
                if (d.AppleLeft >= 0) ap.Add("L" + d.AppleLeft + "%");
                if (d.AppleRight >= 0) ap.Add("R" + d.AppleRight + "%");
                return ap.Count == 0 ? "Apple 蓝牙" : string.Join(" · ", ap.ToArray());
            }
            List<string> parts = new List<string>();
            if (_showMode) parts.Add(d.ModeText);
            if (d.Cached) parts.Add("休眠中");
            if (d.Extra.Length > 0) parts.Add(d.Extra);
            if (d.Battery < 0 && d.Error.Length > 0) parts.Add(d.Error);
            if (parts.Count == 0) parts.Add(d.Battery >= 0 ? "设备在线" : "无读数");
            return string.Join(" · ", parts.ToArray());
        }

        static Color ModeColor(Device d)
        {
            if (d.Mode == ConnMode.Wired) return Color.FromArgb(74, 158, 255);
            if (d.Mode == ConnMode.Receiver24G) return Color.FromArgb(52, 199, 89);
            return Color.FromArgb(126, 124, 255);
        }

        static string Trim(Graphics g, string text, Font f, float maxWidth)
        {
            if (g.MeasureString(text, f, 1000, StringFormat.GenericTypographic).Width <= maxWidth) return text;
            string s = text;
            while (s.Length > 1 && g.MeasureString(s + "…", f, 1000, StringFormat.GenericTypographic).Width > maxWidth)
                s = s.Substring(0, s.Length - 1);
            return s + "…";
        }

        void DrawBattery(Graphics g, Device d, float cx, float cy, float rad)
        {
            RectangleF ring = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);

            Color level = GlassStyle.Muted;
            if (ChargingVisual(d))
                level = Color.FromArgb(255, 212, 175, 55);
            else if (d.Battery >= 0 && d.Battery < 20)
                level = GlassStyle.LowBattery;
            else if (d.Battery >= 0 && !d.Cached)
                level = _theme;

            using (Pen back = new Pen(GlassStyle.Hairline, 2.4f))
                g.DrawEllipse(back, ring);

            if (d.Battery >= 0)
            {
                using (Pen p = new Pen(level, 2.6f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawArc(p, ring, -90f, 3.6f * Math.Max(0, Math.Min(100, d.Battery)));
                }
            }

            if (ChargingVisual(d))
            {
                GlassStyle.DrawChargingBolt(g, ring, _chargePhase);
            }
        }

        void DrawGlyph(Graphics g, DevKind kind, RectangleF r)
        {
            Color ink = GlassStyle.Ink;
            Color cut = Color.FromArgb(120, 22, 22, 30);
            using (SolidBrush b = new SolidBrush(ink))
            using (SolidBrush dark = new SolidBrush(cut))
            using (Pen p = new Pen(ink, Math.Max(1.3f, r.Width * 0.10f)))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;

                if (kind == DevKind.Mouse)
                {
                    float w = r.Width * 0.46f, h = r.Height * 0.82f;
                    float x = r.X + (r.Width - w) / 2f, y = r.Y + (r.Height - h) / 2f;
                    using (GraphicsPath body = Round(new RectangleF(x, y, w, h), w / 2f))
                        g.FillPath(b, body);
                    using (Pen slot = new Pen(cut, Math.Max(0.8f, r.Width * 0.055f)))
                        g.DrawLine(slot, r.X + r.Width / 2f, y + 1.2f, r.X + r.Width / 2f, y + h * 0.42f);
                    using (GraphicsPath wheel = Round(new RectangleF(r.X + r.Width / 2f - 0.8f, y + h * 0.25f, 1.6f, h * 0.22f), 0.8f))
                        g.FillPath(dark, wheel);
                }
                else if (kind == DevKind.Keyboard)
                {
                    RectangleF body = new RectangleF(r.X + 1f, r.Y + r.Height * 0.30f, r.Width - 2f, r.Height * 0.46f);
                    using (GraphicsPath gp = Round(body, 2.6f))
                        g.FillPath(b, gp);
                    float keyW = body.Width * 0.16f, keyH = body.Height * 0.17f;
                    for (int row = 0; row < 2; row++)
                        for (int col = 0; col < 4; col++)
                            using (GraphicsPath key = Round(new RectangleF(
                                body.X + body.Width * 0.12f + col * body.Width * 0.21f,
                                body.Y + body.Height * 0.24f + row * body.Height * 0.30f,
                                keyW, keyH), 0.7f))
                                g.FillPath(dark, key);
                }
                else if (kind == DevKind.Headset)
                {
                    using (Pen band = new Pen(ink, Math.Max(1.6f, r.Width * 0.13f)))
                        g.DrawArc(band, r.X + r.Width * 0.14f, r.Y + r.Height * 0.08f,
                            r.Width * 0.72f, r.Height * 0.62f, 180, 180);
                    RectangleF left = new RectangleF(r.X + r.Width * 0.10f, r.Y + r.Height * 0.48f, r.Width * 0.24f, r.Height * 0.40f);
                    RectangleF right = new RectangleF(r.Right - r.Width * 0.34f, r.Y + r.Height * 0.48f, r.Width * 0.24f, r.Height * 0.40f);
                    using (GraphicsPath lp = Round(left, left.Width / 2f))
                    using (GraphicsPath rp = Round(right, right.Width / 2f))
                    {
                        g.FillPath(b, lp);
                        g.FillPath(b, rp);
                    }
                    using (GraphicsPath lp = Round(new RectangleF(left.X + left.Width * 0.32f, left.Y + left.Height * 0.18f, left.Width * 0.36f, left.Height * 0.64f), 0.9f))
                    using (GraphicsPath rp = Round(new RectangleF(right.X + right.Width * 0.32f, right.Y + right.Height * 0.18f, right.Width * 0.36f, right.Height * 0.64f), 0.9f))
                    {
                        g.FillPath(dark, lp);
                        g.FillPath(dark, rp);
                    }
                }
                else
                {
                    using (GraphicsPath gp = Round(new RectangleF(r.X + r.Width * 0.15f, r.Y + r.Height * 0.15f, r.Width * 0.70f, r.Height * 0.70f), r.Width * 0.18f))
                        g.FillPath(b, gp);
                    g.FillEllipse(dark, r.X + r.Width * 0.44f, r.Y + r.Height * 0.44f, r.Width * 0.12f, r.Height * 0.12f);
                }
            }
        }

        // ---------- interaction ----------
        // WinForms 在本机会把鼠标消息坐标按 DPI 再放大一次，命中判定统一改用光标的真实屏幕坐标
        PointF LogicalCursor()
        {
            Point sp = Cursor.Position;
            return new PointF((sp.X - Location.X) / _scale, (sp.Y - Location.Y) / _scale);
        }

        int CardIndexAt(PointF pt)
        {
            List<Device> devs;
            lock (_sync) devs = _data == null ? new List<Device>() : _data.Devices;
            if (!ShowOfflineDevices) devs = devs.FindAll(delegate(Device d) { return !d.Offline; });
            for (int i = 0; i < devs.Count; i++)
            {
                float top = HeaderH + i * (CardH + CardGap) - _scroll;
                if (pt.Y >= top && pt.Y <= top + CardH && pt.X >= PadX && pt.X <= W - PadX) return i;
            }
            return -1;
        }

        void MoveCard(int from, int to)
        {
            if (from == to) return;
            lock (_sync)
            {
                if (_data == null) return;
                List<Device> list = _data.Devices;
                if (from < 0 || from >= list.Count || to < 0 || to >= list.Count) return;
                Device d = list[from];
                list.RemoveAt(from);
                list.Insert(to, d);
            }
            _dragCardIndex = to;
            SaveDeviceOrder();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            PointF pt = LogicalCursor();

            if (_dragCardIndex >= 0 && (Control.MouseButtons & MouseButtons.Left) != 0)
            {
                if (!_cardDragging && Math.Abs(pt.Y - _dragCardStart.Y) > 4f) _cardDragging = true;
                if (_cardDragging)
                {
                    int target = CardIndexAt(pt);
                    if (target >= 0 && target != _dragCardIndex) MoveCard(_dragCardIndex, target);
                }
            }
            string hot = "";
            if (_closeRect.Contains(pt)) hot = "close";
            else if (_chevronRect.Contains(pt)) hot = "chevron";
            else if (_gearRect.Contains(pt)) hot = "gear";

            int card = -1;
            List<Device> devs;
            lock (_sync) devs = _data == null ? new List<Device>() : _data.Devices;
            ulong refresh = 0;
            if (_collapsed)
            {
                devs = devs.FindAll(delegate(Device d) { return !d.Offline; });
                int idx = (int)((pt.X - 8f) / 52f);
                if (idx >= 0 && idx < devs.Count)
                {
                    float cx = 8f + idx * 52f + 33f;
                    RectangleF ring = new RectangleF(cx - 14f, 3f, 28f, 28f);
                    if (devs[idx].ManualRefresh && devs[idx].BtAddress != 0 && ring.Contains(pt))
                        refresh = devs[idx].BtAddress;
                }
            }
            else
            {
                if (!ShowOfflineDevices) devs = devs.FindAll(delegate(Device d) { return !d.Offline; });
                for (int i = 0; i < devs.Count; i++)
                {
                    float top = HeaderH + i * (CardH + CardGap) - _scroll;
                    if (pt.Y >= top && pt.Y <= top + CardH && pt.X >= PadX && pt.X <= W - PadX) card = i;
                }
                if (card >= 0 && card < devs.Count && devs[card].ManualRefresh && devs[card].BtAddress != 0)
                {
                    float top = HeaderH + card * (CardH + CardGap) - _scroll;
                    RectangleF numArea = new RectangleF(W - PadX - 58f, top + 4f, 54f, CardH - 8f);
                    if (numArea.Contains(pt)) refresh = devs[card].BtAddress;
                }
            }
            bool changed = hot != _hotButton || card != _hotCard || refresh != _hotRefresh;
            _hotButton = hot;
            _hotCard = card;
            _hotRefresh = refresh;
            if (changed) Invalidate();
            Cursor = _cardDragging ? Cursors.SizeAll :
                (hot.Length > 0 || card >= 0 || refresh != 0 ? Cursors.Hand : Cursors.Default);

            if (_dragging)
            {
                Point sp = Cursor.Position;
                Location = new Point(sp.X - _dragOffset.X, sp.Y - _dragOffset.Y);
                ClampToScreen();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            PointF pt = LogicalCursor();
            if (e.Button == MouseButtons.Left)
            {
                for (int i = 0; i < _refreshHits.Count && i < _refreshDevices.Count; i++)
                {
                    if (_refreshHits[i].Contains(pt))
                    {
                        RefreshHeadsetBattery(_refreshDevices[i].BtAddress);
                        return;
                    }
                }
            }
            if (_collapsed) { if (e.Button == MouseButtons.Left) { Point sp = Cursor.Position; _dragging = true; _dragStartLocation = Location; _dragOffset = new Point(sp.X - Location.X, sp.Y - Location.Y); } return; }
            if (e.Button != MouseButtons.Left) { base.OnMouseDown(e); return; }
            for (int i = 0; i < _deleteRects.Count && i < _deleteDevices.Count; i++)
            {
                if (_deleteRects[i].Contains(pt)) { DeleteHistory(_deleteDevices[i]); return; }
            }
            if (_closeRect.Contains(pt)) { HidePanel(); return; }
            if (_chevronRect.Contains(pt)) { ToggleCollapsed(); return; }
            if (_gearRect.Contains(pt)) { ShowSettingsDialog(); return; }
            int cardIndex = CardIndexAt(pt);
            if (cardIndex >= 0 && _settings != null && _settings.AllowDeviceSort)
            {
                _dragCardIndex = cardIndex;
                _dragCardStart = pt;
                _cardDragging = false;
            }
            if (pt.Y < HeaderH)
            {
                Point sp = Cursor.Position;
                _dragging = true;
                _dragStartLocation = Location;
                _dragOffset = new Point(sp.X - Location.X, sp.Y - Location.Y);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _dragging = false;
                ShowPanelMenu(Cursor.Position);
                return;
            }
            if (_dragging && _settings != null && _settings.Position != 0 && Location != _dragStartLocation)
            {
                _settings.Position = 0;      // manual drag -> "自由拖动", keep it for next launch
                TrySaveSettings();
            }
            _dragging = false;
            if (_dragCardIndex >= 0)
            {
                _dragCardIndex = -1;
                _cardDragging = false;
                SaveDeviceOrder();
            }
            base.OnMouseUp(e);
        }

        void ShowPanelMenu(Point screenPoint)
        {
            if (_panelToggleItem != null) _panelToggleItem.Text = _collapsed ? "展开" : "收起";

            // Sit just outside the lower-right edge: adjacent, never covering.
            int mw = _panelMenu.Width > 0 ? _panelMenu.Width : _panelMenu.GetPreferredSize(Size.Empty).Width;
            int mh = _panelMenu.Height > 0 ? _panelMenu.Height : _panelMenu.GetPreferredSize(Size.Empty).Height;
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            int x = Right - mw;
            int y = Bottom + 2;
            if (y + mh > wa.Bottom)
            {
                // No room below: put it to the right, bottom aligned.
                x = Right + 2;
                y = Bottom - mh;
            }
            if (x + mw > wa.Right)
            {
                // No room right either: above the panel, right aligned.
                x = Right - mw;
                y = Top - mh - 2;
            }
            x = Math.Max(wa.Left, Math.Min(wa.Right - mw, x));
            y = Math.Max(wa.Top, Math.Min(wa.Bottom - mh, y));
            _panelMenu.Show(new Point(x, y));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_collapsed) return;
            List<Device> devs;
            lock (_sync) devs = _data == null ? new List<Device>() : _data.Devices;
            if (!ShowOfflineDevices) devs = devs.FindAll(delegate(Device d) { return !d.Offline; });
            int content = devs.Count * (CardH + CardGap) - CardGap;
            int view = (int)(Height / _scale) - HeaderH - FooterH - 6;
            if (content > view)
            {
                _scroll -= e.Delta;
                if (_scroll < 0) _scroll = 0;
                if (_scroll > content - view) _scroll = content - view;
                Invalidate();
            }
            base.OnMouseWheel(e);
        }

        // ---------- worker ----------
        void ReadAllDevices(List<Device> devices)
        {
            if (devices == null || devices.Count == 0) return;
            if (devices.Count == 1) { Scanner.Read(devices[0]); return; }
            int workers = Math.Min(4, devices.Count);
            Thread[] threads = new Thread[workers];
            for (int w = 0; w < workers; w++)
            {
                int start = w;
                threads[w] = new Thread(delegate()
                {
                    for (int i = start; i < devices.Count; i += workers)
                    {
                        try { Scanner.Read(devices[i]); }
                        catch { }
                    }
                });
                threads[w].IsBackground = true;
                threads[w].Start();
            }
            for (int w = 0; w < workers; w++) threads[w].Join();
        }

        void StartWorker()
        {
            _worker = new Thread(delegate ()
            {
                while (!_stop)
                {
                    try
                    {
                        ScanResult r = Scanner.Scan();
                        ReadAllDevices(r.Devices);
                        for (int i = 0; i < r.Devices.Count; i++)
                        {
                            Device d = r.Devices[i];
                            int remembered;
                            if (d.Battery < 0 && d.BtAddress != 0 && _btBatteryMemory.TryGetValue(d.BtAddress, out remembered))
                            {
                                d.Battery = remembered;
                                d.Error = "";
                                d.Cached = false;
                            }
                        }
                        HashSet<string> present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        List<Device> retry = new List<Device>();
                        for (int i = 0; i < r.Devices.Count; i++)
                        {
                            Device d = r.Devices[i];
                            present.Add(d.Name);
                            if (!_lastPresent.Contains(d.Name))
                            {
                                retry.Add(d);
                            }
                        }
                        for (int i = 0; i < retry.Count; i++)
                        {
                            Device d = retry[i];
                            try
                            {
                                if (d.ManualRefresh && d.BtAddress != 0)
                                {
                                    int pct = HeadsetBattery.ReadNow(d.BtAddress);
                                    if (pct >= 0)
                                    {
                                        d.Battery = pct;
                                        d.Cached = false;
                                        d.Error = "";
                                        d.ManualRefresh = false;
                                    }
                                }
                                else Scanner.Read(d);
                            }
                            catch { }
                        }
                        for (int i = 0; i < r.Devices.Count; i++)
                        {
                            Device d = r.Devices[i];
                            if (d.ManualRefresh && d.BtAddress != 0 && d.Battery < 0)
                                ScheduleAutoHeadset(d.BtAddress);
                        }
                        _lastPresent.Clear();
                        foreach (string k in present) _lastPresent.Add(k);
                        r = MergeHistory(r);
                        r = ApplyManualBattery(r);
                        r = ApplyDeviceOrder(r);
                        SortActiveFirst(r);
                        lock (_sync) { _data = r; _lastUpdate = DateTime.Now; }
                        int count = DataRowCount();
                        if (IsHandleCreated)
                            BeginInvoke(new Action(delegate ()
                            {
                                _shimmer = 0f;
                                _skeletonTimer.Stop();
                                ApplySize(count);
                                Invalidate();
                                UpdateTray();
                                UpdateMotionTimer();
                                if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
                            }));
                    }
                    catch { }
                    for (int i = 0; i < 20 && !_stop; i++) Thread.Sleep(100);
                }
            });
            _worker.IsBackground = true;
            _worker.Start();
        }

        void RefreshNow()
        {
            lock (_sync) { _data = null; }
            _shimmer = 0f;
            ApplySize(2);
            _skeletonTimer.Start();
            Invalidate();
            try
            {
                ScanResult r = Scanner.Scan();
                ReadAllDevices(r.Devices);
                SetData(r);
            }
            catch { }
        }

        void SortActiveFirst(ScanResult r)
        {
            if (r == null || r.Devices == null) return;
            List<Device> active = r.Devices.FindAll(delegate(Device d) { return !d.Offline; });
            List<Device> offline = r.Devices.FindAll(delegate(Device d) { return d.Offline; });
            active.AddRange(offline);
            r.Devices = active;
        }

        public void SetData(ScanResult r)
        {
            r = MergeHistory(r);
            r = ApplyManualBattery(r);
            r = ApplyDeviceOrder(r);
            SortActiveFirst(r);
            lock (_sync) { _data = r; _lastUpdate = DateTime.Now; }
            _shimmer = 0f;
            _skeletonTimer.Stop();
            ApplySize(DataRowCount());
            Invalidate();
            UpdateTray();
            UpdateMotionTimer();
            if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
        }

        void LoadHistory()
        {
            if (_historyLoaded) return;
            _historyLoaded = true;
            try
            {
                if (!System.IO.File.Exists(HistoryPath)) return;
                string[] lines = System.IO.File.ReadAllLines(HistoryPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] p = lines[i].Split('\t');
                    if (p.Length < 7) continue;
                    Device d = new Device();
                    d.HistoryKey = p[0];
                    d.Name = p[1];
                    int n;
                    if (int.TryParse(p[2], out n)) d.Kind = (DevKind)n;
                    if (int.TryParse(p[3], out n)) d.Mode = (ConnMode)n;
                    if (int.TryParse(p[4], out n)) d.Battery = n;
                    if (int.TryParse(p[5], out n)) d.Voltage = n;
                    long ticks;
                    if (long.TryParse(p[6], out ticks) && ticks > 0) d.LastSeen = new DateTime(ticks);
                    if (p.Length > 7) d.Extra = p[7];
                    d.Offline = true;
                    if (d.HistoryKey.Length > 0) _history[d.HistoryKey] = d;
                }
            }
            catch { }
        }

        void LoadIgnored()
        {
            if (_ignoredLoaded) return;
            _ignoredLoaded = true;
            try
            {
                if (!System.IO.File.Exists(IgnorePath)) return;
                string[] lines = System.IO.File.ReadAllLines(IgnorePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string k = lines[i].Trim();
                    if (k.Length > 0) _ignored.Add(k);
                }
            }
            catch { }
        }

        void SaveIgnored()
        {
            try
            {
                System.IO.File.WriteAllLines(IgnorePath, new List<string>(_ignored).ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }

        public int IgnoredCount
        {
            get { lock (_sync) { LoadIgnored(); return _ignored.Count; } }
        }

        public void IgnoreDevice(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (_sync)
            {
                LoadIgnored();
                if (_ignored.Add(key)) SaveIgnored();
                if (_data != null)
                    _data.Devices.RemoveAll(delegate (Device d) { return Scanner.KeyFor(d.Name) == key; });
            }
            ApplySize(DataRowCount());
            Invalidate();
            UpdateTray();
            if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
        }

        public bool IsHeadsetReading
        {
            get { return _headsetReading; }
        }

        public void ToggleAirPodsMedia()
        {
            AirPodsBridge.ToggleMedia();
        }

        void AutoRefreshHeadset()
        {
            if (_captureMode || _headsetReading) return;
            int minutes = _settings == null ? 15 : _settings.HeadsetRefreshMinutes;
            if (minutes <= 0) return;
            ulong addr = 0;
            lock (_sync)
            {
                if (_data == null) return;
                for (int i = 0; i < _data.Devices.Count; i++)
                {
                    Device d = _data.Devices[i];
                    if (!d.ManualRefresh || d.BtAddress == 0) continue;
                    DateTime last;
                    if (!_lastManualRefresh.TryGetValue(d.BtAddress, out last))
                    {
                        _lastManualRefresh[d.BtAddress] = DateTime.Now;   // first auto refresh after the selected interval
                        break;
                    }
                    if ((DateTime.Now - last).TotalMinutes >= minutes)
                    {
                        addr = d.BtAddress;
                        break;
                    }
                }
            }
            if (addr != 0) RefreshHeadsetBattery(addr);
        }

        void ScheduleAutoHeadset(ulong address)
        {
            if (address == 0) return;
            lock (_sync)
            {
                DateTime next;
                if (_autoHeadsetNext.TryGetValue(address, out next) && DateTime.Now < next) return;
                _autoHeadsetNext[address] = DateTime.Now.AddSeconds(15);
            }
            AutoRefreshHeadsetLater(address);
        }

        void AutoRefreshHeadsetLater(ulong address)
        {
            if (address == 0) return;
            lock (_sync) { if (!_autoHeadsetPending.Add(address)) return; }
            ThreadPool.QueueUserWorkItem(delegate
            {
                int pct = -1;
                for (int attempt = 0; attempt < 3 && pct < 0; attempt++)
                {
                    try { Thread.Sleep(attempt == 0 ? 900 : 1200); } catch { }
                    try { pct = HeadsetBattery.ReadNow(address); } catch { }
                }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        lock (_sync) _autoHeadsetPending.Remove(address);
                        if (pct >= 0)
                        {
                            lock (_sync)
                            {
                                _btBatteryMemory[address] = pct;
                                if (_data != null)
                                    for (int i = 0; i < _data.Devices.Count; i++)
                                    {
                                        Device d = _data.Devices[i];
                                        if (d.BtAddress != address) continue;
                                        d.Battery = pct;
                                        d.Cached = false;
                                        d.Error = "";
                                        d.ManualRefresh = false;
                                    }
                            }
                            Invalidate();
                            UpdateTray();
                            if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
                        }
                    }));
                }
                catch { }
            });
        }

        public void RefreshHeadsetBattery(ulong address = 0)
        {
            if (_headsetReading) return;
            Device target = null;
            lock (_sync)
            {
                if (_data == null) return;
                for (int i = 0; i < _data.Devices.Count; i++)
                {
                    Device d = _data.Devices[i];
                    if (d.ManualRefresh && d.BtAddress != 0 && (address == 0 || d.BtAddress == address))
                    {
                        target = d;
                        break;
                    }
                }
            }
            if (target == null) return;

            _headsetReading = true;
            lock (_sync) _lastManualRefresh[target.BtAddress] = DateTime.Now;
            if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
            ulong addr = target.BtAddress;
            ThreadPool.QueueUserWorkItem(delegate
            {
                int pct = -1;
                try { pct = HeadsetBattery.ReadNow(addr); }
                catch { }
                try
                {
                    BeginInvoke(new Action(delegate
                    {
                        _headsetReading = false;
                        if (pct >= 0)
                        {
                            lock (_sync)
                            {
                                _manualBattery[addr] = pct;
                                _btBatteryMemory[addr] = pct;
                                Device cached = CloneManual(target);
                                cached.Battery = pct;
                                _manualDevices[addr] = cached;
                                _manualUntil[addr] = DateTime.Now.AddSeconds(20);
                                if (_data != null)
                                {
                                    for (int i = 0; i < _data.Devices.Count; i++)
                                    {
                                        Device d = _data.Devices[i];
                                        if (d.Kind == DevKind.Headset && d.Mode == ConnMode.Bluetooth && d.BtAddress == addr)
                                        {
                                            d.Battery = pct;
                                            d.Error = "";
                                        }
                                    }
                                }
                            }
                        }
                        if (pct < 0)
                        {
                            lock (_sync)
                            {
                                if (_data != null)
                                {
                                    for (int i = 0; i < _data.Devices.Count; i++)
                                    {
                                        Device d = _data.Devices[i];
                                        if (d.BtAddress == addr)
                                        {
                                            d.Error = "无法从 Windows 获取电量";
                                            break;
                                        }
                                    }
                                }
                            }
                            _manualErrorText[addr] = "无法从 Windows 获取电量";
                            DateTime warned;
                            bool notify = !_batteryErrorAt.TryGetValue(addr, out warned) ||
                                (DateTime.Now - warned).TotalMinutes >= 5;
                            if (notify)
                            {
                                _batteryErrorAt[addr] = DateTime.Now;
                                if (_tray != null)
                                {
                                    _tray.BalloonTipTitle = "耳机电量读取失败";
                                    _tray.BalloonTipText = "无法从 Windows 获取 " + target.Name + " 的电量";
                                    _tray.ShowBalloonTip(3000);
                                }
                            }
                        }
                        else
                        {
                            lock (_sync)
                            {
                                _batteryErrorAt.Remove(addr);
                                _manualErrorText.Remove(addr);
                            }
                        }
                        Invalidate();
                        UpdateTray();
                        if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
                    }));
                }
                catch { }
            });
        }

        public void RestoreIgnored(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (_sync)
            {
                LoadIgnored();
                if (!_ignored.Remove(key)) return;
                SaveIgnored();
            }
            if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
            ApplySize(DataRowCount());
            Invalidate();
        }

        public void ClearIgnored()
        {
            lock (_sync)
            {
                if (_ignored.Count == 0) return;
                _ignored.Clear();
                SaveIgnored();
            }
            if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
        }

        void SaveHistory()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (Device d in _history.Values)
                {
                    if (d.HistoryKey.Length == 0) continue;
                    sb.Append(d.HistoryKey.Replace('\t', ' ')).Append('\t')
                      .Append((d.Name ?? "").Replace('\t', ' ')).Append('\t')
                      .Append((int)d.Kind).Append('\t')
                      .Append((int)d.Mode).Append('\t')
                      .Append(d.Battery).Append('\t')
                      .Append(d.Voltage).Append('\t')
                      .Append(d.LastSeen.Ticks).Append('\t')
                      .Append((d.Extra ?? "").Replace('\t', ' ')).Append("\r\n");
                }
                string text = sb.ToString();
                if (text == _historySavedText) return;
                _historySavedText = text;
                System.IO.File.WriteAllText(HistoryPath, text, new UTF8Encoding(false));
            }
            catch { }
        }

        static Device CloneHistory(Device d)
        {
            Device c = new Device();
            c.Name = d.Name;
            c.RawName = d.RawName;
            c.Kind = d.Kind;
            c.Mode = d.Mode;
            c.Battery = d.Battery;
            c.Voltage = d.Voltage;
            c.Charging = false;
            c.Cached = d.Cached;
            c.CachedAt = d.CachedAt;
            c.Extra = d.Extra;
            c.Error = d.Error;
            c.HistoryKey = d.HistoryKey;
            c.LastSeen = d.LastSeen;
            c.Offline = d.Offline;
            return c;
        }

        ScanResult MergeHistory(ScanResult r)
        {
            lock (_sync)
            {
                LoadHistory();
                LoadIgnored();
                r.Devices.RemoveAll(delegate (Device d) { return _ignored.Contains(Scanner.KeyFor(d.Name)); });
                DateTime now = DateTime.Now;
                HashSet<string> active = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < r.Devices.Count; i++)
                {
                    Device d = r.Devices[i];
                    d.Offline = false;
                    d.HistoryKey = Scanner.KeyFor(d.Name);
                    Device old;
                    bool changed = !_history.TryGetValue(d.HistoryKey, out old) ||
                        old.Name != d.Name || old.Kind != d.Kind || old.Mode != d.Mode ||
                        old.Battery != d.Battery ||
                        (old.Extra ?? "") != (d.Extra ?? "");
                    bool refreshSeen = changed || old == null || (now - old.LastSeen).TotalMinutes >= 5;
                    d.LastSeen = refreshSeen ? now : old.LastSeen;
                    active.Add(d.HistoryKey);
                    Device snapshot = CloneHistory(d);
                    if (!changed && old != null) snapshot.Voltage = old.Voltage;
                    _history[d.HistoryKey] = snapshot;
                }
                List<Device> offline = new List<Device>();
                DateTime cutoff = now.AddDays(-7);
                foreach (KeyValuePair<string, Device> kv in _history)
                {
                    if (active.Contains(kv.Key)) continue;
                    if (_ignored.Contains(kv.Key)) continue;
                    if (kv.Value.LastSeen < cutoff) continue;
                    Device c = CloneHistory(kv.Value);
                    c.HistoryKey = kv.Key;
                    c.Offline = true;
                    offline.Add(c);
                }
                offline.Sort(delegate(Device a, Device b) { return b.LastSeen.CompareTo(a.LastSeen); });
                for (int i = 0; i < offline.Count && i < 6; i++) r.Devices.Add(offline[i]);
                SaveHistory();
            }
            return r;
        }

        void LoadDeviceOrder()
        {
            if (_deviceOrderLoaded) return;
            _deviceOrderLoaded = true;
            try
            {
                if (!System.IO.File.Exists(OrderPath)) return;
                string[] lines = System.IO.File.ReadAllLines(OrderPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string k = lines[i].Trim();
                    if (k.Length > 0 && !_deviceOrder.Contains(k)) _deviceOrder.Add(k);
                }
            }
            catch { }
        }

        ScanResult ApplyDeviceOrder(ScanResult r)
        {
            lock (_sync)
            {
                LoadDeviceOrder();
                if (_deviceOrder.Count == 0) return r;
                List<Device> ordered = new List<Device>();
                HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _deviceOrder.Count; i++)
                {
                    string k = _deviceOrder[i];
                    for (int j = 0; j < r.Devices.Count; j++)
                    {
                        if (used.Contains(k)) break;
                        if (Scanner.KeyFor(r.Devices[j].Name) == k)
                        {
                            ordered.Add(r.Devices[j]);
                            used.Add(k);
                            break;
                        }
                    }
                }
                for (int i = 0; i < r.Devices.Count; i++)
                {
                    string k = Scanner.KeyFor(r.Devices[i].Name);
                    if (!used.Contains(k)) ordered.Add(r.Devices[i]);
                }
                r.Devices = ordered;
            }
            return r;
        }

        void SaveDeviceOrder()
        {
            List<string> keys = new List<string>();
            lock (_sync)
            {
                if (_data != null)
                    for (int i = 0; i < _data.Devices.Count; i++)
                        keys.Add(Scanner.KeyFor(_data.Devices[i].Name));
            }
            _deviceOrder.Clear();
            for (int i = 0; i < keys.Count; i++) if (!_deviceOrder.Contains(keys[i])) _deviceOrder.Add(keys[i]);
            _deviceOrderLoaded = true;
            try { System.IO.File.WriteAllLines(OrderPath, _deviceOrder.ToArray(), new UTF8Encoding(false)); }
            catch { }
        }

        ScanResult ApplyManualBattery(ScanResult r)
        {
            lock (_sync)
            {
                for (int i = 0; i < r.Devices.Count; i++)
                {
                    Device d = r.Devices[i];
                    if (d.Kind == DevKind.Headset && d.BtAddress != 0)
                    {
                        int pct;
                        if (_manualBattery.TryGetValue(d.BtAddress, out pct))
                        {
                            d.Battery = pct;
                            if (d.Error == "无电量属性") d.Error = "";
                        }
                        string err;
                        if (_manualErrorText.TryGetValue(d.BtAddress, out err)) d.Error = err;
                    }
                }

                // Releasing HFP for the read can briefly remove the headset
                // from Windows' connected list. Keep the last manual result
                // visible for a short grace period instead of flashing it once.
                foreach (KeyValuePair<ulong, DateTime> pair in _manualUntil)
                {
                    if (pair.Value < DateTime.Now) continue;
                    bool present = false;
                    for (int i = 0; i < r.Devices.Count; i++)
                        if (r.Devices[i].BtAddress == pair.Key) { present = true; break; }
                    if (present) continue;
                    Device cached;
                    if (_manualDevices.TryGetValue(pair.Key, out cached))
                        r.Devices.Add(CloneManual(cached));
                }

                List<ulong> expired = new List<ulong>();
                foreach (KeyValuePair<ulong, int> pair in _manualBattery)
                {
                    bool present = false;
                    for (int i = 0; i < r.Devices.Count; i++)
                        if (r.Devices[i].BtAddress == pair.Key) { present = true; break; }
                    if (present) continue;
                    DateTime until;
                    if (!_manualUntil.TryGetValue(pair.Key, out until) || until < DateTime.Now)
                        expired.Add(pair.Key);
                }
                for (int i = 0; i < expired.Count; i++)
                {
                    _manualBattery.Remove(expired[i]);
                    _manualDevices.Remove(expired[i]);
                    _manualUntil.Remove(expired[i]);
                    _lastManualRefresh.Remove(expired[i]);
                    _manualErrorText.Remove(expired[i]);
                    _batteryErrorAt.Remove(expired[i]);
                }
            }
            return r;
        }

        static Device CloneManual(Device d)
        {
            Device c = new Device();
            c.Name = d.Name;
            c.RawName = d.RawName;
            c.Kind = d.Kind;
            c.Mode = d.Mode;
            c.Provider = d.Provider;
            c.BtAddress = d.BtAddress;
            c.ManualRefresh = d.ManualRefresh;
            c.Battery = d.Battery;
            c.Voltage = d.Voltage;
            c.Charging = false;
            c.Cached = false;
            c.Offline = false;
            c.Error = "";
            c.Extra = d.Extra;
            return c;
        }

        void DeleteHistory(Device d)
        {
            string key = d.HistoryKey;
            if (string.IsNullOrEmpty(key)) key = Scanner.KeyFor(d.Name);
            lock (_sync)
            {
                _history.Remove(key);
                SaveHistory();
                if (_data != null)
                    _data.Devices.RemoveAll(delegate (Device x) { return x.Offline && x.HistoryKey == key; });
            }
            ApplySize(DataRowCount());
            Invalidate();
            UpdateTray();
        }

        public void DumpGeometry(string path)
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            System.IO.File.WriteAllText(path,
                "scale=" + _scale + "\r\n"
                + "dpiX=" + CreateGraphics().DpiX + "\r\n"
                + "workingArea=" + wa.X + "," + wa.Y + "," + wa.Width + "," + wa.Height + "\r\n"
                + "size=" + Width + "," + Height + "\r\n"
                + "location=" + Location.X + "," + Location.Y + "\r\n");
        }

        // ---------- tray & menus ----------
        void BuildTray()
        {
            _menu = new ThemedContextMenu();
            _menu.Items.Add("打开面板", null, delegate { ShowPanel(); });
            _menu.Items.Add("设置", null, delegate { ShowSettingsDialog(); });
            _taskbarItem = new ToolStripMenuItem("任务栏电量", null, delegate { ToggleTaskbarBadge(); });
            _taskbarItem.Checked = _settings != null && _settings.TaskbarBadge;
            _menu.Items.Add(_taskbarItem);
            _menu.Items.Add("退出", null, delegate { Quit(); });
            _menu.Opening += delegate { _trayMenuOpen = true; if (_taskbarItem != null) _taskbarItem.Checked = _settings != null && _settings.TaskbarBadge; };
            _menu.Closed += delegate { _trayMenuOpen = false; };
            MenuTheme.Apply(_menu, _theme);

            _tray = new NotifyIcon();
            _tray.Icon = MakeIcon(null);
            _tray.Text = "电环";
            _tray.Visible = true;
            _tray.ContextMenuStrip = _menu;
            _tray.DoubleClick += delegate { ShowPanel(); };
        }

        public void ShowPanel()
        {
            Show();
            if (IsHandleCreated) Native.ShowWindow(Handle, 5);   // SW_SHOW, in case the native window was hidden
            WindowState = FormWindowState.Normal;
            Native.SetForegroundWindow(Handle);
            BringToFront();
            Activate();
            UpdateMotionTimer();
            Invalidate();
        }

        public void HidePanel()
        {
            _liquidTimer.Stop();
            Hide();
        }

        void UpdateTaskbarBadge(List<Device> devices)
        {
            bool show = !_captureMode && _settings != null && _settings.TaskbarBadge && devices != null && devices.Count > 0;
            if (!show)
            {
                if (_taskbarBadge != null && !_taskbarBadge.IsDisposed) _taskbarBadge.Hide();
                return;
            }
            if (_taskbarBadge == null || _taskbarBadge.IsDisposed)
            {
                TaskbarBadge.CleanupStale();
                _taskbarBadge = new TaskbarBadge();
                _taskbarBadge.Show();
            }
            _taskbarBadge.SetDevices(devices, _theme);
            if (_taskbarItem != null) _taskbarItem.Checked = _settings.TaskbarBadge;
        }

        void ToggleTaskbarBadge()
        {
            if (_settings == null) return;
            _settings.TaskbarBadge = !_settings.TaskbarBadge;
            try { _settings.Save(); } catch { }
            List<Device> active = new List<Device>();
            lock (_sync)
            {
                if (_data != null)
                    for (int i = 0; i < _data.Devices.Count; i++)
                    {
                        Device d = _data.Devices[i];
                        if (!d.Offline && d.Battery >= 0) active.Add(d);
                    }
            }
            UpdateTaskbarBadge(active);
            if (_taskbarItem != null) _taskbarItem.Checked = _settings.TaskbarBadge;
        }

        void UpdateTray()
        {
            if (_tray == null || _trayMenuOpen) return;
            List<Device> devs;
            lock (_sync) devs = _data == null ? new List<Device>() : _data.Devices;
            devs = devs.FindAll(delegate (Device d) { return !d.Offline; });
            StringBuilder sb = new StringBuilder();
            sb.Append("设备电量");
            int? lowest = null;
            foreach (Device d in devs)
            {
                string lvl = d.Battery >= 0 ? d.Battery + "%" : "—";
                sb.Append('\n').Append(d.Name).Append("  ").Append(lvl);
                if (d.Battery >= 0) lowest = (lowest == null ? d.Battery : Math.Min(lowest.Value, d.Battery));
            }
            UpdateTaskbarBadge(devs);
            string txt = sb.ToString();
            if (txt.Length > 63) txt = txt.Substring(0, 63);
            if (txt != _trayText)
            {
                _tray.Text = txt;
                _trayText = txt;
            }

            // Do not replace the NotifyIcon every 2 seconds: the shell loses the
            // context-menu target for a moment while the icon is swapped, which made
            // right-clicks on the tray icon fail intermittently. Only redraw on change.
            int levelKey = lowest.HasValue ? lowest.Value : -1;
            if (levelKey != _trayLevel || _settings.Theme != _trayTheme)
            {
                Icon old = _tray.Icon;
                _tray.Icon = MakeIcon(lowest);
                if (old != null) old.Dispose();
                _trayLevel = levelKey;
                _trayTheme = _settings.Theme;
            }

            // low-battery balloon, once per device per 10 minutes
            DateTime now = DateTime.Now;
            foreach (Device d in devs)
            {
                if (d.Offline || d.Battery < 0) continue;
                if (d.Battery >= 20) { _lastLowNotify.Remove(d.Name); continue; }
                DateTime last;
                if (_lastLowNotify.TryGetValue(d.Name, out last) && (now - last).TotalMinutes < 10) continue;
                _lastLowNotify[d.Name] = now;
                _tray.BalloonTipTitle = "低电量提醒";
                _tray.BalloonTipText = d.Name + " 剩余 " + d.Battery + "%，请及时充电";
                _tray.ShowBalloonTip(3000);
            }
        }

        Icon MakeIcon(int? level)
        {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Color c = level == null ? Color.FromArgb(150, 150, 156) : _theme;
                using (Pen p = new Pen(GlassStyle.Ink, 2.2f))
                    g.DrawRectangle(p, 7, 10, 16, 12);
                using (SolidBrush nb = new SolidBrush(GlassStyle.Ink))
                    g.FillRectangle(nb, 24, 13, 3, 6);
                using (SolidBrush fb = new SolidBrush(c))
                {
                    if (level == null) g.FillRectangle(fb, 9, 15, 12, 2);
                    else g.FillRectangle(fb, 9, 11, (int)Math.Round(14.0 * level.Value / 100.0), 10);
                }
            }
            IntPtr h = bmp.GetHicon();
            return Icon.FromHandle(h);
        }

        bool SetAutoStart(bool enable)
        {
            try
            {
                string lnk = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "电环.lnk");
                if (!enable)
                {
                    if (System.IO.File.Exists(lnk)) System.IO.File.Delete(lnk);
                    return true;
                }
                if (System.IO.File.Exists(lnk)) return true;

                object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                object shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type t = shortcut.GetType();
                t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { Application.ExecutablePath });
                t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { System.IO.Path.GetDirectoryName(Application.ExecutablePath) });
                t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { Application.ExecutablePath + ",0" });
                t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "电环" });
                t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置开机自启失败：" + ex.Message, "电环");
                return false;
            }
        }

        static bool AutoStartEnabled()
        {
            string lnk = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "电环.lnk");
            return System.IO.File.Exists(lnk);
        }

        void ShowSettingsDialog()
        {
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                if (!_settingsForm.Visible) _settingsForm.Show();
                _settingsForm.Activate();
                return;
            }

            SettingsForm f = new SettingsForm(this, _settings, AutoStartEnabled(), _dpiScale, GetPreviewDevices(), IsScanning());
            _settingsForm = f;
            f.FormClosed += delegate
            {
                _settingsForm = null;
                Invalidate();
                Update();
            };
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            f.Location = new Point(wa.Left + (wa.Width - f.Width) / 2, wa.Top + (wa.Height - f.Height) / 2);
            f.PrepareBackdrop();
            f.TopMost = false;
            f.Show();   // independent top-level window; main stays a desktop widget
        }

        public void PreviewTheme(AppSettings s)
        {
            if (s == null) return;
            AppSettings.Activate(s);
            _theme = AppSettings.ThemeColor(s.Theme);
            _material = s.Material;
            Invalidate();
        }

        public bool ApplySettings(AppSettings s, bool autoStart)
        {
            if (s == null) return false;
            if (autoStart != AutoStartEnabled() && !SetAutoStart(autoStart)) return false;

            float oldScale = _userScale;
            int oldPosition = _settings == null ? 0 : _settings.Position;
            bool oldShowOffline = _settings != null && _settings.ShowOffline;

            _settings = s.Clone();
            AppSettings.Activate(_settings);
            _settings.UiScale = AppSettings.NormalizeScale(_settings.UiScale);
            _settings.CornerRadius = AppSettings.NormalizeRadius(_settings.CornerRadius);
            _cornerRadius = _settings.CornerRadius;
            ApplyUserScale();
            _theme = AppSettings.ThemeColor(_settings.Theme);
            _material = _settings.Material;
            UiFonts.Family = _settings.Font;
            _showMode = _settings.ShowMode;
            AirPodsBridge.SetEarControl(_settings.AirPodsEarControl);
            MenuTheme.Apply(_menu, _theme);
            MenuTheme.Apply(_panelMenu, _theme);
            UpdateMotionTimer();

            bool scaleChanged = Math.Abs(oldScale - _userScale) > 0.001f;
            bool positionChanged = oldPosition != _settings.Position;
            bool showOfflineChanged = oldShowOffline != _settings.ShowOffline;
            if (scaleChanged || showOfflineChanged) ApplySize(DataRowCount());
            if (scaleChanged || positionChanged) ApplyPosition(_settings.Position);
            UpdateShape();
            SetOpacity(_settings.Opacity);
            ApplyWindowEffects();
            Invalidate();
            UpdateTray();

            try { _settings.Save(); }
            catch (Exception ex)
            {
                MessageBox.Show("设置已生效，但写入设置文件失败：\n" + ex.Message, "电环");
            }
            return true;
        }

        public List<string> GetConnectedDeviceNames()
        {
            List<string> names = new List<string>();
            lock (_sync)
            {
                if (_data == null) return names;
                for (int i = 0; i < _data.Devices.Count; i++)
                    if (!_data.Devices[i].Offline) names.Add(_data.Devices[i].Name);
            }
            return names;
        }

        public void SetConnectedOrder(List<string> names)
        {
            if (names == null || names.Count == 0) return;
            lock (_sync)
            {
                if (_data == null) return;
                List<Device> ordered = new List<Device>();
                HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < names.Count; i++)
                {
                    for (int j = 0; j < _data.Devices.Count; j++)
                    {
                        Device d = _data.Devices[j];
                        if (used.Contains(d.Name) || d.Offline) continue;
                        if (string.Equals(d.Name, names[i], StringComparison.OrdinalIgnoreCase))
                        {
                            ordered.Add(d);
                            used.Add(d.Name);
                            break;
                        }
                    }
                }
                for (int i = 0; i < _data.Devices.Count; i++)
                    if (!_data.Devices[i].Offline && !used.Contains(_data.Devices[i].Name))
                        ordered.Add(_data.Devices[i]);
                for (int i = 0; i < _data.Devices.Count; i++)
                    if (_data.Devices[i].Offline && !used.Contains(_data.Devices[i].Name))
                        ordered.Add(_data.Devices[i]);
                _data.Devices = ordered;
            }
            SaveDeviceOrder();
            ApplySize(DataRowCount());
            Invalidate();
            if (PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
        }

        public void SetOpacity(int percent)
        {
            percent = AppSettings.NormalizeOpacity(percent);
            try { Opacity = percent / 100f; } catch { }
            if (_settings != null) _settings.Opacity = percent;
            ApplyWindowEffects();
        }

        void ApplyWindowEffects()
        {
            if (!IsHandleCreated) return;
            try
            {
                int style = GetWindowLong(Handle, GWL_EXSTYLE);
                if (_settings != null && _settings.ClickThrough)
                    style |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
                else
                    style &= ~WS_EX_TRANSPARENT;
                if (_settings != null && _settings.Opacity < 100)
                    style |= WS_EX_LAYERED;
                SetWindowLong(Handle, GWL_EXSTYLE, style);
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_settings != null) SetOpacity(_settings.Opacity);
            else ApplyWindowEffects();
        }

        void ApplyPosition(int pos)
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (pos == 1) Location = new Point(wa.Right - Width, wa.Top);
            else if (pos == 2) Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top);
            else if (pos == 5) Location = new Point(wa.Left, wa.Top);
            else if (pos == 3) Location = new Point(wa.Right - Width, wa.Bottom - Height);
            else if (pos == 4) Location = new Point(wa.Left, wa.Bottom - Height);
            ClampToScreen();
        }

        void TrySaveSettings()
        {
            try { _settings.Save(); }
            catch { }
        }

        public bool IsScanning()
        {
            lock (_sync) return _data == null;
        }

        public List<Device> GetPreviewDevices()
        {
            List<Device> copy = new List<Device>();
            lock (_sync)
            {
                if (_data == null) return copy;
                LoadIgnored();
                for (int i = 0; i < _data.Devices.Count; i++)
                {
                    Device d = _data.Devices[i];
                    if (_ignored.Contains(Scanner.KeyFor(d.Name))) continue;
                    Device c = new Device();
                    c.Name = d.Name;
                    c.RawName = d.RawName;
                    c.Kind = d.Kind;
                    c.Mode = d.Mode;
                    c.BtAddress = d.BtAddress;
                    c.ManualRefresh = d.ManualRefresh;
                    c.IsApple = d.IsApple;
                    c.IsAppleCase = d.IsAppleCase;
                    c.Ignored = d.Ignored;
                    c.AppleModel = d.AppleModel;
                    c.AppleLeft = d.AppleLeft; c.AppleRight = d.AppleRight; c.AppleCase = d.AppleCase;
                    c.AppleLeftIn = d.AppleLeftIn; c.AppleRightIn = d.AppleRightIn;
                    c.AppleLid = d.AppleLid; c.AppleBothInCase = d.AppleBothInCase;
                    c.AppleLeftCharging = d.AppleLeftCharging; c.AppleRightCharging = d.AppleRightCharging;
                    c.AppleCaseCharging = d.AppleCaseCharging;
                    c.Battery = d.Battery;
                    c.Voltage = d.Voltage;
                    c.Charging = false;
                    c.Cached = d.Cached;
                    c.Offline = d.Offline;
                    c.LastSeen = d.LastSeen;
                    c.HistoryKey = d.HistoryKey;
                    c.CachedAt = d.CachedAt;
                    c.Extra = d.Extra;
                    c.Error = d.Error;
                    copy.Add(c);
                    if (d.IsApple && d.AppleCase >= 0)
                    {
                        Device box = new Device();
                        box.Name = "充电仓";
                        box.RawName = d.AppleModel;
                        box.Kind = DevKind.Headset;
                        box.Mode = ConnMode.Bluetooth;
                        box.Battery = d.AppleCase;
                        box.Charging = d.AppleCaseCharging;
                        box.IsAppleCase = true;
                        box.AppleModel = d.AppleModel;
                        copy.Add(box);
                    }
                }
                foreach (string key in _ignored)
                {
                    Device c = new Device();
                    c.Name = key;
                    c.RawName = key;
                    c.Kind = DevKind.Mouse;
                    c.Mode = ConnMode.Receiver24G;
                    c.Ignored = true;
                    copy.Add(c);
                }
            }
            List<Device> connected = copy.FindAll(delegate(Device d) { return !d.Offline; });
            List<Device> offline = copy.FindAll(delegate(Device d) { return d.Offline; });
            connected.AddRange(offline);
            return connected;
        }

        public bool DeleteDeviceRecord(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            bool removed = false;
            lock (_sync)
            {
                removed = _history.Remove(key);
                if (removed) SaveHistory();
            }
            if (removed && PreviewChanged != null) PreviewChanged(this, EventArgs.Empty);
            return removed;
        }

        public void CaptureSettings(string path)
        {
            using (SettingsForm f = new SettingsForm(null, _settings, AutoStartEnabled(), _dpiScale, GetPreviewDevices(), IsScanning()))
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                f.Location = new Point(wa.Left + (wa.Width - f.Width) / 2, wa.Top + (wa.Height - f.Height) / 2);
                f.PrepareBackdrop();
                f.PrepareForCapture();
                f.Show();
                Application.DoEvents();
                f.CaptureToFile(path);
                f.Close();
            }
        }

        public void CaptureTheme(string path)
        {
            bool custom = _settings.Theme == 8;
            Color accent = custom ? Color.FromArgb(_settings.CustomAccent) : AppSettings.ThemeColor(_settings.Theme);
            Color secondary = custom ? Color.FromArgb(_settings.CustomSecondary) : AppSettings.ThemeSecondary(_settings.Theme);
            using (ColorPairForm f = new ColorPairForm(accent, secondary, AppSettings.ThemeColor(_settings.Theme),
                _settings.CustomHistory, _settings.CustomFavorites, _settings.CustomSecondaryFavorites, _settings.CustomFavoritePairs))
            {
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(100, 100);
                f.TopMost = true;
                f.Show();
                f.Activate();
                Application.DoEvents();
                System.Threading.Thread.Sleep(250);
                Application.DoEvents();
                using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                        g.CopyFromScreen(f.Left, f.Top, 0, 0, f.Size);
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                f.Close();
            }
        }

        public void Quit()
        {
            _stop = true;
            AirPodsBridge.Stop();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.Close();
            if (_liquidTimer != null) _liquidTimer.Stop();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
            if (_taskbarBadge != null && !_taskbarBadge.IsDisposed)
            {
                _taskbarBadge.DestroyNow();
                _taskbarBadge.Dispose();
                _taskbarBadge = null;
            }
            Close();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_stop && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                // hiding inside FormClosing leaves Visible=true with a hidden native window,
                // so defer it to the next message loop turn
                BeginInvoke(new Action(delegate { HidePanel(); }));
                return;
            }
            base.OnFormClosing(e);
        }

        public void PrepareForCapture()
        {
            _captureMode = true;
            _liquidTimer.Stop();
            _skeletonTimer.Stop();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ApplyWindowEffects();
            ApplyPosition(_settings.Position);
            if (!_captureMode && _data == null) _skeletonTimer.Start();
            UpdateMotionTimer();
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (Mutex mtx = new Mutex(true, "TriModeBattery.Single", out created))
            {
                if (!created)
                {
                    try
                    {
                        EventWaitHandle open = EventWaitHandle.OpenExisting("TriModeBattery.Show");
                        open.Set();
                    }
                    catch { }
                    return;
                }
                EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "TriModeBattery.Show");
                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                if (args.Length >= 2 && args[0] == "--shot-skeleton")
                {
                    MainForm shot = new MainForm(null, true);
                    shot.PrepareForCapture();
                    shot.Show();
                    shot.ToggleCollapsed();
                    for (int i = 0; i < 8; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(35); }
                    Bitmap bmp = new Bitmap(shot.Width, shot.Height);
                    shot.DrawToBitmap(bmp, new Rectangle(0, 0, shot.Width, shot.Height));
                    bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                    bmp.Dispose();
                    shot.DumpGeometry(args[1] + ".txt");
                    shot.Quit();
                    return;
                }

                if (args.Length >= 2 && args[0] == "--shot-theme")
                {
                    MainForm shot = new MainForm(null);
                    shot.PrepareForCapture();
                    shot.Show();
                    Application.DoEvents();
                    shot.CaptureTheme(args[1]);
                    shot.Quit();
                    return;
                }

                if (args.Length >= 2 && args[0] == "--shot-settings")
                {
                    MainForm shot = new MainForm(null);
                    shot.PrepareForCapture();
                    shot.Show();
                    Application.DoEvents();
                    for (int i = 0; i < 30; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(100); }
                    ScanResult r = Scanner.Scan();
                    for (int i = 0; i < r.Devices.Count; i++) Scanner.Read(r.Devices[i]);
                    shot.SetData(r);
                    Application.DoEvents();
                    shot.CaptureSettings(args[1]);
                    shot.Quit();
                    return;
                }

                if (args.Length >= 2 && args[0] == "--shot")
                {
                    MainForm shot = new MainForm(null);
                    shot.PrepareForCapture();
                    shot.Show();
                    Application.DoEvents();
                    ScanResult r = Scanner.Scan();
                    for (int i = 0; i < r.Devices.Count; i++) Scanner.Read(r.Devices[i]);
                    shot.SetData(r);
                    if (args.Length >= 3 && args[2] == "expanded") shot.ToggleCollapsed();
                    shot.Refresh();
                    Application.DoEvents();
                    Bitmap bmp = new Bitmap(shot.Width, shot.Height);
                    shot.DrawToBitmap(bmp, new Rectangle(0, 0, shot.Width, shot.Height));
                    bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                    bmp.Dispose();
                    shot.DumpGeometry(args[1] + ".txt");
                    shot.Quit();
                    return;
                }

                MainForm form = new MainForm(showEvent);
                Application.Run(form);
            }
        }
    }
}
