using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
namespace AquaLite {
 public sealed class NativeThemePlan {
  public string Path;public int FrameRate;
  public static NativeThemePlan Resolve(Settings settings){
   if(settings.Mode!="Galeria de temas")return null;
   var theme=LibraryTheme.Load(settings.LibraryThemeId);if(string.IsNullOrEmpty(theme.NativeVideo))return null;
   string root=System.IO.Path.GetFullPath(LibraryTheme.Root)+System.IO.Path.DirectorySeparatorChar;
   string source=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,theme.Id,theme.NativeVideo));
   if(!source.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(source))throw new IOException("O vídeo deste tema não está disponível.");
   int fps=Math.Max(1,Math.Min(120,theme.VideoFrameRate));
   int rotation=((settings.Rotation%360)+360)%360;
   if(rotation==0)return new NativeThemePlan{Path=source,FrameRate=fps};
   string folder=System.IO.Path.Combine(Settings.Folder,"videos");Directory.CreateDirectory(folder);
   var file=new FileInfo(source);string target=System.IO.Path.Combine(folder,"tema-"+theme.Id+"-"+file.Length+"-"+file.LastWriteTimeUtc.Ticks+"-"+rotation+".h264");
   if(!File.Exists(target)){
    string temporary=target+".tmp";
    string decoder=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"media","decoder.exe");
    string radians=(rotation*Math.PI/180).ToString("0.########",CultureInfo.InvariantCulture);
    var start=new ProcessStartInfo(decoder,"-nostdin -hide_banner -loglevel error -r "+fps+" -i \""+source+"\" -an -vf \"rotate="+radians+":ow=480:oh=480:c=0x080e18\" -c:v libx264 -preset ultrafast -tune zerolatency -x264opts bframes=0:repeat-headers=1 -crf 18 -pix_fmt yuv420p -r "+fps+" -f h264 -y \""+temporary+"\""){UseShellExecute=false,CreateNoWindow=true};
    using(var process=Process.Start(start)){
     if(!process.WaitForExit(60000)){process.Kill();throw new IOException("Não foi possível ajustar o vídeo à rotação escolhida.");}
     if(process.ExitCode!=0)throw new IOException("Não foi possível ajustar o vídeo à rotação escolhida.");
    }
    File.Move(temporary,target);
   }
   return new NativeThemePlan{Path=target,FrameRate=fps};
  }
 }
}
