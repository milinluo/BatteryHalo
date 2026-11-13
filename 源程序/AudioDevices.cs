using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TriMode
{
    static class AudioDevices
    {
        public class Info
        {
            public string Id;
            public string Name;
            public override string ToString() { return Name; }
        }

        const int FLOW_RENDER = 0;
        const int FLOW_CAPTURE = 1;
        const int STATE_ACTIVE = 1;
        const int ROLE_CONSOLE = 0, ROLE_MULTIMEDIA = 1, ROLE_COMMUNICATIONS = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct PROPERTYKEY
        {
            public Guid fmtid;
            public int pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct PROPVARIANT
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr pointerValue;
        }

        [DllImport("ole32.dll")]
        static extern int PropVariantClear(ref PROPVARIANT pv);

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            int GetDevice(string id, out IMMDevice device);
            int RegisterEndpointNotificationCallback(IntPtr client);
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            int GetCount(out int count);
            int Item(int index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IntPtr iface);
            int OpenPropertyStore(int access, out IPropertyStore properties);
            int GetId(out IntPtr id);
            int GetState(out int state);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            int GetCount(out int count);
            int GetAt(int index, out PROPERTYKEY key);
            int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
            int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
            int Commit();
        }

        [ComImport, Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
        class CPolicyConfigClient { }

        [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPolicyConfig
        {
            int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, out IntPtr format);
            int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, bool defaultFormat, out IntPtr format);
            int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName);
            int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr endpointFormat, IntPtr mixFormat);
            int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, bool defaultPeriod, out long defaultPeriodValue, out long minimumPeriod);
            int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceName, ref long period);
            int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, out IntPtr mode);
            int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr mode);
            int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr key, out IntPtr value);
            int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceName, IntPtr key, IntPtr value);
            int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceName, int role);
            int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceName, int visible);
        }

        static IMMDeviceEnumerator CreateEnumerator()
        {
            return (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        }

        public static List<Info> GetActive(int flow)
        {
            List<Info> result = new List<Info>();
            IMMDeviceEnumerator en = null;
            IMMDeviceCollection col = null;
            try
            {
                en = CreateEnumerator();
                if (en.EnumAudioEndpoints(flow, STATE_ACTIVE, out col) != 0 || col == null) return result;
                int count;
                if (col.GetCount(out count) != 0) return result;
                for (int i = 0; i < count; i++)
                {
                    IMMDevice dev;
                    if (col.Item(i, out dev) != 0 || dev == null) continue;
                    try
                    {
                        Info info = new Info();
                        IntPtr idPtr;
                        if (dev.GetId(out idPtr) == 0 && idPtr != IntPtr.Zero)
                            info.Id = Marshal.PtrToStringUni(idPtr);
                        info.Name = FriendlyName(dev);
                        if (string.IsNullOrEmpty(info.Name)) info.Name = info.Id;
                        if (!string.IsNullOrEmpty(info.Id)) result.Add(info);
                    }
                    finally { Marshal.ReleaseComObject(dev); }
                }
            }
            catch { }
            finally
            {
                if (col != null) Marshal.ReleaseComObject(col);
                if (en != null) Marshal.ReleaseComObject(en);
            }
            return result;
        }

        static string FriendlyName(IMMDevice dev)
        {
            IPropertyStore store = null;
            try
            {
                if (dev.OpenPropertyStore(0, out store) != 0 || store == null) return "";
                PROPERTYKEY key = new PROPERTYKEY();
                key.fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0");
                key.pid = 14;
                PROPVARIANT pv;
                if (store.GetValue(ref key, out pv) != 0) return "";
                try
                {
                    if (pv.vt == 31 && pv.pointerValue != IntPtr.Zero)
                        return Marshal.PtrToStringUni(pv.pointerValue);
                }
                finally { PropVariantClear(ref pv); }
            }
            catch { }
            finally { if (store != null) Marshal.ReleaseComObject(store); }
            return "";
        }

        public static string GetDefaultName(int flow)
        {
            IMMDeviceEnumerator en = null;
            IMMDevice dev = null;
            try
            {
                en = CreateEnumerator();
                if (en.GetDefaultAudioEndpoint(flow, ROLE_MULTIMEDIA, out dev) != 0 || dev == null) return "";
                string name = FriendlyName(dev);
                if (string.IsNullOrEmpty(name))
                {
                    IntPtr id;
                    if (dev.GetId(out id) == 0 && id != IntPtr.Zero) name = Marshal.PtrToStringUni(id);
                }
                return name;
            }
            catch { return ""; }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
                if (en != null) Marshal.ReleaseComObject(en);
            }
        }

        public static bool SetDefault(string deviceId, int flow)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;
            try
            {
                IPolicyConfig policy = (IPolicyConfig)new CPolicyConfigClient();
                bool ok = false;
                for (int role = ROLE_CONSOLE; role <= ROLE_COMMUNICATIONS; role++)
                {
                    try { if (policy.SetDefaultEndpoint(deviceId, role) == 0) ok = true; }
                    catch { }
                }
                Marshal.ReleaseComObject(policy);
                return ok;
            }
            catch { return false; }
        }

        public static int FlowRender { get { return FLOW_RENDER; } }
        public static int FlowCapture { get { return FLOW_CAPTURE; } }
    }
}
