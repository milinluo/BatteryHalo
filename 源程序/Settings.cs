using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TriMode
{
    // ---------------------------------------------------------------------
    // 可持久化的外观 / 行为设置（绿色程序：设置.ini 与 exe 放在一起）
    // ---------------------------------------------------------------------
    public class AppSettings
    {
        public float UiScale = 1.1f;       // 小 1.0 / 标准 1.1 / 大 1.2
        public int Version = 3;
        public int Theme = 0;
        public int Position = 0;
        public GlassMaterial Material = GlassMaterial.Gradient;
        public string Font = UiFonts.DefaultFamily;
        public bool ShowMode = true;
        public bool ShowOffline = false;
        public bool AllowDeviceSort = true;
        public bool ClickThrough = false;
        public bool TaskbarBadge = true;
        public int Opacity = 100;
        public int HeadsetRefreshMinutes = 15;
        public int CornerRadius = 11;
        public bool AutoFlattenCorners = true;
        public bool AirPodsEarControl;
        public int CustomAccent = Color.FromArgb(255, 79, 163).ToArgb();
        public int CustomSecondary = Color.FromArgb(126, 124, 255).ToArgb();
        public string CustomHistory = "";
        public string CustomFavorites = "";
        public string CustomFavoritePairs = "";
        public string CustomSecondaryFavorites = "";

        public static string SettingsPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "设置.ini"); }
        }

        public static AppSettings Defaults()
        {
            return new AppSettings();
        }

        public AppSettings Clone()
        {
            AppSettings s = new AppSettings();
            s.UiScale = UiScale;
            s.Version = Version;
            s.Theme = Theme;
            s.Position = Position;
            s.Material = Material;
            s.Font = Font;
            s.ShowMode = ShowMode;
            s.ShowOffline = ShowOffline;
            s.AllowDeviceSort = AllowDeviceSort;
            s.ClickThrough = ClickThrough;
            s.TaskbarBadge = TaskbarBadge;
            s.Opacity = Opacity;
            s.HeadsetRefreshMinutes = HeadsetRefreshMinutes;
            s.CornerRadius = CornerRadius;
            s.AutoFlattenCorners = AutoFlattenCorners;
            s.AirPodsEarControl = AirPodsEarControl;
            s.CustomAccent = CustomAccent;
            s.CustomSecondary = CustomSecondary;
            s.CustomHistory = CustomHistory;
            s.CustomFavorites = CustomFavorites;
            s.CustomFavoritePairs = CustomFavoritePairs;
            s.CustomSecondaryFavorites = CustomSecondaryFavorites;
            return s;
        }

        public static AppSettings Load()
        {
            AppSettings s = Defaults();
            try
            {
                if (!File.Exists(SettingsPath)) return s;
                string[] lines = File.ReadAllLines(SettingsPath, Encoding.UTF8);
                int version = 0;
                bool hasScale = false;
                int materialRaw = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    if (key == "version")
                    {
                        int v;
                        if (int.TryParse(val, out v)) version = v;
                    }
                    else if (key == "scale")
                    {
                        float f;
                        if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                        {
                            s.UiScale = f;
                            hasScale = true;
                        }
                    }
                    else if (key == "theme")
                    {
                        int v;
                        if (int.TryParse(val, out v)) s.Theme = Math.Max(0, Math.Min(8, v));
                    }
                    else if (key == "customaccent")
                    {
                        int v;
                        if (int.TryParse(val, out v)) s.CustomAccent = v;
                    }
                    else if (key == "customsecondary")
                    {
                        int v;
                        if (int.TryParse(val, out v)) s.CustomSecondary = v;
                    }
                    else if (key == "customhistory") s.CustomHistory = val;
                    else if (key == "customfavorites") s.CustomFavorites = val;
                    else if (key == "customfavoritepairs") s.CustomFavoritePairs = val;
                    else if (key == "customsecondaryfavorites") s.CustomSecondaryFavorites = val;
                    else if (key == "position")
                    {
                        int v;
                        if (int.TryParse(val, out v)) s.Position = Math.Max(0, Math.Min(5, v));
                    }
                    else if (key == "material")
                    {
                        int v;
                        if (int.TryParse(val, out v)) materialRaw = v;
                    }
                    else if (key == "font") s.Font = UiFonts.Normalize(val);
                    else if (key == "showmode") s.ShowMode = IsTrue(val);
                    else if (key == "showoffline") s.ShowOffline = IsTrue(val);
                    else if (key == "allowdevicesort") s.AllowDeviceSort = IsTrue(val);
                    else if (key == "clickthrough") s.ClickThrough = IsTrue(val);
                    else if (key == "taskbarbadge") s.TaskbarBadge = IsTrue(val);
                    else if (key == "opacity")
                    {
                        int ov;
                        if (int.TryParse(val, out ov)) s.Opacity = NormalizeOpacity(ov);
                    }
                    else if (key == "headsetrefresh")
                    {
                        int rm;
                        if (int.TryParse(val, out rm)) s.HeadsetRefreshMinutes = NormalizeRefresh(rm);
                    }
                    else if (key == "cornerradius")
                    {
                        int cr;
                        if (int.TryParse(val, out cr)) s.CornerRadius = NormalizeRadius(cr);
                    }
                    else if (key == "autoflatten") s.AutoFlattenCorners = IsTrue(val);
                    else if (key == "airpodsearcontrol") s.AirPodsEarControl = IsTrue(val);
                }
                if (hasScale)
                {
                    // v1 had 0.85 / 1.0 / 1.2. Shift one tier up so the old
                    // standard size becomes the new small size.
                    s.UiScale = version < 2 ? MigrateLegacyScale(s.UiScale) : NormalizeScale(s.UiScale);
                }
                // v3 replaced liquid(0)/frosted(1)/solid(2) with solid(0)/gradient(1).
                if (materialRaw >= 0)
                    s.Material = version < 3
                        ? (materialRaw == 2 ? GlassMaterial.Solid : GlassMaterial.Gradient)
                        : (materialRaw == 0 ? GlassMaterial.Solid : GlassMaterial.Gradient);
                s.Version = 3;
            }
            catch { }
            return s;
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# 电环 - 设置");
            sb.AppendLine("version=3");
            sb.AppendLine("scale=" + UiScale.ToString("0.##", CultureInfo.InvariantCulture));
            sb.AppendLine("theme=" + Theme);
            sb.AppendLine("position=" + Position);
            sb.AppendLine("material=" + (int)Material);
            sb.AppendLine("font=" + Font);
            sb.AppendLine("showMode=" + (ShowMode ? "1" : "0"));
            sb.AppendLine("showOffline=" + (ShowOffline ? "1" : "0"));
            sb.AppendLine("allowDeviceSort=" + (AllowDeviceSort ? "1" : "0"));
            sb.AppendLine("clickThrough=" + (ClickThrough ? "1" : "0"));
            sb.AppendLine("taskbarBadge=" + (TaskbarBadge ? "1" : "0"));
            sb.AppendLine("opacity=" + NormalizeOpacity(Opacity));
            sb.AppendLine("headsetRefresh=" + NormalizeRefresh(HeadsetRefreshMinutes));
            sb.AppendLine("cornerRadius=" + NormalizeRadius(CornerRadius));
            sb.AppendLine("autoFlatten=" + (AutoFlattenCorners ? "1" : "0"));
            sb.AppendLine("airPodsEarControl=" + (AirPodsEarControl ? "1" : "0"));
            sb.AppendLine("customAccent=" + CustomAccent);
            sb.AppendLine("customSecondary=" + CustomSecondary);
            sb.AppendLine("customHistory=" + CustomHistory);
            sb.AppendLine("customFavorites=" + CustomFavorites);
            sb.AppendLine("customFavoritePairs=" + CustomFavoritePairs);
            sb.AppendLine("customSecondaryFavorites=" + CustomSecondaryFavorites);
            File.WriteAllText(SettingsPath, sb.ToString(), new UTF8Encoding(false));
        }

        public static int NormalizeOpacity(int value)
        {
            return Math.Max(30, Math.Min(100, value));
        }

        public static int NormalizeRadius(int radius)
        {
            if (radius <= 0) return 0;
            if (radius == 6 || radius == 11 || radius == 18) return radius;
            return 11;
        }

        public static int RadiusIndex(int radius)
        {
            int r = NormalizeRadius(radius);
            return r == 0 ? 0 : (r == 6 ? 1 : (r == 11 ? 2 : 3));
        }

        public static string RadiusName(int radius)
        {
            int r = NormalizeRadius(radius);
            return r == 0 ? "直角" : (r == 6 ? "小" : (r == 11 ? "标准" : "大"));
        }

        public static int RadiusValue(int index)
        {
            if (index <= 0) return 0;
            if (index == 1) return 6;
            if (index == 2) return 11;
            return 18;
        }

        public static int NormalizeRefresh(int minutes)
        {
            if (minutes <= 0) return 0;
            if (minutes == 10 || minutes == 15 || minutes == 30 || minutes == 60) return minutes;
            return 15;
        }

        static bool IsTrue(string s)
        {
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        public static float NormalizeScale(float f)
        {
            if (f < 1.05f) return 1f;
            if (f < 1.18f) return 1.1f;
            return 1.2f;
        }

        public static int ScaleIndex(float f)
        {
            if (f < 1.05f) return 0;
            if (f < 1.18f) return 1;
            return 2;
        }

        static float MigrateLegacyScale(float f)
        {
            if (f < 0.95f) return 1f;
            if (f < 1.15f) return 1.1f;
            return 1.2f;
        }

        public static string ScaleName(float f)
        {
            int i = ScaleIndex(f);
            return i == 0 ? "小" : (i == 2 ? "大" : "标准");
        }

        public static int ActiveThemeIndex;
        public static GlassMaterial ActiveMaterial;
        public static Color ActiveCustomAccent = Color.FromArgb(255, 79, 163);
        public static Color ActiveCustomSecondary = Color.FromArgb(126, 124, 255);

        public static void Activate(AppSettings s)
        {
            if (s == null) return;
            ActiveThemeIndex = s.Theme;
            ActiveMaterial = s.Material;
            ActiveCustomAccent = Color.FromArgb(s.CustomAccent);
            ActiveCustomSecondary = Color.FromArgb(s.CustomSecondary);
            Color accent = ThemeColor(s.Theme);
            GlassStyle.Configure(s.Theme, accent, s.Theme == 8 ? ActiveCustomSecondary : Color.Empty);
        }

        public static Color ThemeColor(int index)
        {
            if (index == 0) return Color.FromArgb(74, 158, 255);
            if (index == 1) return Color.FromArgb(255, 79, 163);
            if (index == 2) return Color.FromArgb(52, 199, 89);
            if (index == 3) return Color.FromArgb(126, 124, 255);
            if (index == 4) return Color.FromArgb(255, 92, 92);
            if (index == 5) return Color.FromArgb(255, 184, 46);
            if (index == 6) return Color.FromArgb(26, 26, 30);
            if (index == 7) return Color.FromArgb(244, 244, 248);
            if (index == 8) return ActiveCustomAccent;
            return Color.FromArgb(74, 158, 255);
        }

        public static Color ThemeSecondary(int index)
        {
            if (index == 0) return Color.FromArgb(44, 188, 196);
            if (index == 1) return Color.FromArgb(126, 124, 255);
            if (index == 2) return Color.FromArgb(74, 158, 255);
            if (index == 3) return Color.FromArgb(255, 79, 163);
            if (index == 4) return Color.FromArgb(255, 184, 46);
            if (index == 5) return Color.FromArgb(255, 92, 92);
            if (index == 6) return Color.FromArgb(100, 100, 120);
            if (index == 7) return Color.FromArgb(74, 158, 255);
            if (index == 8) return ActiveCustomSecondary;
            return Color.FromArgb(126, 124, 255);
        }

        public static string ThemeName(int index)
        {
            if (index == 0) return "雾蓝";
            if (index == 1) return "樱粉";
            if (index == 2) return "薄荷";
            if (index == 3) return "鸢尾";
            if (index == 4) return "赤霞";
            if (index == 5) return "琥珀";
            if (index == 6) return "霜雪";
            if (index == 7) return "墨眉";
            if (index == 8) return "自定义主题";
            return "雾蓝";
        }

        public static int PositionIndex(int pos)
        {
            if (pos == 5) return 1;
            if (pos == 1) return 2;
            if (pos == 2) return 3;
            if (pos == 3) return 4;
            if (pos == 4) return 5;
            return 0;
        }

        public static int PositionValue(int index)
        {
            if (index == 1) return 5;
            if (index == 2) return 1;
            if (index == 3) return 2;
            if (index == 4) return 3;
            if (index == 5) return 4;
            return 0;
        }

        public static string PositionName(int index)
        {
            if (index == 5) return "左上角";
            if (index == 1) return "右上角";
            if (index == 2) return "顶部居中";
            if (index == 3) return "右下角";
            if (index == 4) return "左下角";
            return "自由拖动";
        }
    }

    // ---------------------------------------------------------------------
    // 设置窗口
    // 设计参考自 LeiBox：
    //   · 玻璃标签栏展开菜单 —— 选中块连续滑动的分段控件
    //   · 模型思考强度滑块 —— 三档 / 五档吸附式交互
    //   · 立体按压按钮 —— 保存按钮按下时内缩
    //   · 点阵加载 —— 读取设备时的骨架屏呼吸 / 扫光
    // ---------------------------------------------------------------------
    public class SettingsForm : Form
    {
        const int W = 480;
        const int Pad = 24;
        const int ContentRight = 456;
        const float WindowRadius = 18f;

        readonly MainForm _owner;
        AppSettings _draft;
        readonly float _scale;
        int _formH;
        bool _autoStartDraft;

        List<Device> _previewDevices;
        readonly List<Device> _previewDeleteDevices = new List<Device>();
        readonly List<RectangleF> _previewDeleteRects = new List<RectangleF>();
        readonly List<Device> _previewRefreshDevices = new List<Device>();
        readonly List<RectangleF> _previewRefreshRects = new List<RectangleF>();
        bool _scanning;
        int _previewDragIndex = -1;
        float _previewDragY;
        float _previewDragOffset;
        readonly Dictionary<string, float> _previewAnimY = new Dictionary<string, float>();
        Bitmap _backdrop;
        Bitmap _surface;
        int _previewRows;
        int _appearanceTitleY;
        int _appearanceRowsY;
        int _displayTitleY;
        int _displayRowsY;
        int _systemTitleY;
        int _systemRowY;
        int _activeTab;
        int _tabY;
        float _tabPos;
        int _lastLayoutTab = -1;
        readonly RectangleF[] _tabRects = new RectangleF[3];

        readonly RectangleF _headerDrag;
        readonly RectangleF _closeRect;
        RectangleF _previewRect;
        RectangleF _materialTrack;
        RectangleF _sizeTrack;
        RectangleF _fontTrack;
        RectangleF _radiusTrack;
        readonly RectangleF[] _themeRects = new RectangleF[9];
        RectangleF _positionTrack;
        RectangleF _modeToggle;
        RectangleF _panelSegment;
        RectangleF _autoStartToggle;
        RectangleF _autoFlattenToggle;
        RectangleF _showOfflineToggle;
        RectangleF _previewRangeToggle;
        RectangleF _sortToggle;
        RectangleF _opacityTrack;
        bool _opacityPressed;
        bool _opacityDragMoved;
        int _opacityPressTick;
        PointF _opacityPressPoint;
        int _opacityTarget;
        float _opacityDisplay;
        bool _opacitySavePending;
        RectangleF _airPodsEarToggle;
        RectangleF _clickThroughToggle;
        RectangleF _taskbarToggle;
        RectangleF _ignoreClearButton;
        RectangleF _refreshMinutesTrack;
        bool _panelExpanded;
        bool _panelClosed;
        bool _showAirPodsRow;
        bool _showRefreshRow;
        ulong _hotPreviewRefresh;
        string _audioOutName = "";
        string _audioInName = "";
        List<AudioDevices.Info> _audioOuts = new List<AudioDevices.Info>();
        List<AudioDevices.Info> _audioIns = new List<AudioDevices.Info>();
        List<RectangleF> _audioOutRects = new List<RectangleF>();
        List<RectangleF> _audioInRects = new List<RectangleF>();

        float _animMaterial;
        float _animMaterialTarget;
        float _animSize;
        float _animSizeTarget;
        float _animPos;
        float _animPosTarget;
        float _animRadius;
        float _animRadiusTarget;
        float _animFont;
        float _animFontTarget;
        float _animPanel;
        float _animPanelTarget;
        float _animRefresh;
        float _animRefreshTarget;
        float _shimmer;
        float _chargePhase;
        readonly Timer _animTimer;
        readonly Timer _chargeTimer;
        readonly Timer _tabTimer;

        string _hot = "";
        ColorPairForm _themeForm;
        AppSettings _themeBefore;
        bool _captureMode;
        bool _dragging;
        Point _dragOffset;

        // ---------- acrylic / rounded window ----------
        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy
        {
            public int AccentState;
            public uint AccentFlags;
            public uint GradientColor;
            public uint AnimationId;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }
        [DllImport("user32.dll")]
        static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);


        public SettingsForm(MainForm owner, AppSettings current, bool autoStart, float dpiScale,
            List<Device> previewDevices, bool scanning)
        {
            _owner = owner;
            if (_owner != null) _owner.PanelStateChanged += OwnerPanelStateChanged;
            if (_owner != null) _owner.PreviewChanged += OwnerPreviewChanged;
            _draft = current == null ? AppSettings.Defaults() : current.Clone();
            _opacityTarget = _draft.Opacity;
            _opacityDisplay = _draft.Opacity;
            AppSettings.Activate(_draft);
            _draft.Font = UiFonts.Normalize(_draft.Font);
            UiFonts.Family = _draft.Font;
            _autoStartDraft = autoStart;
            _previewDevices = previewDevices == null ? new List<Device>() : previewDevices;
            ApplyPreviewFilter();
            SnapPreviewOrder();
            _scanning = scanning;
            try
            {
                int probeTab;
                if (int.TryParse(Environment.GetEnvironmentVariable("TM_SETTINGS_TAB"), out probeTab))
                    _activeTab = Math.Max(0, Math.Min(2, probeTab));
            }
            catch { }
            _showAirPodsRow = HasAppleDevice();
            _showRefreshRow = HasManualRefreshDevice();
            _audioOutName = AudioDevices.GetDefaultName(AudioDevices.FlowRender);
            _audioInName = AudioDevices.GetDefaultName(AudioDevices.FlowCapture);
            RefreshAudioLists();

            int n = _previewDevices.Count;
            _previewRows = _scanning ? 2 : (n == 0 ? 1 : Math.Min(n, 5));

            int previewH = 30 + _previewRows * 40 + 8;
            _previewRect = new RectangleF(Pad, 84, ContentRight - Pad, previewH);
            _tabY = (int)_previewRect.Bottom + 8;
            int contentTitle = _tabY + 34 + 8;
            _appearanceTitleY = contentTitle;
            _displayTitleY = contentTitle;
            _systemTitleY = contentTitle;
            _appearanceRowsY = contentTitle + 20;
            _displayRowsY = _appearanceRowsY;
            _systemRowY = _appearanceRowsY;
            _formH = FormHeight();
            float tabW = (ContentRight - Pad) / 3f;
            for (int i = 0; i < 3; i++)
                _tabRects[i] = new RectangleF(Pad + i * tabW, _tabY, tabW - 4f, 30f);
            _tabPos = _activeTab;

            float scale = dpiScale < 1f ? 1f : dpiScale;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            float maxH = wa.Height - 40f;
            float maxW = wa.Width - 40f;
            if (_formH * scale > maxH) scale = maxH / _formH;
            if (W * scale > maxW) scale = maxW / W;
            _scale = Math.Max(0.85f, scale);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;   // independent window, visible in Alt+Tab
            MaximizeBox = false;
            MinimizeBox = false;
            KeyPreview = true;
            AllowTransparency = false;
            BackColor = Color.FromArgb(18, 18, 23);
            Text = "设置 - 电环";
            ClientSize = new Size((int)Math.Round(W * _scale), (int)Math.Round(_formH * _scale));
            ApplyWindowShape();

            _headerDrag = new RectangleF(0, 0, W, 78);
            _closeRect = new RectangleF(W - 46, 12, 30, 30);
            LayoutControls();
            _panelExpanded = owner == null || !owner.IsPanelCollapsed;
            _panelClosed = owner != null && !owner.Visible;

            _animMaterial = (float)(int)_draft.Material;
            _animMaterialTarget = _animMaterial;
            _animSize = AppSettings.ScaleIndex(_draft.UiScale);
            _animSizeTarget = _animSize;
            _animPos = AppSettings.PositionIndex(_draft.Position);
            _animPosTarget = _animPos;
            _animRadius = AppSettings.RadiusIndex(_draft.CornerRadius);
            _animRadiusTarget = _animRadius;
            _animFont = UiFonts.Index(_draft.Font);
            _animFontTarget = _animFont;
            _animPanel = _panelClosed ? 0f : (_panelExpanded ? 2f : 1f);
            _animPanelTarget = _animPanel;
            int[] refreshValuesInit = { 0, 10, 15, 30, 60 };
            _animRefresh = 2f;
            for (int ri = 0; ri < refreshValuesInit.Length; ri++)
                if (refreshValuesInit[ri] == _draft.HeadsetRefreshMinutes) { _animRefresh = ri; break; }
            _animRefreshTarget = _animRefresh;

            _animTimer = new Timer();
            _animTimer.Interval = 24;
            _animTimer.Tick += delegate { OnAnimTick(); };

            _tabTimer = new Timer();
            _tabTimer.Interval = 40;
            _tabTimer.Tick += delegate
            {
                bool moving = false;
                _tabPos = Approach(_tabPos, _activeTab, 0.55f, ref moving);
                if (!moving)
                {
                    _tabTimer.Stop();
                    Invalidate();
                }
                else
                {
                    int top = (int)(_tabY * _scale) - 4;
                    int height = (int)(38f * _scale) + 8;
                    Invalidate(new Rectangle(0, top, Width, height));
                }
            };

            _chargeTimer = new Timer();
            _chargeTimer.Interval = 100;
            _chargeTimer.Tick += delegate
            {
                if (!Visible || !HasChargingPreview()) { _chargeTimer.Stop(); return; }
                _chargePhase += 0.07f;
                if (_chargePhase > 1f) _chargePhase -= 1f;
                Invalidate();
            };
        }

        void HideInactiveControls()
        {
            if (_activeTab != 0)
            {
                _materialTrack = RectangleF.Empty;
                _sizeTrack = RectangleF.Empty;
                _radiusTrack = RectangleF.Empty;
                _fontTrack = RectangleF.Empty;
                _positionTrack = RectangleF.Empty;
                for (int i = 0; i < _themeRects.Length; i++) _themeRects[i] = RectangleF.Empty;
            }
            if (_activeTab != 1)
            {
                _modeToggle = RectangleF.Empty;
                _panelSegment = RectangleF.Empty;
                _autoFlattenToggle = RectangleF.Empty;
                _showOfflineToggle = RectangleF.Empty;
                _previewRangeToggle = RectangleF.Empty;
                _sortToggle = RectangleF.Empty;
                _opacityTrack = RectangleF.Empty;
            }
            if (_activeTab != 2)
            {
                _autoStartToggle = RectangleF.Empty;
                _ignoreClearButton = RectangleF.Empty;
                _refreshMinutesTrack = RectangleF.Empty;
                _airPodsEarToggle = RectangleF.Empty;
                _clickThroughToggle = RectangleF.Empty;
                _taskbarToggle = RectangleF.Empty;
            }
        }
        void SelectAudioDevice(bool output, int index)
        {
            List<AudioDevices.Info> list = output ? _audioOuts : _audioIns;
            if (index < 0 || index >= list.Count) return;
            AudioDevices.Info info = list[index];
            AudioDevices.SetDefault(info.Id,
                output ? AudioDevices.FlowRender : AudioDevices.FlowCapture);
            if (output) _audioOutName = AudioDevices.GetDefaultName(AudioDevices.FlowRender);
            else _audioInName = AudioDevices.GetDefaultName(AudioDevices.FlowCapture);
            RefreshAudioLists();
            Invalidate();
        }

        void RefreshAudioLists()
        {
            try
            {
                _audioOuts = SortBuiltInAudio(AudioDevices.GetActive(AudioDevices.FlowRender), true);
                _audioIns = SortBuiltInAudio(AudioDevices.GetActive(AudioDevices.FlowCapture), false);
                _audioOutName = AudioDevices.GetDefaultName(AudioDevices.FlowRender);
                _audioInName = AudioDevices.GetDefaultName(AudioDevices.FlowCapture);
            }
            catch { }
        }

        void LayoutControls()
        {
            _materialTrack = new RectangleF(252, _appearanceRowsY + 7, 204, 30);
            _sizeTrack = new RectangleF(252, _appearanceRowsY + 44 + 7, 204, 30);
            _radiusTrack = new RectangleF(252, _appearanceRowsY + 88 + 7, 204, 30);
            _fontTrack = new RectangleF(252, _appearanceRowsY + 132 + 7, 204, 30);
            for (int i = 0; i < 9; i++)
            {
                float cx = 258 + i * 22;
                float cy = _appearanceRowsY + 176 + 22;
                _themeRects[i] = new RectangleF(cx - 11, cy - 11, 22, 22);
            }
            _positionTrack = new RectangleF(252, _appearanceRowsY + 220 + 7, 204, 30);
            _modeToggle = new RectangleF(414, _displayRowsY + 11, 42, 22);
            _panelSegment = new RectangleF(252, _displayRowsY + 44 + 7, 204, 30);
            _autoStartToggle = new RectangleF(414, _systemRowY + 44 + 11, 42, 22);
            _autoFlattenToggle = new RectangleF(414, _displayRowsY + 88 + 11, 42, 22);
            _showOfflineToggle = new RectangleF(414, _displayRowsY + 132 + 11, 42, 22);
            _previewRangeToggle = new RectangleF(414, _displayRowsY + 132 + 11, 42, 22);
            _sortToggle = new RectangleF(414, _displayRowsY + 176 + 11, 42, 22);
            _opacityTrack = new RectangleF(246, _displayRowsY + 220 + 7, 210, 30);
            _ignoreClearButton = new RectangleF(384, _systemRowY + 88 + 8, 72, 26);
            _taskbarToggle = new RectangleF(414, _systemRowY + 132 + 11, 42, 22);
            int nextRow = 176;
            _refreshMinutesTrack = _showRefreshRow
                ? new RectangleF(252, _systemRowY + nextRow + 7, 204, 30) : RectangleF.Empty;
            if (_showRefreshRow) nextRow += 44;
            _airPodsEarToggle = _showAirPodsRow
                ? new RectangleF(414, _systemRowY + nextRow + 11, 42, 22) : RectangleF.Empty;
            if (_showAirPodsRow) nextRow += 44;
            _clickThroughToggle = new RectangleF(414, _systemRowY + 11, 42, 22);
            int outputRow = Math.Max(_systemRowY + nextRow, _systemRowY + 176);
            float tabW = (ContentRight - Pad) / 3f;
            for (int i = 0; i < 3; i++)
                _tabRects[i] = new RectangleF(Pad + i * tabW, _tabY, tabW - 4f, 30f);
            HideInactiveControls();
        }

        int SystemRowCount()
        {
            if (_showRefreshRow && _showAirPodsRow) return 8;
            if (_showRefreshRow || _showAirPodsRow) return 7;
            return 6;
        }

        int ActiveRowCount()
        {
            return SystemRowCount();
        }

        int FormHeight()
        {
            return _appearanceRowsY + ActiveRowCount() * 44 + 16;
        }

        bool HasAppleDevice()
        {
            for (int i = 0; i < _previewDevices.Count; i++)
                if (_previewDevices[i].IsApple) return true;
            return false;
        }

        bool HasManualRefreshDevice()
        {
            for (int i = 0; i < _previewDevices.Count; i++)
                if (_previewDevices[i].ManualRefresh && !_previewDevices[i].Ignored) return true;
            return false;
        }

        void UpdatePreviewLayout()
        {
            int rows = _scanning ? 2 : (_previewDevices.Count == 0 ? 1 : Math.Min(_previewDevices.Count, 5));
            bool showAirPods = HasAppleDevice();
            bool showRefresh = HasManualRefreshDevice();
            if (rows == _previewRows && showAirPods == _showAirPodsRow &&
                showRefresh == _showRefreshRow && _lastLayoutTab == _activeTab && _formH > 0)
            {
                Invalidate();
                return;
            }
            _previewRows = rows;
            _showAirPodsRow = showAirPods;
            _showRefreshRow = showRefresh;
            _lastLayoutTab = _activeTab;
            int previewH = 30 + _previewRows * 40 + 8;
            _previewRect = new RectangleF(Pad, 84, ContentRight - Pad, previewH);
            _tabY = (int)_previewRect.Bottom + 8;
            int contentTitle = _tabY + 34 + 8;
            _appearanceTitleY = contentTitle;
            _displayTitleY = contentTitle;
            _systemTitleY = contentTitle;
            _appearanceRowsY = contentTitle + 20;
            _displayRowsY = _appearanceRowsY;
            _systemRowY = _appearanceRowsY;
            _formH = FormHeight();

            LayoutControls();

            ClientSize = new Size((int)Math.Round(W * _scale), (int)Math.Round(_formH * _scale));
            ApplyWindowShape();
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            if (Top + Height > wa.Bottom)
                Top = Math.Max(wa.Top, wa.Bottom - Height);
            InvalidateSurface();
            Invalidate();
        }

        void EnableEffects()
        {
            try
            {
                AccentPolicy accent = new AccentPolicy();
                accent.AccentState = 4;
                accent.GradientColor = 0x9016161B;
                IntPtr data = Marshal.AllocHGlobal(Marshal.SizeOf(accent));
                Marshal.StructureToPtr(accent, data, false);
                WindowCompositionAttributeData wcad = new WindowCompositionAttributeData();
                wcad.Attribute = 19;
                wcad.Data = data;
                wcad.SizeOfData = Marshal.SizeOf(accent);
                SetWindowCompositionAttribute(Handle, ref wcad);
                Marshal.FreeHGlobal(data);

                int pref = 2;
                DwmSetWindowAttribute(Handle, 33, ref pref, sizeof(int));
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
                int noBorder = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
                DwmSetWindowAttribute(Handle, 34, ref noBorder, sizeof(int));
                int ncDisabled = 1; // DWMNCRP_DISABLED: no DWM frame/shadow
                DwmSetWindowAttribute(Handle, 2, ref ncDisabled, sizeof(int));
            }
            catch { }
        }

        // Capture what is behind the window before it is shown, then blur it.
        // This keeps the glass look identical on every machine / DWM setting
        // instead of depending on an undocumented acrylic policy.
        public void PrepareBackdrop()
        {
            try
            {
                Rectangle rect = new Rectangle(Location, Size);
                Rectangle screen = SystemInformation.VirtualScreen;
                if (!screen.Contains(rect))
                {
                    if (_backdrop != null) { _backdrop.Dispose(); _backdrop = null; }
                    return;
                }

                using (Bitmap shot = new Bitmap(Size.Width, Size.Height))
                {
                    using (Graphics g = Graphics.FromImage(shot))
                        g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);

                    int sw = Math.Max(6, Size.Width / 22);
                    int sh = Math.Max(6, Size.Height / 22);
                    using (Bitmap small = new Bitmap(sw, sh))
                    {
                        using (Graphics g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(shot, new Rectangle(0, 0, sw, sh));
                        }
                        Bitmap full = new Bitmap(Size.Width, Size.Height);
                        using (Graphics g = Graphics.FromImage(full))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(small, new Rectangle(0, 0, Size.Width, Size.Height));
                        }
                        if (_backdrop != null) _backdrop.Dispose();
                        _backdrop = full;
                        InvalidateSurface();
                    }
                }
            }
            catch
            {
                if (_backdrop != null) { _backdrop.Dispose(); _backdrop = null; }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_captureMode)
            {
                Refresh();
            }
            Native.SetForegroundWindow(Handle);
            Activate();
            if (!_captureMode && _scanning) _animTimer.Start();
            if (!_captureMode && HasChargingPreview()) _chargeTimer.Start();
        }

        void ApplyWindowShape()
        {
            // Let DWM round the window. Region clipping is pixel-quantized and
            // produces visible stair-steps on the corners.
            if (Region != null) { Region.Dispose(); Region = null; }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int pref = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(Handle, 33, ref pref, sizeof(int));
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
                int noBorder = unchecked((int)0xFFFFFFFE);
                DwmSetWindowAttribute(Handle, 34, ref noBorder, sizeof(int));
            }
            catch { }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不画系统背景，让 Acrylic 从半透明绘制层里透出来
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.TextContrast = 4;
            g.ScaleTransform(_scale, _scale);

            if (_surface == null) BuildSurface();
            if (_surface != null)
                g.DrawImage(_surface, new RectangleF(-1.5f, -1.5f, W + 3f, _formH + 3f));
            else
                DrawBackground(g);

            _previewDeleteDevices.Clear();
            _previewDeleteRects.Clear();
            _previewRefreshDevices.Clear();
            _previewRefreshRects.Clear();
            DrawHeader(g);
            DrawPreview(g);
            DrawTabs(g);
            if (_activeTab == 0) DrawAppearance(g);
            else if (_activeTab == 1) DrawDisplay(g);
            else DrawSystem(g);
        }

        void DrawTabs(Graphics g)
        {
            string[] labels = { "外观", "显示", "系统" };
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            float tabW = (ContentRight - Pad) / 3f;
            RectangleF track = new RectangleF(Pad, _tabY, ContentRight - Pad, 30f);
            using (GraphicsPath gp = Round(track, 10f))
            {
                using (SolidBrush b = new SolidBrush(GlassStyle.Tile))
                    g.FillPath(b, gp);
                using (Pen p = new Pen(GlassStyle.Hairline, 1f))
                    g.DrawPath(p, gp);
            }

            float pillW = tabW;
            if (pillW > track.Width) pillW = track.Width;
            float pillX = Pad + _tabPos * tabW + (tabW - pillW) / 2f;
            if (pillX < track.X) pillX = track.X;
            if (pillX + pillW > track.Right) pillX = track.Right - pillW;
            RectangleF pill = new RectangleF(pillX, _tabY, pillW, 30f);
            GraphicsState tabState = g.Save();
            using (GraphicsPath clip = Round(track, 10f))
                g.SetClip(clip, CombineMode.Intersect);
            for (int hi = 0; hi < 3; hi++)
            {
                if (hi == _activeTab || _hot != "tab" + hi) continue;
                RectangleF hoverCell = new RectangleF(Pad + hi * tabW + 3f, _tabY + 3f, tabW - 6f, 24f);
                using (GraphicsPath hg = Round(hoverCell, 8f))
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(31, 255, 255, 255)))
                    g.FillPath(hb, hg);
            }
            using (GraphicsPath gp = Round(pill, 10f))
            using (LinearGradientBrush lg = new LinearGradientBrush(pill,
                Lighten(accent, 0.10f), Darken(accent, 0.08f), 90f))
                g.FillPath(lg, gp);
            g.Restore(tabState);

            using (Font f = F("Microsoft YaHei UI", 8.2f, FontStyle.Bold))
            {
                for (int i = 0; i < 3; i++)
                {
                    bool settled = Math.Abs(_tabPos - _activeTab) < 0.04f;
                    Color ink = (i == _activeTab && settled) ? GlassStyle.OnAccent : GlassStyle.Muted;
                    using (SolidBrush b = new SolidBrush(ink))
                        DrawCentered(g, labels[i], f, b, _tabRects[i]);
                }
            }
        }

        void DrawAppearance(Graphics g)
        {
            DrawSection(g, _appearanceTitleY, "01", "外观");
            DrawRowText(g, "背景", GlassStyle.Name(_draft.Material) + " · " + GlassStyle.Description(_draft.Material), _appearanceRowsY);
            DrawSegmented(g, _materialTrack, new string[] { "纯色", "渐变" }, (int)_draft.Material, _animMaterial, "material");
            DrawRowLine(g, _appearanceRowsY + 44);

            DrawRowText(g, "界面大小", "整体缩放 · " + AppSettings.ScaleName(_draft.UiScale), _appearanceRowsY + 44);
            DrawSegmented(g, _sizeTrack, new string[] { "小", "标准", "大" }, AppSettings.ScaleIndex(_draft.UiScale), _animSize, "size");
            DrawRowLine(g, _appearanceRowsY + 88);

            DrawRowText(g, "圆角", "面板圆角 · " + AppSettings.RadiusName(_draft.CornerRadius), _appearanceRowsY + 88);
            DrawSegmented(g, _radiusTrack, new string[] { "直角", "小", "标准", "大" },
                AppSettings.RadiusIndex(_draft.CornerRadius), _animRadius, "radius");
            DrawRowLine(g, _appearanceRowsY + 132);

            DrawRowText(g, "字体", "界面字体 · " + UiFonts.Label(_draft.Font), _appearanceRowsY + 132);
            DrawFontSegmented(g, _fontTrack, UiFonts.Labels, UiFonts.Families, UiFonts.Index(_draft.Font), _animFont, "font");
            DrawRowLine(g, _appearanceRowsY + 176);

            string themeDesc = _draft.Theme == 8
                ? "自定义主题"
                : "强调色 · " + AppSettings.ThemeName(_draft.Theme);
            DrawRowText(g, "主题色", themeDesc, _appearanceRowsY + 176);
            DrawThemeSwatches(g);
            DrawRowLine(g, _appearanceRowsY + 220);

            DrawRowText(g, "面板位置", "启动时停靠 · " + AppSettings.PositionName(_draft.Position), _appearanceRowsY + 220);
            DrawSegmented(g, _positionTrack, new string[] { "自由", "左上", "右上", "顶部", "右下", "左下" },
                AppSettings.PositionIndex(_draft.Position), _animPos, "pos");
        }

        void DrawDisplay(Graphics g)
        {
            DrawSection(g, _displayTitleY, "02", "显示");
            DrawToggleRow(g, "显示连接方式", "标注有线 / 2.4G / 蓝牙", _displayRowsY, _draft.ShowMode, _modeToggle, "mode");
            DrawRowLine(g, _displayRowsY + 44);
            DrawRowText(g, "面板状态", "关闭 / 收起 / 展开主面板", _displayRowsY + 44);
            int panelIndex = _panelClosed ? 0 : (_panelExpanded ? 2 : 1);
            DrawSegmented(g, _panelSegment, new string[] { "关闭", "收起", "展开" }, panelIndex, _animPanel, "panel");
            DrawRowLine(g, _displayRowsY + 88);
            DrawToggleRow(g, "贴边自动直角", "靠近屏幕边缘时取消贴边圆角", _displayRowsY + 88,
                _draft.AutoFlattenCorners, _autoFlattenToggle, "autoflatten");
            DrawRowLine(g, _displayRowsY + 132);
            DrawToggleRow(g, "实时预览", _draft.ShowOffline ? "显示全部连接过设备" : "仅显示实时连接设备", _displayRowsY + 132,
                !_draft.ShowOffline, _previewRangeToggle, "previewrange");

            DrawRowLine(g, _displayRowsY + 176);
            DrawToggleRow(g, "拖动排序", "主面板/设置可拖动 · 仅已连接设备", _displayRowsY + 176,
                _draft.AllowDeviceSort, _sortToggle, "devicesort");
            DrawRowLine(g, _displayRowsY + 220);
            DrawRowText(g, "透明度", "主面板不透明度 · " + _draft.Opacity + "%", _displayRowsY + 220);
            DrawOpacitySlider(g, _opacityTrack);
        }

        void DrawSystem(Graphics g)
        {
            DrawSection(g, _systemTitleY, "03", "系统");
            DrawToggleRow(g, "点击穿透", "鼠标穿透主面板 · 托盘设置关闭", _systemRowY, _draft.ClickThrough, _clickThroughToggle, "clickthrough");
            DrawToggleRow(g, "开机自启", "登录 Windows 后自动运行", _systemRowY + 44, _autoStartDraft, _autoStartToggle, "autostart");

            int ignored = _owner == null ? 0 : _owner.IgnoredCount;
            DrawRowText(g, "忽略名单", ignored == 0 ? "预览中右键设备可忽略" : "已忽略 " + ignored + " 个 · 列表中可恢复", _systemRowY + 88);
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            using (GraphicsPath gp = Round(_ignoreClearButton, _ignoreClearButton.Height / 2f))
            using (SolidBrush b = new SolidBrush(ignored > 0 ? accent : GlassStyle.Tile))
                g.FillPath(b, gp);
            if (_hot == "clearignore")
            {
                using (GraphicsPath hg = Round(_ignoreClearButton, _ignoreClearButton.Height / 2f))
                using (SolidBrush hb = new SolidBrush(Color.FromArgb(31, 255, 255, 255)))
                    g.FillPath(hb, hg);
            }
            using (Font f = F("Microsoft YaHei UI", 7.8f, FontStyle.Bold))
            using (SolidBrush b = new SolidBrush(ignored > 0 ? GlassStyle.OnAccent : GlassStyle.Muted))
                DrawCentered(g, "恢复", f, b, _ignoreClearButton);

            DrawToggleRow(g, "任务栏电量", "在任务栏通知区域左侧显示设备电量",
                _systemRowY + 132, _draft.TaskbarBadge, _taskbarToggle, "taskbar");

            int nextRow = 176;
            if (_showRefreshRow)
            {
                int[] refreshValues = { 0, 10, 15, 30, 60 };
                int refreshIndex = 2;
                for (int i = 0; i < refreshValues.Length; i++)
                    if (refreshValues[i] == _draft.HeadsetRefreshMinutes) { refreshIndex = i; break; }
                DrawRowText(g, "自动刷新电量时间",
                    _draft.HeadsetRefreshMinutes == 0 ? "已关闭自动刷新" : "每 " + _draft.HeadsetRefreshMinutes + " 分钟 · 刷新时音频可能会中断",
                    _systemRowY + nextRow);
                DrawSegmented(g, _refreshMinutesTrack, new string[] { "关", "10", "15", "30", "60" },
                    refreshIndex, _animRefresh, "refreshmin");
                nextRow += 44;
            }

            if (_showAirPodsRow)
            {
                DrawToggleRow(g, "AirPods 入耳控制", "双耳入耳时自动播放/暂停媒体", _systemRowY + nextRow,
                    _draft.AirPodsEarControl, _airPodsEarToggle, "airpods");
                nextRow += 44;
            }
            int outputY = Math.Max(_systemRowY + nextRow, _systemRowY + 176);
            _audioOutRects.Clear(); _audioInRects.Clear();
            DrawAudioColumnDesign(g, "输出设备", _audioOuts, _audioOutName, 16, 216, outputY, _audioOutRects, "audioout");
            DrawAudioColumnDesign(g, "输入设备", _audioIns, _audioInName, 240, 216, outputY, _audioInRects, "audioin");
        }

        static float Lum(Color c)
        {
            return c.R * 0.299f + c.G * 0.587f + c.B * 0.114f;
        }

        static string ShortAudio(string s)
        {
            if (string.IsNullOrEmpty(s)) return "默认";
            int max = s.IndexOf(" (", StringComparison.Ordinal);
            if (max > 0) s = s.Substring(0, max);
            return s.Length > 18 ? s.Substring(0, 17) + "…" : s;
        }

        // ---------- backdrop ----------
        void InvalidateSurface()
        {
            if (_surface != null) { _surface.Dispose(); _surface = null; }
        }

        void BuildSurface()
        {
            InvalidateSurface();
            Bitmap bmp = new Bitmap(Width, Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(_scale, _scale);
                DrawBackground(g);
            }
            _surface = bmp;
        }

        void DrawBackground(Graphics g)
        {
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            RectangleF full = new RectangleF(0, 0, W, _formH);
            using (GraphicsPath gp = Round(full, WindowRadius))
            {
                if (_draft.Material == GlassMaterial.Solid)
                {
                    using (SolidBrush b = new SolidBrush(GlassStyle.SolidBackground(accent)))
                    {
                        g.FillRectangle(b, full);
                        g.FillPath(b, gp);
                    }
                }
                else
                {
                    using (LinearGradientBrush lg = new LinearGradientBrush(full,
                        GlassStyle.GradientTop(accent), GlassStyle.GradientBottom(accent),
                        LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(lg, full);
                        g.FillPath(lg, gp);
                    }
                }

                GraphicsState st = g.Save();
                g.SetClip(gp);
                using (GraphicsPath glow = new GraphicsPath())
                {
                    glow.AddEllipse(W - 270, -190, 390, 360);
                    using (PathGradientBrush pg = new PathGradientBrush(glow))
                    {
                        pg.CenterColor = Color.FromArgb(_draft.Material == GlassMaterial.Gradient ? 46 : 28, accent);
                        pg.SurroundColors = new Color[] { Color.FromArgb(0, accent) };
                        g.FillPath(pg, glow);
                    }
                }
                g.Restore(st);
            }
        }

        void DrawHeader(Graphics g)
        {
            DrawAppMark(g, new RectangleF(Pad, 26, 34, 34));

            using (Font fTitle = F("Microsoft YaHei UI", 15.5f, FontStyle.Bold))
            using (Font fSub = F("Microsoft YaHei UI", 8.8f, FontStyle.Regular))
            using (SolidBrush bTitle = new SolidBrush(GlassStyle.Ink))
            using (SolidBrush bSub = new SolidBrush(GlassStyle.Muted))
            {
                DrawLeftText(g, "设置", fTitle, bTitle, new RectangleF(72, 16, 260, 26));
                string[] subLines = BalancedLines(g, "外观、显示与系统行为", fSub, 300f);
                for (int i = 0; i < subLines.Length; i++)
                    DrawLeftText(g, subLines[i], fSub, bSub, new RectangleF(73, 45 + i * 12, 300, 15));
            }

            bool hot = _hot == "close";
            if (hot)
            {
                using (SolidBrush b = new SolidBrush(GlassStyle.Tile))
                using (GraphicsPath gp = Round(_closeRect, 8f))
                    g.FillPath(b, gp);
            }
            using (Pen p = new Pen(hot ? Color.FromArgb(245, 245, 248) : GlassStyle.Muted, 1.4f))
            {
                float cx = _closeRect.X + _closeRect.Width / 2f;
                float cy = _closeRect.Y + _closeRect.Height / 2f;
                g.DrawLine(p, cx - 4f, cy - 4f, cx + 4f, cy + 4f);
                g.DrawLine(p, cx + 4f, cy - 4f, cx - 4f, cy + 4f);
            }
        }

        void DrawAppMark(Graphics g, RectangleF r)
        {
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            using (GraphicsPath gp = Round(r, 10f))
            {
                using (LinearGradientBrush lg = new LinearGradientBrush(r,
                    Lighten(accent, 0.18f), Darken(accent, 0.14f), 55f))
                {
                    g.FillPath(lg, gp);
                }
                using (Pen border = new Pen(GlassStyle.Hairline, 1f))
                    g.DrawPath(border, gp);
            }

            using (Pen p = new Pen(GlassStyle.Ink, 1.35f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
            {
                g.DrawRectangle(p, r.X + 9f, r.Y + 10f, 15f, 13f);
                using (GraphicsPath tip = Round(new RectangleF(r.X + 24f, r.Y + 14f, 3f, 5f), 1.2f))
                    g.FillPath(b, tip);
                g.FillRectangle(b, r.X + 12f, r.Y + 15f, 3f, 3f);
                g.FillRectangle(b, r.X + 16.5f, r.Y + 15f, 3f, 3f);
            }
        }

        // ---------- preview: every device, not just one ----------
        void DrawPreview(Graphics g)
        {
            RectangleF card = _previewRect;
            GlassStyle.DrawDropShadow(g, card, 14f, 4, 12, 1.8f);
            using (GraphicsPath gp = Round(card, 14f))
            {
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
                    g.FillPath(sb, gp);
                using (Pen p = new Pen(GlassStyle.Hairline, 1f))
                    g.DrawPath(p, gp);
            }

            using (Font fTiny = F("Microsoft YaHei UI", 8.2f, FontStyle.Regular))
            using (Font fCount = F("Microsoft YaHei UI", 8.0f, FontStyle.Regular))
            using (SolidBrush bTiny = new SolidBrush(GlassStyle.Muted))
            using (SolidBrush bCount = new SolidBrush(GlassStyle.Muted))
            using (SolidBrush bAccent = new SolidBrush(AppSettings.ThemeColor(_draft.Theme)))
            {
                g.FillEllipse(bAccent, card.X + 16, card.Y + 13, 4, 4);
                DrawLeftText(g, _draft.ShowOffline ? "总览" : "实时预览", fTiny, bTiny,
                    new RectangleF(card.X + 24, card.Y + 3, 120, 18));
                int onlineCount = 0, offlineCount = 0;
                for (int pi = 0; pi < _previewDevices.Count; pi++)
                {
                    if (_previewDevices[pi].IsAppleCase) continue;
                    if (_previewDevices[pi].Offline) offlineCount++; else onlineCount++;
                }
                string countText;
                int totalCount = onlineCount + offlineCount;
                if (_scanning) countText = "正在读取设备…";
                else if (totalCount == 0) countText = "0 台设备";
                else if (offlineCount > 0) countText = onlineCount + " 台在线 · " + offlineCount + " 台已断开";
                else if (totalCount > 5) countText = "共 " + totalCount + " 台 · 显示前 5 台";
                else countText = totalCount + " 台设备";
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Far;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(countText, fCount, bCount, new RectangleF(card.X + 120, card.Y + 3 + KaiShift, card.Width - 136, 22), sf);
            }

            if (_scanning)
            {
                for (int i = 0; i < 2; i++) DrawPreviewSkeletonRow(g, i);
                DrawShimmer(g, card);
                return;
            }

            if (_previewDevices.Count == 0)
            {
                using (Font f = F("Microsoft YaHei UI", 7.2f, FontStyle.Regular))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(132, 132, 144)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("暂未发现已连接设备", f, b,
                        new RectangleF(card.X, card.Y + 30, card.Width, 40), sf);
                }
                return;
            }

            int rows = Math.Min(_previewDevices.Count, _previewRows);
            for (int i = 0; i < rows; i++)
            {
                string key = _previewDevices[i].Name;
                if (!_previewAnimY.ContainsKey(key))
                    _previewAnimY[key] = i;
                if (i == _previewDragIndex) continue;
                if (_previewDragIndex >= 0 && IsAttachedCase(_previewDevices[_previewDragIndex], _previewDevices[i]))
                    continue;
                float top = card.Y + 30 + _previewAnimY[key] * 40f;
                DrawPreviewRowAt(g, _previewDevices[i], i, top);
                if (_previewDragIndex < 0 && i < rows - 1)
                {
                    using (Pen p = new Pen(Color.FromArgb(14, 255, 255, 255), 1f))
                        g.DrawLine(p, card.X + 16, card.Y + 30 + (i + 1) * 40, card.Right - 16, card.Y + 30 + (i + 1) * 40);
                }
            }
            if (_previewDragIndex >= 0 && _previewDragIndex < rows)
            {
                float dragTop = _previewDragY - _previewDragOffset;
                DrawPreviewRowAt(g, _previewDevices[_previewDragIndex], _previewDragIndex, dragTop);
                Device attached = AttachedCase(_previewDevices[_previewDragIndex]);
                if (attached != null) DrawPreviewRowAt(g, attached, _previewDragIndex + 1, dragTop + 40f);
            }
        }

        void DrawAppleEarRings(Graphics g, Device d, RectangleF card, float top)
        {
            float cy = top + 20f;
            float[] centers = { card.Right - 158f, card.Right - 70f };
            float[] pctX = { card.Right - 146f, card.Right - 58f };
            int[] values = { d.AppleLeft, d.AppleRight };
            bool[] charging = { d.AppleLeftCharging, d.AppleRightCharging };
            string[] labels = { "L", "R" };
            using (Font fLabel = F("Microsoft YaHei UI", 6.4f, FontStyle.Bold))
            using (Font fVal = F(UiFonts.Family, 10.8f, FontStyle.Bold))
            using (SolidBrush bLabel = new SolidBrush(GlassStyle.Muted))
            {
                for (int i = 0; i < 2; i++)
                {
                    float cx = centers[i];
                    float rad = 11.5f;
                    RectangleF ring = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);
                    Color c = charging[i] && values[i] < 100 ? Color.FromArgb(255, 212, 175, 55)
                        : (values[i] >= 0 && values[i] < 20 ? GlassStyle.LowBattery
                            : (values[i] >= 0 ? AppSettings.ThemeColor(_draft.Theme) : Color.FromArgb(120, 120, 130)));
                    using (Pen back = new Pen(GlassStyle.Hairline, 3f))
                        g.DrawEllipse(back, ring);
                    if (values[i] >= 0)
                    using (Pen p = new Pen(c, 3.2f))
                    {
                        p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                        g.DrawArc(p, ring, -90f, 3.6f * Math.Max(0, Math.Min(100, values[i])));
                    }
                    DrawCentered(g, labels[i], fLabel, bLabel,
                        new RectangleF(cx - 8f, cy - 7f, 16f, 14f));
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Far;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                    using (SolidBrush b = new SolidBrush(c))
                        g.DrawString(values[i] >= 0 ? values[i] + "%" : "—", fVal, b,
                            new RectangleF(pctX[i], top + 4f + KaiShift, 50f, 32f), sf);
                }
            }
        }

        void DrawCaseGlyph(Graphics g, RectangleF r)
        {
            Color ink = GlassStyle.Ink;
            using (Pen p = new Pen(ink, 1.3f))
            {
                using (GraphicsPath gp = Round(new RectangleF(r.X + 2f, r.Y + 4f, r.Width - 4f, r.Height - 6f), 2.2f))
                    g.DrawPath(p, gp);
                g.DrawLine(p, r.X + 4f, r.Y + 8f, r.Right - 4f, r.Y + 8f);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
                    g.FillEllipse(b, r.X + r.Width / 2f - 1.2f, r.Y + r.Height / 2f - 1.2f, 2.4f, 2.4f);
            }
        }

        void DrawPreviewRowAt(Graphics g, Device d, int index, float top)
        {
            RectangleF card = _previewRect;
            float nudge = index > 0 ? 4f : 0f;
            float indent = d.IsAppleCase ? 16f : 0f;
            Color accent = AppSettings.ThemeColor(_draft.Theme);

            RectangleF tile = new RectangleF(card.X + 16 + indent, top + 7 + nudge, 26, 26);
            using (GraphicsPath gp = Round(tile, 7f))
            using (SolidBrush b = new SolidBrush(GlassStyle.Tile))
                g.FillPath(b, gp);
            if (d.IsAppleCase)
                DrawCaseGlyph(g, new RectangleF(tile.X + 4f, tile.Y + 4f, 18f, 18f));
            else
                DrawGlyph(g, d.Kind, new RectangleF(tile.X + 4f, tile.Y + 4f, 18f, 18f));

            using (Font fName = F("Microsoft YaHei UI", 8.5f, FontStyle.Bold))
            using (Font fSub = F("Microsoft YaHei UI", 8.0f, FontStyle.Regular))
            using (Font fPct = F(UiFonts.Family, 10.8f, FontStyle.Bold))
            using (SolidBrush bName = new SolidBrush(GlassStyle.Ink))
            using (SolidBrush bSub = new SolidBrush(GlassStyle.Muted))
            using (SolidBrush bPct = new SolidBrush(d.Cached ? GlassStyle.Muted : GlassStyle.Ink))
            {
                float nameX = card.X + 50 + indent;
                DrawLeftText(g, Trim(g, d.Name, fName, 270f - indent), fName, bName, new RectangleF(nameX, top + 2 + nudge, 270, 18));

                string status = PreviewStatus(d);
                float subX = nameX;
                if (_draft.ShowMode)
                {
                    using (SolidBrush dot = new SolidBrush(ModeColor(d.Mode)))
                        g.FillEllipse(dot, nameX, top + 23f + nudge, 3f, 3f);
                    subX = nameX + 8f;
                }
                if (d.IsApple)
                {
                    using (Font fAppleStatus = F("Microsoft YaHei UI", 7.0f, FontStyle.Regular))
                    {
                        float statusMax = card.Right - 176f - subX;
                        DrawLeftText(g, Trim(g, status, fAppleStatus, statusMax), fAppleStatus, bSub,
                            new RectangleF(subX, top + 17 + nudge, statusMax, 16));
                    }
                    DrawAppleEarRings(g, d, card, top + nudge);
                    return;
                }
                else
                {
                    DrawLeftText(g, Trim(g, status, fSub, 262f), fSub, bSub, new RectangleF(subX, top + 17 + nudge, 262, 16));
                }

                bool manualRefresh = d.ManualRefresh && d.BtAddress != 0;
                bool showRefresh = manualRefresh &&
                    (_hotPreviewRefresh == d.BtAddress || (_owner != null && _owner.IsHeadsetReading));
                if (manualRefresh)
                {
                    _previewRefreshRects.Add(new RectangleF(card.Right - 62f, top + 4f + nudge, 54f, 32f));
                    _previewRefreshDevices.Add(d);
                }

                if (d.Offline)
                {
                    RectangleF del = new RectangleF(card.Right - 38f, top + 9f + nudge, 22f, 22f);
                    using (GraphicsPath gp = Round(del, 7f))
                    using (SolidBrush b = new SolidBrush(GlassStyle.Tile))
                        g.FillPath(b, gp);
                    using (Pen p = new Pen(Color.FromArgb(190, 255, 130, 140), 1.6f))
                    {
                        p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                        g.DrawLine(p, del.X + 7f, del.Y + 7f, del.Right - 7f, del.Bottom - 7f);
                        g.DrawLine(p, del.Right - 7f, del.Y + 7f, del.X + 7f, del.Bottom - 7f);
                    }
                    _previewDeleteRects.Add(del);
                    _previewDeleteDevices.Add(d);
                    return;
                }

                float cx = card.Right - 70f, cy = top + 20f + nudge, rad = 10f;
                RectangleF ring = new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2);
                Color ringColor = d.Charging && d.Battery < 100
                    ? Color.FromArgb(255, 212, 175, 55)
                    : (d.Battery >= 0 && d.Battery < 20
                        ? GlassStyle.LowBattery
                        : ((d.Offline || d.Battery < 0 || d.Cached)
                            ? Color.FromArgb(122, 122, 134)
                            : accent));
                using (Pen back = new Pen(GlassStyle.Hairline, 3f))
                    g.DrawEllipse(back, ring);
                if (d.Charging)
                {
                    using (Pen halo = new Pen(Color.FromArgb(58, ringColor), 6f))
                        g.DrawEllipse(halo, ring);
                }
                if (d.Battery >= 0)
                {
                    using (Pen p = new Pen(ringColor, 3.2f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        g.DrawArc(p, ring, -90f, 3.6f * Math.Max(0, Math.Min(100, d.Battery)));
                    }
                }
                if (d.Charging)
                {
                    GlassStyle.DrawChargingBolt(g, ring, _chargePhase);
                }

                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Far;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                Color pctColor = d.Charging && d.Battery < 100
                    ? Color.FromArgb(255, 212, 175, 55)
                    : (d.Battery >= 0 && d.Battery < 20
                        ? GlassStyle.LowBattery
                        : ((d.Cached || d.Offline) ? GlassStyle.Muted : accent));
                if (showRefresh)
                {
                    GlassStyle.DrawRefreshIcon(g, new RectangleF(card.Right - 44f, top + 4f + nudge + 8f, 16f, 16f),
                        GlassStyle.Ink);
                }
                else
                {
                    using (SolidBrush pctBrush = new SolidBrush(pctColor))
                        g.DrawString(d.Battery >= 0 ? d.Battery + "%" : "—", fPct, pctBrush,
                            new RectangleF(card.Right - 58, top + 4 + nudge + KaiShift, 50, 32), sf);
                }
            }
        }

        void DrawPreviewSkeletonRow(Graphics g, int index)
        {
            RectangleF card = _previewRect;
            float top = card.Y + 30 + index * 40 + (index > 0 ? 4f : 0f);
            Color block = GlassStyle.Tile;
            using (SolidBrush b = new SolidBrush(block))
            {
                using (GraphicsPath gp = Round(new RectangleF(card.X + 16, top + 7, 26, 26), 7f))
                    g.FillPath(b, gp);
                using (GraphicsPath gp = Round(new RectangleF(card.X + 50, top + 9, 112, 7), 3.5f))
                    g.FillPath(b, gp);
                using (GraphicsPath gp = Round(new RectangleF(card.X + 50, top + 23, 150, 5), 2.5f))
                    g.FillPath(b, gp);
                using (GraphicsPath gp = Round(new RectangleF(card.Right - 56, top + 13, 34, 9), 4.5f))
                    g.FillPath(b, gp);
            }
            using (Pen p = new Pen(Color.FromArgb(44, 255, 255, 255), 2.6f))
                g.DrawEllipse(p, card.Right - 80f, top + 10f, 20f, 20f);
        }

        void DrawShimmer(Graphics g, RectangleF area)
        {
            GraphicsState st = g.Save();
            using (GraphicsPath clip = Round(area, 14f))
            {
                g.SetClip(clip, CombineMode.Intersect);
                float band = 150f;
                float x = area.X - band + _shimmer * (area.Width + band * 2f);
                RectangleF bandRect = new RectangleF(x, area.Y, band, area.Height);
                using (LinearGradientBrush lg = new LinearGradientBrush(bandRect,
                    Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                {
                    ColorBlend cb = new ColorBlend(3);
                    cb.Colors = new Color[] {
                        Color.FromArgb(0, 255, 255, 255),
                        Color.FromArgb(16, 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255)
                    };
                    cb.Positions = new float[] { 0f, 0.5f, 1f };
                    lg.InterpolationColors = cb;
                    g.FillRectangle(lg, bandRect);
                }
            }
            g.Restore(st);
        }

        // ---------- sections / rows ----------
        void DrawSection(Graphics g, int y, string number, string title)
        {
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            using (Font fNum = F("Segoe UI", 10.5f, FontStyle.Bold))
            using (SolidBrush bNum = new SolidBrush(accent))
                DrawLeftText(g, number, fNum, bNum, new RectangleF(Pad, y - 4, 30, 22));

            using (Font fTitle = F("Microsoft YaHei UI", 10.5f, FontStyle.Bold))
            using (SolidBrush bTitle = new SolidBrush(GlassStyle.Ink))
                DrawLeftText(g, title, fTitle, bTitle, new RectangleF(Pad + 32, y - 4, 130, 22));

            using (Pen p = new Pen(Color.FromArgb(16, 255, 255, 255), 1f))
                g.DrawLine(p, Pad + 73, y + 6, ContentRight, y + 6);
        }

        void DrawAudioColumnDesign(Graphics g, string title, List<AudioDevices.Info> devices,
            string current, int x, int width, int y, List<RectangleF> rects, string idPrefix)
        {
            RectangleF header = new RectangleF(x, y, width, 20);
            using (GraphicsPath gp = Round(header, 10f))
            using (SolidBrush b = new SolidBrush(GlassStyle.Tile))
                g.FillPath(b, gp);
            using (Font fHead = F("Microsoft YaHei UI", 9.0f, FontStyle.Bold))
            using (SolidBrush ink = new SolidBrush(GlassStyle.Ink))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(title, fHead, ink, header, sf);
            }
            bool hasDefault = false;
            for (int i = 0; i < devices.Count; i++)
                if (string.Equals(devices[i].Name, current, StringComparison.Ordinal)) { hasDefault = true; break; }
            if (hasDefault && IsBuiltInAudio(current, idPrefix == "audioout"))
            {
                using (Font fDef = F("Microsoft YaHei UI", 6.8f, FontStyle.Bold))
                using (SolidBrush defInk = new SolidBrush(AppSettings.ThemeColor(_draft.Theme)))
                using (StringFormat defSf = new StringFormat())
                {
                    defSf.Alignment = StringAlignment.Center;
                    defSf.LineAlignment = StringAlignment.Center;
                    g.DrawString("默认", fDef, defInk,
                        new RectangleF(header.Right - 40, header.Y, 30, header.Height), defSf);
                }
            }

            int rowY = y + 23;
            if (devices.Count == 0)
            {
                using (Font f = F("Microsoft YaHei UI", 8.0f, FontStyle.Regular))
                using (SolidBrush muted = new SolidBrush(GlassStyle.Muted))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("无可用设备", f, muted,
                        new RectangleF(x, rowY, width, 20), sf);
                }
                return;
            }

            Color accent = AppSettings.ThemeColor(_draft.Theme);
            int count = Math.Min(devices.Count, 3);
            using (Font fItem = F("Microsoft YaHei UI", 9.0f, FontStyle.Regular))
            using (SolidBrush ink = new SolidBrush(GlassStyle.Ink))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                for (int i = 0; i < count; i++)
                {
                    RectangleF row = new RectangleF(x, rowY + i * 20, width, 19);
                    bool selected = string.Equals(devices[i].Name, current, StringComparison.Ordinal);
                    if (_hot == idPrefix + i)
                    {
                        Color hover = GlassStyle.LightMode
                            ? Color.FromArgb(24, 0, 0, 0) : Color.FromArgb(34, 255, 255, 255);
                        using (GraphicsPath hg = Round(row, 7f))
                        using (SolidBrush hb = new SolidBrush(hover))
                            g.FillPath(hb, hg);
                    }
                    g.DrawString(devices[i].Name, fItem, ink,
                        new RectangleF(row.X + 5, row.Y, row.Width - 18, row.Height), sf);
                    if (selected)
                    {
                        using (Pen check = new Pen(accent, 1.8f))
                        {
                            g.DrawLines(check, new PointF[] {
                                new PointF(row.Right - 18, row.Y + 10),
                                new PointF(row.Right - 14, row.Y + 14),
                                new PointF(row.Right - 6, row.Y + 5) });
                        }
                    }
                    rects.Add(row);
                }
            }
        }

        static List<AudioDevices.Info> SortBuiltInAudio(List<AudioDevices.Info> list, bool output)
        {
            List<AudioDevices.Info> builtIn = new List<AudioDevices.Info>();
            List<AudioDevices.Info> other = new List<AudioDevices.Info>();
            for (int i = 0; i < list.Count; i++)
                (IsBuiltInAudio(list[i].Name, output) ? builtIn : other).Add(list[i]);
            builtIn.AddRange(other);
            return builtIn;
        }

        static bool IsBuiltInAudio(string name, bool output)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (output)
                return n.Contains("扬声器") || n.Contains("speaker") || n.Contains("realtek");
            return n.Contains("麦克风") || n.Contains("microphone") || n.Contains("realtek");
        }

        void DrawRowText(Graphics g, string title, string desc, int rowY)
        {
            DrawRowTextAt(g, title, desc, Pad, rowY);
        }

        void DrawRowTextAt(Graphics g, string title, string desc, int x, int rowY)
        {
            using (Font fTitle = F("Microsoft YaHei UI", 9.0f, FontStyle.Bold))
            using (Font fDesc = F("Microsoft YaHei UI", 8.4f, FontStyle.Regular))
            using (SolidBrush bTitle = new SolidBrush(GlassStyle.Ink))
            using (SolidBrush bDesc = new SolidBrush(GlassStyle.Muted))
            {
                DrawLeftText(g, title, fTitle, bTitle, new RectangleF(x, rowY + 2, 220, 18));
                DrawLeftText(g, desc, fDesc, bDesc, new RectangleF(x, rowY + 18, 220, 18));
            }
        }

        void DrawRowLine(Graphics g, int y)
        {
            using (Pen p = new Pen(Color.FromArgb(12, 255, 255, 255), 1f))
                g.DrawLine(p, Pad, y, ContentRight, y);
        }

        int OpacityFromX(float x)
        {
            float ratio = (x - (_opacityTrack.X + 12f)) / Math.Max(1f, _opacityTrack.Width - 24f);
            ratio = Math.Max(0f, Math.Min(1f, ratio));
            return AppSettings.NormalizeOpacity((int)Math.Round(30f + ratio * 70f));
        }

        int OpacityTickAt(PointF p)
        {
            if (p.Y < _opacityTrack.Y + 18f || p.Y > _opacityTrack.Bottom) return -1;
            float railX = _opacityTrack.X + 2f;
            float railW = _opacityTrack.Width - 4f;
            for (int i = 0; i < 8; i++)
            {
                float tx = railX + 9f + (railW - 18f) * (i / 7f);
                if (Math.Abs(p.X - tx) <= 6f) return 30 + i * 10;
            }
            return -1;
        }

        void DrawOpacitySlider(Graphics g, RectangleF track)
        {
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            float opacity = _opacityDisplay / 100f;
            int alpha = (int)(110 + opacity * 145f); // 30% -> 153, 100% -> 255
            RectangleF rail = new RectangleF(track.X + 2f, track.Y + 11f, track.Width - 4f, 8f);
            using (GraphicsPath gp = Round(rail, 3f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, GlassStyle.Track)))
                g.FillPath(b, gp);

            float ratio = (_opacityDisplay - 30f) / 70f;
            float knobX = rail.X + 9f + (rail.Width - 18f) * ratio;
            RectangleF active = new RectangleF(rail.X, rail.Y, Math.Max(0f, knobX - rail.X), rail.Height);
            using (GraphicsPath gp = Round(active, 3f))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, accent)))
                g.FillPath(b, gp);

            for (int i = 0; i < 8; i++)
            {
                float tx = rail.X + 9f + (rail.Width - 18f) * (i / 7f);
                using (Pen p = new Pen(Color.FromArgb(210, GlassStyle.Muted), 3f))
                    g.DrawLine(p, tx, rail.Y + rail.Height + 1f, tx, rail.Y + rail.Height + 8f);
            }

            RectangleF knob = new RectangleF(knobX - 10f, track.Y + 5f, 20f, 20f);
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(Math.Min(70, alpha / 3), accent)))
                g.FillEllipse(glow, knob.X - 3f, knob.Y - 3f, knob.Width + 6f, knob.Height + 6f);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                g.FillEllipse(b, knob);
            using (Pen p = new Pen(Color.FromArgb(alpha, accent), 2f))
                g.DrawEllipse(p, knob);
        }

        void DrawSegmented(Graphics g, RectangleF track, string[] labels, int selected, float anim, string idPrefix)
        {
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            using (GraphicsPath gp = Round(track, 9f))
            using (SolidBrush b = new SolidBrush(GlassStyle.Tile))
                g.FillPath(b, gp);

            float segW = (track.Width - 4f) / labels.Length;
            float px = track.X + 2f + anim * segW;
            RectangleF pill = new RectangleF(px + 1f, track.Y + 3f, segW - 2f, track.Height - 6f);
            GlassStyle.DrawDropShadow(g, pill, 6f, 2, 12, 1f);
            using (GraphicsPath gp = Round(pill, 6f))
            using (LinearGradientBrush lg = new LinearGradientBrush(pill,
                Lighten(accent, 0.08f), Darken(accent, 0.08f), 90f))
                g.FillPath(lg, gp);

            using (Font f = F("Microsoft YaHei UI", 7.8f, FontStyle.Regular))
            using (Font fb = F("Microsoft YaHei UI", 7.8f, FontStyle.Bold))
            {
                for (int i = 0; i < labels.Length; i++)
                {
                    RectangleF cell = new RectangleF(track.X + 2f + i * segW, track.Y + 3f, segW, track.Height - 6f);
                    bool active = i == selected;
                    if (!active && _hot == idPrefix + i)
                    {
                        using (GraphicsPath gp = Round(cell, 6f))
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(31, 255, 255, 255)))
                            g.FillPath(b, gp);
                    }
                    using (SolidBrush b = new SolidBrush(active ? GlassStyle.OnAccent : GlassStyle.Muted))
                        DrawCentered(g, labels[i], active ? fb : f, b, cell);
                }
            }
        }

        void DrawFontSegmented(Graphics g, RectangleF track, string[] labels, string[] families, int selected, float anim, string idPrefix)
        {
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            using (GraphicsPath gp = Round(track, 9f))
            using (SolidBrush b = new SolidBrush(GlassStyle.Tile))
                g.FillPath(b, gp);

            float segW = (track.Width - 4f) / labels.Length;
            float px = track.X + 2f + anim * segW;
            RectangleF pill = new RectangleF(px + 1f, track.Y + 3f, segW - 2f, track.Height - 6f);
            GlassStyle.DrawDropShadow(g, pill, 6f, 2, 12, 1f);
            using (GraphicsPath gp = Round(pill, 6f))
            using (LinearGradientBrush lg = new LinearGradientBrush(pill,
                Lighten(accent, 0.08f), Darken(accent, 0.08f), 90f))
                g.FillPath(lg, gp);

            for (int i = 0; i < labels.Length; i++)
            {
                RectangleF cell = new RectangleF(track.X + 2f + i * segW, track.Y + 3f, segW, track.Height - 6f);
                bool active = i == selected;
                if (!active && _hot == idPrefix + i)
                {
                    using (GraphicsPath gp = Round(cell, 6f))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(31, 255, 255, 255)))
                        g.FillPath(b, gp);
                }
                using (Font f = F(families[i], 7.8f, active ? FontStyle.Bold : FontStyle.Regular))
                using (SolidBrush b = new SolidBrush(active ? GlassStyle.OnAccent : GlassStyle.Muted))
                    DrawCentered(g, labels[i], f, b, cell);
            }
        }

        void DrawThemeSwatches(Graphics g)
        {
            float cy = _appearanceRowsY + 176 + 22;
            for (int i = 0; i < 9; i++)
            {
                float cx = 258 + i * 22;
                bool selected = _draft.Theme == i;
                bool hot = _hot == "theme" + i;

                Color ringInk = GlassStyle.LightMode ? Color.FromArgb(72, 0, 0, 0) : Color.FromArgb(238, 255, 255, 255);
                if (selected)
                {
                    using (Pen p = new Pen(ringInk, 1.7f))
                        g.DrawEllipse(p, cx - 10.5f, cy - 10.5f, 21f, 21f);
                }
                else if (hot)
                {
                    using (Pen p = new Pen(GlassStyle.Hairline, 1.4f))
                        g.DrawEllipse(p, cx - 10f, cy - 10f, 20f, 20f);
                }

                RectangleF sw = new RectangleF(cx - 9f, cy - 9f, 18f, 18f);
                if (i == 8)
                {
                    using (GraphicsPath gp = Round(sw, 9f))
                    using (LinearGradientBrush lg = new LinearGradientBrush(sw, Color.Red, Color.Magenta, 45f))
                    {
                        ColorBlend cb = new ColorBlend(7);
                        cb.Colors = new Color[] {
                            Color.FromArgb(255, 92, 92), Color.FromArgb(255, 184, 46),
                            Color.FromArgb(52, 199, 89), Color.FromArgb(44, 188, 196),
                            Color.FromArgb(74, 158, 255), Color.FromArgb(126, 124, 255),
                            Color.FromArgb(255, 79, 163)
                        };
                        cb.Positions = new float[] { 0f, 0.17f, 0.34f, 0.50f, 0.67f, 0.84f, 1f };
                        lg.InterpolationColors = cb;
                        g.FillPath(lg, gp);
                    }
                    using (Pen p = new Pen(Color.FromArgb(150, 0, 0, 0), 1f))
                        g.DrawEllipse(p, sw);
                }
                else
                {
                    Color swatch = i == 6 ? Color.FromArgb(245, 245, 248)
                        : (i == 7 ? Color.FromArgb(16, 16, 20) : AppSettings.ThemeColor(i));
                    using (SolidBrush b = new SolidBrush(swatch))
                        g.FillEllipse(b, sw);
                    using (Pen p = new Pen(i == 6 ? Color.FromArgb(110, 0, 0, 0) : Color.FromArgb(70, 0, 0, 0), 1f))
                        g.DrawEllipse(p, sw);
                }

                if (selected)
                {
                    Color tick = i == 6 ? Color.FromArgb(35, 35, 40) : Color.White;
                    using (Pen p = new Pen(tick, 1.5f))
                    {
                        p.StartCap = LineCap.Round;
                        p.EndCap = LineCap.Round;
                        g.DrawLine(p, cx - 3.5f, cy, cx - 0.8f, cy + 2.8f);
                        g.DrawLine(p, cx - 0.8f, cy + 2.8f, cx + 4f, cy - 3f);
                    }
                }
            }
        }

        void DrawToggleRow(Graphics g, string title, string desc, int rowY, bool on, RectangleF toggle, string id)
        {
            DrawRowText(g, title, desc, rowY);
            DrawToggle(g, toggle, on, _hot == id);
        }

        void DrawToggle(Graphics g, RectangleF r, bool on, bool hot)
        {
            Color accent = AppSettings.ThemeColor(_draft.Theme);
            using (GraphicsPath gp = Round(r, r.Height / 2f))
            {
                Color fill = on ? (hot ? Lighten(accent, 0.08f) : accent) : GlassStyle.Tile;
                using (SolidBrush b = new SolidBrush(fill))
                    g.FillPath(b, gp);
                using (Pen p = new Pen(GlassStyle.Hairline, 1f))
                    g.DrawPath(p, gp);
            }
            float kd = 18f;
            float kx = on ? r.Right - kd - 2f : r.X + 2f;
            float ky = r.Y + 2f;
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
                g.FillEllipse(shadow, kx, ky + 1f, kd, kd);
            using (SolidBrush knob = new SolidBrush(on ? GlassStyle.OnAccent : GlassStyle.Ink))
                g.FillEllipse(knob, kx, ky, kd, kd);
        }

        // ---------- animation ----------
        void OnAnimTick()
        {
            bool moving = false;
            _animMaterial = Approach(_animMaterial, _animMaterialTarget, 0.34f, ref moving);
            _animSize = Approach(_animSize, _animSizeTarget, 0.34f, ref moving);
            _animPos = Approach(_animPos, _animPosTarget, 0.34f, ref moving);
            _animRadius = Approach(_animRadius, _animRadiusTarget, 0.34f, ref moving);
            _animFont = Approach(_animFont, _animFontTarget, 0.34f, ref moving);
            _animPanel = Approach(_animPanel, _animPanelTarget, 0.34f, ref moving);
            _animRefresh = Approach(_animRefresh, _animRefreshTarget, 0.34f, ref moving);
            if (_scanning)
            {
                _shimmer += 0.018f;
                if (_shimmer > 1f) _shimmer -= 1f;
                moving = true;
            }
            for (int i = 0; i < _previewDevices.Count; i++)
            {
                string key = _previewDevices[i].Name;
                float cur;
                if (!_previewAnimY.TryGetValue(key, out cur)) { _previewAnimY[key] = i; continue; }
                float next = Approach(cur, i, 0.34f, ref moving);
                if (Math.Abs(next - cur) > 0.001f) _previewAnimY[key] = next;
            }
            if (_previewDragIndex >= 0) moving = true;
            _opacityDisplay = Approach(_opacityDisplay, _opacityTarget, 0.28f, ref moving);
            if (_owner != null) _owner.SetOpacity((int)Math.Round(_opacityDisplay));
            if (Math.Abs(_opacityDisplay - _opacityTarget) < 0.15f)
            {
                _opacityDisplay = _opacityTarget;
                if (_opacitySavePending)
                {
                    _opacitySavePending = false;
                    ApplyLive();
                }
            }

            if (moving) Invalidate();
            else _animTimer.Stop();
        }

        static float Approach(float current, float target, float factor, ref bool moving)
        {
            float d = target - current;
            if (Math.Abs(d) < 0.002f)
            {
                if (d != 0f) moving = true;
                return target;
            }
            moving = true;
            return current + d * factor;
        }

        // ---------- interaction ----------
        bool IsWindowEdge(PointF p)
        {
            float w = Width / _scale, h = Height / _scale;
            return p.X <= 18f || p.X >= w - 18f || p.Y <= 18f || p.Y >= h - 18f;
        }

        PointF LogicalCursor()
        {
            Point sp = Cursor.Position;
            return new PointF((sp.X - Location.X) / _scale, (sp.Y - Location.Y) / _scale);
        }

        string HitTest(PointF p)
        {
            if (_closeRect.Contains(p)) return "close";
            for (int i = 0; i < _tabRects.Length; i++)
                if (_tabRects[i].Contains(p)) return "tab" + i;
            if (_modeToggle.Contains(p)) return "mode";
            if (_panelSegment.Contains(p))
            {
                float segW = (_panelSegment.Width - 4f) / 3f;
                int i = Math.Max(0, Math.Min(2, (int)((p.X - _panelSegment.X - 2f) / segW)));
                return "panel" + i;
            }
            if (_previewRangeToggle.Contains(p)) return "previewrange";
            if (_sortToggle.Contains(p)) return "devicesort";
            if (_opacityTrack.Contains(p)) return "opacity";
            if (_autoStartToggle.Contains(p)) return "autostart";
            if (_autoFlattenToggle.Contains(p)) return "autoflatten";
            if (_airPodsEarToggle.Contains(p)) return "airpods";
            if (_clickThroughToggle.Contains(p)) return "clickthrough";
            if (_taskbarToggle.Contains(p)) return "taskbar";
            for (int i = 0; i < _audioOutRects.Count; i++)
                if (_audioOutRects[i].Contains(p)) return "audioout" + i;
            for (int i = 0; i < _audioInRects.Count; i++)
                if (_audioInRects[i].Contains(p)) return "audioin" + i;
            if (_ignoreClearButton.Contains(p)) return "clearignore";
            if (_refreshMinutesTrack.Contains(p))
            {
                float segW = (_refreshMinutesTrack.Width - 4f) / 5f;
                int i = Math.Max(0, Math.Min(4, (int)((p.X - _refreshMinutesTrack.X - 2f) / segW)));
                return "refreshmin" + i;
            }
            if (_materialTrack.Contains(p))
            {
                float segW = (_materialTrack.Width - 4f) / 2f;
                int i = Math.Max(0, Math.Min(1, (int)((p.X - _materialTrack.X - 2f) / segW)));
                return "material" + i;
            }
            if (_sizeTrack.Contains(p))
            {
                float segW = (_sizeTrack.Width - 4f) / 3f;
                int i = Math.Max(0, Math.Min(2, (int)((p.X - _sizeTrack.X - 2f) / segW)));
                return "size" + i;
            }
            if (_radiusTrack.Contains(p))
            {
                float segW = (_radiusTrack.Width - 4f) / 4f;
                int i = Math.Max(0, Math.Min(3, (int)((p.X - _radiusTrack.X - 2f) / segW)));
                return "radius" + i;
            }
            if (_fontTrack.Contains(p))
            {
                float segW = (_fontTrack.Width - 4f) / 4f;
                int i = Math.Max(0, Math.Min(3, (int)((p.X - _fontTrack.X - 2f) / segW)));
                return "font" + i;
            }
            if (_positionTrack.Contains(p))
            {
                float segW = (_positionTrack.Width - 4f) / 6f;
                int i = Math.Max(0, Math.Min(5, (int)((p.X - _positionTrack.X - 2f) / segW)));
                return "pos" + i;
            }
            for (int i = 0; i < _themeRects.Length; i++) if (_themeRects[i].Contains(p)) return "theme" + i;
            return "";
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            PointF pt = LogicalCursor();
            if (_dragging)
            {
                Point sp = Cursor.Position;
                Location = new Point(sp.X - _dragOffset.X, sp.Y - _dragOffset.Y);
                Cursor = Cursors.Default;
                base.OnMouseMove(e);
                return;
            }
            if (_opacityPressed && (Control.MouseButtons & MouseButtons.Left) != 0)
            {
                if (!_opacityDragMoved &&
                    (Math.Abs(pt.X - _opacityPressPoint.X) > 3f || Math.Abs(pt.Y - _opacityPressPoint.Y) > 3f))
                    _opacityDragMoved = true;
                if (_opacityDragMoved)
                {
                    _opacityTarget = OpacityFromX(pt.X);
                    _opacityDisplay = _opacityTarget;
                    _draft.Opacity = _opacityTarget;
                    if (_owner != null) _owner.SetOpacity(_opacityTarget);
                    Invalidate();
                }
                base.OnMouseMove(e);
                return;
            }
            if (_previewDragIndex >= 0 && (Control.MouseButtons & MouseButtons.Left) != 0)
            {
                _previewDragY = pt.Y;
                Device moving = _previewDevices[_previewDragIndex];
                _previewDragIndex = ReorderPreviewGroups(moving, pt.Y);
                Cursor = Cursors.SizeAll;
                _animTimer.Start();
                Invalidate();
                base.OnMouseMove(e);
                return;
            }

            ulong previewRefresh = 0;
            int pi = PreviewIndexAt(pt);
            if (pi >= 0 && pi < _previewDevices.Count && _previewDevices[pi].ManualRefresh &&
                _previewDevices[pi].BtAddress != 0)
            {
                float top = _previewRect.Y + 30 + pi * 40 + (pi > 0 ? 4f : 0f);
                if (new RectangleF(_previewRect.Right - 62f, top + 4f, 54f, 32f).Contains(pt))
                    previewRefresh = _previewDevices[pi].BtAddress;
            }
            bool refreshChanged = previewRefresh != _hotPreviewRefresh;
            _hotPreviewRefresh = previewRefresh;

            string hot = HitTest(pt);
            if (previewRefresh != 0) hot = "previewrefresh";
            if (hot != _hot || refreshChanged)
            {
                _hot = hot;
                Invalidate();
            }

            if (hot.StartsWith("audioout") || hot.StartsWith("audioin"))
                Cursor = Cursors.Default;
            else if (hot.Length > 0) Cursor = Cursors.Hand;
            else Cursor = Cursors.Default;
            base.OnMouseMove(e);
        }

        void PickCustomColors()
        {
            if (_themeForm != null && !_themeForm.IsDisposed)
            {
                _themeForm.Activate();
                return;
            }
            _themeBefore = _draft.Clone();
            bool customApplied = _draft.Theme == 8;
            Color initialAccent = customApplied ? Color.FromArgb(_draft.CustomAccent) : AppSettings.ThemeColor(_draft.Theme);
            Color initialSecondary = customApplied ? Color.FromArgb(_draft.CustomSecondary) : AppSettings.ThemeSecondary(_draft.Theme);
            ColorPairForm f = new ColorPairForm(
                initialAccent,
                initialSecondary,
                AppSettings.ThemeColor(_draft.Theme),
                _draft.CustomHistory, _draft.CustomFavorites,
                _draft.CustomSecondaryFavorites, _draft.CustomFavoritePairs);
            _themeForm = f;
            f.PreviewChanged += delegate
            {
                if (f.IsDisposed) return;
                _draft.Theme = 8;
                _draft.CustomAccent = f.AccentColor.ToArgb();
                _draft.CustomSecondary = f.SecondaryColor.ToArgb();
                AppSettings.Activate(_draft);
                if (_owner != null) _owner.PreviewTheme(_draft);
                InvalidateSurface();
                Invalidate();
            };
            f.ApplyRequested += delegate
            {
                if (f.IsDisposed) return;
                _draft.Theme = 8;
                _draft.CustomAccent = f.AccentColor.ToArgb();
                _draft.CustomSecondary = f.SecondaryColor.ToArgb();
                _draft.CustomHistory = f.History;
                _draft.CustomFavorites = f.Favorites;
                _draft.CustomSecondaryFavorites = f.SecondaryFavorites;
                _draft.CustomFavoritePairs = f.FavoritePairs;
                AppSettings.ActiveCustomAccent = Color.FromArgb(_draft.CustomAccent);
                AppSettings.ActiveCustomSecondary = Color.FromArgb(_draft.CustomSecondary);
                ApplyLive();
                _themeBefore = _draft.Clone();
                InvalidateSurface();
                Invalidate();
            };
            f.FormClosed += delegate
            {
                ColorPairForm closed = f;
                _themeForm = null;
                if (_themeBefore != null) _draft = _themeBefore;
                _draft.CustomHistory = closed.History;
                _draft.CustomFavorites = closed.Favorites;
                _draft.CustomSecondaryFavorites = closed.SecondaryFavorites;
                _draft.CustomFavoritePairs = closed.FavoritePairs;
                _draft.Save();
                AppSettings.Activate(_draft);
                if (_owner != null) _owner.PreviewTheme(_draft);
                InvalidateSurface();
                Invalidate();
            };
            f.Show();
        }
        int PreviewIndexAt(PointF p)
        {
            if (!_previewRect.Contains(p)) return -1;
            float y = p.Y - (_previewRect.Y + 30);
            if (y < 0) return -1;
            int idx = (int)(y / 40f);
            if (idx < 0 || idx >= _previewDevices.Count || idx >= _previewRows) return -1;
            return idx;
        }

        void ShowDeviceMenu(Device d)
        {
            string key = string.IsNullOrEmpty(d.HistoryKey) ? Scanner.KeyFor(d.Name) : d.HistoryKey;
            ThemedContextMenu m = new ThemedContextMenu();
            if (d.Ignored)
            {
                m.Items.Add("恢复此设备", null, delegate
                {
                    if (_owner != null) _owner.RestoreIgnored(key);
                });
                MenuTheme.Apply(m, AppSettings.ThemeColor(_draft.Theme));
                m.Show(Cursor.Position);
                return;
            }
            m.Items.Add("忽略此设备", null, delegate
            {
                if (_owner != null) _owner.IgnoreDevice(key);
            });
            if (d.IsApple)
                m.Items.Add("播放/暂停媒体", null, delegate
                {
                    if (_owner != null) _owner.ToggleAirPodsMedia();
                });
            if (d.Offline)
                m.Items.Add("删除记录", null, delegate
                {
                    if (_owner != null) _owner.DeleteDeviceRecord(key);
                });
            MenuTheme.Apply(m, AppSettings.ThemeColor(_draft.Theme));
            m.Show(Cursor.Position);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            PointF pt = LogicalCursor();
            Focus();
            if (e.Button != MouseButtons.Left)
            {
                if (e.Button == MouseButtons.Right)
                {
                    int di = PreviewIndexAt(pt);
                    if (di >= 0) { ShowDeviceMenu(_previewDevices[di]); return; }
                }
                base.OnMouseDown(e);
                return;
            }

            if (_closeRect.Contains(pt)) { Close(); return; }
            for (int i = 0; i < _tabRects.Length; i++)
            {
                if (_tabRects[i].Contains(pt))
                {
                    if (_activeTab != i)
                    {
                        _activeTab = i;
                        _hot = "";
                        UpdatePreviewLayout();
                        LayoutControls();
                        _tabTimer.Start();
                        Invalidate();
                    }
                    return;
                }
            }

            if (_autoFlattenToggle.Contains(pt))
            {
                _draft.AutoFlattenCorners = !_draft.AutoFlattenCorners;
                ApplyLive();
                Invalidate();
                return;
            }

            if (_airPodsEarToggle.Contains(pt))
            {
                _draft.AirPodsEarControl = !_draft.AirPodsEarControl;
                ApplyLive();
                Invalidate();
                return;
            }
            if (_clickThroughToggle.Contains(pt))
            {
                _draft.ClickThrough = !_draft.ClickThrough;
                ApplyLive();
                Invalidate();
                return;
            }
            if (_taskbarToggle.Contains(pt))
            {
                _draft.TaskbarBadge = !_draft.TaskbarBadge;
                ApplyLive();
                Invalidate();
                return;
            }

            for (int i = 0; i < _audioOutRects.Count; i++)
                if (_audioOutRects[i].Contains(pt)) { SelectAudioDevice(true, i); return; }
            for (int i = 0; i < _audioInRects.Count; i++)
                if (_audioInRects[i].Contains(pt)) { SelectAudioDevice(false, i); return; }
            if (_ignoreClearButton.Contains(pt))
            {
                if (_owner != null) _owner.ClearIgnored();
                Invalidate();
                return;
            }
            for (int i = 0; i < _previewRefreshRects.Count && i < _previewRefreshDevices.Count; i++)
            {
                if (_previewRefreshRects[i].Contains(pt))
                {
                    if (_owner != null) _owner.RefreshHeadsetBattery(_previewRefreshDevices[i].BtAddress);
                    Invalidate();
                    return;
                }
            }

            for (int i = 0; i < _previewDeleteRects.Count && i < _previewDeleteDevices.Count; i++)
            {
                if (_previewDeleteRects[i].Contains(pt))
                {
                    if (_owner != null) _owner.DeleteDeviceRecord(_previewDeleteDevices[i].HistoryKey);
                    Invalidate();
                    return;
                }
            }

            int dragIndex = PreviewIndexAt(pt);
            if (_draft.AllowDeviceSort && dragIndex >= 0 && dragIndex < _previewDevices.Count &&
                !_previewDevices[dragIndex].Offline && !_previewDevices[dragIndex].IsAppleCase)
            {
                _previewDragIndex = dragIndex;
                _previewDragY = pt.Y;
                _previewDragOffset = pt.Y - (_previewRect.Y + 30 + dragIndex * 40);
                Capture = true;
                _animTimer.Start();
                Invalidate();
                return;
            }

            if (_modeToggle.Contains(pt)) { _draft.ShowMode = !_draft.ShowMode; ApplyLive(); Invalidate(); return; }
            if (_previewRangeToggle.Contains(pt))
            {
                _draft.ShowOffline = !_draft.ShowOffline;
                ApplyLive();
                OwnerPreviewChanged(this, EventArgs.Empty);
                Invalidate();
                return;
            }
            if (_sortToggle.Contains(pt)) { _draft.AllowDeviceSort = !_draft.AllowDeviceSort; ApplyLive(); Invalidate(); return; }
            if (_opacityTrack.Contains(pt))
            {
                _opacityPressed = true;
                _opacityDragMoved = false;
                _opacityPressTick = OpacityTickAt(pt);
                _opacityPressPoint = pt;
                Capture = true;
                return;
            }
            if (_refreshMinutesTrack.Contains(pt))
            {
                int[] vals = { 0, 10, 15, 30, 60 };
                float segW = (_refreshMinutesTrack.Width - 4f) / 5f;
                int i = Math.Max(0, Math.Min(4, (int)((pt.X - _refreshMinutesTrack.X - 2f) / segW)));
                _draft.HeadsetRefreshMinutes = vals[i];
                _animRefreshTarget = i;
                _animTimer.Start();
                ApplyLive();
                Invalidate();
                return;
            }

            if (_panelSegment.Contains(pt))
            {
                float segW = (_panelSegment.Width - 4f) / 3f;
                int i = Math.Max(0, Math.Min(2, (int)((pt.X - _panelSegment.X - 2f) / segW)));
                _animPanelTarget = i;
                _animTimer.Start();
                if (i == 0)
                {
                    _panelClosed = true;
                    _panelExpanded = false;
                    if (_owner != null) _owner.HidePanel();
                }
                else
                {
                    _panelClosed = false;
                    _panelExpanded = i == 2;
                    if (_owner != null)
                    {
                        _owner.ShowPanel();
                        _owner.SetPanelExpanded(_panelExpanded);
                    }
                }
                Invalidate();
                return;
            }
            if (_autoStartToggle.Contains(pt))
            {
                _autoStartDraft = !_autoStartDraft;
                ApplyLive();
                Invalidate();
                return;
            }

            if (_materialTrack.Contains(pt))
            {
                float segW = (_materialTrack.Width - 4f) / 2f;
                int i = Math.Max(0, Math.Min(1, (int)((pt.X - _materialTrack.X - 2f) / segW)));
                _draft.Material = (GlassMaterial)i;
                _animMaterialTarget = i;
                _animTimer.Start();
                InvalidateSurface();
                ApplyLive();
                Invalidate();
                return;
            }
            if (_sizeTrack.Contains(pt))
            {
                float segW = (_sizeTrack.Width - 4f) / 3f;
                int i = Math.Max(0, Math.Min(2, (int)((pt.X - _sizeTrack.X - 2f) / segW)));
                _draft.UiScale = i == 0 ? 1f : (i == 2 ? 1.2f : 1.1f);
                _animSizeTarget = i;
                _animTimer.Start();
                ApplyLive();
                Invalidate();
                return;
            }
            if (_radiusTrack.Contains(pt))
            {
                float segW = (_radiusTrack.Width - 4f) / 4f;
                int i = Math.Max(0, Math.Min(3, (int)((pt.X - _radiusTrack.X - 2f) / segW)));
                _draft.CornerRadius = AppSettings.RadiusValue(i);
                _animRadiusTarget = i;
                _animTimer.Start();
                ApplyLive();
                Invalidate();
                return;
            }
            if (_fontTrack.Contains(pt))
            {
                float segW = (_fontTrack.Width - 4f) / 4f;
                int i = Math.Max(0, Math.Min(3, (int)((pt.X - _fontTrack.X - 2f) / segW)));
                _draft.Font = UiFonts.Families[i];
                UiFonts.Family = _draft.Font;
                _animFontTarget = i;
                _animTimer.Start();
                ApplyLive();
                Invalidate();
                return;
            }
            if (_positionTrack.Contains(pt))
            {
                float segW = (_positionTrack.Width - 4f) / 6f;
                int i = Math.Max(0, Math.Min(5, (int)((pt.X - _positionTrack.X - 2f) / segW)));
                _draft.Position = AppSettings.PositionValue(i);
                _animPosTarget = i;
                _animTimer.Start();
                ApplyLive();
                Invalidate();
                return;
            }
            for (int i = 0; i < _themeRects.Length; i++)
            {
                if (_themeRects[i].Contains(pt))
                {
                    if (i == 8)
                    {
                        PickCustomColors();
                        return;
                    }
                    _draft.Theme = i;
                    InvalidateSurface();
                    ApplyLive();
                    if (_themeForm != null) _themeBefore = _draft.Clone();
                    Invalidate();
                    return;
                }
            }

            if (_headerDrag.Contains(pt) || IsWindowEdge(pt))
            {
                Point sp = Cursor.Position;
                _dragging = true;
                _dragOffset = new Point(sp.X - Location.X, sp.Y - Location.Y);
                Capture = true;
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            PointF pt = LogicalCursor();
            Capture = false;
            _dragging = false;
            if (_opacityPressed)
            {
                _opacityPressed = false;
                if (_opacityDragMoved)
                {
                    _opacityDragMoved = false;
                    _opacityDisplay = _opacityTarget;
                    _draft.Opacity = _opacityTarget;
                    if (_owner != null) _owner.SetOpacity(_opacityTarget);
                    ApplyLive();
                    Invalidate();
                }
                else
                {
                    _opacityTarget = _opacityPressTick >= 0 ? _opacityPressTick : OpacityFromX(pt.X);
                    _draft.Opacity = _opacityTarget;
                    _opacitySavePending = true;
                    _animTimer.Start();
                    Invalidate();
                }
                return;
            }
            if (_previewDragIndex >= 0)
            {
                List<string> names = new List<string>();
                for (int i = 0; i < _previewDevices.Count; i++)
                    if (!_previewDevices[i].Offline && !_previewDevices[i].IsAppleCase)
                        names.Add(_previewDevices[i].Name);
                _previewDragIndex = -1;
                SnapPreviewOrder();
                _animTimer.Start();
                if (_owner != null && names.Count > 0)
                    _owner.SetConnectedOrder(names);
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        bool HasChargingPreview()
        {
            for (int i = 0; i < _previewDevices.Count; i++)
                if (!_previewDevices[i].Offline && _previewDevices[i].Charging) return true;
            return false;
        }

        void ApplyLive()
        {
            AppSettings.Activate(_draft);
            if (_themeForm != null) _themeForm.RefreshThemeVisuals();
            if (_owner == null) return;
            if (!_owner.ApplySettings(_draft, _autoStartDraft))
            {
                // ApplySettings only fails when the startup shortcut cannot be changed.
                _autoStartDraft = !_autoStartDraft;
                Invalidate();
            }
        }

        // ---------- helpers ----------
        void ApplyPreviewFilter()
        {
            if (!_draft.ShowOffline)
                _previewDevices.RemoveAll(delegate(Device d) { return d.Offline; });
        }

        void SnapPreviewOrder()
        {
            _previewAnimY.Clear();
            for (int i = 0; i < _previewDevices.Count; i++)
                _previewAnimY[_previewDevices[i].Name] = i;
        }

        int ReorderPreviewGroups(Device dragged, float pointerY)
        {
            if (dragged == null) return _previewDragIndex;
            List<List<Device>> groups = new List<List<Device>>();
            HashSet<Device> used = new HashSet<Device>();
            for (int i = 0; i < _previewDevices.Count; i++)
            {
                Device d = _previewDevices[i];
                if (d.Offline || d.IsAppleCase || used.Contains(d)) continue;
                List<Device> group = new List<Device>();
                group.Add(d);
                used.Add(d);
                Device c = AttachedCase(d);
                if (c != null && !used.Contains(c))
                {
                    group.Add(c);
                    used.Add(c);
                }
                groups.Add(group);
            }

            int dragGroup = -1;
            for (int i = 0; i < groups.Count; i++)
                if (groups[i][0] == dragged) { dragGroup = i; break; }
            if (dragGroup < 0) return _previewDragIndex;

            int target = 0;
            float best = float.MaxValue;
            float baseY = _previewRect.Y + 30f;
            float lastCenter = baseY;
            int row = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                int count = groups[i].Count;
                float center = baseY + row * 40f + (count - 1) * 20f;
                float dist = Math.Abs(pointerY - center);
                if (dist < best) { best = dist; target = i; }
                lastCenter = center;
                row += count;
            }
            if (pointerY > lastCenter) target = groups.Count;

            List<Device> movingGroup = groups[dragGroup];
            groups.RemoveAt(dragGroup);
            if (dragGroup < target) target--;
            target = Math.Max(0, Math.Min(groups.Count, target));
            groups.Insert(target, movingGroup);

            List<Device> flat = new List<Device>();
            for (int i = 0; i < groups.Count; i++) flat.AddRange(groups[i]);
            for (int i = 0; i < _previewDevices.Count; i++)
            {
                Device d = _previewDevices[i];
                if ((d.Offline || d.IsAppleCase) && !used.Contains(d)) flat.Add(d);
            }
            _previewDevices = flat;
            return _previewDevices.IndexOf(dragged);
        }

        Device AttachedCase(Device parent)
        {
            if (parent == null || string.IsNullOrEmpty(parent.AppleModel)) return null;
            for (int i = 0; i < _previewDevices.Count; i++)
            {
                Device c = _previewDevices[i];
                if (c.IsAppleCase && c.AppleModel == parent.AppleModel) return c;
            }
            return null;
        }

        bool IsAttachedCase(Device parent, Device candidate)
        {
            return candidate != null && candidate.IsAppleCase && parent != null &&
                !string.IsNullOrEmpty(parent.AppleModel) && candidate.AppleModel == parent.AppleModel;
        }

        void AttachCaseToParent(Device parent)
        {
            if (parent == null || string.IsNullOrEmpty(parent.AppleModel)) return;
            int parentIndex = _previewDevices.IndexOf(parent);
            if (parentIndex < 0) return;
            for (int i = _previewDevices.Count - 1; i >= 0; i--)
            {
                Device c = _previewDevices[i];
                if (!c.IsAppleCase || c.AppleModel != parent.AppleModel || i == parentIndex + 1) continue;
                _previewDevices.RemoveAt(i);
                if (i < parentIndex) parentIndex--;
                _previewDevices.Insert(parentIndex + 1, c);
            }
            _previewDragIndex = parentIndex;
        }

        void OwnerPanelStateChanged(object sender, EventArgs e)
        {
            if (_owner == null || IsDisposed) return;
            _panelExpanded = !_owner.IsPanelCollapsed;
            Invalidate();
        }

        void OwnerPreviewChanged(object sender, EventArgs e)
        {
            if (_owner == null || IsDisposed) return;
            if (_previewDragIndex >= 0) return;
            List<Device> latest = _owner.GetPreviewDevices();
            _previewDevices.Clear();
            _previewDevices.AddRange(latest);
            ApplyPreviewFilter();
            SnapPreviewOrder();
            RefreshAudioLists();
            _scanning = _owner.IsScanning();
            if (_scanning) _animTimer.Start(); else _animTimer.Stop();
            if (HasChargingPreview()) _chargeTimer.Start(); else _chargeTimer.Stop();
            UpdatePreviewLayout();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_themeForm != null && !_themeForm.IsDisposed) { try { _themeForm.Close(); } catch { } _themeForm = null; }
            if (_owner != null) _owner.PanelStateChanged -= OwnerPanelStateChanged;
                if (_owner != null) _owner.PreviewChanged -= OwnerPreviewChanged;
                if (_animTimer != null) _animTimer.Dispose();
                if (_chargeTimer != null) _chargeTimer.Dispose();
                if (_tabTimer != null) _tabTimer.Dispose();
                if (_backdrop != null) { _backdrop.Dispose(); _backdrop = null; }
                if (_surface != null) { _surface.Dispose(); _surface = null; }
            }
            base.Dispose(disposing);
        }

        public void PrepareForCapture()
        {
            _captureMode = true;
            _animTimer.Stop();
            _chargeTimer.Stop();
        }

        public void CaptureToFile(string path)
        {
            _animTimer.Stop();
            _chargeTimer.Stop();
            Refresh();
            using (Bitmap bmp = new Bitmap(Width, Height))
            {
                DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                bmp.Save(path, ImageFormat.Png);
            }
            File.WriteAllText(path + ".txt",
                "scale=" + _scale.ToString(CultureInfo.InvariantCulture) + "\r\n"
                + "size=" + Width + "," + Height + "\r\n"
                + "settings=" + AppSettings.SettingsPath + "\r\n",
                new UTF8Encoding(false));
        }

        string PreviewStatus(Device d)
        {
            if (d.Offline)
                return "已断开";
            if (d.Ignored) return "已忽略 · 右键恢复";
            if (d.IsAppleCase)
                return (string.IsNullOrEmpty(d.AppleModel) ? "AirPods" : d.AppleModel) +
                    (d.Charging ? " · 充电中" : "");
            if (d.IsApple)
            {
                if (d.AppleLeftIn && d.AppleRightIn) return "蓝牙 · 双耳入耳";
                if (d.AppleLeftIn) return "蓝牙 · 左耳入耳";
                if (d.AppleRightIn) return "蓝牙 · 右耳入耳";
                return "蓝牙";
            }
            List<string> parts = new List<string>();
            if (_draft.ShowMode) parts.Add(ModeText(d.Mode));
            if (d.Cached) parts.Add("休眠中");
            if (d.Extra.Length > 0) parts.Add(d.Extra);
            if (d.Battery < 0 && d.Error.Length > 0) parts.Add(d.Error);
            if (parts.Count == 0) parts.Add(d.Battery >= 0 ? "设备在线" : "无读数");
            return string.Join(" · ", parts.ToArray());
        }

        static string ModeText(ConnMode mode)
        {
            if (mode == ConnMode.Wired) return "有线";
            if (mode == ConnMode.Receiver24G) return "2.4G";
            return "蓝牙";
        }

        static Color ModeColor(ConnMode mode)
        {
            if (mode == ConnMode.Wired) return Color.FromArgb(74, 158, 255);
            if (mode == ConnMode.Receiver24G) return Color.FromArgb(52, 199, 89);
            return Color.FromArgb(126, 124, 255);
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

        static Font F(string family, float pt, FontStyle style)
        {
            string use = family == "Microsoft YaHei UI" ? UiFonts.Family : family;
            float px = (pt + 1f) * 4f / 3f;
            if (use == "楷体") px += 1f;
            return UiFonts.Create(use, (float)Math.Round(px), style);
        }

        static Color Lighten(Color c, float amount)
        {
            return Color.FromArgb(c.A,
                (int)Math.Min(255, c.R + (255 - c.R) * amount),
                (int)Math.Min(255, c.G + (255 - c.G) * amount),
                (int)Math.Min(255, c.B + (255 - c.B) * amount));
        }

        static Color Darken(Color c, float amount)
        {
            return Color.FromArgb(c.A,
                (int)Math.Max(0, c.R * (1f - amount)),
                (int)Math.Max(0, c.G * (1f - amount)),
                (int)Math.Max(0, c.B * (1f - amount)));
        }

        // Keep two-line titles balanced instead of leaving a single orphan word.
        static string[] BalancedLines(Graphics g, string text, Font font, float maxWidth)
        {
            if (g.MeasureString(text, font, 1000, StringFormat.GenericTypographic).Width <= maxWidth)
                return new string[] { text };

            int best = -1;
            float bestDelta = float.MaxValue;
            for (int i = 1; i < text.Length; i++)
            {
                string a = text.Substring(0, i);
                string b = text.Substring(i);
                float wa = g.MeasureString(a, font, 1000, StringFormat.GenericTypographic).Width;
                float wb = g.MeasureString(b, font, 1000, StringFormat.GenericTypographic).Width;
                if (wa <= maxWidth && wb <= maxWidth)
                {
                    float delta = Math.Abs(wa - wb);
                    if (delta < bestDelta) { bestDelta = delta; best = i; }
                }
            }
            if (best < 1) return new string[] { text };
            return new string[] { text.Substring(0, best), text.Substring(best) };
        }

        static string Trim(Graphics g, string text, Font f, float maxWidth)
        {
            if (g.MeasureString(text, f, 1000, StringFormat.GenericTypographic).Width <= maxWidth) return text;
            string s = text;
            while (s.Length > 1 && g.MeasureString(s + "…", f, 1000, StringFormat.GenericTypographic).Width > maxWidth)
                s = s.Substring(0, s.Length - 1);
            return s + "…";
        }

        static float KaiShift
        {
            get { return UiFonts.Family == "楷体" ? 2f : 0f; }
        }

        static void DrawLeftText(Graphics g, string text, Font f, Brush b, RectangleF r)
        {
            r.Y += KaiShift;
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, f, b, r, sf);
            }
        }

        static void DrawCentered(Graphics g, string text, Font f, Brush b, RectangleF r)
        {
            r.Y += KaiShift;
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(text, f, b, r, sf);
            }
        }

        static void DrawGlyph(Graphics g, DevKind kind, RectangleF r)
        {
            Color ink = GlassStyle.Muted;
            using (Pen p = new Pen(ink, 1.2f))
            using (SolidBrush b = new SolidBrush(ink))
            {
                if (kind == DevKind.Mouse)
                {
                    using (GraphicsPath gp = Round(new RectangleF(r.X + 4.5f, r.Y + 1f, r.Width - 9f, r.Height - 2f), 6f))
                        g.DrawPath(p, gp);
                    g.DrawLine(p, r.X + r.Width / 2f, r.Y + 1.5f, r.X + r.Width / 2f, r.Y + 8f);
                    g.FillRectangle(b, r.X + r.Width / 2f - 0.7f, r.Y + 3.6f, 1.3f, 3.2f);
                }
                else if (kind == DevKind.Keyboard)
                {
                    using (GraphicsPath gp = Round(new RectangleF(r.X + 1f, r.Y + 5.5f, r.Width - 2f, r.Height - 11f), 2.4f))
                        g.DrawPath(p, gp);
                    for (int row = 0; row < 2; row++)
                        for (int col = 0; col < 4; col++)
                            g.FillRectangle(b, r.X + 3.7f + col * 3.9f, r.Y + 8.2f + row * 3.1f, 2.1f, 1.5f);
                }
                else if (kind == DevKind.Headset)
                {
                    g.DrawArc(p, r.X + 2f, r.Y + 1f, r.Width - 4f, r.Height - 4f, 180, 180);
                    using (GraphicsPath gp = Round(new RectangleF(r.X + 1.5f, r.Y + 11.5f, 4.2f, 8.6f), 2f))
                        g.DrawPath(p, gp);
                    using (GraphicsPath gp = Round(new RectangleF(r.Right - 5.7f, r.Y + 11.5f, 4.2f, 8.6f), 2f))
                        g.DrawPath(p, gp);
                }
                else
                {
                    g.DrawEllipse(p, r.X + 3f, r.Y + 3f, r.Width - 6f, r.Height - 6f);
                    g.FillEllipse(b, r.X + r.Width / 2f - 1.4f, r.Y + r.Height / 2f - 1.4f, 2.8f, 2.8f);
                }
            }
        }
    }
}
