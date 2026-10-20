using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace AquaLite {
 public sealed class CpuBridge : IDisposable {
  const int Capacity=262144,PayloadOffset=64,Magic=0x41515333;
  MemoryMappedFile map;MemoryMappedViewAccessor view;DateTime retry=DateTime.MinValue;
  static string Sid {get{return WindowsIdentity.GetCurrent().User.Value;}}
  static string MapName {get{return @"Local\AquaLite.Sensors.v3."+Sid;}}
  public static void Configure() {
   InstallAccess();
   Process.Start(new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,"--sensor-host"){UseShellExecute=false,CreateNoWindow=true});
  }
  public static void InstallAccess() {
   if(!CpuSensor.Installed){string installer=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sensors","PawnIO_setup.exe");using(var p=Process.Start(new ProcessStartInfo(installer,"-install -silent"){UseShellExecute=false,CreateNoWindow=true})){p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Não foi possível ativar o leitor de sensores. Código "+p.ExitCode);}}
  }
  internal static byte[] Encode(SensorSnapshot snapshot) {using(var stream=new MemoryStream()){new DataContractJsonSerializer(typeof(SensorSnapshot)).WriteObject(stream,snapshot);if(stream.Length>Capacity-PayloadOffset)throw new IOException("O conjunto de sensores excedeu o limite permitido.");return stream.ToArray();}}
  internal static SensorSnapshot Decode(byte[] bytes) {
   if(bytes==null||bytes.Length==0||bytes.Length>Capacity-PayloadOffset)return null;
   using(var stream=new MemoryStream(bytes)){var result=(SensorSnapshot)new DataContractJsonSerializer(typeof(SensorSnapshot)).ReadObject(stream);
    if(result==null||result.Version!=3||result.Sensors==null||result.Sensors.Count>512)return null;
    foreach(var s in result.Sensors)if(s==null||s.Id==null||s.Id.Length>512||s.Name==null||s.Name.Length>512||s.Group==null||s.Group.Length>64||s.Kind==null||s.Kind.Length>64||s.Unit==null||s.Unit.Length>32||(s.Value.HasValue&&!SensorReading.IsFinite(s.Value.Value)))return null;
    return result;
   }
  }
  public static void Host() {bool created;using(var mutex=new Mutex(true,@"Local\AquaLite.Sensors.v3.Host."+Sid,out created)){if(!created)return;
   try {
    var security=new MemoryMappedFileSecurity();security.SetAccessRule(new AccessRule<MemoryMappedFileRights>(new SecurityIdentifier(Sid),MemoryMappedFileRights.FullControl,AccessControlType.Allow));
    using(var map=MemoryMappedFile.CreateOrOpen(MapName,Capacity,MemoryMappedFileAccess.ReadWrite,MemoryMappedFileOptions.None,security,HandleInheritability.None))using(var view=map.CreateViewAccessor())using(var sensor=new CpuSensor()) {
     DateTime started=DateTime.UtcNow;int sequence=0;
     while(true) {DateTime now=DateTime.UtcNow;long heartbeat=view.ReadInt64(16);if(now-started>TimeSpan.FromSeconds(45)&&(heartbeat==0||now.Ticks-heartbeat>TimeSpan.FromSeconds(20).Ticks))break;
      var snapshot=sensor.ReadSnapshot();byte[] bytes=Encode(snapshot);
      view.Write(24,++sequence);Thread.MemoryBarrier();view.Write(32,0);view.WriteArray(PayloadOffset,bytes,0,bytes.Length);view.Write(36,bytes.Length);view.Write(0,snapshot.TimeUtc.Ticks);view.Write(32,Magic);Thread.MemoryBarrier();view.Write(24,++sequence);
      Thread.Sleep(1000);
     }
    }
   } finally {mutex.ReleaseMutex();}
  }}
  public SensorSnapshot ReadSnapshot() {try {
   if(view==null){if(DateTime.UtcNow<retry)return null;retry=DateTime.UtcNow.AddSeconds(3);try{map=MemoryMappedFile.OpenExisting(MapName,MemoryMappedFileRights.ReadWrite);view=map.CreateViewAccessor(0,Capacity,MemoryMappedFileAccess.ReadWrite);}catch(FileNotFoundException){return null;}}
   long now=DateTime.UtcNow.Ticks;view.Write(16,now);int first=view.ReadInt32(24);Thread.MemoryBarrier();long timestamp=view.ReadInt64(0);int length=view.ReadInt32(36);
   if((first&1)!=0||view.ReadInt32(32)!=Magic||length<=0||length>Capacity-PayloadOffset||timestamp>now+TimeSpan.FromSeconds(2).Ticks||now-timestamp>TimeSpan.FromSeconds(6).Ticks)return null;
   byte[] bytes=new byte[length];view.ReadArray(PayloadOffset,bytes,0,length);Thread.MemoryBarrier();int last=view.ReadInt32(24);if(first!=last||(last&1)!=0)return null;
   // JSON DateTime has millisecond precision; the header retains 100 ns ticks.
   // Compare the same millisecond after the existing sequence and freshness checks.
   var result=Decode(bytes);if(result==null||!SameSampleTime(result.TimeUtc,timestamp))return null;return result;
  }catch(IOException){Dispose();return null;}catch(UnauthorizedAccessException){Dispose();return null;}catch(System.Runtime.Serialization.SerializationException){return null;}catch(ArgumentException){return null;}}
  public double? Read(){var snapshot=ReadSnapshot();if(snapshot==null)return null;double? maximum=null,package=null;foreach(var s in snapshot.Sensors)if(s.Group=="CPU"&&s.Kind=="Temperature"&&s.Value.HasValue){maximum=maximum.HasValue?Math.Max(maximum.Value,s.Value.Value):s.Value;if(s.Name.IndexOf("Package",StringComparison.OrdinalIgnoreCase)>=0)package=s.Value;}return package??maximum;}
  public static bool SameSampleTime(DateTime serialized,long headerTicks){return serialized.Ticks/TimeSpan.TicksPerMillisecond==headerTicks/TimeSpan.TicksPerMillisecond;}
  public void Dispose(){if(view!=null){view.Dispose();view=null;}if(map!=null){map.Dispose();map=null;}}
 }
}
