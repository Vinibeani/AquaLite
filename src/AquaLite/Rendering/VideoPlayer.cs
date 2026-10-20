using System;using System.Diagnostics;using System.Drawing;using System.Drawing.Imaging;using System.IO;using System.Runtime.InteropServices;using System.Threading;
namespace AquaLite {
 public sealed class VideoPlayer:IDisposable {
  readonly object gate=new object();Process process;Thread worker;Bitmap latest;volatile bool stopping;string error;long serial;
  public long Serial {get{return Interlocked.Read(ref serial);}}
  public VideoPlayer(string path){
   string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"media","decoder.exe");if(!File.Exists(exe))throw new IOException("O componente de vídeo não foi encontrado. Mantenha a pasta media junto do AquaLite.");
   var start=new ProcessStartInfo(exe,"-nostdin -hide_banner -loglevel error -threads 1 -re -stream_loop -1 -i \""+path+"\" -an -sn -vf scale=480:480:flags=bilinear -r 30 -fps_mode cfr -pix_fmt bgr24 -f rawvideo pipe:1"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   process=new Process{StartInfo=start};process.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs e){if(!string.IsNullOrEmpty(e.Data))error=e.Data;};process.Start();process.BeginErrorReadLine();
   worker=new Thread(Read){IsBackground=true,Name="AquaLite.Video"};worker.Start();DateTime until=DateTime.UtcNow.AddSeconds(6);while(Serial==0&&DateTime.UtcNow<until&&!process.HasExited)Thread.Sleep(10);if(Serial==0){string reason=error;Dispose();throw new IOException("Não foi possível reproduzir o vídeo. "+reason);}
  }
  void Read(){byte[] bytes=new byte[480*480*3];try{Stream input=process.StandardOutput.BaseStream;while(!stopping){int offset=0;while(offset<bytes.Length&&!stopping){int n=input.Read(bytes,offset,bytes.Length-offset);if(n==0)return;offset+=n;}if(stopping)return;var frame=new Bitmap(480,480,PixelFormat.Format24bppRgb);var data=frame.LockBits(new Rectangle(0,0,480,480),ImageLockMode.WriteOnly,PixelFormat.Format24bppRgb);try{Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{frame.UnlockBits(data);}lock(gate){var old=latest;latest=frame;if(old!=null)old.Dispose();}Interlocked.Increment(ref serial);}}catch(IOException){}catch(ObjectDisposedException){}catch(InvalidOperationException){}}
  public Bitmap Snapshot(){lock(gate)return latest==null?null:(Bitmap)latest.Clone();}
  public void Dispose(){stopping=true;if(process!=null){try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}if(worker!=null)worker.Join(1500);process.Dispose();process=null;}lock(gate){if(latest!=null)latest.Dispose();latest=null;}}
 }
}
