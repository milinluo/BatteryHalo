using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TriMode
{
    public enum ConnMode { Wired, Receiver24G, Bluetooth }
    public enum DevKind { Mouse, Keyboard, Headset, Other }
    public enum Provider { None, AsusVendor, VgnVendor, BtProperty, MchoseVendor, StandardHid, LogitechHidpp, AtkVendor, AulaVendor, RazerVendor, CompxVendor }

    public class Device
    {
        public string Key;
        public string Name;
        public string RawName;
        public ConnMode Mode;
        public DevKind Kind = DevKind.Other;
        public Provider Provider = Provider.None;
        public int Battery = -1;
        public bool Charging;
        public int Voltage = -1;
        public string Error = "";
        public string Extra = "";
        public string CacheKey = "";
        public bool Cached;
        public bool LinkActive;
        public bool Offline;
        public bool ManualRefresh;
        public ulong BtAddress;
        public bool IsApple;
        public bool IsAppleCase;
        public bool Ignored;
        public string AppleModel = "";
        public int AppleLeft = -1, AppleRight = -1, AppleCase = -1;
        public bool AppleLeftIn, AppleRightIn, AppleLid, AppleBothInCase;
        public bool AppleLeftCharging, AppleRightCharging, AppleCaseCharging;
        public DateTime LastSeen;
        public string HistoryKey;
        public DateTime CachedAt;
        public string HidPath;
        public int OutLen;
        public int FeatureLen;
        public int HidppIndex;
        public int HidppFeature;
        public int HidppFeatureId;
        public string UsageTag;

        public string ModeText
        {
            get
            {
                if (Mode == ConnMode.Wired) return "有线";
                if (Mode == ConnMode.Receiver24G) return "2.4G";
                return "蓝牙";
            }
        }

        public string BatteryText { get { return Battery < 0 ? "—" : Battery.ToString() + "%"; } }

        public string SubText
        {
            get
            {
                if (Offline)
                    return "已断开";
                var sb = new StringBuilder();
                sb.Append(ModeText);
                if (Cached) sb.Append(" · 休眠中");
                if (Voltage > 0) sb.Append(" · ").Append((Voltage / 1000.0).ToString("0.00")).Append("V");
                if (Extra.Length > 0) sb.Append(" · ").Append(Extra);
                if (Battery < 0 && Error.Length > 0) sb.Append(" · ").Append(Error);
                return sb.ToString();
            }
        }
    }

    public class ScanResult
    {
        public List<Device> Devices = new List<Device>();
        public DateTime Stamp = DateTime.Now;
        public string Note = "";
    }

    public static class Scanner
    {
        class Known
        {
            public ushort Vid, Pid; public string Name; public DevKind Kind; public ConnMode Mode; public Provider Provider;
        }

        static readonly Known[] KnownTable = new Known[]
        {
            new Known { Vid = 0x0B05, Pid = 0x1AD0, Name = "ROG Harpe II Ace", Kind = DevKind.Mouse, Mode = ConnMode.Receiver24G, Provider = Provider.AsusVendor },
            new Known { Vid = 0x320F, Pid = 0x5088, Name = "VGN V98 Pro", Kind = DevKind.Keyboard, Mode = ConnMode.Receiver24G, Provider = Provider.VgnVendor },
            new Known { Vid = 0x320F, Pid = 0x5055, Name = "VGN V98 Pro", Kind = DevKind.Keyboard, Mode = ConnMode.Wired, Provider = Provider.VgnVendor },
        };

        static string Lower(string s) { return (s ?? "").ToLowerInvariant(); }

        static bool HasAny(string s, params string[] keys)
        {
            string l = Lower(s);
            for (int i = 0; i < keys.Length; i++) if (l.Contains(keys[i])) return true;
            return false;
        }

        static bool IsMchose(ushort vid, ushort pid)
        {
            if (vid == 0x5253) return true;
            if (vid == 0x3837)
                return pid == 0x100C || pid == 0x100B || pid == 0x100A || pid == 0x1001 || pid == 0x1012;
            return false;
        }

        static Known FindKnown(ushort vid, ushort pid)
        {
            for (int i = 0; i < KnownTable.Length; i++)
                if (KnownTable[i].Vid == vid && KnownTable[i].Pid == pid) return KnownTable[i];
            return null;
        }

        static DevKind GuessKind(string name)
        {
            if (HasAny(name, "mouse", "鼠标", "mice")) return DevKind.Mouse;
            if (HasAny(name, "keyboard", "键盘", "keychron")) return DevKind.Keyboard;
            if (HasAny(name, "headphone", "headset", "airpods", "buds", "wh-", "耳机")) return DevKind.Headset;
            if (HasAny(name, "harpe", "viper", "deathadder", "glorious", "superlight", "龙鳞", "月刃", "战刃")) return DevKind.Mouse;
            if (HasAny(name, "v98", "v87", "vgn", "keyboard", "k7", "k99", "k87", "k75", "gx87", "ace60", "jet75")) return DevKind.Keyboard;
            if (HasAny(name, "mchose", "迈从", "l7", "m7", "a7", "a5", "g3", "v9", "logitech", "razer", "viper", "deathadder", "aula", "atk", "g pro", "g502", "mx master")) return DevKind.Mouse;
            return DevKind.Other;
        }

        static string CleanName(string product)
        {
            string s = product.Trim();
            string[] junk = { " 8K RECEIVER", " RECEIVER", " 2.4G DONGLE", " 2.4G Dongle", " DONGLE", " RECEIVER ", " BT1", " BT2", " BT3", " BT4" };
            for (int i = 0; i < junk.Length; i++)
            {
                if (s.EndsWith(junk[i], StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - junk[i].Length).Trim();
            }
            if (s.Length == 0) s = product.Trim();
            return s;
        }

        static bool IsAppleName(string name)
        {
            return HasAny(name, "airpods", "beats");
        }

        static bool IsStorageName(string name)
        {
            return HasAny(name, "usb storage", "flash drive", "hard disk", "hard drive", "external drive",
                "portable ssd", "u盘", "硬盘", "移动硬盘", "存储设备");
        }

        static bool IsInputLike(string product)
        {
            return HasAny(product, "mouse", "鼠标", "keyboard", "键盘", "receiver", "dongle", "2.4g", "耳机", "headset", "headphone",
                "vgn", "v98", "v87", "atk", "madlions", "womier", "mchose", "迈从", "logitech", "razer", "aula", "罗技", "雷蛇", "狼蛛");
        }

        static bool HasCompx(List<HidIface> set)
        {
            for (int i = 0; i < set.Count; i++)
                if (set[i].UsagePage == 0xFF02 && set[i].Usage == 0x0002 && set[i].OutLen >= 17)
                    return true;
            return false;
        }

        static int StandardBattery(List<HidIface> set, out DevKind kind)
        {
            kind = DevKind.Other;
            for (int i = 0; i < set.Count; i++)
            {
                DevKind k;
                int level = Hid.ReadStandardBattery(set[i], out k);
                if (level >= 0)
                {
                    kind = k;
                    return level;
                }
            }
            return -1;
        }

        public static ScanResult Scan()
        {
            LoadCache();
            var res = new ScanResult();
            List<HidIface> hid = Hid.Enumerate();

            // A paired Bluetooth device can stay in PnP with a cached battery
            // property while it is not connected. Its HID interface only
            // exists while the link is live, so use that as the gate.
            List<string> btLinks = new List<string>();
            for (int i = 0; i < hid.Count; i++)
                if (hid[i].IsBluetooth && !string.IsNullOrEmpty(hid[i].Path))
                    btLinks.Add(hid[i].Path.ToLowerInvariant());

            // group HID interfaces by vid:pid
            var groups = new Dictionary<string, List<HidIface>>();
            for (int i = 0; i < hid.Count; i++)
            {
                HidIface f = hid[i];
                if (f.IsBluetooth) continue;
                string k = f.Vid.ToString("X4") + ":" + f.Pid.ToString("X4");
                if (!groups.ContainsKey(k)) groups.Add(k, new List<HidIface>());
                groups[k].Add(f);
            }

            foreach (KeyValuePair<string, List<HidIface>> kv in groups)
            {
                List<HidIface> set = kv.Value;
                ushort vid = set[0].Vid, pid = set[0].Pid;
                string product = "";
                for (int i = 0; i < set.Count; i++) if (set[i].Product.Length > product.Length) product = set[i].Product;

                if (IsStorageName(product)) continue;
                Known known = FindKnown(vid, pid);
                bool mchose = known == null && IsMchose(vid, pid);
                bool logi = known == null && vid == 0x046D;
                bool aula = known == null && vid == 0x0C45;
                bool razer = known == null && vid == 0x1532;
                bool atk = known == null && vid == 0x373B &&
                    (pid == 0x101B || pid == 0x1016 || pid == 0x111F || pid == 0x111E);
                bool compx = known == null && !atk && HasCompx(set);
                DevKind stdKind = DevKind.Other;
                int stdLevel = (known == null && !mchose && !logi && !atk && !aula && !razer && !compx) ? StandardBattery(set, out stdKind) : -1;
                if (known == null && !mchose && !logi && !atk && !aula && !razer && !compx && stdLevel < 0 && !IsInputLike(product)) continue;
                DevKind guessed = known != null ? known.Kind : GuessKind(product);
                if (stdLevel >= 0 && stdKind != DevKind.Other) guessed = stdKind;
                if (mchose && guessed == DevKind.Other) guessed = DevKind.Mouse;
                if (logi && guessed == DevKind.Other) guessed = DevKind.Mouse;
                if (atk) guessed = (pid == 0x111F || pid == 0x111E) ? DevKind.Keyboard : DevKind.Mouse;
                if (aula) guessed = DevKind.Keyboard;
                if (razer && guessed == DevKind.Other) guessed = DevKind.Mouse;
                if (compx && guessed == DevKind.Other) guessed = DevKind.Mouse;
                if (stdLevel >= 0 && guessed == DevKind.Other) guessed = DevKind.Mouse;
                // Only mouse / keyboard / headset classes are shown. This keeps
                // storage and miscellaneous HID collections out of the widget.
                if (guessed == DevKind.Other) continue;

                var d = new Device();
                d.Key = "HID:" + kv.Key;
                d.RawName = product;
                d.Name = known != null ? known.Name : CleanName(product);
                if (mchose && d.Name.Length == 0) d.Name = "MCHOSE";
                d.Kind = guessed;
                d.Mode = known != null ? known.Mode : ConnMode.Wired;
                d.Provider = known != null ? known.Provider : (mchose ? Provider.MchoseVendor : (logi ? Provider.LogitechHidpp : (atk ? Provider.AtkVendor : (aula ? Provider.AulaVendor : (razer ? Provider.RazerVendor : (compx ? Provider.CompxVendor : Provider.None))))));
                if (stdLevel >= 0)
                {
                    d.Provider = Provider.StandardHid;
                    d.Battery = stdLevel;
                    d.Error = "";
                }

                if (known == null && HasAny(product, "receiver", "dongle", "2.4g", "接收器")) d.Mode = ConnMode.Receiver24G;

                if (d.Provider == Provider.StandardHid)
                {
                    res.Devices.Add(d);
                    continue;
                }

                // choose the best channel for a proprietary read
                HidIface picked = PickChannel(set, d.Provider);
                if (picked != null)
                {
                    d.HidPath = picked.Path;
                    d.OutLen = picked.OutLen;
                    d.FeatureLen = picked.FeatureLen;
                    d.UsageTag = picked.Tag;
                    if (d.Provider == Provider.None) d.Provider = Provider.AsusVendor;
                    if (d.Provider == Provider.VgnVendor && d.Mode == ConnMode.Receiver24G)
                        ProbeVgnReceiver(d);
                    if (d.Provider == Provider.LogitechHidpp)
                        ProbeLogitech(d);
                }
                else if (d.Provider != Provider.None)
                {
                    d.Error = "未找到写入通道";
                }
                res.Devices.Add(d);
            }

            // bluetooth nodes
            List<BtDevice> bt = Bluetooth.Enumerate();
            for (int i = 0; i < bt.Count; i++)
            {
                BtDevice b = bt[i];
                if (IsStorageName(b.Name)) continue;
                DevKind kind = GuessKind(b.Name);
                if (!b.Connected) continue;   // paired but not currently connected
                // Only peripherals, not phones / drives / watches. Headsets
                // are included even without a battery property so a connected
                // headset is still visible.
                bool keep = kind == DevKind.Mouse || kind == DevKind.Keyboard || kind == DevKind.Headset;
                if (!keep) continue;

                string addr = BtAddress(b.InstanceId);
                bool linked = false;
                if (addr.Length > 0)
                    for (int h = 0; h < btLinks.Count; h++)
                        if (btLinks[h].IndexOf(addr.ToLowerInvariant(), StringComparison.Ordinal) >= 0)
                        { linked = true; break; }
                // Audio devices usually expose no HID interface; presence in
                // the Bluetooth device list is the best available signal.
                if (!linked && kind != DevKind.Headset) continue;

                var d = new Device();
                d.Key = "BT:" + b.InstanceId;
                d.RawName = b.Name;
                d.Name = CleanName(b.Name);
                d.Kind = kind;
                d.Mode = ConnMode.Bluetooth;
                d.Provider = Provider.BtProperty;
                d.BtAddress = b.Address;
                d.ManualRefresh = kind == DevKind.Headset && b.Battery < 0;
                d.Battery = b.Battery;
                if (b.Battery < 0) d.Error = "无电量属性";
                if (kind == DevKind.Headset)
                {
                    AirPodsBridge.Status ap = AirPodsBridge.Snapshot();
                    if (ap != null && IsAppleName(b.Name))
                    {
                        d.IsApple = true;
                        d.AppleModel = ap.Model;
                        d.AppleLeft = ap.Left;
                        d.AppleRight = ap.Right;
                        d.AppleCase = ap.Case;
                        d.AppleLeftIn = ap.LeftIn;
                        d.AppleRightIn = ap.RightIn;
                        d.AppleLid = ap.LidOpen;
                        d.AppleBothInCase = ap.BothInCase;
                        d.AppleLeftCharging = ap.LeftCharging;
                        d.AppleRightCharging = ap.RightCharging;
                        d.AppleCaseCharging = ap.CaseCharging;
                        int low = int.MaxValue;
                        if (ap.Left >= 0) low = Math.Min(low, ap.Left);
                        if (ap.Right >= 0) low = Math.Min(low, ap.Right);
                        if (ap.Case >= 0) low = Math.Min(low, ap.Case);
                        if (low != int.MaxValue) d.Battery = low;
                        d.Charging = ap.LeftCharging || ap.RightCharging || ap.CaseCharging;
                        d.ManualRefresh = false;
                        d.Error = "";
                    }
                }
                res.Devices.Add(d);
            }

            // merge the same physical device seen over several links (2.4G receiver + bluetooth),
            // keeping the signal that actually carries a battery reading
            List<Device> merged = new List<Device>();
            Dictionary<string, Device> byName = new Dictionary<string, Device>();
            for (int i = 0; i < res.Devices.Count; i++)
            {
                Device d = res.Devices[i];
                string key = NormKey(d.Name);
                Device kept;
                if (!byName.TryGetValue(key, out kept))
                {
                    byName.Add(key, d);
                    merged.Add(d);
                    continue;
                }
                Device winner = Prefer(kept, d);
                Device loser = winner == kept ? d : kept;
                if (winner != kept)
                {
                    merged[merged.IndexOf(kept)] = winner;
                    byName[key] = winner;
                }
                // keep the model-grade name (from the receiver side) rather than a bluetooth alias
                if (loser.Provider != Provider.BtProperty && winner.Provider == Provider.BtProperty)
                    winner.Name = loser.Name;
                bool loserLive = loser.Battery >= 0 ||
                    (!string.IsNullOrEmpty(loser.HidPath) &&
                     (loser.Mode != ConnMode.Receiver24G || loser.LinkActive));
                // When the receiver is active, its mode is the one to show;
                // don't append a confusing "蓝牙在线" alt-link tag.
                if (loser.Mode == ConnMode.Bluetooth && winner.Mode == ConnMode.Receiver24G)
                    loserLive = false;
                // The receiver may not expose a battery report. The paired
                // Bluetooth node for the same keyboard still carries the
                // last battery value; use it as a fallback, not as the mode.
                if (winner.Battery < 0 && loser.Battery >= 0)
                {
                    winner.Battery = loser.Battery;
                    winner.Voltage = loser.Voltage;
                    winner.Cached = loser.Cached;
                    winner.CachedAt = loser.CachedAt;
                }
                if (loserLive)
                {
                    string tag = loser.Mode == ConnMode.Receiver24G ? "接收器在线"
                               : loser.Mode == ConnMode.Wired ? "有线在线" : "蓝牙在线";
                    winner.Extra = winner.Extra.Length == 0 ? tag : winner.Extra + "，" + tag;
                }
            }
            res.Devices = merged;

            for (int i = 0; i < res.Devices.Count; i++) res.Devices[i].CacheKey = NormKey(res.Devices[i].Name);

            // stable order: mouse, keyboard, headset, other
            res.Devices.Sort(delegate (Device a, Device b)
            {
                int ka = (int)a.Kind, kb = (int)b.Kind;
                if (ka != kb) return ka.CompareTo(kb);
                int ma = a.Mode == ConnMode.Receiver24G ? 0 : (a.Mode == ConnMode.Wired ? 1 : 2);
                int mb = b.Mode == ConnMode.Receiver24G ? 0 : (b.Mode == ConnMode.Wired ? 1 : 2);
                if (ma != mb) return ma.CompareTo(mb);
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return res;
        }

        static string BtAddress(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return "";
            int i = instanceId.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "";
            string rest = instanceId.Substring(i + 4);
            int slash = rest.IndexOf('\\');
            if (slash >= 0) rest = rest.Substring(0, slash);
            return rest.Trim();
        }

        public static string KeyFor(string name)
        {
            return NormKey(name);
        }

        static string NormKey(string name)
        {
            string s = Lower(name).Replace(" ", "").Replace("-", "").Replace("_", "");
            string[] suffix = { "bt1", "bt2", "bt3", "bt4" };
            for (int i = 0; i < suffix.Length; i++)
                if (s.EndsWith(suffix[i])) s = s.Substring(0, s.Length - 3);
            return s;
        }

        static int Score(Device d)
        {
            // connection mode decides which link is "the current one": a plugged cable beats
            // the receiver, which beats a paired bluetooth link; a reading only breaks ties
            int s = 0;
            if (d.Mode == ConnMode.Wired) s += 300;
            else if (d.Mode == ConnMode.Receiver24G)
            {
                s += 200;
                // Only a receiver with a live keyboard link outranks Bluetooth.
                if (!string.IsNullOrEmpty(d.HidPath) && d.LinkActive) s += 600;
            }
            else s += 100;
            // A real battery reading is stronger evidence than a bare
            // transport type, but weaker than an active receiver channel.
            if (d.Battery >= 0) s += 500;
            if (d.Cached) s -= 50;
            return s;
        }

        static Device Prefer(Device a, Device b)
        {
            return Score(b) > Score(a) ? b : a;
        }

        static HidIface PickChannel(List<HidIface> set, Provider provider)
        {
            if (provider == Provider.LogitechHidpp)
            {
                for (int i = 0; i < set.Count; i++)
                    if (set[i].UsagePage == 0xFF00 && set[i].OutLen > 0)
                        return set[i];
            }
            if (provider == Provider.AtkVendor)
            {
                for (int i = 0; i < set.Count; i++)
                    if ((set[i].UsagePage == 0xFF02 || set[i].UsagePage == 0xFF60) && set[i].OutLen > 0)
                        return set[i];
            }
            if (provider == Provider.MchoseVendor)
            {
                for (int i = 0; i < set.Count; i++)
                    if (set[i].UsagePage == 0xFF01 && set[i].Usage == 0x0001 && set[i].FeatureLen >= 65)
                        return set[i];
            }
            for (int i = 0; i < set.Count; i++)
            {
                HidIface f = set[i];
                if (f.OutLen <= 0) continue;
                if (provider == Provider.VgnVendor && f.UsagePage == 0xFF1C && f.Usage == 0x0092) return f;
                if (provider == Provider.AsusVendor && f.UsagePage == 0xFF01 && f.Usage == 0x0001) return f;
            }
            // unknown vendor device: first writable vendor collection
            for (int i = 0; i < set.Count; i++)
            {
                HidIface f = set[i];
                if (f.OutLen > 0 && f.UsagePage >= 0xFF00) return f;
            }
            return null;
        }

        static readonly Dictionary<string, int> cacheBat = new Dictionary<string, int>();
        static readonly Dictionary<string, int> cacheVolt = new Dictionary<string, int>();
        static readonly Dictionary<string, DateTime> cacheTime = new Dictionary<string, DateTime>();
        static bool cacheLoaded;
        static DateTime lastSave = DateTime.MinValue;
        static string lastCacheText = "";

        static string CacheFile
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "上次读数.txt"); }
        }

        static void LoadCache()
        {
            if (cacheLoaded) return;
            cacheLoaded = true;
            try
            {
                if (!System.IO.File.Exists(CacheFile)) return;
                string[] lines = System.IO.File.ReadAllLines(CacheFile, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split('|');
                    if (parts.Length < 4) continue;
                    cacheBat[parts[0]] = int.Parse(parts[1]);
                    cacheVolt[parts[0]] = int.Parse(parts[2]);
                    cacheTime[parts[0]] = new DateTime(long.Parse(parts[3]));
                }
            }
            catch { }
        }

        static void SaveCache()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, int> kv in cacheBat)
                {
                    sb.Append(kv.Key).Append('|').Append(kv.Value).Append('|')
                      .Append(cacheVolt.ContainsKey(kv.Key) ? cacheVolt[kv.Key] : -1).Append('|')
                      .Append(cacheTime.ContainsKey(kv.Key) ? cacheTime[kv.Key].Ticks : 0).Append("\r\n");
                }
                string text = sb.ToString();
                if (text == lastCacheText) return;
                lastCacheText = text;
                System.IO.File.WriteAllText(CacheFile, text, Encoding.UTF8);
            }
            catch { }
        }

        static void Store(Device d)
        {
            if (d.CacheKey.Length == 0) return;
            int old = cacheBat.ContainsKey(d.CacheKey) ? cacheBat[d.CacheKey] : -2;
            cacheBat[d.CacheKey] = d.Battery;
            cacheVolt[d.CacheKey] = d.Voltage;
            cacheTime[d.CacheKey] = DateTime.Now;
            if (old != d.Battery || (DateTime.Now - lastSave).TotalSeconds > 30)
            {
                lastSave = DateTime.Now;
                SaveCache();
            }
        }

        static void Fail(Device d, string reason)
        {
            d.Error = reason;
            if (d.CacheKey.Length > 0 && cacheBat.ContainsKey(d.CacheKey) && cacheBat[d.CacheKey] >= 0)
            {
                d.Battery = cacheBat[d.CacheKey];
                d.Voltage = cacheVolt.ContainsKey(d.CacheKey) ? cacheVolt[d.CacheKey] : -1;
                d.Cached = true;
                d.CachedAt = cacheTime[d.CacheKey];
            }
        }

        static void ProbeVgnReceiver(Device d)
        {
            try
            {
                byte[] req = new byte[d.OutLen > 0 ? d.OutLen : 64];
                req[0] = 0x04; req[1] = 0x00; req[2] = 0x00; req[3] = 0x1A; req[4] = 0x06;
                List<byte> reply = Hid.Exchange(d.HidPath, req, 64, 650, 1);
                if (reply != null && reply.Count >= 9 && reply[0] == 0x04)
                {
                    d.LinkActive = reply[3] != 0xFF;
                    if (d.LinkActive)
                    {
                        d.Battery = reply[8];
                        d.Error = "";
                    }
                    else d.Error = "接收器未连接";
                }
                else
                {
                    d.LinkActive = false;
                    d.Error = "接收器未连接";
                }
            }
            catch
            {
                d.LinkActive = false;
                d.Error = "接收器未连接";
            }
        }

        static int HidppFeature(string path, int deviceIndex, ushort featureId)
        {
            for (int longReport = 0; longReport < 2; longReport++)
            {
                byte[] req = new byte[longReport == 0 ? 7 : 20];
                req[0] = (byte)(longReport == 0 ? 0x10 : 0x11);
                req[1] = (byte)deviceIndex;
                req[2] = 0x00;                       // root feature
                req[3] = 0x00;                       // getFeature
                req[4] = (byte)(featureId & 0xFF);
                req[5] = (byte)(featureId >> 8);
                List<byte> reply = Hid.Exchange(path, req, 32, 700, 1);
                if (reply != null && reply.Count >= 4 && reply[1] == (byte)deviceIndex)
                {
                    int featureIndex = reply[2];
                    if (featureIndex > 0) return featureIndex;
                }
            }
            return 0;
        }

        static void ProbeLogitech(Device d)
        {
            int[] indices = { 0xFF, 1, 2 };
            ushort[] featureIds = { 0x1001, 0x1000 };
            for (int f = 0; f < featureIds.Length; f++)
            {
                for (int i = 0; i < indices.Length; i++)
                {
                    int feature = HidppFeature(d.HidPath, indices[i], featureIds[f]);
                    if (feature <= 0) continue;
                    d.HidppIndex = indices[i];
                    d.HidppFeature = feature;
                    d.HidppFeatureId = featureIds[f];
                    ReadLogitech(d);
                    if (d.Battery >= 0) return;
                }
            }
            d.Error = "HID++ 无响应";
        }

        static void ReadLogitech(Device d)
        {
            if (string.IsNullOrEmpty(d.HidPath) || d.HidppFeature <= 0)
            {
                Fail(d, "HID++ 无响应");
                return;
            }
            byte[] req = new byte[7];
            req[0] = 0x10;
            req[1] = (byte)d.HidppIndex;
            req[2] = (byte)d.HidppFeature;
            req[3] = 0x01; // get level / voltage
            List<byte> reply = Hid.Exchange(d.HidPath, req, 32, 800, 2);
            if (reply == null || reply.Count < 5)
            {
                Fail(d, "HID++ 无响应");
                return;
            }
            if (d.HidppFeatureId == 0x1001)
            {
                d.Battery = reply[4] & 0x7F;
                d.Error = "";
                Store(d);
                return;
            }
            int mv = reply[4] | (reply[5] << 8);
            if (mv >= 2500 && mv <= 5000)
            {
                int percent = (int)Math.Round((mv - 3300) * 100.0 / 900.0);
                d.Battery = Math.Max(0, Math.Min(100, percent));
                d.Voltage = mv;
                d.Error = "";
                Store(d);
                return;
            }
            Fail(d, "HID++ 电量无效");
        }

        static void ReadCompx(Device d)
        {
            byte[] req = new byte[17];
            req[0] = 0x08; req[1] = 0x04;
            int sum = 0;
            for (int i = 0; i < 16; i++) sum += req[i];
            req[16] = (byte)(0x55 - (sum & 0xFF));
            List<byte> b = Hid.Exchange(d.HidPath, req, 64, 500, 2);
            if (b == null || b.Count < 17 || b[0] != 0x08 || b[1] != 0x04)
            {
                Fail(d, "Compx 无响应");
                return;
            }
            int check = 0;
            for (int i = 0; i < 16; i++) check += b[i];
            if (b[16] != (byte)(0x55 - (check & 0xFF)))
            {
                Fail(d, "Compx 校验失败");
                return;
            }
            d.Battery = b[6];
            d.Charging = b[7] != 0;
            d.Error = "";
            Store(d);
        }

        static void ReadRazer(Device d)
        {
            byte[] p = new byte[91];
            p[2] = 0x1F; p[6] = 0x02; p[7] = 0x07; p[8] = 0x80;
            byte crc = 0;
            for (int i = 3; i <= 88; i++) crc ^= p[i];
            p[89] = crc;
            byte[] r = Hid.FeatureZeroAccess(d.HidPath, p, 91);
            if (r == null || (r[1] != 0x01 && r[1] != 0x02))
            {
                Fail(d, "Razer 无响应");
                return;
            }
            int raw = r[10];
            d.Battery = (int)Math.Round(raw / 255.0 * 100);
            d.Error = "";
            Store(d);
        }

        static void ReadAula(Device d)
        {
            byte[] req = new byte[33];
            req[1] = 0xAA; req[2] = 0x10;
            List<byte> b = Hid.Exchange(d.HidPath, req, 64, 1500, 2);
            if (b == null || b.Count < 27)
            {
                Fail(d, "AULA 无响应");
                return;
            }
            int level = -1;
            if (b[0] == 0x55 && b[1] == 0x10) level = b[25];
            else if (b[1] == 0x55 && b[2] == 0x10) level = b[26];
            if (level < 1 || level > 100)
            {
                Fail(d, "AULA 无电量");
                return;
            }
            d.Battery = level;
            d.Charging = false;
            d.Error = "";
            Store(d);
        }

        static void ReadAtk(Device d)
        {
            if (d.Kind == DevKind.Keyboard)
            {
                byte[] status = new byte[33];
                status[1] = 0xAA; status[2] = 0xFD;
                List<byte> st = Hid.Exchange(d.HidPath, status, 64, 1000, 1);
                if (st == null || st.Count < 4 || st[1] != 0x55 || st[2] != 0xFD)
                {
                    Fail(d, "ATK 无响应");
                    return;
                }
                if (st[3] == 2) { Fail(d, "设备休眠"); return; }

                byte[] req = new byte[33];
                req[1] = 0x02; req[2] = 0x96; req[3] = 0x24;
                List<byte> r = Hid.Exchange(d.HidPath, req, 64, 1500, 1);
                if (r == null || r.Count < 6 || r[1] != 0x02 || r[2] != 0x96 || r[3] != 0x24)
                {
                    Fail(d, "ATK 电量读取失败");
                    return;
                }
                d.Charging = r[5] != 0;
                d.Battery = d.Charging ? -1 : r[4];
                d.Error = d.Charging ? "充电中" : "";
                if (!d.Charging) Store(d);
                return;
            }

            byte[] m = new byte[17];
            m[0] = 8; m[1] = 4;
            int sum = 0;
            for (int i = 0; i < 16; i++) sum += m[i];
            m[16] = (byte)(0x55 - (sum & 0xFF));
            List<byte> b = Hid.Exchange(d.HidPath, m, 64, 1500, 1);
            if (b == null || b.Count < 8 || b[0] != 8 || b[1] != 4 || b[2] != 0)
            {
                Fail(d, "ATK 无响应");
                return;
            }
            d.Battery = b[6];
            d.Charging = b[7] != 0;
            d.Error = "";
            Store(d);
        }

        static void ReadMchose(Device d)
        {
            int len = d.FeatureLen >= 65 ? d.FeatureLen : 65;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (attempt > 0) System.Threading.Thread.Sleep(100);
                byte[] req = new byte[len];
                for (int i = 0; i < req.Length; i++) req[i] = 0xFF;
                req[0] = 0x12;              // report id
                req[1] = 0x06 ^ 0xFF;       // status command, inverted
                byte[] r = Hid.Feature(d.HidPath, req, len);
                if (r == null || r.Length < 13 || r[0] != 0x12 || r[1] != 0xF9) continue;

                byte[] p = new byte[11];
                bool allZero = true;
                for (int i = 0; i < p.Length; i++)
                {
                    p[i] = (byte)(r[2 + i] ^ 0xFF);
                    if (p[i] != 0) allZero = false;
                }
                if (allZero)
                {
                    d.Error = "设备休眠";
                    return;
                }

                int level = p[9];
                if (level <= 100)
                {
                    d.Battery = level;
                    d.Charging = p[10] != 0;
                    d.Error = "";
                    Store(d);
                    return;
                }
            }
            Fail(d, "无响应或设备休眠");
        }

        public static void Read(Device d)
        {
            if (d.Provider == Provider.BtProperty) return;   // filled during scan
            if (d.Provider == Provider.StandardHid) return;  // filled during scan
            if (string.IsNullOrEmpty(d.HidPath)) { Fail(d, "未找到数据通道"); return; }

            if (d.Provider == Provider.LogitechHidpp)
            {
                ReadLogitech(d);
                return;
            }

            if (d.Provider == Provider.MchoseVendor)
            {
                ReadMchose(d);
                return;
            }

            byte[] req = new byte[d.OutLen > 0 ? d.OutLen : 64];
            List<byte> reply = null;

            if (d.Provider == Provider.VgnVendor)
            {
                req[0] = 0x04; req[1] = 0x00; req[2] = 0x00; req[3] = 0x1A; req[4] = 0x06;
                reply = Hid.Exchange(d.HidPath, req, 64, 2600, 1);
                if (reply != null && reply.Count >= 9)
                {
                    if (reply[3] == 0xFF) { Fail(d, "设备休眠"); return; }
                    if (reply[0] == 0x04)
                    {
                        d.Battery = reply[8];
                        d.Error = "";
                        Store(d);
                        return;
                    }
                }
                Fail(d, "无响应");
                return;
            }

            if (d.Provider == Provider.AsusVendor)
            {
                // new firmware (SpeedNova receiver): report id 3 carries "12 07"
                req[0] = 0x03; req[1] = 0x12; req[2] = 0x07;
                reply = Hid.Exchange(d.HidPath, req, 64, 900, 2);
                if (reply != null && reply.Count >= 8 && reply[0] == 0x03 && reply[1] == 0x12 && reply[2] == 0x07)
                {
                    d.Battery = reply[5];
                    if (reply.Count > 8) d.Voltage = reply[8] | (reply[9] << 8);
                    d.Charging = reply.Count > 10 && reply[10] != 0;
                    d.Error = "";
                    Store(d);
                    return;
                }
                // legacy wired mice: report id 0, echo "12 07"
                byte[] legacy = new byte[d.OutLen > 0 ? d.OutLen : 65];
                legacy[0] = 0x00; legacy[1] = 0x12; legacy[2] = 0x07;
                reply = Hid.Exchange(d.HidPath, legacy, 65, 900, 2);
                for (int i = 0; i + 1 < (reply == null ? 0 : reply.Count); i++)
                {
                    if (reply[i] == 0x12 && reply[i + 1] == 0x07)
                    {
                        int baseIdx = i + 2;
                        if (baseIdx + 2 < reply.Count) d.Battery = reply[baseIdx + 2];
                        if (baseIdx + 7 < reply.Count) d.Charging = reply[baseIdx + 7] != 0;
                        d.Error = "";
                        Store(d);
                        return;
                    }
                }
                Fail(d, "设备休眠");
            }
        }
    }
}
