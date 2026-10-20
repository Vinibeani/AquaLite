using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace AquaLite {
 public sealed class CpuSensor : IDisposable {
  object computer; Type type; DateTime nextAttempt = DateTime.MinValue;
  readonly Dictionary<string,DateTime> nextUpdates = new Dictionary<string,DateTime>();
  public string Status = "Leitor local não ativado";
  public static bool Installed { get { using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64)) using(var key=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO")) return key!=null; } }
  static object Property(object obj,string name) { var p=obj.GetType().GetProperty(name); return p==null?null:p.GetValue(obj,null); }
  static string Text(object obj,string name) { return Convert.ToString(Property(obj,name)); }
  void Enable(string name) { var property=type.GetProperty(name); if(property!=null)property.SetValue(computer,true,null); }
  bool Open() {
   if(computer!=null)return true;
   if(DateTime.UtcNow<nextAttempt)return false;
   nextAttempt=DateTime.UtcNow.AddSeconds(10);
   string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sensors","LibreHardwareMonitorLib.dll");
   if(!File.Exists(path)){Status="Biblioteca de sensores não encontrada";return false;}
   var assembly=Assembly.LoadFrom(path);type=assembly.GetType("LibreHardwareMonitor.Hardware.Computer",true);computer=Activator.CreateInstance(type);
   Enable("IsCpuEnabled");Enable("IsGpuEnabled");Enable("IsMemoryEnabled");Enable("IsMotherboardEnabled");Enable("IsStorageEnabled");Enable("IsBatteryEnabled");
   // Controllers and write/control APIs remain disabled. Hardware.Update and sensor getters only.
   type.GetMethod("Open").Invoke(computer,null);return true;
  }
  public SensorSnapshot ReadSnapshot() {
   var result=new SensorSnapshot();
   try {
    if(Open()) {
     foreach(object hardware in (IEnumerable)type.GetProperty("Hardware").GetValue(computer,null)) Collect(hardware,result.Sensors,0);
     bool cpu=false;foreach(var s in result.Sensors)if(s.Group=="CPU"&&s.Kind=="Temperature"&&s.Value.HasValue)cpu=true;
     Status=cpu?"Sensores locais ativos":(Installed?"Sensores ativos; temperatura da CPU indisponível":"Sensores ativos; ative o acesso à temperatura da CPU");
    }
   } catch(Exception e) { Status="Leitor local: "+(e.InnerException??e).Message;Dispose();nextAttempt=DateTime.UtcNow.AddSeconds(30); }
   result.Status=Status;result.TimeUtc=DateTime.UtcNow;return result;
  }
  void Collect(object hardware,List<SensorReading> readings,int depth) {
   if(depth>8||readings.Count>=512)return;
   string identifier=Text(hardware,"Identifier"), hardwareType=Text(hardware,"HardwareType"),name=Text(hardware,"Name");
   string group=Group(hardwareType); DateTime now=DateTime.UtcNow,next;
   int seconds=group=="CPU"?1:group=="Armazenamento"?5:2;
   if(!nextUpdates.TryGetValue(identifier,out next)||now>=next) {
    try {hardware.GetType().GetMethod("Update").Invoke(hardware,null);}catch { }
    nextUpdates[identifier]=now.AddSeconds(seconds);
   }
   DateTime sampled=nextUpdates.ContainsKey(identifier)?nextUpdates[identifier].AddSeconds(-seconds):now;
   var sensors=Property(hardware,"Sensors") as IEnumerable;
   if(sensors!=null)foreach(object sensor in sensors) {
    if(readings.Count>=512)break;
    string kind=Text(sensor,"SensorType"); if(kind=="Control")continue;
    object raw=Property(sensor,"Value");double? value=raw==null?(double?)null:Convert.ToDouble(raw);
    if(value.HasValue&&!SensorReading.IsFinite(value.Value))value=null;
    readings.Add(new SensorReading{Id=Text(sensor,"Identifier"),Group=group,Hardware=name,Name=Text(sensor,"Name"),Kind=kind,Index=Convert.ToInt32(Property(sensor,"Index")),Value=value,Unit=Unit(kind),Source="LibreHardwareMonitor (local)",Time=sampled});
   }
   var children=Property(hardware,"SubHardware") as IEnumerable;if(children!=null)foreach(object child in children)Collect(child,readings,depth+1);
  }
  static string Group(string kind) {
   if(kind=="Cpu")return "CPU";if(kind.StartsWith("Gpu",StringComparison.Ordinal))return "GPU";
   if(kind=="Memory")return "Memória";if(kind=="Storage")return "Armazenamento";if(kind=="Network")return "Rede";
   if(kind=="Motherboard"||kind=="SuperIO"||kind=="EmbeddedController")return "Placa-mãe";
   if(kind=="Battery")return "Bateria";return "Outros";
  }
  static string Unit(string kind) {
   switch(kind){case "Voltage":return "V";case "Current":return "A";case "Clock":return "MHz";case "Temperature":return "°C";case "Load":case "Level":case "Humidity":return "%";case "Frequency":return "Hz";case "Fan":return "RPM";case "Flow":return "L/h";case "Power":return "W";case "Data":return "GB";case "SmallData":return "MB";case "Throughput":return "B/s";case "TimeSpan":return "s";case "Energy":return "mWh";default:return "";}
  }
  public double? Read() {var snapshot=ReadSnapshot();double? package=null,maximum=null;foreach(var s in snapshot.Sensors)if(s.Group=="CPU"&&s.Kind=="Temperature"&&s.Value.HasValue&&s.Value>=0&&s.Value<=140){maximum=maximum.HasValue?Math.Max(maximum.Value,s.Value.Value):s.Value;if(s.Name.IndexOf("Package",StringComparison.OrdinalIgnoreCase)>=0)package=s.Value;}return package??maximum;}
  public void Dispose() {if(computer!=null){try{type.GetMethod("Close").Invoke(computer,null);}catch{}computer=null;nextUpdates.Clear();}}
 }
}
