using System;
using System.IO;

namespace AquaLite {
    public static class AppPaths {
        public static bool ServiceProcess { get; internal set; }
        public static bool InstalledMode { get { return ServiceProcess||File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installed.mode")); } }
        public static string UserFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AquaLite"); } }
        public static string SharedFolder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"AquaLite"); } }
        public static string DataFolder { get { return InstalledMode?SharedFolder:UserFolder; } }
    }
}
