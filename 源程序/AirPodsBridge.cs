using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace TriMode
{
    // Bridges the net48 WinRT BLE helper (AirPodsHelper.exe) to this .NET 4.0 app.
    static class AirPodsBridge
    {
        public class Status
        {
            public string Model = "";
            public string Address = "";
            public int Left = -1, Right = -1, Case = -1;
            public bool LeftIn, RightIn, LidOpen, BothInCase;
            public bool LeftCharging, RightCharging, CaseCharging;
            public DateTime Updated = DateTime.MinValue;
        }

        static readonly object Sync = new object();
        static Process _process;
        static Status _last;
        static bool _started;

        public static void Start()
        {
            lock (Sync)
            {
                if (_started) return;
                _started = true;
                try
                {
                    string file = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AirPodsHelper.exe");
                    if (!System.IO.File.Exists(file)) return;
                    ProcessStartInfo psi = new ProcessStartInfo(file);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardError = true;
                    psi.StandardOutputEncoding = Encoding.UTF8;
                    _process = Process.Start(psi);
                    if (_process == null) return;
                    Thread t = new Thread(ReadLoop);
                    t.IsBackground = true;
                    t.Start(_process);
                }
                catch { _process = null; }
            }
        }

        public static void Stop()
        {
            Process p;
            lock (Sync) { p = _process; _process = null; }
            try { if (p != null && !p.HasExited) p.Kill(); } catch { }
            try { if (p != null) p.Dispose(); } catch { }
        }

        static void ReadLoop(object state)
        {
            Process p = (Process)state;
            try
            {
                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    if (line.IndexOf("\"ready\"", StringComparison.Ordinal) >= 0) continue;
                    Status s = Parse(line);
                    if (s == null) continue;
                    lock (Sync) _last = s;
                }
            }
            catch { }
            lock (Sync)
            {
                if (_process == p) { _process = null; _last = null; }
            }
        }

        static Status Parse(string json)
        {
            try
            {
                Status s = new Status();
                s.Model = Str(json, "model");
                s.Address = Str(json, "address");
                s.Left = Int(json, "left");
                s.Right = Int(json, "right");
                s.Case = Int(json, "case");
                s.LeftIn = Bool(json, "leftIn");
                s.RightIn = Bool(json, "rightIn");
                s.LidOpen = Bool(json, "lidOpen");
                s.BothInCase = Bool(json, "bothInCase");
                s.LeftCharging = Bool(json, "leftCharging");
                s.RightCharging = Bool(json, "rightCharging");
                s.CaseCharging = Bool(json, "caseCharging");
                s.Updated = DateTime.Now;
                return s.Model.Length == 0 ? null : s;
            }
            catch { return null; }
        }

        static int Int(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\":(-?\\d+)");
            int v;
            return m.Success && int.TryParse(m.Groups[1].Value, out v) ? v : -1;
        }

        static bool Bool(string json, string key)
        {
            return Regex.IsMatch(json, "\"" + key + "\":true");
        }

        static string Str(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\":\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : "";
        }

        public static Status Snapshot()
        {
            lock (Sync)
            {
                if (_last == null) return null;
                Status s = new Status();
                s.Model = _last.Model; s.Address = _last.Address;
                s.Left = _last.Left; s.Right = _last.Right; s.Case = _last.Case;
                s.LeftIn = _last.LeftIn; s.RightIn = _last.RightIn;
                s.LidOpen = _last.LidOpen; s.BothInCase = _last.BothInCase;
                s.LeftCharging = _last.LeftCharging; s.RightCharging = _last.RightCharging;
                s.CaseCharging = _last.CaseCharging; s.Updated = _last.Updated;
                return s;
            }
        }

        public static void Send(string command)
        {
            try
            {
                Process p;
                lock (Sync) p = _process;
                if (p == null || p.HasExited) return;
                p.StandardInput.WriteLine(command);
                p.StandardInput.Flush();
            }
            catch { }
        }

        public static void SetEarControl(bool on)
        {
            Send(on ? "earcontrol=1" : "earcontrol=0");
        }

        public static void ToggleMedia()
        {
            Send("playpause");
        }
    }
}
