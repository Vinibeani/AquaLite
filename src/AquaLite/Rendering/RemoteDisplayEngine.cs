using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace AquaLite {
    public sealed class RemoteDisplayEngine:IDisplayEngine {
        readonly object gate=new object(),commands=new object();
        readonly ManualResetEvent wake=new ManualResetEvent(false);
        readonly Thread worker;
        readonly string pipeName;
        readonly bool startService;
        Settings pendingSettings,lastSettings;
        bool? pendingConnection;
        DateTime lastPreviewRequest=DateTime.MinValue;
        EngineState state=new EngineState{Reading=new Reading(),Status="Conectando ao serviço da tela..."};
        volatile bool stopping,wanted=true;
        public bool Wanted{get{return wanted;}}
        public RemoteDisplayEngine(Settings initial):this(initial,ServiceClient.PipeName,true){}
        public RemoteDisplayEngine(Settings initial,string pipeName,bool startService) {
            this.pipeName=pipeName;this.startService=startService;lastSettings=initial.Copy();pendingSettings=lastSettings;
            worker=new Thread(Run){IsBackground=true,Name="AquaLite.Painel"};worker.Start();
        }
        public void Update(Settings value){lock(gate){lastSettings=value.Copy();pendingSettings=lastSettings;}wake.Set();}
        public void SetConnection(bool enabled){lock(gate){pendingConnection=enabled;wanted=enabled;}wake.Set();}
        public void Save(Settings value) {
            lock(commands){lock(gate){lastSettings=value.Copy();pendingSettings=null;}ServiceClient.Send(new ServiceRequest{Operation="save",Settings=lastSettings},pipeName);}
        }
        public EngineState Snapshot(bool bitmap) {
            lock(gate) {
                if(bitmap)lastPreviewRequest=DateTime.UtcNow;
                return new EngineState{Reading=state.Reading,Preview=bitmap&&state.Preview!=null?(Bitmap)state.Preview.Clone():null,Connected=state.Connected,Status=state.Status,Firmware=state.Firmware,ThemeId=state.ThemeId,Frames=state.Frames,Bytes=state.Bytes,FrameMilliseconds=state.FrameMilliseconds};
            }
        }
        void Run() {
            DateTime nextStart=DateTime.MinValue;
            while(!stopping) {
                int delay=250;
                try {
                    if(startService&&DateTime.UtcNow>=nextStart){nextStart=DateTime.UtcNow.AddSeconds(10);WindowsStartup.EnsureRunning();}
                    lock(commands) {
                        Settings update;bool? connection;bool preview;
                        lock(gate){update=pendingSettings;pendingSettings=null;connection=pendingConnection;pendingConnection=null;preview=DateTime.UtcNow-lastPreviewRequest<TimeSpan.FromSeconds(1);}
                        if(update!=null)ServiceClient.Send(new ServiceRequest{Operation="update",Settings=update},pipeName);
                        if(connection.HasValue)ServiceClient.Send(new ServiceRequest{Operation="connection",Enabled=connection.Value},pipeName);
                        ServiceReply reply=ServiceClient.Send(new ServiceRequest{Operation="snapshot",IncludePreview=preview},pipeName);
                        Bitmap bitmap=null;
                        if(!string.IsNullOrEmpty(reply.PreviewPng))using(var stream=new MemoryStream(Convert.FromBase64String(reply.PreviewPng)))using(var decoded=new Bitmap(stream)){if(decoded.Width!=480||decoded.Height!=480)throw new IOException("Prévia inválida recebida do serviço.");bitmap=new Bitmap(decoded);}
                        lock(gate) {
                            if(bitmap!=null){if(state.Preview!=null)state.Preview.Dispose();state.Preview=bitmap;}
                            state.Reading=reply.Reading??new Reading();state.Connected=reply.Connected;state.Status=reply.Status;state.Firmware=reply.Firmware;state.ThemeId=reply.ThemeId;state.Frames=reply.Frames;state.Bytes=reply.Bytes;state.FrameMilliseconds=reply.FrameMilliseconds;wanted=reply.Wanted;
                        }
                        delay=preview?50:250;
                    }
                }catch(Exception error){lock(gate){pendingSettings=lastSettings.Copy();state.Connected=false;state.Reading=new Reading();state.Status="Serviço da tela indisponível: "+error.Message;}delay=1500;}
                wake.WaitOne(delay);wake.Reset();
            }
        }
        public void Dispose(){stopping=true;wake.Set();if(worker.Join(7000)){wake.Dispose();lock(gate)state.Dispose();}}
    }
}
