using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AquaLite {
    internal static class UsbDisplayDiscovery {
        public static List<string> Find() {
            List<string> paths = new List<string>();
            foreach (Guid item in new Guid[] {new Guid("88bae032-5a81-49f0-bc3d-a4ff138216d6"), new Guid("a5dcbf10-6530-11d2-901f-00c04fb951ed")}) {
                Guid guid = item;
                IntPtr set = WinUsbNative.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 0x12);
                if (set == new IntPtr(-1)) continue;
                try {
                    for (uint i = 0; ; i++) {
                        WinUsbNative.InterfaceData data = new WinUsbNative.InterfaceData(); data.Size = Marshal.SizeOf(typeof(WinUsbNative.InterfaceData));
                        if (!WinUsbNative.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data)) {
                            int error = Marshal.GetLastWin32Error();
                            if (error != 259) throw new Win32Exception(error, "Não foi possível listar as telas USB.");
                            break;
                        }
                        uint needed;
                        WinUsbNative.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out needed, IntPtr.Zero);
                        if (needed < 8 || needed > 65536) continue;
                        IntPtr buffer = Marshal.AllocHGlobal((int)needed);
                        try {
                            Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                            if (!WinUsbNative.SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, needed, out needed, IntPtr.Zero))
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                            string path = Marshal.PtrToStringUni(IntPtr.Add(buffer, 4));
                            if (path != null && path.ToLowerInvariant().Contains("vid_1cbe&pid_0021") && !paths.Contains(path)) paths.Add(path);
                        } finally { Marshal.FreeHGlobal(buffer); }
                    }
                } finally { WinUsbNative.SetupDiDestroyDeviceInfoList(set); }
                if (paths.Count > 0) break;
            }
            return paths;
        }
    }
}
