using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

namespace AquaLite {
    public sealed class EngineState : IDisposable {
        public Bitmap Preview;
        public Reading Reading;
        public bool Connected;
        public string Status, Firmware;
        public string ThemeId;
        public long Frames, Bytes;
        public double FrameMilliseconds;
        public void Dispose() { if (Preview != null) Preview.Dispose(); }
    }
    public sealed class DisplayEngine : IDisplayEngine {
        readonly object gate = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Thread thread;
        Settings settings;
        volatile bool stopping, wanted;
        EngineState state = new EngineState {Reading=new Reading(), Status="Preparando a prévia..."};
        int revision;
        public DisplayEngine(Settings settings, bool connect) {
            this.settings=settings.Copy(); wanted=connect;
            thread = new Thread(Run); thread.Name="AquaLite.Tela"; thread.IsBackground=true; thread.Start();
        }
        public void Update(Settings value) { lock(gate) {settings=value.Copy(); revision++;} wake.Set(); }
        public void SetConnection(bool enabled) { wanted=enabled; wake.Set(); }
        public bool Wanted { get {return wanted;} }
        public EngineState Snapshot(bool includeBitmap) {
            lock(gate) {
                return new EngineState { Preview = includeBitmap && state.Preview!=null ? (Bitmap)state.Preview.Clone() : null, Reading=state.Reading,
                    Status=state.Status,Connected=state.Connected,Firmware=state.Firmware,ThemeId=state.ThemeId, Frames=state.Frames,Bytes=state.Bytes,FrameMilliseconds=state.FrameMilliseconds };
            }
        }
        void Log(string message) {
            try { Directory.CreateDirectory(Settings.Folder); string path=Path.Combine(Settings.Folder,"diagnostico.log");
                if(File.Exists(path) && new FileInfo(path).Length>256*1024) File.Move(path,path+"."+DateTime.Now.ToString("yyyyMMddHHmmss")+".bak");
                File.AppendAllText(path,DateTime.Now.ToString("s")+" "+message+Environment.NewLine);
            } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
        void Run() {
            using (ScreenRenderer renderer = new ScreenRenderer())
            using (BackgroundTelemetry telemetry = new BackgroundTelemetry(settings))
            using (UsbDisplay display = new UsbDisplay()) {
                DateTime nextRead=DateTime.MinValue,nextRetry=DateTime.MinValue;
                ThemePlayback playback=new ThemePlayback();
                NativeVideoStream native=null;LibraryRenderer overlay=null;
                string nativeKey="",overlayKey="";
                int previousRevision=-1,previousBrightness=-1;
                Reading reading=new Reading(); byte[] cachedJpeg=null;
                string lastKey=""; long frames=0,bytes=0; double frameTime=0;
                while(!stopping) {
                    Stopwatch cycle=Stopwatch.StartNew();
                    Settings current; int currentRevision;
                    lock(gate) {current=settings.Copy(); currentRevision=revision;}
                    current.LibraryThemeId=playback.Select(current,DateTime.UtcNow);
                    string status;
                    try {
                        if (DateTime.UtcNow>=nextRead||previousRevision!=currentRevision) {telemetry.Update(current);reading=telemetry.Snapshot(); nextRead=DateTime.UtcNow.AddMilliseconds(200);}
                        if (!wanted && display.IsOpen) {if(native!=null){native.Dispose();native=null;}if(overlay!=null){overlay.Dispose();overlay=null;}nativeKey="";display.EndSession();display.Dispose();}
                        renderer.Load(current.Mode=="Imagem ou GIF" || current.Mode=="Vídeo" || current.Mode=="Personalizado" && current.CustomBackground ? current.MediaPath : "");
                        string key=currentRevision+"/"+current.LibraryThemeId+"/"+(current.Mode=="Imagem ou GIF" && !renderer.Animated ? "estatico" : reading.Time.Ticks.ToString());
                        if(renderer.Animated) key += "/"+Environment.TickCount;
                        if(key!=lastKey || previousRevision!=currentRevision || (cachedJpeg==null&&native==null)) {
                            Stopwatch elapsed=Stopwatch.StartNew();
                            using(Bitmap bitmap=renderer.Render(current,reading)) {
                                if(native==null)cachedJpeg=renderer.Encode(bitmap,current.Performance=="Fluido"?86:80);
                                lock(gate) { if(state.Preview!=null)state.Preview.Dispose(); state.Preview=(Bitmap)bitmap.Clone();state.ThemeId=current.LibraryThemeId; }
                            }
                            frameTime=elapsed.Elapsed.TotalMilliseconds; lastKey=key; previousRevision=currentRevision;
                        }
                        if (wanted && !display.IsOpen && DateTime.UtcNow>=nextRetry) {
                            display.Open(); display.Query(); display.Prepare(); display.ClearOverlay(ScreenRenderer.EmptyOverlay());previousBrightness=-1;
                            Log("Tela conectada; canais="+display.PipeDescription);
                        }
                        if(display.IsOpen && wanted) {
                            string requestedNative=current.Mode+"/"+current.LibraryThemeId+"/"+current.Rotation;
                            if(nativeKey!=requestedNative){
                                if(native!=null){native.Dispose();native=null;cachedJpeg=null;display.Prepare();display.ClearOverlay(ScreenRenderer.EmptyOverlay());}
                                if(overlay!=null){overlay.Dispose();overlay=null;}
                                NativeThemePlan plan=NativeThemePlan.Resolve(current);
                                if(plan!=null){native=new NativeVideoStream(display,plan.Path,plan.FrameRate);overlay=new LibraryRenderer();overlay.Load(current.LibraryThemeId,true);}
                                nativeKey=requestedNative;overlayKey="";
                            }
                            if(previousBrightness!=current.Brightness) {display.Brightness(current.Brightness);previousBrightness=current.Brightness;}
                            if(native!=null){
                                if(native.Error!=null)throw new IOException(native.Error);
                                string nextOverlay=currentRevision+"/"+reading.Time.Ticks;
                                if(nextOverlay!=overlayKey){byte[] png=overlay.Overlay(reading,current.Rotation);display.ClearOverlay(png);overlayKey=nextOverlay;}
                            }else{if(cachedJpeg==null)using(Bitmap frame=renderer.Render(current,reading))cachedJpeg=renderer.Encode(frame,86);display.SendJpeg(cachedJpeg);frames++;bytes+=cachedJpeg.Length+512;}
                            status="Tela conectada e atualizada";
                        } else status=wanted?"Reconectando à tela...":"Prévia local • envio pausado";
                    } catch(Exception e) {
                        if(native!=null){try{native.Dispose();}catch{}native=null;}if(overlay!=null){overlay.Dispose();overlay=null;}nativeKey="";
                        display.Dispose();
                        status=e.Message; nextRetry=DateTime.UtcNow.AddSeconds(5);
                        Log(e.GetType().Name+": "+e.Message);
                        if(!current.Reconnect)wanted=false;
                    }
                    lock(gate) {
                        state.Reading=reading;state.Status=status;state.Connected=display.IsOpen;state.Firmware=display.Firmware;
                        state.Frames=frames;state.Bytes=bytes;state.FrameMilliseconds=frameTime;
                    }
                    int delay=renderer.Animated ? PlaybackTiming.Remaining(current.Performance,cycle.Elapsed.TotalMilliseconds) : 1000;
                    wake.WaitOne(delay);
                }
                if(native!=null)try{native.Dispose();}catch(Exception error){Log("Encerramento do vídeo: "+error.Message);}
                if(overlay!=null)overlay.Dispose();
                if(display.IsOpen)try{display.EndSession();}catch(Exception error){Log("Encerramento da sessão: "+error.Message);}
            }
        }
        public void Dispose() {
            stopping=true;wake.Set();
            if(thread.Join(6000)) {wake.Dispose();lock(gate)state.Dispose();}
        }
    }
}
