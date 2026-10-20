using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AquaLite {
    internal static class WinUsbNative {
        [StructLayout(LayoutKind.Sequential)] internal struct InterfaceData {
            public int Size; public Guid Guid; public int Flags; public IntPtr Reserved;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct UsbInterface {
            public byte Length, DescriptorType, Number, Alternate, Endpoints, Class, SubClass, Protocol, Index;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct PipeInfo {
            public int Type; public byte Id; public ushort PacketSize; public byte Interval;
        }
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr hwnd, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint needed, IntPtr dev);
        [DllImport("setupapi.dll")] internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("winusb.dll", SetLastError = true)] internal static extern bool WinUsb_Initialize(SafeFileHandle file, out IntPtr handle);
        [DllImport("winusb.dll")] internal static extern bool WinUsb_Free(IntPtr handle);
        [DllImport("winusb.dll", SetLastError = true)] internal static extern bool WinUsb_QueryInterfaceSettings(IntPtr handle, byte alternate, out UsbInterface info);
        [DllImport("winusb.dll", SetLastError = true)] internal static extern bool WinUsb_QueryPipe(IntPtr handle, byte alternate, byte index, out PipeInfo info);
        [DllImport("winusb.dll", SetLastError = true)] internal static extern bool WinUsb_SetPipePolicy(IntPtr handle, byte pipe, uint policy, uint length, ref uint value);
        [DllImport("winusb.dll", SetLastError = true)] internal static extern bool WinUsb_WritePipe(IntPtr handle, byte pipe, byte[] data, uint length, out uint count, IntPtr overlapped);
        [DllImport("winusb.dll", SetLastError = true)] internal static extern bool WinUsb_ReadPipe(IntPtr handle, byte pipe, byte[] data, uint length, out uint count, IntPtr overlapped);
        [DllImport("winusb.dll", SetLastError = true)] internal static extern bool WinUsb_FlushPipe(IntPtr handle, byte pipe);
    }
}
