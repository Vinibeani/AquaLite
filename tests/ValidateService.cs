using System;
using System.Drawing;
using System.IO;
using System.Threading;
using AquaLite;

class ValidateService {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("OK: "+message);}
    static ServiceReply Snapshot(string pipe,bool preview){return ServiceClient.Send(new ServiceRequest{Operation="snapshot",IncludePreview=preview},pipe);}
    static int Main(){string folder=Path.Combine(Path.GetTempPath(),"AquaLite-ServiceTest-"+Guid.NewGuid().ToString("N"));try{
        Directory.CreateDirectory(folder);string config=Path.Combine(folder,"configuracao.json"),pipe="AquaLite.Test."+Guid.NewGuid().ToString("N");
        var settings=new Settings{Mode="Galeria de temas",LibraryThemeId="aqua",ThemeSlideshow=false,LocalSensorsEnabled=false,WeatherEnabled=false};
        using(var runtime=new ServiceRuntime(settings,false,pipe,config,true)){
            ServiceReply reply=null;DateTime until=DateTime.UtcNow.AddSeconds(8);
            while(DateTime.UtcNow<until){reply=Snapshot(pipe,true);if(!string.IsNullOrEmpty(reply.PreviewPng))break;Thread.Sleep(80);}
            Check(reply!=null&&!reply.Connected&&!reply.Wanted,"Motor do teste funciona sem abrir USB");
            Check(!string.IsNullOrEmpty(reply.PreviewPng),"Prévia atravessa a comunicação entre painel e serviço");
            using(var bytes=new MemoryStream(Convert.FromBase64String(reply.PreviewPng)))using(var image=new Bitmap(bytes))Check(image.Width==480&&image.Height==480,"Prévia recebida continua com 480 × 480 pixels");
            var changed=settings.Copy();changed.Caption="Teste & serviço";changed.LibraryThemeId="brasa";
            ServiceClient.Send(new ServiceRequest{Operation="save",Settings=changed},pipe);
            Check(runtime.CurrentSettings.Caption==changed.Caption&&Settings.LoadFrom(config).LibraryThemeId=="brasa","Serviço aplica e salva a configuração recebida");
            bool rejected=false;try{ServiceClient.Send(new ServiceRequest{Operation="comando-invalido"},pipe);}catch(IOException){rejected=true;}
            Check(rejected&&Snapshot(pipe,false)!=null,"Comando inválido é rejeitado sem interromper o serviço");
            var invalid=changed.Copy();invalid.Widgets.Add(null);rejected=false;
            try{ServiceClient.Send(new ServiceRequest{Operation="update",Settings=invalid},pipe);}catch(IOException){rejected=true;}
            Check(rejected&&runtime.CurrentSettings.Widgets.Count==changed.Widgets.Count,"Configuração inválida não substitui o estado válido");
            using(var client=new RemoteDisplayEngine(changed,pipe,false)){
                until=DateTime.UtcNow.AddSeconds(6);bool ready=false;
                while(DateTime.UtcNow<until){using(var state=client.Snapshot(true))ready=state.Reading!=null&&state.Preview!=null;if(ready)break;Thread.Sleep(80);}
                Check(ready,"Painel remoto recebe dados e imagem sem criar outro motor USB");
                changed.Caption="Persistido pelo painel";client.Update(changed);client.Save(changed);
            }
            Check(Snapshot(pipe,false)!=null&&Settings.LoadFrom(config).Caption=="Persistido pelo painel","Fechar painel preserva serviço ativo e última configuração");
        }
        using(var restarted=new ServiceRuntime(Settings.LoadFrom(config),false,pipe,config,true))Check(restarted.CurrentSettings.Caption=="Persistido pelo painel","Motor reiniciado recupera a última configuração sem depender de janela");
        Check(ServiceRegistration.BinaryCommand(@"C:\Program Files\AquaLite\AquaLite.exe")=="\"C:\\Program Files\\AquaLite\\AquaLite.exe\" --service","Registro de inicialização protege caminho com espaços e seleciona modo de serviço");
        bool oversize=false;using(var input=new MemoryStream(BitConverter.GetBytes(ServiceWire.MaximumBytes+1)))try{ServiceWire.Read<ServiceRequest>(input);}catch(InvalidDataException){oversize=true;}
        Check(oversize,"Mensagens acima do limite são rejeitadas antes da alocação");
        return 0;
    }catch(Exception error){Console.WriteLine(error);string log=Path.Combine(folder,"servico.log");if(File.Exists(log))Console.WriteLine(File.ReadAllText(log));return 1;}
    finally{if(Directory.Exists(folder)){string resolved=Path.GetFullPath(folder);if(resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(resolved).StartsWith("AquaLite-ServiceTest-",StringComparison.Ordinal))Directory.Delete(resolved,true);}}
    }
}
