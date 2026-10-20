using System;
using AquaLite;
class ValidateCompatibility {
 static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("OK: "+name);}
 static string Text(Reading reading,string data,string sub,bool unit){return LibraryRenderer.TextOf(new ThemeLayer{Kind="Data",Data=data,Sub=sub,Unit=unit},reading);}
 static int Main(){try{
  var r=new Reading{Time=new DateTime(2026,10,19,14,5,6),MemoryUsedGB=8};r.Values["RAMTOTAL"]=32768;r.Values["CPUCLOCK_G"]=4.2;r.Values["Volume"]=38;r.Values["UPSPEED"]=125;
  Check(Text(r,"RAM","",true)=="8192 MB","Galeria mantém a unidade MB do tema");
  Check(Text(r,"RAMTOTAL","",true)=="32768 MB"&&Text(r,"CPUCLOCK_G","",true)=="4,2 GHz","Galeria consome canais compartilhados");
  Check(Text(r,"Volume","",true)=="38%"&&Text(r,"UPSPEED","",true)=="125 KB/s","Volume e rede chegam à galeria");
  Check(Text(r,"APM","",false)=="PM"&&Text(r,"TIME","h:m:s",false)=="14:05:06","Calendário mantém formatos do tema");
  Check(Text(r,"Weather","",false)=="—"&&Text(r,"FPS","",true)=="—","Temas não mostram amostras gravadas como leituras atuais");
  r.Values["CPUCLOCK_G"]=double.PositiveInfinity;Check(Text(r,"CPUCLOCK_G","",true)=="—","Galeria rejeita valores não finitos");
  var settings=new Settings{Mode="Galeria de temas",LibraryThemeId="aqua",ThemeSlideshow=true,ThemeIntervalSeconds=10};var playback=new ThemePlayback(new[]{"aqua","brasa","oceano"});DateTime now=DateTime.UtcNow;
  Check(playback.Select(settings,now)=="aqua"&&playback.Select(settings,now.AddSeconds(9))=="aqua","Reprodução respeita intervalo");
  Check(playback.Select(settings,now.AddSeconds(10))=="brasa"&&playback.Select(settings,now.AddSeconds(20))=="oceano"&&playback.Select(settings,now.AddSeconds(30))=="aqua","Reprodução percorre e repete a biblioteca");
  settings.RandomThemes=true;Check(playback.Select(settings,now.AddSeconds(40))!="aqua","Ordem aleatória evita repetição imediata");
  settings.LibraryThemeId="oceano";Check(playback.Select(settings,now.AddSeconds(41))=="oceano","Escolha manual reinicia reprodução no tema escolhido");
  settings.ThemeSlideshow=false;Check(playback.Select(settings,now.AddSeconds(60))=="oceano","Desativar alternância mantém escolha manual");
  Check(Weather.Describe(0)=="Céu limpo"&&Weather.Describe(95)=="Trovoadas"&&Weather.Describe(null)=="Clima","Códigos meteorológicos são apresentados em português");
  return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
