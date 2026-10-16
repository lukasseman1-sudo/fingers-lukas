using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HandMouse
{
    internal sealed class HidMouseOutput : IDisposable
    {
        const uint GENERIC_WRITE = 0x40000000;
        const uint OPEN_EXISTING = 3;
        const uint FILE_DEVICE_HANDMOUSE = 0x8000;
        const uint IOCTL_MOUSE_MOVE = (FILE_DEVICE_HANDMOUSE << 16) | (2u << 14) | (0x800u << 2);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern SafeFileHandle CreateFile(string path, uint access, uint share,
            IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle device, uint code,
            byte[] input, int inputLength, IntPtr output, int outputLength,
            out int bytesReturned, IntPtr overlap);
        private SafeFileHandle handle;

        public bool Connect()
        {
            Dispose();
            handle = CreateFile(@"\\.\HandMouseHid", GENERIC_WRITE, 3, IntPtr.Zero,
                                OPEN_EXISTING, 0, IntPtr.Zero);
            return handle != null && !handle.IsInvalid;
        }
        public bool Move(int dx, int dy)
        {
            if (handle == null || handle.IsInvalid) return false;
            int parts = Math.Max(1, Math.Max((int)Math.Ceiling(Math.Abs((double)dx) / 127),
                                             (int)Math.Ceiling(Math.Abs((double)dy) / 127)));
            int remainderX = dx, remainderY = dy;
            for (int i = 0; i < parts; i++)
            {
                int steps = parts - i;
                int x = Math.Max(-127, Math.Min(127, remainderX / steps));
                int y = Math.Max(-127, Math.Min(127, remainderY / steps));
                byte[] data = { unchecked((byte)x), unchecked((byte)y), 0 };
                int returned;
                if (!DeviceIoControl(handle, IOCTL_MOUSE_MOVE, data, data.Length,
                    IntPtr.Zero, 0, out returned, IntPtr.Zero)) return false;
                remainderX -= x; remainderY -= y;
            }
            return true;
        }
        public void Dispose() { if (handle != null) { handle.Dispose(); handle = null; } }
    }
}