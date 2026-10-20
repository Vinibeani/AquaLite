using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
namespace AquaLite {
 public sealed class NativeVideoStream : IDisposable {
  readonly UsbDisplay display;
  readonly Thread worker;
  readonly ManualResetEvent stop=new ManualResetEvent(false);
  readonly string path;readonly int blockSize;
  volatile bool stopping;
  volatile string error;
  public string Error {get{return error;}}
  public NativeVideoStream(UsbDisplay display,string path,int frameRate){
   if(!File.Exists(path)||new FileInfo(path).Length==0)throw new IOException("O vídeo do tema não foi encontrado.");
   this.display=display;this.path=path;blockSize=display.BeginH264(frameRate,ScreenRenderer.EmptyOverlay());
   worker=new Thread(Run){IsBackground=true,Name="AquaLite.VideoLCD"};worker.Start();
  }
  void Run(){try{
   using(var stream=File.OpenRead(path)){
    byte[] buffer=new byte[blockSize];
    while(!stopping){int count=0;while(count<buffer.Length){int n=stream.Read(buffer,count,buffer.Length-count);if(n==0)break;count+=n;}
     if(count==0){stream.Position=0;continue;}
     byte[] chunk=buffer;if(count!=buffer.Length){chunk=new byte[count];Buffer.BlockCopy(buffer,0,chunk,0,count);}
     int queue=display.SendH264(chunk,stream.Position==stream.Length);
     if(stop.WaitOne(30))break;
     if(queue>3){var timeout=Stopwatch.StartNew();do{if(stop.WaitOne(50))return;queue=display.VideoQueue();if(timeout.Elapsed.TotalSeconds>10)throw new IOException("A reprodução na tela parou de responder.");}while(queue>2);}
     if(stream.Position==stream.Length)stream.Position=0;
    }
   }
  }catch(Exception failure){if(!stopping)error=failure.Message;}}
  public void Dispose(){if(stopping)return;stopping=true;stop.Set();if(worker.Join(5000))stop.Dispose();display.StopH264();}
 }
}
