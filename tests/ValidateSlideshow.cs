using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using AquaLite;
class ValidateSlideshow {
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("OK: "+name);}
 static int Main(){try{
  var settings=new Settings{Mode="Galeria de temas",LibraryThemeId="aqua",ThemeSlideshow=true,ThemeIntervalSeconds=10,LocalSensorsEnabled=false,WeatherEnabled=false};
  DateTime start=new DateTime(2026,10,19,0,0,0,DateTimeKind.Utc);var playback=new ThemePlayback(new[]{"aqua","brasa","oceano"});
  Check(playback.Select(settings.Copy(),start)=="aqua","Inicia no tema escolhido");
  Check(playback.Select(settings.Copy(),start.AddSeconds(10))=="brasa"&&playback.Select(settings.Copy(),start.AddSeconds(20))=="oceano","Cópias iguais de Settings permitem duas mudanças");
  var slower=settings.Copy();slower.ThemeIntervalSeconds=60;playback=new ThemePlayback(new[]{"aqua","brasa","oceano"});playback.Select(slower,start);slower.ThemeIntervalSeconds=10;
  Check(playback.Select(slower.Copy(),start.AddSeconds(11))=="brasa","Reduzir intervalo de 60 para 10 segundos aplica imediatamente");
  FieldInfo themeField=typeof(EngineState).GetField("ThemeId");Check(themeField!=null,"EngineState expõe identidade do tema efetivamente renderizado");
  var rendered=new List<string>();var watch=Stopwatch.StartNew();
  using(var engine=new DisplayEngine(settings,false)){
   while(watch.Elapsed.TotalSeconds<27&&rendered.Count<3){
    // Match the UI saving unrelated preferences while preserving slideshow inputs.
    engine.Update(settings.Copy());
    using(var snapshot=engine.Snapshot(true)){
     string id=Convert.ToString(themeField.GetValue(snapshot));
     if(snapshot.Preview!=null&&!string.IsNullOrEmpty(id)&&(rendered.Count==0||rendered[rendered.Count-1]!=id)){rendered.Add(id);Console.WriteLine("Renderizado: "+id+" em "+watch.Elapsed.TotalSeconds.ToString("0.0")+" s");}
    }
    Thread.Sleep(200);
   }
  }
  Check(rendered.Count>=3&&rendered[0]!=rendered[1]&&rendered[1]!=rendered[2],"DisplayEngine sem USB renderiza ao menos duas trocas reais em intervalo de 10 segundos");
  return 0;
 }catch(Exception error){Console.WriteLine(error);return 1;}}
}
