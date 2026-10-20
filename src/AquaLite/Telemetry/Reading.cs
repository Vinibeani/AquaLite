using System;
using System.Collections.Generic;

namespace AquaLite {
 public sealed class Reading {
  public double Cpu, MemoryPercent, MemoryUsedGB, MemoryTotalGB;
  public double? CpuTemperature,GpuTemperature,GpuLoad,CpuClockMHz,CpuPowerWatts,CpuVoltage,GpuClockMHz,GpuMemoryClockMHz,GpuPowerWatts,GpuVoltage,GpuFanRpm,GpuFanPercent,GpuMemoryUsedGB,GpuMemoryTotalGB,GpuMemoryPercent,NetworkDownloadBytesPerSecond,NetworkUploadBytesPerSecond;
  public string CpuModel="",GpuModel="",RamModel="",WeatherText;
  public string TemperatureSource="Nenhum sensor compatível disponível",SensorStatus="Leitor de sensores não ativado";
  public DateTime Time=DateTime.Now;
  public List<SensorReading> Sensors=new List<SensorReading>();
  public Dictionary<string,double?> Values=new Dictionary<string,double?>(StringComparer.OrdinalIgnoreCase);
  public string GetText(string data) {switch(data){case "CPUMODEL":return CpuModel;case "GPUMODEL":return GpuModel;case "RAMMODEL":return RamModel;case "Weather":return WeatherText??"—";default:return null;}}
  public double? GetValue(string data,string sub,int index) {
   if(string.IsNullOrEmpty(data))return null;
   if(data=="HDDTEMP"||data=="HDDUSED"||data=="DRVLOAD") {
    string letter=(sub??"").Trim().TrimEnd(':','\\').ToUpperInvariant();
    if(letter.Length==1){double? driveValue;if(Values.TryGetValue(data+":"+letter,out driveValue))return driveValue;}
    if(data=="HDDTEMP")return Find("Armazenamento","Temperature",sub,index);
   }
   if(data.EndsWith("_b",StringComparison.OrdinalIgnoreCase)) {
    string kind=data.StartsWith("CPUTEMP",StringComparison.OrdinalIgnoreCase)?"Temperature":data.StartsWith("CPULOAD",StringComparison.OrdinalIgnoreCase)?"Load":data.StartsWith("CPUVOLTAGE",StringComparison.OrdinalIgnoreCase)?"Voltage":data.StartsWith("FAN",StringComparison.OrdinalIgnoreCase)?"Fan":null;
    if(kind!=null)return Find(kind=="Fan"?"Placa-mãe":"CPU",kind,sub,index);
   }
   double? value;return Values.TryGetValue(data,out value)?value:null;
  }
  public double? GetValue(string data) {return GetValue(data,null,0);}
  double? Find(string group,string kind,string sub,int index) {
   SensorReading first=null;int item=0;
   foreach(var sensor in Sensors)if(sensor.Group==group&&sensor.Kind==kind&&sensor.Value.HasValue) {
    if(!string.IsNullOrEmpty(sub)&&(string.Equals(sensor.Name,sub,StringComparison.OrdinalIgnoreCase)||string.Equals(sensor.Id,sub,StringComparison.OrdinalIgnoreCase)||string.Equals(sensor.Hardware,sub,StringComparison.OrdinalIgnoreCase)))return sensor.Value;
    if(item++==index)first=sensor;
   }
   return first==null?null:first.Value;
  }
   public Reading Copy() {
   Reading r=this;
   var copy=new Reading{
    Cpu=r.Cpu,MemoryPercent=r.MemoryPercent,MemoryUsedGB=r.MemoryUsedGB,MemoryTotalGB=r.MemoryTotalGB,
    CpuTemperature=r.CpuTemperature,GpuTemperature=r.GpuTemperature,GpuLoad=r.GpuLoad,CpuClockMHz=r.CpuClockMHz,CpuPowerWatts=r.CpuPowerWatts,CpuVoltage=r.CpuVoltage,
    GpuClockMHz=r.GpuClockMHz,GpuMemoryClockMHz=r.GpuMemoryClockMHz,GpuPowerWatts=r.GpuPowerWatts,GpuVoltage=r.GpuVoltage,GpuFanRpm=r.GpuFanRpm,GpuFanPercent=r.GpuFanPercent,
    GpuMemoryUsedGB=r.GpuMemoryUsedGB,GpuMemoryTotalGB=r.GpuMemoryTotalGB,GpuMemoryPercent=r.GpuMemoryPercent,NetworkDownloadBytesPerSecond=r.NetworkDownloadBytesPerSecond,NetworkUploadBytesPerSecond=r.NetworkUploadBytesPerSecond,
    CpuModel=r.CpuModel,GpuModel=r.GpuModel,RamModel=r.RamModel,WeatherText=r.WeatherText,TemperatureSource=r.TemperatureSource,SensorStatus=r.SensorStatus,Time=r.Time,
    Values=new Dictionary<string,double?>(r.Values,StringComparer.OrdinalIgnoreCase)
   };
   foreach(var s in r.Sensors)copy.Sensors.Add(new SensorReading{Id=s.Id,Group=s.Group,Name=s.Name,Unit=s.Unit,Source=s.Source,Hardware=s.Hardware,Kind=s.Kind,Index=s.Index,Value=s.Value,Time=s.Time});
   return copy;
  }
 }
}
