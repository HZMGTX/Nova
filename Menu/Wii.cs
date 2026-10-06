using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Poison.Menu
{
    internal sealed class Wii : IDisposable
    {
        internal struct State
        {
            public bool connected;
            public int buttons, pressed, points, session;
            public float x, y;
            public long time;
        }

        private readonly object gate = new object();
        private readonly Thread thread;
        private volatile bool stopped;
        private State state;
        private string status = "Wiimote: searching (DolphinBar on mode 4)";

        public Wii()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                status = "Direct Wiimote input requires Windows";
                return;
            }
            thread = new Thread(Run) { IsBackground = true, Name = "Menu Wiimote" };
            thread.Start();
        }

        public State Read(out string text)
        {
            lock (gate)
            {
                State result = state;
                state.pressed = 0;
                if (Age(result.time) > 0.5)
                {
                    result.connected = false;
                    result.buttons = result.pressed = result.points = 0;
                }
                text = status;
                return result;
            }
        }

        public void Dispose() => stopped = true;
        public bool Closing => stopped;
        public bool Finished => thread == null || !thread.IsAlive;

        private static double Age(long time) => (Stopwatch.GetTimestamp() - time) / (double)Stopwatch.Frequency;

        private void SetStatus(string text)
        {
            lock (gate) status = text;
        }

        private void Run()
        {
            try
            {
                while (!stopped)
                {
                    bool found = false;
                    foreach (string path in Paths())
                    {
                        if (stopped) break;
                        found = true;
                        using (var device = new Device(path))
                        {
                            if (!device.Valid) continue;
                            try
                            {
                                if (!device.Send(0x15, 0)) continue;
                                var data = new byte[22];
                                if (!Reply(device, data, 0x20, -1)) continue;
                                SetStatus("Wiimote: starting IR camera");
                                if (!Setup(device, data))
                                {
                                    SetStatus("Wiimote: IR setup failed; close other Wiimote apps");
                                    continue;
                                }
                                lock (gate)
                                {
                                    int session = state.session + 1;
                                    state = new State { session = session };
                                }
                                long last = Stopwatch.GetTimestamp();
                                while (!stopped && Age(last) < 1.5)
                                {
                                    int size = device.Read(data);
                                    if (size < 0) break;
                                    if (size == 0) continue;
                                    if (data[0] == 0x20)
                                    {
                                        if (!device.Send(0x12, 4, 0x33)) break;
                                        continue;
                                    }
                                    if (!Parse(data, size, out State next)) continue;
                                    last = next.time = Stopwatch.GetTimestamp();
                                    next.connected = true;
                                    lock (gate)
                                    {
                                        next.session = state.session;
                                        next.pressed = state.pressed | (next.buttons & ~state.buttons);
                                        state = next;
                                        status = next.points >= 2 ? "Wiimote: pointing" : "Wiimote: aim at the bar, or use the D-pad";
                                    }
                                }
                            }
                            finally
                            {
                                if (stopped)
                                {
                                    device.Send(0x12, 0, 0x30);
                                    device.Send(0x13, 0);
                                    device.Send(0x1a, 0);
                                }
                                lock (gate)
                                {
                                    state.connected = false;
                                    state.buttons = state.pressed = state.points = 0;
                                }
                            }
                        }
                    }
                    if (!stopped)
                    {
                        SetStatus(found ? "Wiimote: not responding; sync in mode 4 and close other Wiimote apps" :
                            "Wiimote: not found; sync the DolphinBar in mode 4");
                        for (int i = 0; i < 20 && !stopped; i++) Thread.Sleep(50);
                    }
                }
            }
            catch (Exception error)
            {
                SetStatus("Wiimote: " + error.Message);
            }
            finally
            {
                lock (gate) state = default;
            }
        }

        private bool Reply(Device device, byte[] data, byte report, int command)
        {
            long start = Stopwatch.GetTimestamp();
            while (!stopped && Age(start) < 0.8)
            {
                int size = device.Read(data);
                if (size < 0) return false;
                if (size < (command < 0 ? 7 : 5) || data[0] != report) continue;
                if (command < 0) return true;
                if (data[3] == command) return data[4] == 0;
            }
            return false;
        }

        private bool Command(Device device, byte[] data, byte report, params byte[] payload)
        {
            if (stopped || !device.Send(report, payload) || !Reply(device, data, 0x22, report)) return false;
            Thread.Sleep(50);
            return !stopped;
        }

        private bool Register(Device device, byte[] data, byte address, params byte[] value)
        {
            var payload = new byte[21];
            payload[0] = 4;
            payload[1] = 0xb0;
            payload[3] = address;
            payload[4] = (byte)value.Length;
            Array.Copy(value, 0, payload, 5, value.Length);
            return Command(device, data, 0x16, payload);
        }

        private bool Setup(Device device, byte[] data)
        {
            return Command(device, data, 0x13, 6) && Command(device, data, 0x1a, 6) &&
                Register(device, data, 0x30, 8) &&
                Register(device, data, 0x00, 2, 0, 0, 0x71, 1, 0, 0xaa, 0, 0x64) &&
                Register(device, data, 0x1a, 0x63, 3) &&
                Register(device, data, 0x33, 3) && Register(device, data, 0x30, 8) &&
                device.Send(0x12, 4, 0x33);
        }

        internal static bool Parse(byte[] data, int size, out State result)
        {
            result = default;
            if (data == null || size < 18 || size > data.Length || data[0] != 0x33) return false;
            result.buttons = ((data[1] & 0x1f) << 8) | (data[2] & 0x9f);
            for (int i = 6; i < 18; i += 3)
            {
                int x = data[i] | ((data[i + 2] & 0x30) << 4);
                int y = data[i + 1] | ((data[i + 2] & 0xc0) << 2);
                if (x == 1023 && y == 1023 || y > 767) continue;
                result.x += x;
                result.y += y;
                result.points++;
            }
            if (result.points > 0)
            {
                result.x = 1 - result.x / (result.points * 1023f);
                result.y /= result.points * 767f;
            }
            return true;
        }

        private static List<string> Paths()
        {
            var paths = new List<string>();
            Guid hid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (CM_Get_Device_Interface_List_SizeW(out uint size, ref hid, IntPtr.Zero, 0) != 0)
                    throw new InvalidOperationException("Could not list HID devices");
                var buffer = new char[size];
                if (CM_Get_Device_Interface_ListW(ref hid, IntPtr.Zero, buffer, size, 0) != 0) continue;
                foreach (string path in new string(buffer).Split('\0'))
                {
                    string lower = path.ToLowerInvariant();
                    if ((lower.Contains("vid_057e") || lower.Contains("vid&0002057e")) &&
                        (lower.Contains("pid_0306") || lower.Contains("pid_0330") ||
                         lower.Contains("pid&0306") || lower.Contains("pid&0330")))
                        paths.Add(path);
                }
                return paths;
            }
            return paths;
        }

        private sealed class Device : IDisposable
        {
            private readonly IntPtr handle;
            private readonly IntPtr signal;
            public bool Valid => handle != IntPtr.Zero && handle != new IntPtr(-1) && signal != IntPtr.Zero;

            public Device(string path)
            {
                handle = CreateFileW(path, 0xc0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
                if (handle != new IntPtr(-1)) signal = CreateEventW(IntPtr.Zero, true, false, null);
            }

            public int Read(byte[] data) => Transfer(data, data.Length, false);

            public bool Send(byte report, params byte[] payload)
            {
                var data = new byte[22];
                data[0] = report;
                Array.Copy(payload, 0, data, 1, payload.Length);
                if (Transfer(data, data.Length, true) > 0) return true;
                return payload.Length < 21 && Transfer(data, payload.Length + 1, true) > 0;
            }

            private unsafe int Transfer(byte[] data, int length, bool write)
            {
                if (!Valid) return -1;
                ResetEvent(signal);
                var request = new Request { signal = signal };
                fixed (byte* buffer = data)
                {
                    uint count;
                    bool done = write ? WriteFile(handle, buffer, (uint)length, out count, &request) :
                        ReadFile(handle, buffer, (uint)length, out count, &request);
                    if (done) return (int)count;
                    if (Marshal.GetLastWin32Error() != 997) return -1;
                    bool timeout = WaitForSingleObject(signal, 200) != 0;
                    if (timeout) CancelIo(handle);
                    if (GetOverlappedResult(handle, &request, out count, true)) return (int)count;
                    return timeout && Marshal.GetLastWin32Error() == 995 ? 0 : -1;
                }
            }

            public void Dispose()
            {
                if (handle != IntPtr.Zero && handle != new IntPtr(-1)) CloseHandle(handle);
                if (signal != IntPtr.Zero) CloseHandle(signal);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Request
        {
            public UIntPtr status, count;
            public uint offset, offsetHigh;
            public IntPtr signal;
        }

        [DllImport("cfgmgr32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_Device_Interface_List_SizeW(out uint size, ref Guid guid, IntPtr id, uint flags);
        [DllImport("cfgmgr32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_Device_Interface_ListW(ref Guid guid, IntPtr id, [Out] char[] buffer, uint size, uint flags);
        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEventW(IntPtr attributes, bool manual, bool initial, string name);
        [DllImport("kernel32.dll")]
        private static extern bool ResetEvent(IntPtr handle);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")]
        private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")]
        private static extern bool CancelIo(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern unsafe bool ReadFile(IntPtr handle, byte* data, uint length, out uint count, Request* request);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern unsafe bool WriteFile(IntPtr handle, byte* data, uint length, out uint count, Request* request);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern unsafe bool GetOverlappedResult(IntPtr handle, Request* request, out uint count, bool wait);
    }
}
