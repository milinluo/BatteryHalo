using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace TriMode
{
    // ---- Win32 / HID / SetupAPI ----
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA { public int cbClass; public Guid ClassGuid; public int DevInst; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA { public int cbInterface; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }
        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY { public Guid fmtid; public int pid; }
        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }
        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage; public ushort UsagePage;
            public ushort InputReportByteLength; public ushort OutputReportByteLength; public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps; public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices; public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps; public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps; public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct OVERLAPPED2 { public IntPtr Internal; public IntPtr InternalHigh; public int Offset; public int OffsetHigh; public IntPtr hEvent; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BLUETOOTH_FIND_RADIO_PARAMS { public uint dwSize; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct BLUETOOTH_DEVICE_INFO
        {
            public uint dwSize;
            public ulong Address;
            public uint ulServiceClass;
            [MarshalAs(UnmanagedType.Bool)] public bool fConnected;
            [MarshalAs(UnmanagedType.Bool)] public bool fRemembered;
            [MarshalAs(UnmanagedType.Bool)] public bool fAuthenticated;
            public SYSTEMTIME stLastSeen;
            public SYSTEMTIME stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string szName;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public uint dwSize;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnAuthenticated;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnRemembered;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnUnknown;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnConnected;
            [MarshalAs(UnmanagedType.Bool)] public bool fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS p, out IntPtr hRadio);
        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern bool BluetoothFindNextRadio(IntPtr hFind, out IntPtr hRadio);
        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern bool BluetoothFindRadioClose(IntPtr hFind);
        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS p, ref BLUETOOTH_DEVICE_INFO info);
        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO info);
        [DllImport("bthprops.cpl", SetLastError = true)]
        public static extern bool BluetoothFindDeviceClose(IntPtr hFind);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr hwndParent, int flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr devInfo, IntPtr devInfoData, ref Guid classGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr devInfo, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int detailSize, out int required, IntPtr devInfoData);
        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(IntPtr devInfo, int index, ref SP_DEVINFO_DATA data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInstanceId(IntPtr devInfo, ref SP_DEVINFO_DATA data, StringBuilder id, int size, out int required);
        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiGetDevicePropertyW(IntPtr devInfo, ref SP_DEVINFO_DATA data, ref DEVPROPKEY key, out int type, byte[] buffer, int size, out int required, int flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr devInfo);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetAttributes(IntPtr h, ref HIDD_ATTRIBUTES a);
        [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool HidD_GetProductString(IntPtr h, StringBuilder s, int size);
        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(IntPtr h, out IntPtr data);
        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll", SetLastError = true)]
        public static extern int HidP_GetCaps(IntPtr data, ref HIDP_CAPS caps);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint create, uint flags, IntPtr tmpl);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(IntPtr h, byte[] buf, int count, out int written, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(IntPtr h, byte[] buf, int count, out int read, IntPtr ov);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateEvent(IntPtr sec, bool manual, bool initial, IntPtr name);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(IntPtr h, IntPtr ov, out int transferred, bool wait);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ResetEvent(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIo(IntPtr h);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 1;
        public const uint FILE_SHARE_WRITE = 2;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        public static Guid HID_GUID = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        public static Guid BT_CLASS_GUID = new Guid("e0cbf06c-cd8b-4647-bb8a-263b43f0f974");
        public static DEVPROPKEY PKEY_BT_BATTERY = new DEVPROPKEY { fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), pid = 2 };
        public static DEVPROPKEY PKEY_FRIENDLY_NAME = new DEVPROPKEY { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
    }

    // ---- one HID interface ----
    public class HidIface
    {
        public string Path;
        public ushort Vid, Pid;
        public string Product = "";
        public int InterfaceNumber = -1;
        public ushort Usage, UsagePage;
        public int InLen, OutLen, FeatureLen;
        public string InstanceId = "";
        public bool IsBluetooth;

        public bool HasVendorPage { get { return UsagePage >= 0xFF00; } }
        public string Tag { get { return UsagePage.ToString("X4") + ":" + Usage.ToString("X4"); } }
    }

    public class BtDevice
    {
        public string InstanceId;
        public string Name = "";
        public int Battery = -1;
        public bool Connected;
        public ulong Address;
    }

    public static class Hid
    {
        public static List<HidIface> Enumerate()
        {
            var list = new List<HidIface>();
            IntPtr set = Native.SetupDiGetClassDevs(ref Native.HID_GUID, IntPtr.Zero, IntPtr.Zero, 0x12);
            if (set == IntPtr.Zero) return list;
            int cbDetail = IntPtr.Size == 8 ? 8 : 6;
            try
            {
                for (int i = 0; ; i++)
                {
                    Native.SP_DEVICE_INTERFACE_DATA did = new Native.SP_DEVICE_INTERFACE_DATA();
                    did.cbInterface = Marshal.SizeOf(typeof(Native.SP_DEVICE_INTERFACE_DATA));
                    if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref Native.HID_GUID, i, ref did)) break;
                    IntPtr detail = Marshal.AllocHGlobal(cbDetail + 1024);
                    try
                    {
                        Marshal.WriteInt32(detail, cbDetail);
                        int req;
                        if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref did, detail, cbDetail + 1024, out req, IntPtr.Zero)) continue;
                        string path = Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
                        HidIface info = Inspect(path);
                        if (info != null) list.Add(info);
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                }
            }
            finally { Native.SetupDiDestroyDeviceInfoList(set); }
            return list;
        }

        static HidIface Inspect(string path)
        {
            IntPtr h = Native.CreateFile(path, Native.GENERIC_READ | Native.GENERIC_WRITE, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == IntPtr.Zero) h = Native.CreateFile(path, 0, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (h == IntPtr.Zero) return null;
            try
            {
                Native.HIDD_ATTRIBUTES attr = new Native.HIDD_ATTRIBUTES();
                attr.Size = Marshal.SizeOf(typeof(Native.HIDD_ATTRIBUTES));
                if (!Native.HidD_GetAttributes(h, ref attr)) return null;
                var info = new HidIface();
                info.Path = path;
                info.Vid = attr.VendorID;
                info.Pid = attr.ProductID;
                if (info.Vid == 0 && info.Pid == 0) return null;   // not a real HID node
                StringBuilder sb = new StringBuilder(512);
                if (Native.HidD_GetProductString(h, sb, 512)) info.Product = sb.ToString().Trim();
                IntPtr pp;
                if (Native.HidD_GetPreparsedData(h, out pp))
                {
                    Native.HIDP_CAPS caps = new Native.HIDP_CAPS();
                    caps.Reserved = new ushort[17];
                    if (Native.HidP_GetCaps(pp, ref caps) >= 0)
                    {
                        info.Usage = caps.Usage; info.UsagePage = caps.UsagePage;
                        info.InLen = caps.InputReportByteLength;
                        info.OutLen = caps.OutputReportByteLength;
                        info.FeatureLen = caps.FeatureReportByteLength;
                    }
                    Native.HidD_FreePreparsedData(pp);
                }
                string p = path.ToLowerInvariant();
                int mi = p.IndexOf("&mi_");
                if (mi > 0)
                {
                    try { info.InterfaceNumber = Convert.ToInt32(p.Substring(mi + 4, 2), 16); }
                    catch { info.InterfaceNumber = -1; }
                }
                else info.InterfaceNumber = 0;
                info.InstanceId = PathToInstanceId(path);
                info.IsBluetooth = p.Contains("bth");
                return info;
            }
            finally { Native.CloseHandle(h); }
        }

        static string PathToInstanceId(string path)
        {
            string p = path;
            if (p.StartsWith(@"\\?\")) p = p.Substring(4);
            int brace = p.IndexOf("#{");
            if (brace > 0) p = p.Substring(0, brace);
            string[] parts = p.Split('#');
            if (parts.Length < 3) return p;
            return parts[0].ToUpperInvariant() + @"\" + parts[1].ToUpperInvariant() + @"\" + string.Join("#", parts, 2, parts.Length - 2);
        }

        // report = payload including report id as byte 0
        public static List<byte> Exchange(string path, byte[] payload, int inLen, int readTimeoutMs, int attempts)
        {
            var reply = new List<byte>();
            IntPtr h = Native.CreateFile(path, Native.GENERIC_READ | Native.GENERIC_WRITE, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (h == IntPtr.Zero) return reply;
            IntPtr ev = Native.CreateEvent(IntPtr.Zero, true, false, IntPtr.Zero);
            IntPtr ov = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Native.OVERLAPPED2)));
            try
            {
                int bufLen = Math.Max(inLen, 65);
                for (int a = 0; a < attempts; a++)
                {
                    Marshal.StructureToPtr(new Native.OVERLAPPED2 { hEvent = ev }, ov, false);
                    Native.ResetEvent(ev);
                    int wrote;
                    Native.WriteFile(h, payload, payload.Length, out wrote, ov);
                    if (Native.WaitForSingleObject(ev, 250) != 0) { Native.CancelIo(h); continue; }
                    int got0;
                    Native.GetOverlappedResult(h, ov, out got0, false);

                    byte[] buf = new byte[bufLen];
                    for (int r = 0; r < 8; r++)
                    {
                        Marshal.StructureToPtr(new Native.OVERLAPPED2 { hEvent = ev }, ov, false);
                        Native.ResetEvent(ev);
                        int got;
                        Native.ReadFile(h, buf, bufLen, out got, ov);
                        if (Native.WaitForSingleObject(ev, (uint)readTimeoutMs) != 0) { Native.CancelIo(h); break; }
                        if (!Native.GetOverlappedResult(h, ov, out got, false) || got <= 0) continue;
                        bool allZero = true;
                        for (int i = 0; i < got; i++) { if (buf[i] != 0) { allZero = false; break; } }
                        if (allZero) continue;
                        reply.Clear();
                        for (int i = 0; i < got; i++) reply.Add(buf[i]);
                        return reply;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ov);
                Native.CloseHandle(ev);
                Native.CloseHandle(h);
            }
            return reply;
        }
    }

    public static class Bluetooth
    {
        static ulong AddressOf(string instance)
        {
            try
            {
                int i = instance.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);
                if (i < 0) return 0;
                i += 4;
                int end = instance.IndexOf('\\', i);
                string hex = end > i ? instance.Substring(i, end - i) : instance.Substring(i);
                return ulong.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            }
            catch { return 0; }
        }

        static HashSet<ulong> ConnectedAddresses()
        {
            HashSet<ulong> set = new HashSet<ulong>();
            IntPtr radio;
            Native.BLUETOOTH_FIND_RADIO_PARAMS rp = new Native.BLUETOOTH_FIND_RADIO_PARAMS();
            rp.dwSize = (uint)Marshal.SizeOf(typeof(Native.BLUETOOTH_FIND_RADIO_PARAMS));
            IntPtr hFind = Native.BluetoothFindFirstRadio(ref rp, out radio);
            if (hFind == IntPtr.Zero) return set;
            try
            {
                Native.BLUETOOTH_DEVICE_SEARCH_PARAMS sp = new Native.BLUETOOTH_DEVICE_SEARCH_PARAMS();
                sp.dwSize = (uint)Marshal.SizeOf(typeof(Native.BLUETOOTH_DEVICE_SEARCH_PARAMS));
                sp.fReturnConnected = true;
                sp.hRadio = radio;
                Native.BLUETOOTH_DEVICE_INFO di = new Native.BLUETOOTH_DEVICE_INFO();
                di.dwSize = (uint)Marshal.SizeOf(typeof(Native.BLUETOOTH_DEVICE_INFO));
                IntPtr dev = Native.BluetoothFindFirstDevice(ref sp, ref di);
                if (dev != IntPtr.Zero)
                {
                    try
                    {
                        do
                        {
                            if (di.fConnected) set.Add(di.Address);
                        }
                        while (Native.BluetoothFindNextDevice(dev, ref di));
                    }
                    finally { Native.BluetoothFindDeviceClose(dev); }
                }
            }
            finally
            {
                Native.CloseHandle(radio);
                Native.BluetoothFindRadioClose(hFind);
            }
            return set;
        }

        public static List<BtDevice> Enumerate()
        {
            var list = new List<BtDevice>();
            HashSet<ulong> connected = ConnectedAddresses();
            IntPtr set = Native.SetupDiGetClassDevs(ref Native.BT_CLASS_GUID, IntPtr.Zero, IntPtr.Zero, 0x02);
            if (set == IntPtr.Zero) return list;
            try
            {
                for (int i = 0; ; i++)
                {
                    Native.SP_DEVINFO_DATA did = new Native.SP_DEVINFO_DATA();
                    did.cbClass = Marshal.SizeOf(typeof(Native.SP_DEVINFO_DATA));
                    if (!Native.SetupDiEnumDeviceInfo(set, i, ref did)) break;
                    StringBuilder sb = new StringBuilder(512);
                    int req;
                    if (!Native.SetupDiGetDeviceInstanceId(set, ref did, sb, 512, out req)) continue;
                    string inst = sb.ToString();
                    // only the bluetooth device node itself, not per-service children
                    if (!(inst.StartsWith(@"BTHENUM\DEV_", StringComparison.OrdinalIgnoreCase)
                        || inst.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase))) continue;

                    var d = new BtDevice();
                    d.InstanceId = inst;
                    byte[] buf = new byte[256];
                    int type, need;
                    if (Native.SetupDiGetDevicePropertyW(set, ref did, ref Native.PKEY_FRIENDLY_NAME, out type, buf, buf.Length, out need, 0))
                        if (type == 0x12 && need > 0) d.Name = Encoding.Unicode.GetString(buf, 0, need).TrimEnd('\0').Trim();
                    if (Native.SetupDiGetDevicePropertyW(set, ref did, ref Native.PKEY_BT_BATTERY, out type, buf, buf.Length, out need, 0))
                    {
                        if (need >= 4) d.Battery = BitConverter.ToInt32(buf, 0);
                        else if (need >= 1) d.Battery = buf[0];
                    }
                    if (d.Name.Length == 0) continue;
                    d.Address = AddressOf(inst);
                    d.Connected = connected.Contains(d.Address);
                    list.Add(d);
                }
            }
            finally { Native.SetupDiDestroyDeviceInfoList(set); }
            return list;
        }
    }
}
