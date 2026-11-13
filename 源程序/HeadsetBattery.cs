using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

namespace TriMode
{
    // On-demand classic headset battery read. Windows owns the HFP AT
    // channel while the device is connected, so briefly release it, perform
    // the handshake, then restore it. This is intentionally NOT called by the
    // background scanner: it may cause a 1-2 second audio interruption.
    static class HeadsetBattery
    {
        [DllImport("bthprops.cpl", SetLastError = true)]
        static extern int BluetoothGetDeviceInfo(IntPtr hRadio, ref Native.BLUETOOTH_DEVICE_INFO info);
        [DllImport("bthprops.cpl", SetLastError = true)]
        static extern int BluetoothSetServiceState(IntPtr hRadio, ref Native.BLUETOOTH_DEVICE_INFO info, ref Guid service, int flags);

        static readonly Guid Hfp = new Guid("0000111e-0000-1000-8000-00805f9b34fb");

        public static int ReadNow(ulong address)
        {
            if (address == 0) return -1;
            BluetoothAddress btAddr;
            try { btAddr = new BluetoothAddress(unchecked((long)address)); }
            catch { return -1; }

            int channel;
            try
            {
                BluetoothDeviceInfo bi = new BluetoothDeviceInfo(btAddr);
                ServiceRecord[] records = bi.GetServiceRecords(Hfp);
                if (records.Length == 0) return -1;
                channel = ServiceRecordHelper.GetRfcommChannelNumber(records[0]);
            }
            catch { return -1; }
            if (channel <= 0) return -1;

            Guid hfp = Hfp;
            IntPtr radio = IntPtr.Zero;
            IntPtr find = IntPtr.Zero;
            bool disabled = false;
            bool gotInfo = false;
            Native.BLUETOOTH_DEVICE_INFO dev = new Native.BLUETOOTH_DEVICE_INFO();
            try
            {
                Native.BLUETOOTH_FIND_RADIO_PARAMS rp = new Native.BLUETOOTH_FIND_RADIO_PARAMS();
                rp.dwSize = (uint)Marshal.SizeOf(typeof(Native.BLUETOOTH_FIND_RADIO_PARAMS));
                find = Native.BluetoothFindFirstRadio(ref rp, out radio);
                if (find != IntPtr.Zero && radio != IntPtr.Zero)
                {
                    dev.dwSize = (uint)Marshal.SizeOf(typeof(Native.BLUETOOTH_DEVICE_INFO));
                    dev.Address = address;
                    gotInfo = BluetoothGetDeviceInfo(radio, ref dev) == 0;
                    if (gotInfo)
                    {
                        disabled = BluetoothSetServiceState(radio, ref dev, ref hfp, 0) == 0;
                        Thread.Sleep(300);
                    }
                }

                using (BluetoothClient client = new BluetoothClient())
                {
                    client.Connect(new BluetoothEndPoint(btAddr, BluetoothService.Empty, channel));
                    using (NetworkStream stream = client.GetStream())
                    {
                        stream.ReadTimeout = 1200;
                        stream.WriteTimeout = 1000;
                        int pct = Handshake(stream);
                        if (pct >= 0) return pct;
                    }
                }
            }
            catch { }
            finally
            {
                if (disabled && gotInfo && radio != IntPtr.Zero)
                {
                    try { BluetoothSetServiceState(radio, ref dev, ref hfp, 1); } catch { }
                }
                if (radio != IntPtr.Zero) Native.CloseHandle(radio);
                if (find != IntPtr.Zero) Native.BluetoothFindRadioClose(find);
            }
            return -1;
        }

        static int Handshake(NetworkStream stream)
        {
            byte[] buffer = new byte[2048];
            StringBuilder pending = new StringBuilder();
            DateTime deadline = DateTime.Now.AddSeconds(8);
            while (DateTime.Now < deadline)
            {
                int n;
                try { n = stream.Read(buffer, 0, buffer.Length); }
                catch { break; }
                if (n <= 0) break;
                pending.Append(Encoding.ASCII.GetString(buffer, 0, n));

                int split;
                while ((split = pending.ToString().IndexOfAny(new char[] { '\r', '\n' })) >= 0)
                {
                    string line = pending.ToString(0, split).Trim();
                    pending.Remove(0, split + 1);
                    if (line.Length == 0) continue;
                    if (line.IndexOf("IPHONEACCEV", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int pct = ParseBattery(line);
                        if (pct >= 0) return pct;
                    }
                    Respond(stream, line);
                }
            }
            return -1;
        }

        static int ParseBattery(string line)
        {
            try
            {
                int eq = line.IndexOf('=');
                if (eq < 0) return -1;
                string[] parts = line.Substring(eq + 1).Split(',');
                if (parts.Length < 3) return -1;
                int level;
                if (!int.TryParse(parts[parts.Length - 1].Trim(), out level)) return -1;
                if (level == 10) return 100;
                if (level < 0 || level > 9) return -1;
                return Math.Min(100, (level + 1) * 10);
            }
            catch { return -1; }
        }

        static void Respond(NetworkStream stream, string line)
        {
            if (!line.StartsWith("AT+", StringComparison.OrdinalIgnoreCase)) return;
            string reply;
            if (line.StartsWith("AT+XAPL", StringComparison.OrdinalIgnoreCase))
                reply = "\r\n+XAPL=iPhone,7\r\n\r\nOK\r\n";
            else if (line.StartsWith("AT+BRSF", StringComparison.OrdinalIgnoreCase))
                reply = "\r\n+BRSF: 0\r\n\r\nOK\r\n";
            else if (line.StartsWith("AT+CIND=?", StringComparison.OrdinalIgnoreCase))
                reply = "\r\n+CIND: (\"service\",(0-1)),(\"call\",(0-1)),(\"callsetup\",(0-3)),(\"battchg\",(0-5)),(\"signal\",(0-5)),(\"roam\",(0-1))\r\n\r\nOK\r\n";
            else if (line.StartsWith("AT+CIND?", StringComparison.OrdinalIgnoreCase))
                reply = "\r\n+CIND: 1,0,0,5,5,0\r\n\r\nOK\r\n";
            else if (line.StartsWith("AT+CHLD=?", StringComparison.OrdinalIgnoreCase))
                reply = "\r\n+CHLD: (0,1,2)\r\n\r\nOK\r\n";
            else
                reply = "\r\nOK\r\n";
            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(reply);
                stream.Write(bytes, 0, bytes.Length);
            }
            catch { }
        }
    }
}
