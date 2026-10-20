using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;

namespace AquaLite {
    public sealed class DisplayService:ServiceBase {
        ServiceRuntime runtime;
        public DisplayService(){ServiceName=ServiceRegistration.Name;CanStop=true;CanShutdown=true;AutoLog=true;}
        protected override void OnStart(string[] args){
            Settings config;
            try{config=Settings.Load();}catch(Exception error){
                Directory.CreateDirectory(Settings.Folder);File.AppendAllText(Path.Combine(Settings.Folder,"servico.log"),DateTime.Now.ToString("s")+" Configuração não lida: "+error.Message+Environment.NewLine);
                try{config=Settings.LoadFrom(Settings.PathName+".bak");}catch{config=new Settings{Mode="Galeria de temas",LibraryThemeId="aqua",LocalSensorsEnabled=true};}
            }
            runtime=new ServiceRuntime(config,true,ServiceClient.PipeName,Settings.PathName,false);
        }
        protected override void OnStop(){if(runtime!=null){runtime.Dispose();runtime=null;}}
        protected override void OnShutdown(){OnStop();base.OnShutdown();}
    }
    [DataContract] public sealed class ServiceHealth {
        [DataMember] public DateTime TimeUtc;
        [DataMember] public int ProcessId,SessionId;
        [DataMember] public bool Connected;
        [DataMember] public string Status,ThemeId;
        [DataMember] public double? CpuTemperature;
        [DataMember] public long Frames;
    }
    public sealed class ServiceRuntime:IDisposable {
        readonly object gate=new object(),pipeGate=new object();
        readonly DisplayEngine engine;
        readonly Thread listener,health;
        readonly ManualResetEvent stop=new ManualResetEvent(false);
        readonly string pipeName,configPath;
        readonly bool testing;
        Settings settings;
        NamedPipeServerStream activePipe;
        volatile bool stopping;
        public Settings CurrentSettings {get{lock(gate)return settings.Copy();}}
        public ServiceRuntime(Settings initial,bool connect,string pipeName,string configPath,bool testing) {
            this.settings=initial.Copy();this.pipeName=pipeName;this.configPath=configPath;this.testing=testing;
            engine=new DisplayEngine(settings,connect);
            listener=new Thread(Listen){IsBackground=true,Name="AquaLite.Comandos"};
            health=new Thread(Report){IsBackground=true,Name="AquaLite.Estado"};listener.Start();health.Start();
        }
        PipeSecurity Security() {
            var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid,null),PipeAccessRights.FullControl,AccessControlType.Deny));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
            if(testing)security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
            return security;
        }
        void Listen() {
            while(!stopping)try {
                using(var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,8192,8192,Security())) {
                    lock(pipeGate){if(stopping)return;activePipe=pipe;}
                    pipe.WaitForConnection();
                    ServiceReply reply;
                    try{reply=Handle(ServiceWire.Read<ServiceRequest>(pipe));}
                    catch(Exception error){reply=new ServiceReply{Error=error.Message};}
                    ServiceWire.Write(pipe,reply);
                    lock(pipeGate){activePipe=null;}
                }
            }catch(Exception error){if(!stopping){Log(error);stop.WaitOne(150);}}
        }
        void Apply(Settings value) {
            if(value==null||value.Widgets==null||value.Widgets.Count>40)throw new ArgumentException("Configuração de tela inválida.");
            if(value.LibraryThemeId==null||Path.GetFileName(value.LibraryThemeId)!=value.LibraryThemeId)throw new ArgumentException("Tema inválido.");
            if(value.Caption!=null&&value.Caption.Length>256)throw new ArgumentException("Nome muito longo.");
            foreach(var widget in value.Widgets)if(widget==null)throw new ArgumentException("Elemento de tema inválido.");
            settings=value.Copy();engine.Update(settings);
        }
        ServiceReply Handle(ServiceRequest request) {
            if(request==null)throw new InvalidDataException("Comando vazio.");
            lock(gate) {
                switch(request.Operation) {
                    case "snapshot":break;
                    case "update":Apply(request.Settings);break;
                    case "save":if(request.Settings!=null)Apply(request.Settings);settings.SaveTo(configPath);break;
                    case "connection":engine.SetConnection(request.Enabled);break;
                    case "sensors":settings.LocalSensorsEnabled=true;engine.Update(settings);settings.SaveTo(configPath);break;
                    default:throw new InvalidDataException("Comando do serviço desconhecido.");
                }
            }
            using(var state=engine.Snapshot(request.IncludePreview)) {
                var reply=new ServiceReply{Reading=state.Reading.Copy(),Connected=state.Connected,Wanted=engine.Wanted,Status=state.Status,Firmware=state.Firmware,ThemeId=state.ThemeId,Frames=state.Frames,Bytes=state.Bytes,FrameMilliseconds=state.FrameMilliseconds};
                if(state.Preview!=null)using(var stream=new MemoryStream()){state.Preview.Save(stream,ImageFormat.Png);reply.PreviewPng=Convert.ToBase64String(stream.ToArray());}
                return reply;
            }
        }
        void Report() {
            while(!stopping) {
                try {
                    using(var state=engine.Snapshot(false))using(var process=Process.GetCurrentProcess()) {
                        var value=new ServiceHealth{TimeUtc=DateTime.UtcNow,ProcessId=process.Id,SessionId=process.SessionId,Connected=state.Connected,Status=state.Status,ThemeId=state.ThemeId,CpuTemperature=state.Reading.CpuTemperature,Frames=state.Frames};
                        string path=Path.Combine(Path.GetDirectoryName(configPath),"service-status.json");
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using(var stream=File.Create(path+".tmp"))new DataContractJsonSerializer(typeof(ServiceHealth)).WriteObject(stream,value);
                        if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
                    }
                }catch(Exception error){Log(error);}
                if(stop.WaitOne(2000))break;
            }
        }
        void Log(Exception error){try{File.AppendAllText(Path.Combine(Path.GetDirectoryName(configPath),"servico.log"),DateTime.Now.ToString("s")+" "+error.Message+Environment.NewLine);}catch(IOException){}catch(UnauthorizedAccessException){}}
        public void Dispose() {
            if(stopping)return;stopping=true;stop.Set();
            lock(pipeGate){if(activePipe!=null)activePipe.Dispose();}
            listener.Join(6000);health.Join(3000);engine.Dispose();
            if(!listener.IsAlive&&!health.IsAlive)stop.Dispose();
        }
    }
}
