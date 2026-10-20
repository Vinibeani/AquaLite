using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;

namespace AquaLite {
    [DataContract]
    public sealed class Settings {
        [DataMember] public string Mode = "Painel do PC";
        [DataMember] public string LibraryThemeId = "aqua";
        [DataMember] public string MediaPath = "";
        [DataMember] public bool WeatherEnabled,ActivityEnabled;
        [DataMember] public bool LocalSensorsEnabled;
        [DataMember] public bool ThemeSlideshow,RandomThemes;
        [DataMember] public int ThemeIntervalSeconds=60;
        [DataMember] public double WeatherLatitude,WeatherLongitude;
        [DataMember] public int Rotation = 0;
        [DataMember] public int Brightness = 65;
        [DataMember] public string Performance = "Fluido";
        [DataMember] public int PlaybackVersion=1;
        [DataMember] public string Accent = "#3BE0CE";
        [DataMember] public bool Crop = true;
        [DataMember] public bool KeepRunning = true;
        [DataMember] public bool Reconnect = true;
        [DataMember] public string Caption = "MEU PC";
        [DataMember] public bool CustomBackground;
        [DataMember] public List<Widget> Widgets = Widget.Defaults();
        public Settings Copy() {
            Settings copy=(Settings)MemberwiseClone();copy.Widgets=new List<Widget>();
            foreach(Widget item in Widgets??Widget.Defaults())copy.Widgets.Add(item.Copy());return copy;
        }
        public static string Folder { get { return AppPaths.DataFolder; } }
        public static string PathName { get { return Path.Combine(Folder, "configuracao.json"); } }
        public static Settings Load() {
            return LoadFrom(PathName);
        }
        public static Settings LoadFrom(string path) {
            if (!File.Exists(path)) return new Settings();
            using (FileStream stream = File.OpenRead(path)) {
                Settings value = (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
                value.Rotation = ((value.Rotation % 360) + 360) % 360;
                value.Brightness = Math.Max(0, Math.Min(100, value.Brightness));
                value.ThemeIntervalSeconds=Math.Max(10,Math.Min(3600,value.ThemeIntervalSeconds==0?60:value.ThemeIntervalSeconds));
                if (value.Mode != "Painel do PC" && value.Mode != "Relógio" && value.Mode != "Imagem ou GIF" && value.Mode != "Personalizado" && value.Mode != "Galeria de temas" && value.Mode!="Vídeo") value.Mode = "Painel do PC";
                if(value.PlaybackVersion<1){value.Performance="Fluido";value.PlaybackVersion=1;}
                if (value.Performance != "Econômico" && value.Performance != "Equilibrado" && value.Performance != "Fluido") value.Performance = "Fluido";
                if (string.IsNullOrWhiteSpace(value.Accent)) value.Accent = "#3BE0CE";
                if (value.Caption == null) value.Caption = "MEU PC";
                if (value.MediaPath == null) value.MediaPath = "";
                if(string.IsNullOrWhiteSpace(value.LibraryThemeId)||Path.GetFileName(value.LibraryThemeId)!=value.LibraryThemeId||!File.Exists(Path.Combine(LibraryTheme.Root,value.LibraryThemeId,"theme.json")))value.LibraryThemeId="aqua";
                if(value.Widgets==null)value.Widgets=Widget.Defaults();
                if(value.Widgets.Count>40)throw new SerializationException("O tema ultrapassa 40 elementos.");
                foreach(Widget widget in value.Widgets){widget.X=Math.Max(0,Math.Min(480,widget.X));widget.Y=Math.Max(0,Math.Min(480,widget.Y));widget.Size=Math.Max(10,Math.Min(140,widget.Size));widget.Width=Math.Max(20,Math.Min(460,widget.Width));}
                return value;
            }
        }
        public void Save() {
            SaveTo(PathName);
        }
        public void SaveTo(string path) {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";
            using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                new DataContractJsonSerializer(typeof(Settings)).WriteObject(stream, this);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        public static bool StartupEnabled { get { return WindowsStartup.Enabled; } }
        public static bool? StartupState { get { return WindowsStartup.State; } }
        public static void SetStartup(bool enabled,string executable) { WindowsStartup.SetEnabled(enabled,executable); }

    }
    [DataContract]
    public sealed class Widget {
        [DataMember] public string Kind="Texto";
        [DataMember] public string Text="Meu texto";
        [DataMember] public string SensorId="";
        [DataMember] public int X=240,Y=240,Size=32,Width=280;
        [DataMember] public string Color="#EAF4FA";
        [DataMember] public bool Visible=true,Bold=true;
        public Widget Copy(){return (Widget)MemberwiseClone();}
        public override string ToString(){return (Visible?"":"[oculto] ")+(Kind=="Texto"?Text:Kind);}
        public static List<Widget> Defaults(){return new List<Widget> {
            new Widget{Kind="Texto",Text="MEU PC",Y=90,Size=18,Color="#3BE0CE"},
            new Widget{Kind="Uso da CPU",Y=190,Size=58},
            new Widget{Kind="Barra da CPU",Y=250,Width=250,Size=12,Color="#3BE0CE"},
            new Widget{Kind="Uso da RAM",Y=302,Size=28},
            new Widget{Kind="Hora",Y=375,Size=32}
        };}
    }
}
