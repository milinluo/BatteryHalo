using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TriMode
{
    // Child window of Shell_TrayWnd, placed immediately left of the
    // notification area. Content is drawn into a 32-bit ARGB layered
    // surface, so transparency and ClearType fringes do not occur.
    class TaskbarBadge : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int CX, CY; }
        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")]
        static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder text, int max);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll")]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dstDc, IntPtr dstPoint,
            ref SIZE size, IntPtr srcDc, ref POINT srcPoint, int colorKey,
            ref BLENDFUNCTION blend, int flags);
        delegate bool EnumChildProc(IntPtr hwnd, IntPtr lparam);
        [DllImport("user32.dll")]
        static extern bool EnumChildWindows(IntPtr parent, EnumChildProc proc, IntPtr lparam);

        readonly List<Device> _devices = new List<Device>();
        Color _accent = Color.FromArgb(74, 158, 255);
        int _desiredWidth = 104;
        IntPtr _parent;
        int _lastX, _lastY, _lastW, _lastH;
        bool _placed;
        string _signature = "";
        readonly Timer _placeTimer;

        public TaskbarBadge()
        {
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(140, 36);
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            DoubleBuffered = true;
            _placeTimer = new Timer();
            _placeTimer.Interval = 1000;
            _placeTimer.Tick += delegate { PlaceOnTaskbar(); };
            _placeTimer.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                return cp;
            }
        }

        public void SetDevices(List<Device> devices, Color accent)
        {
            List<Device> active = new List<Device>();
            if (devices != null)
                for (int i = 0; i < devices.Count && active.Count < 4; i++)
                    if (!devices[i].Offline && devices[i].Battery >= 0)
                        active.Add(devices[i]);

            int width = Math.Max(104, active.Count * 106 + 16);
            System.Text.StringBuilder sig = new System.Text.StringBuilder();
            sig.Append(accent.ToArgb()).Append('|').Append(width);
            for (int i = 0; i < active.Count; i++)
                sig.Append('|').Append(active[i].Name).Append(':')
                   .Append(active[i].Kind).Append(':').Append(active[i].Battery);
            string signature = sig.ToString();
            if (signature == _signature && _devices.Count == active.Count && Visible)
            {
                PlaceOnTaskbar();
                return;
            }

            _devices.Clear();
            _devices.AddRange(active);
            _accent = accent;
            _desiredWidth = width;
            _signature = signature;
            if (!Visible) Show();
            PlaceOnTaskbar();
            Invalidate();
        }

        public void PlaceOnTaskbar()
        {
            IntPtr shell = FindWindow("Shell_TrayWnd", null);
            if (shell == IntPtr.Zero) return;
            RECT taskbar;
            if (!GetWindowRect(shell, out taskbar)) return;

            bool parentChanged = _parent != shell;
            if (parentChanged)
            {
                SetParent(Handle, shell);
                int style = GetWindowLong(Handle, -16);
                style = (style & ~unchecked((int)0x80000000)) | 0x40000000 | 0x10000000;
                SetWindowLong(Handle, -16, style);
                _parent = shell;
                _placed = false;
            }

            float dpi = 1f;
            try { using (Graphics g = CreateGraphics()) dpi = g.DpiX / 96f; } catch { }
            if (dpi < 1f) dpi = 1f;

            int y = (taskbar.Bottom - taskbar.Top - 38) / 2;
            int pw = (int)Math.Round(_desiredWidth * dpi);
            int ph = (int)Math.Round(38 * dpi);

            if (!_placed || pw != _lastW || ph != _lastH)
            {
                SetWindowPos(Handle, IntPtr.Zero, 4, y, pw, ph,
                    0x0004 | 0x0010 | 0x0040);
                _lastW = pw; _lastH = ph;
                _placed = true;
            }

            RECT wr;
            if (!GetWindowRect(Handle, out wr)) return;
            int actualW = wr.Right - wr.Left;
            int x = taskbar.Right - taskbar.Left - actualW - 300;
            IntPtr tray = FindWindowEx(shell, IntPtr.Zero, "TrayNotifyWnd", null);
            if (tray != IntPtr.Zero)
            {
                RECT tr;
                if (GetWindowRect(tray, out tr))
                    x = tr.Left - taskbar.Left - actualW - 2;
            }
            if (x < 4) x = 4;
            if (x != _lastX || y != _lastY)
            {
                SetWindowPos(Handle, IntPtr.Zero, x, y, 0, 0,
                    0x0001 | 0x0004 | 0x0010);
                _lastX = x; _lastY = y;
            }
        }

        void RenderLayered()
        {
            RECT cr;
            if (!GetClientRect(Handle, out cr)) return;
            int cw = cr.Right, ch = cr.Bottom;
            if (cw <= 0 || ch <= 0) return;

            using (Bitmap bmp = new Bitmap(cw, ch, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    DrawContent(g, cw, ch);
                }
                IntPtr screen = GetDC(IntPtr.Zero);
                IntPtr mem = CreateCompatibleDC(screen);
                IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0));
                IntPtr old = SelectObject(mem, hbmp);
                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = 1;
                SIZE size = new SIZE();
                size.CX = cw; size.CY = ch;
                POINT src = new POINT();
                UpdateLayeredWindow(Handle, screen, IntPtr.Zero, ref size, mem,
                    ref src, 0, ref blend, 2);
                SelectObject(mem, old);
                DeleteObject(hbmp);
                DeleteDC(mem);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            RECT cr; GetClientRect(Handle, out cr);
            DrawContent(e.Graphics, cr.Right, cr.Bottom);
        }

        void DrawContent(Graphics g, int cw, int ch)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_devices.Count == 0) return;

            Color ink = _accent;
            float brightness = (ink.R * 0.299f + ink.G * 0.587f + ink.B * 0.114f);
            if (brightness < 96f) ink = Color.FromArgb(240, 240, 246);
            using (Font f = new Font("Segoe UI Variable Text", 20.0f, FontStyle.Bold))
            using (SolidBrush text = new SolidBrush(ink))
            {
                float right = cw - 6f;
                for (int i = _devices.Count - 1; i >= 0; i--)
                {
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        string value = _devices[i].Battery + "%";
                        path.AddString(value, f.FontFamily, (int)FontStyle.Bold, f.Size,
                            new PointF(0f, 0f), StringFormat.GenericTypographic);
                        RectangleF b = path.GetBounds();
                        float tx = right - b.Width - b.X;
                        float ty = (ch - b.Height) / 2f - b.Y - 8f;
                        using (Matrix m = new Matrix())
                        {
                            m.Translate(tx, ty);
                            path.Transform(m);
                        }
                        float iconRight = tx - 5f;
                        DrawGlyph(g, _devices[i].Kind,
                            new RectangleF(iconRight - 28f, (ch - 28f) / 2f - 6f, 28f, 28f), ink);
                        g.FillPath(text, path);
                    }
                    right -= 86f;
                }
            }
        }

        static void DrawGlyph(Graphics g, DevKind kind, RectangleF r, Color ink)
        {
            using (Pen p = new Pen(ink, 2.0f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                if (kind == DevKind.Headset)
                {
                    g.DrawArc(p, r.X + 2f, r.Y + 1f, r.Width - 4f, r.Height - 6f, 200f, 140f);
                    g.DrawArc(p, r.X + 3f, r.Y + 8f, 6f, 11f, 90f, 180f);
                    g.DrawArc(p, r.Right - 9f, r.Y + 8f, 6f, 11f, 270f, 180f);
                }
                else if (kind == DevKind.Mouse)
                {
                    using (GraphicsPath gp = Round(new RectangleF(r.X + 7f, r.Y + 2f, 14f, 23f), 6.5f))
                        g.DrawPath(p, gp);
                    g.DrawLine(p, r.X + 14f, r.Y + 5f, r.X + 14f, r.Y + 11f);
                }
                else if (kind == DevKind.Keyboard)
                {
                    using (GraphicsPath gp = Round(new RectangleF(r.X + 1f, r.Y + 7f, 26f, 14f), 2.8f))
                        g.DrawPath(p, gp);
                    g.DrawLine(p, r.X + 5f, r.Y + 12f, r.Right - 4f, r.Y + 12f);
                    g.DrawLine(p, r.X + 5f, r.Y + 17f, r.Right - 4f, r.Y + 17f);
                }
                else
                {
                    using (GraphicsPath gp = Round(new RectangleF(r.X + 6f, r.Y + 7f, 16f, 14f), 2.8f))
                        g.DrawPath(p, gp);
                    g.DrawLine(p, r.Right - 4f, r.Y + 11f, r.Right - 4f, r.Y + 16f);
                }
            }
        }

        public void DestroyNow()
        {
            if (_placeTimer != null) _placeTimer.Stop();
            if (Handle != IntPtr.Zero)
            {
                SetParent(Handle, IntPtr.Zero);
                DestroyWindow(Handle);
            }
        }

        public static void CleanupStale()
        {
            IntPtr shell = FindWindow("Shell_TrayWnd", null);
            if (shell == IntPtr.Zero) return;
            uint current = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumChildWindows(shell, delegate(IntPtr h, IntPtr l)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
                GetWindowText(h, sb, 64);
                if (sb.ToString() != "电环电量") return true;
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid != current)
                {
                    SetParent(h, IntPtr.Zero);
                    DestroyWindow(h);
                }
                return true;
            }, IntPtr.Zero);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _placeTimer != null) _placeTimer.Dispose();
            base.Dispose(disposing);
        }

        static GraphicsPath Round(RectangleF r, float rad)
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
    }
}
