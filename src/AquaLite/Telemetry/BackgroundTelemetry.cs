using System;
using System.Diagnostics;
using System.Threading;

namespace AquaLite {
 public sealed class BackgroundTelemetry : IDisposable {
  readonly object gate=new object();
  readonly Thread worker;
  Settings settings;
  Reading latest=new Reading{SensorStatus="Coletando leituras do computador..."};
  bool stopping;
  public BackgroundTelemetry(Settings initial) {
   if(initial==null)throw new ArgumentNullException("initial");
   settings=initial.Copy();
   worker=new Thread(Run){IsBackground=true,Name="AquaLite.Sensores"};
   worker.Start();
  }
  public void Update(Settings value) {
   if(value==null)throw new ArgumentNullException("value");
   lock(gate){if(stopping)return;settings=value.Copy();}
  }
  public Reading Snapshot() {
   Reading r;lock(gate)r=latest;
   return r.Copy();
  }
  void Run() {
   try {
    bool useHardware=false;
    using(var hardware=AppPaths.ServiceProcess?new CpuSensor():null)
    using(var telemetry=new Telemetry(hardware==null?null:new Func<SensorSnapshot>(delegate{return useHardware?hardware.ReadSnapshot():null;})))using(var weather=new Weather()) {
     while(true) {
      Settings current;lock(gate){if(stopping)return;current=settings;}
      useHardware=current.LocalSensorsEnabled;
      var cycle=Stopwatch.StartNew();
      try {
       Reading result=telemetry.Read();weather.Update(current,result);
       lock(gate){if(stopping)return;latest=result;}
      }catch(Exception error){
       lock(gate){if(stopping)return;var failure=latest.Copy();failure.SensorStatus="Falha na coleta: "+error.Message;latest=failure;}
      }
      lock(gate){if(stopping)return;int delay=Math.Max(1,1000-(int)Math.Min(1000,cycle.ElapsedMilliseconds));Monitor.Wait(gate,delay);}
     }
    }
   }catch(Exception error){lock(gate){if(!stopping)latest=new Reading{SensorStatus="Leitor indisponível: "+error.Message};}}
  }
  public void Dispose() {
   lock(gate){if(stopping)return;stopping=true;Monitor.PulseAll(gate);}
   // The worker owns and disposes providers after any in-flight native call returns.
   // No provider or synchronization handle is disposed from the UI thread.
   if(Thread.CurrentThread!=worker)worker.Join(100);
  }
 }
}
