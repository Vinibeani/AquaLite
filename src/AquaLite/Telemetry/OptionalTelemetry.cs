using System;
using System.Runtime.InteropServices;
namespace AquaLite {
 public static class OptionalTelemetry {
  [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator {}
  [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDeviceEnumerator {
   int EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);
   [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
  }
  [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice {
   [PreserveSig]int Activate(ref Guid id,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object endpoint);
  }
  [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IEndpointVolume {
   int RegisterControlChangeNotify(IntPtr notify);
   int UnregisterControlChangeNotify(IntPtr notify);
   int GetChannelCount(out uint count);
   int SetMasterVolumeLevel(float level,IntPtr context);
   int SetMasterVolumeLevelScalar(float level,IntPtr context);
   int GetMasterVolumeLevel(out float level);
   [PreserveSig]int GetMasterVolumeLevelScalar(out float level);
  }
  public static double? ReadMasterVolume() {
   object enumerator=null,endpoint=null;IDevice device=null;
   try {
    enumerator=new DeviceEnumerator();
    if(((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0,1,out device)<0||device==null)return null;
    Guid id=typeof(IEndpointVolume).GUID;
    if(device.Activate(ref id,23,IntPtr.Zero,out endpoint)<0||endpoint==null)return null;
    float level;if(((IEndpointVolume)endpoint).GetMasterVolumeLevelScalar(out level)<0||float.IsNaN(level)||float.IsInfinity(level))return null;
    return Math.Max(0,Math.Min(100,level*100.0));
   }catch(COMException){return null;}catch(InvalidCastException){return null;}
   finally{if(endpoint!=null&&Marshal.IsComObject(endpoint))Marshal.ReleaseComObject(endpoint);if(device!=null&&Marshal.IsComObject(device))Marshal.ReleaseComObject(device);if(enumerator!=null&&Marshal.IsComObject(enumerator))Marshal.ReleaseComObject(enumerator);}
  }
 }
}
