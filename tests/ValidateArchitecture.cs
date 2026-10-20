using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using AquaLite;

class ValidateArchitecture {
    static void Check(bool value,string name) { if(!value)throw new Exception(name);Console.WriteLine("OK: "+name); }
    static void Packet(byte command,byte[] arguments,string expected) {
        byte[] bytes=DisplayProtocol.Header(command,arguments,123456);
        using(var hash=SHA256.Create())Check(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()==expected,"Comando "+command+" preserva os 512 bytes da versão 0.3.1");
    }
    static int Main() { try {
        // Golden packets captured from the previous executable, before extracting the transport.
        Packet(10,new byte[0],"a5fb99597c3b8405373861725bd51adc9bae741e024a5a746e1c0ff637d1f693");
        Packet(14,new byte[]{165},"f354e3c10562b98fb3dba5cb552a90ed3f30ee844be1b02657ad06cba6068887");
        Packet(51,new byte[]{7,234,10,20,12,30,45,2},"232cb80eedc0018b403312a2d1e9c8f043e0d9f0277e2702d21e19949c3d7f82");
        Packet(101,new byte[]{0,0,16,0},"14e2b5bf866b71d381cf364444f2dffcf4b1a2ee693ed940b29ffc59c0ba7e5e");
        Packet(102,new byte[]{0,0,8,0},"130248f5e05227a091b92d2f450bc4f83708bbab3ae800ed68cf617907d69762");
        Packet(121,new byte[]{0,3,24,0,1},"f8fb51659b6a867cec7936d409d957d3b5ddc95006292683e82d33df7c63a266");
        var original=new Reading{Cpu=35,CpuTemperature=48,Time=new DateTime(2026,10,20,12,30,0),WeatherText="Céu limpo"};
        original.Values["CPUTEMP"]=48;
        original.Sensors.Add(new SensorReading{Id="cpu",Value=48,Name="CPU",Time=original.Time});
        Reading first=original.Copy(),second=original.Copy();
        first.Values["CPUTEMP"]=99;first.Sensors[0].Value=99;first.Sensors.Add(new SensorReading{Id="extra"});first.CpuTemperature=99;
        Check(original.Values["CPUTEMP"]==48&&second.Values["CPUTEMP"]==48,"Snapshots não compartilham dicionários de leituras");
        Check(original.Sensors.Count==1&&original.Sensors[0].Value==48&&second.Sensors[0].Value==48,"Snapshots não compartilham a lista nem objetos de sensores");
        Check(second.Cpu==35&&second.CpuTemperature==48&&second.Time==original.Time&&second.WeatherText==original.WeatherText,"Cópia conserva valores e horário da amostra");
        byte[] overlay=ScreenRenderer.EmptyOverlay();
        using(var stream=new MemoryStream(overlay))using(var image=new Bitmap(stream))Check(image.Width==480&&image.Height==480&&image.GetPixel(240,240).A==0,"Renderizador produz camada PNG transparente de 480 × 480");
        using(var display=new UsbDisplay()) {
            bool rejected=false;try{display.BeginH264(15,null);}catch(ArgumentException){rejected=true;}
            Check(rejected&&!display.IsOpen,"Vídeo rejeita camada ausente antes de acessar USB");
        }
        Check(Settings.PathName==Path.Combine(Settings.Folder,"configuracao.json"),"Arquivo de preferências permanece na pasta local do usuário");
        return 0;
    }catch(Exception error){Console.WriteLine(error);return 1;} }
}
