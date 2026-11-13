using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace TriModeAirPods
{
    // Small persistent helper: Windows PowerShell/.NET 4.0 cannot subscribe to
    // WinRT events, so this net48 helper owns the BLE watcher. The main app
    // reads one JSON line per state change from stdout.
    static class Program
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

        static readonly object Sync = new object();
        static string _lastJson = "";
        static bool _earControl;
        static bool _lastBothInEar;
        static bool _hasLastInEar;

        static void Main()
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try { Console.WriteLine("{\"ready\":true}"); Console.Out.Flush(); } catch { }

            Thread input = new Thread(ReadCommands);
            input.IsBackground = true;
            input.Start();

            BluetoothLEAdvertisementWatcher watcher = new BluetoothLEAdvertisementWatcher();
            watcher.ScanningMode = BluetoothLEScanningMode.Active;
            watcher.Received += OnReceived;
            watcher.Stopped += delegate(BluetoothLEAdvertisementWatcher s, BluetoothLEAdvertisementWatcherStoppedEventArgs e)
            {
                Emit("{\"error\":\"" + Escape(e.Error.ToString()) + "\"}");
            };
            watcher.Start();

            while (true) Thread.Sleep(1000);
        }

        static void ReadCommands()
        {
            try
            {
                string line;
                while ((line = Console.ReadLine()) != null)
                {
                    line = line.Trim().ToLowerInvariant();
                    if (line == "earcontrol=1" || line == "earcontrol=0")
                    {
                        lock (Sync) { _earControl = line.EndsWith("1"); }
                    }
                    else if (line == "playpause")
                    {
                        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, UIntPtr.Zero);
                        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 2, UIntPtr.Zero);
                    }
                    else if (line == "quit") Environment.Exit(0);
                }
            }
            catch { }
        }

        static void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            try
            {
                for (int i = 0; i < args.Advertisement.ManufacturerData.Count; i++)
                {
                    BluetoothLEManufacturerData section = args.Advertisement.ManufacturerData[i];
                    if (section.CompanyId != 76) continue;
                    byte[] bytes = ReadBuffer(section.Data);
                    if (!IsAirPodsPacket(bytes)) continue;
                    Parse(bytes, args.BluetoothAddress);
                    return;
                }
            }
            catch { }
        }

        static byte[] ReadBuffer(IBuffer buffer)
        {
            byte[] bytes = new byte[buffer.Length];
            DataReader reader = DataReader.FromBuffer(buffer);
            reader.ReadBytes(bytes);
            return bytes;
        }

        static bool IsAirPodsPacket(byte[] b)
        {
            return b != null && b.Length == 27 && b[0] == 0x07 && b[1] == 25;
        }

        static void Parse(byte[] b, ulong address)
        {
            int flags = b[5];
            int modelId = b[3] | (b[4] << 8);
            bool leftBroadcast = ((flags >> 5) & 1) == 1;
            bool currInEar = ((flags >> 1) & 1) != 0;
            bool bothInCase = ((flags >> 2) & 1) != 0;
            bool anotInEar = ((flags >> 3) & 1) != 0;

            int currLevel = b[6] & 0x0F;
            int anotLevel = (b[6] >> 4) & 0x0F;
            int caseLevel = b[7] & 0x0F;
            bool currCharging = (b[7] & 0x10) != 0;
            bool anotCharging = (b[7] & 0x20) != 0;
            bool caseCharging = (b[7] & 0x40) != 0;
            bool lidOpen = (b[8] & 0x08) == 0;

            int left = leftBroadcast ? currLevel : anotLevel;
            int right = leftBroadcast ? anotLevel : currLevel;
            bool leftCharging = leftBroadcast ? currCharging : anotCharging;
            bool rightCharging = leftBroadcast ? anotCharging : currCharging;
            bool leftInEar = !leftCharging && (leftBroadcast ? currInEar : anotInEar);
            bool rightInEar = !rightCharging && (leftBroadcast ? anotInEar : currInEar);

            if (left > 10) left = -1; else left *= 10;
            if (right > 10) right = -1; else right *= 10;
            if (caseLevel > 10) caseLevel = -1; else caseLevel *= 10;

            bool bothInEar = leftInEar && rightInEar;
            lock (Sync)
            {
                if (_earControl && _hasLastInEar && bothInEar != _lastBothInEar)
                {
                    keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 2, UIntPtr.Zero);
                }
                _lastBothInEar = bothInEar;
                _hasLastInEar = true;

                string json = "{\"model\":\"" + ModelName(modelId) + "\",\"modelId\":" + modelId +
                    ",\"left\":" + left + ",\"right\":" + right + ",\"case\":" + caseLevel +
                    ",\"leftIn\":" + (leftInEar ? "true" : "false") +
                    ",\"rightIn\":" + (rightInEar ? "true" : "false") +
                    ",\"leftCharging\":" + (leftCharging ? "true" : "false") +
                    ",\"rightCharging\":" + (rightCharging ? "true" : "false") +
                    ",\"caseCharging\":" + (caseCharging ? "true" : "false") +
                    ",\"lidOpen\":" + (lidOpen ? "true" : "false") +
                    ",\"bothInCase\":" + (bothInCase ? "true" : "false") +
                    ",\"address\":\"" + address.ToString("X12") + "\"}";
                if (json != _lastJson)
                {
                    _lastJson = json;
                    Emit(json);
                }
            }
        }

        static string ModelName(int id)
        {
            switch (id)
            {
                case 0x2002: return "AirPods 1";
                case 0x200F: return "AirPods 2";
                case 0x2013: return "AirPods 3";
                case 0x2019: return "AirPods 4";
                case 0x201B: return "AirPods 4 ANC";
                case 0x2030:
                case 0x2036: return "AirPods 5";
                case 0x200E: return "AirPods Pro";
                case 0x2014: return "AirPods Pro 2";
                case 0x2024: return "AirPods Pro 2 USB-C";
                case 0x2027: return "AirPods Pro 3";
                case 0x200A: return "AirPods Max";
                case 0x201F: return "AirPods Max USB-C";
                case 0x2012: return "Beats Fit Pro";
                default: return "Apple Audio";
            }
        }

        static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static void Emit(string json)
        {
            try { Console.WriteLine(json); Console.Out.Flush(); } catch { }
        }
    }
}
