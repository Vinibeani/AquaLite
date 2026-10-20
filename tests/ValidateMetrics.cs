using System;
using System.Globalization;
using System.Reflection;
using AquaLite;
class ValidateMetrics {
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("OK: "+name);}
 static string Text(string kind,Reading reading){return ScreenRenderer.WidgetText(new Widget{Kind=kind},reading);}
 static int Main(){try{
  CultureInfo.CurrentCulture=new CultureInfo("en-US");
  DateTime sample=new DateTime(2026,10,19,18,0,0,DateTimeKind.Utc).AddTicks(1423);
  DateTime jsonTime=new DateTime(sample.Ticks/TimeSpan.TicksPerMillisecond*TimeSpan.TicksPerMillisecond,DateTimeKind.Utc);
  Check(CpuBridge.SameSampleTime(jsonTime,sample.Ticks),"Horário JSON em milissegundos aceita a mesma amostra do sensor");
  Check(!CpuBridge.SameSampleTime(jsonTime.AddMilliseconds(-1),sample.Ticks),"Outra amostra de sensor não é aceita como a atual");
  Reading r=new Reading{MemoryTotalGB=32,MemoryUsedGB=8,GpuMemoryTotalGB=12,GpuMemoryUsedGB=3,CpuClockMHz=4200,CpuPowerWatts=95,CpuVoltage=1.25,GpuClockMHz=1800,GpuPowerWatts=150,GpuFanRpm=1200,NetworkDownloadBytesPerSecond=1572864,NetworkUploadBytesPerSecond=2048};
  r.Sensors.Add(new SensorReading{Id="pump",Group="Placa-mãe",Kind="Fan",Name="SLC Pump",Value=2100,Unit="RPM"});
  typeof(Telemetry).GetMethod("PopulateValues",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{r});
  Check(r.GetValue("RAMTOTAL")==32768&&r.GetValue("GPURAMTOTAL")==12288,"Totais RAM/VRAM usam MB sem confundir memória livre");
  Check(r.GetValue("GPUVALIDRAM")==9216&&r.GetValue("SLCWATERPUMP")==2100,"Memória livre e bomba SLC mantêm canais independentes");
  Check(Text("Clock da CPU",r)=="CPU 4200 MHz"&&Text("Clock da GPU",r)=="GPU 1800 MHz","Clocks exibem a leitura real");
  Check(Text("Potência da CPU",r)=="CPU 95 W"&&Text("Potência da GPU",r)=="GPU 150 W","Potências exibem watts");
  Check(Text("Tensão da CPU",r)=="CPU 1,25 V"&&Text("VRAM usada",r)=="VRAM 3,0 GB","Decimais permanecem em português com Windows em inglês");
  Check(Text("Download",r)=="Download 1,5 MB/s"&&Text("Upload",r)=="Upload 2 KB/s","Rede converte bytes por segundo em unidades legíveis");
  Check(ScreenRenderer.WidgetText(new Widget{Kind="Sensor",SensorId="pump"},r)=="2100 RPM","Elemento Sensor usa o identificador escolhido");
  Check(ScreenRenderer.WidgetText(new Widget{Kind="Sensor",SensorId="missing"},r)=="—"&&Text("Tensão da CPU",new Reading())=="CPU — V","Ausência de leitura não apresenta texto ou número fictício");
  r.CpuVoltage=double.NaN;Check(Text("Tensão da CPU",r)=="CPU — V","Leitura não finita é indisponível");
  r.Time=new DateTime(2026,10,19,0,0,0);Check(Text("AM/PM",r)=="AM","Meia-noite usa AM");r.Time=r.Time.AddHours(12);Check(Text("AM/PM",r)=="PM","Meio-dia usa PM");
  Check(Text("Clima",r)=="—"&&r.GetText("Weather")=="—","Clima ausente permanece indisponível");r.WeatherText="24 °C · Céu limpo";Check(Text("Clima",r)==r.WeatherText&&r.GetText("Weather")==r.WeatherText,"Clima é compartilhado com temas importados");
  return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
