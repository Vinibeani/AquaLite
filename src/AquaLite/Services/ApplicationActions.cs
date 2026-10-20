using System;
using System.Diagnostics;
using System.IO;

namespace AquaLite {
    public static class ApplicationActions {
        public static void ActivateHardwareReader(string executable) {
            if(AppPaths.InstalledMode){CpuBridge.InstallAccess();ServiceClient.Send(new ServiceRequest{Operation="sensors"});return;}
            using(var bridge=new CpuBridge())if(bridge.ReadSnapshot()!=null)return;
            StartElevated(executable,"--setup-sensor");
        }
        public static void AdjustUsb(string executable,bool hide) {
            StartElevated(executable,"--usb-menu "+(hide?"hide":"restore"));
        }
        public static void AdjustIdentity(string executable,bool restore) {
            StartElevated(executable,"--device-identity "+(restore?"restore":"apply"));
        }
        static void StartElevated(string executable,string arguments) {
            Process.Start(new ProcessStartInfo(executable,arguments){UseShellExecute=true,Verb="runas"});
        }
        public static void OpenFiles() {
            Directory.CreateDirectory(Settings.Folder);
            Process.Start("explorer.exe",Settings.Folder);
        }
        public static string ImportMedia(string source,string prefix) {
            using(var renderer=new ScreenRenderer())renderer.ValidateMedia(source);
            string folder=Path.Combine(Settings.Folder,"midia");Directory.CreateDirectory(folder);
            string target=Path.Combine(folder,prefix+Guid.NewGuid().ToString("N")+Path.GetExtension(source).ToLowerInvariant());
            File.Copy(source,target);
            return target;
        }
    }
}
