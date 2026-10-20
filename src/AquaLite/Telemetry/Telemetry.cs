using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace AquaLite {
 public sealed class Telemetry : IDisposable {
  [StructLayout(LayoutKind.Sequential)] struct FileTime {public uint Low,High;public ulong Value{get{return ((ulong)High<<32)|Low;}}}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)] class MemoryStatus {public uint Length=(uint)Marshal.SizeOf(typeof(MemoryStatus)),Load;public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended;}
  [StructLayout(LayoutKind.Sequential)] struct Utilization {public uint Gpu,Memory;}
  [StructLayout(LayoutKind.Sequential)] struct GpuMemory {public ulong Total,Free,Used;}
  [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
  [DllImport("kernel32.dll",CharSet=CharSet.Auto)] static extern bool GlobalMemoryStatusEx([In,Out]MemoryStatus status);
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlInit_v2();
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlShutdown();
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetHandleByIndex_v2(uint index,out IntPtr device);
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetTemperature(IntPtr device,uint sensor,out uint temperature);
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetUtilizationRates(IntPtr device,out Utilization value);
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetMemoryInfo(IntPtr device,out GpuMemory value);
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetClockInfo(IntPtr device,uint type,out uint value);
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetPowerUsage(IntPtr device,out uint value);
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetFanSpeed(IntPtr device,out uint value);
  [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Ansi)] static extern int nvmlDeviceGetName(IntPtr device,StringBuilder name,uint length);
  ulong lastIdle,lastTotal;IntPtr gpu;bool nvmlInitialized;string cpuModel="",gpuModel="";
  readonly CpuBridge bridge=new CpuBridge();
  readonly Func<SensorSnapshot> directSnapshot;
  readonly Dictionary<string,NetworkSample> network=new Dictionary<string,NetworkSample>();
  readonly List<SensorReading> drives=new List<SensorReading>();DateTime nextDrives=DateTime.MinValue,nextWmi=DateTime.MinValue;
  readonly List<SensorReading> wmiReadings=new List<SensorReading>();
  sealed class NetworkSample {public long Received,Sent,Timestamp;}
  public Telemetry():this(null) { }
  public Telemetry(Func<SensorSnapshot> snapshotSource) {
   directSnapshot=snapshotSource;
   FileTime idle,kernel,user;if(GetSystemTimes(out idle,out kernel,out user)){lastIdle=idle.Value;lastTotal=kernel.Value+user.Value;}
   try{using(var key=Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))if(key!=null)cpuModel=Convert.ToString(key.GetValue("ProcessorNameString")).Trim();}catch(System.Security.SecurityException){}
   try{if(nvmlInit_v2()==0){nvmlInitialized=true;nvmlDeviceGetHandleByIndex_v2(0,out gpu);if(gpu!=IntPtr.Zero){var name=new StringBuilder(128);if(nvmlDeviceGetName(gpu,name,128)==0)gpuModel=name.ToString();}}}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
  }
  public Reading Read() {
   var r=new Reading{CpuModel=cpuModel,GpuModel=gpuModel};
   FileTime idle,kernel,user;if(GetSystemTimes(out idle,out kernel,out user)){ulong total=kernel.Value+user.Value,delta=total>=lastTotal?total-lastTotal:0;if(delta>0&&idle.Value>=lastIdle)r.Cpu=Math.Max(0,Math.Min(100,100.0*(1.0-(double)(idle.Value-lastIdle)/delta)));lastIdle=idle.Value;lastTotal=total;}
   var memory=new MemoryStatus();if(GlobalMemoryStatusEx(memory)){r.MemoryTotalGB=memory.TotalPhysical/1073741824.0;r.MemoryUsedGB=(memory.TotalPhysical-memory.AvailablePhysical)/1073741824.0;r.MemoryPercent=memory.Load;}
   Add(r,"/windows/cpu/load","CPU","Uso total","Load","%",r.Cpu,"Windows",cpuModel);
   Add(r,"/windows/memory/load","Memória","Uso da memória","Load","%",r.MemoryPercent,"Windows","Memória física");
   Add(r,"/windows/memory/used","Memória","Memória em uso","Data","GB",r.MemoryUsedGB,"Windows","Memória física");
   Add(r,"/windows/memory/free","Memória","Memória disponível","Data","GB",r.MemoryTotalGB-r.MemoryUsedGB,"Windows","Memória física");
   Add(r,"/windows/memory/total","Memória","Memória instalada","Data","GB",r.MemoryTotalGB,"Windows","Memória física");
   var snapshot=directSnapshot==null?bridge.ReadSnapshot():directSnapshot();
   if(snapshot!=null){r.Sensors.AddRange(snapshot.Sensors);r.SensorStatus=snapshot.Status;ApplyHardware(r);}
   else {r.SensorStatus="Leitor local não ativo; dados básicos do Windows disponíveis";ReadWmiFallback(r);}
   ReadGpu(r);ReadNetwork(r);ReadDrives(r);PopulateValues(r);
   r.Values["Volume"]=OptionalTelemetry.ReadMasterVolume();
   r.TemperatureSource=r.CpuTemperature.HasValue?"Sensor local da CPU"+(r.GpuTemperature.HasValue?" e GPU":""):r.GpuTemperature.HasValue?"GPU disponível; temperatura da CPU indisponível":"Temperaturas indisponíveis; ative o leitor local";
   return r;
  }
  static void Add(Reading r,string id,string group,string name,string kind,string unit,double? value,string source,string hardware) {r.Sensors.Add(new SensorReading{Id=id,Group=group,Name=name,Kind=kind,Unit=unit,Value=value,Source=source,Hardware=hardware,Time=DateTime.UtcNow});}
  static double? Select(Reading r,string group,string kind,string preferred,bool maximum) {
   double? result=null;foreach(var s in r.Sensors){if(s.Group!=group||s.Kind!=kind||!s.Value.HasValue||s.Source=="Windows")continue;
    if(!string.IsNullOrEmpty(preferred)&&s.Name.IndexOf(preferred,StringComparison.OrdinalIgnoreCase)>=0)return s.Value;
    if(!result.HasValue)result=s.Value;else if(maximum)result=Math.Max(result.Value,s.Value.Value);
   }return result;
  }
  static double? Named(Reading r,string group,string kind,string name) {foreach(var s in r.Sensors)if(s.Group==group&&s.Kind==kind&&s.Value.HasValue&&s.Name.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0)return s.Value;return null;}
  static void ApplyHardware(Reading r) {
   r.CpuTemperature=Select(r,"CPU","Temperature","Package",true);r.CpuClockMHz=Select(r,"CPU","Clock","Core",true);r.CpuPowerWatts=Named(r,"CPU","Power","Package");r.CpuVoltage=Select(r,"CPU","Voltage","Core",false);
   r.GpuTemperature=Select(r,"GPU","Temperature","Core",false);r.GpuLoad=Named(r,"GPU","Load","Core");r.GpuClockMHz=Named(r,"GPU","Clock","Core");r.GpuMemoryClockMHz=Named(r,"GPU","Clock","Memory");r.GpuPowerWatts=Select(r,"GPU","Power","Package",false);r.GpuVoltage=Select(r,"GPU","Voltage","Core",false);r.GpuFanRpm=Select(r,"GPU","Fan",null,false);
   foreach(var s in r.Sensors){if(s.Group=="CPU"&&string.IsNullOrEmpty(r.CpuModel))r.CpuModel=s.Hardware;if(s.Group=="GPU"&&string.IsNullOrEmpty(r.GpuModel))r.GpuModel=s.Hardware;}
   double? used=Named(r,"GPU","SmallData","Memory Used"),total=Named(r,"GPU","SmallData","Memory Total");r.GpuMemoryUsedGB=used.HasValue?used/1024:Named(r,"GPU","Data","Memory Used");r.GpuMemoryTotalGB=total.HasValue?total/1024:Named(r,"GPU","Data","Memory Total");
   if(r.GpuMemoryUsedGB.HasValue&&r.GpuMemoryTotalGB>0)r.GpuMemoryPercent=100*r.GpuMemoryUsedGB/r.GpuMemoryTotalGB;
  }
  void ReadGpu(Reading r) {
   if(gpu==IntPtr.Zero)return;
   try {uint value;Utilization usage;GpuMemory memory;
    if(nvmlDeviceGetTemperature(gpu,0,out value)==0){r.GpuTemperature=value;Add(r,"/nvidia/0/temperature","GPU","Temperatura da GPU","Temperature","°C",value,"NVIDIA NVML",gpuModel);}
    if(nvmlDeviceGetUtilizationRates(gpu,out usage)==0){r.GpuLoad=usage.Gpu;Add(r,"/nvidia/0/load","GPU","Uso da GPU","Load","%",usage.Gpu,"NVIDIA NVML",gpuModel);}
    try{if(nvmlDeviceGetMemoryInfo(gpu,out memory)==0){r.GpuMemoryUsedGB=memory.Used/1073741824.0;r.GpuMemoryTotalGB=memory.Total/1073741824.0;r.GpuMemoryPercent=memory.Total>0?100.0*memory.Used/memory.Total:(double?)null;Add(r,"/nvidia/0/memory/used","GPU","Memória de vídeo em uso","Data","GB",r.GpuMemoryUsedGB,"NVIDIA NVML",gpuModel);Add(r,"/nvidia/0/memory/load","GPU","Uso da memória de vídeo","Load","%",r.GpuMemoryPercent,"NVIDIA NVML",gpuModel);}}catch(EntryPointNotFoundException){}
    try{if(nvmlDeviceGetClockInfo(gpu,0,out value)==0){r.GpuClockMHz=value;Add(r,"/nvidia/0/clock/core","GPU","Clock da GPU","Clock","MHz",value,"NVIDIA NVML",gpuModel);}if(nvmlDeviceGetClockInfo(gpu,2,out value)==0){r.GpuMemoryClockMHz=value;Add(r,"/nvidia/0/clock/memory","GPU","Clock da memória de vídeo","Clock","MHz",value,"NVIDIA NVML",gpuModel);}}catch(EntryPointNotFoundException){}
    try{if(nvmlDeviceGetPowerUsage(gpu,out value)==0){r.GpuPowerWatts=value/1000.0;Add(r,"/nvidia/0/power","GPU","Consumo da GPU","Power","W",r.GpuPowerWatts,"NVIDIA NVML",gpuModel);}}catch(EntryPointNotFoundException){}
    try{if(nvmlDeviceGetFanSpeed(gpu,out value)==0){r.GpuFanPercent=value;Add(r,"/nvidia/0/fan/load","GPU","Velocidade da ventoinha","Level","%",value,"NVIDIA NVML",gpuModel);}}catch(EntryPointNotFoundException){}
   }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
  }
  void ReadNetwork(Reading r) {
   long timestamp=Stopwatch.GetTimestamp();double down=0,up=0;bool sampled=false;
   try{foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
    if(adapter.OperationalStatus!=OperationalStatus.Up||adapter.NetworkInterfaceType==NetworkInterfaceType.Loopback||adapter.NetworkInterfaceType==NetworkInterfaceType.Tunnel)continue;
    try{var stats=adapter.GetIPv4Statistics();NetworkSample old;if(network.TryGetValue(adapter.Id,out old)){double elapsed=(double)(timestamp-old.Timestamp)/Stopwatch.Frequency;if(elapsed>=0.1&&stats.BytesReceived>=old.Received&&stats.BytesSent>=old.Sent){double received=(stats.BytesReceived-old.Received)/elapsed,sent=(stats.BytesSent-old.Sent)/elapsed;Add(r,"/network/"+adapter.Id+"/down","Rede",adapter.Name+" • recebimento","Throughput","B/s",received,"Windows",adapter.Description);Add(r,"/network/"+adapter.Id+"/up","Rede",adapter.Name+" • envio","Throughput","B/s",sent,"Windows",adapter.Description);
       string description=adapter.Description.ToLowerInvariant();if((adapter.NetworkInterfaceType==NetworkInterfaceType.Ethernet||adapter.NetworkInterfaceType==NetworkInterfaceType.Wireless80211)&&!description.Contains("virtual")&&!description.Contains("vpn")&&!description.Contains("tap-")&&!description.Contains("hyper-v")){down+=received;up+=sent;sampled=true;}}}
     network[adapter.Id]=new NetworkSample{Received=stats.BytesReceived,Sent=stats.BytesSent,Timestamp=timestamp};
    }catch(NetworkInformationException){} }
   }catch(NetworkInformationException){}
   if(sampled){r.NetworkDownloadBytesPerSecond=down;r.NetworkUploadBytesPerSecond=up;}
  }
  void ReadDrives(Reading r) {
   if(DateTime.UtcNow>=nextDrives){nextDrives=DateTime.UtcNow.AddSeconds(5);drives.Clear();
    foreach(var drive in DriveInfo.GetDrives()){try{if(drive.DriveType!=DriveType.Fixed||!drive.IsReady||drive.TotalSize<=0)continue;string letter=drive.Name.Substring(0,1);DateTime now=DateTime.UtcNow;
      drives.Add(new SensorReading{Id="/windows/storage/"+letter+"/load",Group="Armazenamento",Name=letter+": • espaço utilizado",Kind="Load",Unit="%",Value=100.0*(drive.TotalSize-drive.AvailableFreeSpace)/drive.TotalSize,Source="Windows",Hardware=letter+":",Time=now});
      drives.Add(new SensorReading{Id="/windows/storage/"+letter+"/used",Group="Armazenamento",Name=letter+": • espaço em uso",Kind="Data",Unit="GB",Value=(drive.TotalSize-drive.AvailableFreeSpace)/1073741824.0,Source="Windows",Hardware=letter+":",Time=now});
      drives.Add(new SensorReading{Id="/windows/storage/"+letter+"/free",Group="Armazenamento",Name=letter+": • espaço disponível",Kind="Data",Unit="GB",Value=drive.AvailableFreeSpace/1073741824.0,Source="Windows",Hardware=letter+":",Time=now});
    }catch(IOException){}catch(UnauthorizedAccessException){}}
   }r.Sensors.AddRange(drives);
  }
  void ReadWmiFallback(Reading r) {
   if(DateTime.UtcNow>=nextWmi){nextWmi=DateTime.UtcNow.AddSeconds(15);wmiReadings.Clear();
    foreach(string provider in new[]{"LibreHardwareMonitor","OpenHardwareMonitor"}){try{var options=new EnumerationOptions{Timeout=TimeSpan.FromMilliseconds(500)};
      using(var searcher=new ManagementObjectSearcher(new ManagementScope(@"root\"+provider),new ObjectQuery("SELECT Name,Identifier,Value FROM Sensor WHERE SensorType='Temperature'"),options))using(var results=searcher.Get())foreach(ManagementObject sensor in results)using(sensor){string id=Convert.ToString(sensor["Identifier"]);double value=Convert.ToDouble(sensor["Value"]);if(!SensorReading.IsFinite(value)||value< -30||value>140)continue;string group=id.IndexOf("cpu",StringComparison.OrdinalIgnoreCase)>=0?"CPU":id.IndexOf("gpu",StringComparison.OrdinalIgnoreCase)>=0?"GPU":"Outros";wmiReadings.Add(new SensorReading{Id=id,Group=group,Name=Convert.ToString(sensor["Name"]),Kind="Temperature",Unit="°C",Source=provider+" WMI",Hardware=provider,Value=value,Time=DateTime.UtcNow});}
      if(wmiReadings.Count>0)break;
     }catch(ManagementException){}catch(UnauthorizedAccessException){}catch(COMException){}}
   }r.Sensors.AddRange(wmiReadings);ApplyHardware(r);
  }
  static void PopulateValues(Reading r) {
   var v=r.Values;v["CPULOAD"]=r.Cpu;v["CPUTEMP"]=r.CpuTemperature;v["CPUCLOCK"]=r.CpuClockMHz;v["CPUCLOCK_G"]=r.CpuClockMHz/1000;v["CPUPWR"]=r.CpuPowerWatts;v["CPUVOLTAGE"]=r.CpuVoltage;
   v["GPUTEMP"]=r.GpuTemperature;v["GPULOAD"]=r.GpuLoad;v["GPUCLOCK"]=r.GpuClockMHz;v["GPUCLOCK_G"]=r.GpuClockMHz/1000;v["GPUPWR"]=r.GpuPowerWatts;v["GPUVOLTAGE"]=r.GpuVoltage;v["GPUFAN"]=r.GpuFanRpm;
   v["GPURAM"]=r.GpuMemoryUsedGB*1024;v["GPURAMTOTAL"]=r.GpuMemoryTotalGB*1024;v["GPUVALIDRAM"]=(r.GpuMemoryTotalGB-r.GpuMemoryUsedGB)*1024;v["GPURAMLOAD"]=r.GpuMemoryPercent;
   v["RAM"]=r.MemoryUsedGB*1024;v["RAMTOTAL"]=r.MemoryTotalGB*1024;v["RAM_GB"]=r.MemoryUsedGB;v["RAMTOTAL_GB"]=r.MemoryTotalGB;v["RAMVALID"]=(r.MemoryTotalGB-r.MemoryUsedGB)*1024;v["RAMVALID_GB"]=r.MemoryTotalGB-r.MemoryUsedGB;v["RAMLOAD"]=r.MemoryPercent;
   v["UPSPEED"]=r.NetworkUploadBytesPerSecond/1024;v["DOWNDSPEED"]=r.NetworkDownloadBytesPerSecond/1024;
   v["CPUFAN"]=Named(r,"Placa-mãe","Fan","CPU");v["WATERPUMP"]=Named(r,"Placa-mãe","Fan","Pump");v["CASEFAN1"]=Named(r,"Placa-mãe","Fan","System Fan #1");v["CASEFAN2"]=Named(r,"Placa-mãe","Fan","System Fan #2");
   v["SLCWATERPUMP"]=Named(r,"Placa-mãe","Fan","SLC");
   v["HDDTEMP"]=Select(r,"Armazenamento","Temperature",null,false);
   foreach(var sensor in r.Sensors)if(sensor.Source=="Windows"&&sensor.Group=="Armazenamento"&&sensor.Hardware.Length==2){string letter=sensor.Hardware.Substring(0,1);if(sensor.Kind=="Load"){v["DRVLOAD:"+letter]=sensor.Value;if(!v.ContainsKey("DRVLOAD"))v["DRVLOAD"]=sensor.Value;}else if(sensor.Id.EndsWith("/used",StringComparison.Ordinal)){v["HDDUSED:"+letter]=sensor.Value;if(!v.ContainsKey("HDDUSED"))v["HDDUSED"]=sensor.Value;}}
  }
  public void Dispose(){bridge.Dispose();if(nvmlInitialized){try{nvmlShutdown();}catch{}nvmlInitialized=false;}}
 }
}
