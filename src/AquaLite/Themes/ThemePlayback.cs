using System;
using System.IO;
using System.Linq;
namespace AquaLite {
 public sealed class ThemePlayback {
  readonly string[] themes;
  readonly Random random=new Random();
  string current,selected;DateTime next=DateTime.MinValue,last=DateTime.MinValue;bool enabled;int interval;
  public ThemePlayback():this(Directory.Exists(LibraryTheme.Root)?Directory.GetDirectories(LibraryTheme.Root).Where(p=>File.Exists(Path.Combine(p,"theme.json"))).Select(Path.GetFileName).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray():new string[0]){}
  public ThemePlayback(string[] themes){this.themes=(themes??new string[0]).Where(p=>!string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
  public string Select(Settings settings,DateTime now){
   if(!settings.ThemeSlideshow||settings.Mode!="Galeria de temas"){enabled=false;return settings.LibraryThemeId;}
   int requestedInterval=Math.Max(10,Math.Min(3600,settings.ThemeIntervalSeconds));
   if(!enabled||selected!=settings.LibraryThemeId){enabled=true;selected=settings.LibraryThemeId;current=selected;interval=requestedInterval;last=now;next=now.AddSeconds(interval);return current;}
   // Apply interval changes immediately, without resetting the selected theme on every Settings copy.
   if(interval!=requestedInterval){DateTime started=next.AddSeconds(-interval);interval=requestedInterval;next=started.AddSeconds(interval);}
   if(now<last)next=now.AddSeconds(interval);last=now;
   if(now>=next){int index=Array.FindIndex(themes,p=>string.Equals(p,current,StringComparison.OrdinalIgnoreCase));if(themes.Length>1){if(index<0)current=themes[0];else {int offset=settings.RandomThemes?random.Next(1,themes.Length):1;current=themes[(index+offset)%themes.Length];}}else if(themes.Length==1)current=themes[0];next=now.AddSeconds(interval);}
   return current;
  }
 }
}
