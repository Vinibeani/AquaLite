using System;
using System.IO;
using System.ServiceProcess;
using Microsoft.Win32;

namespace AquaLite {
    public static class WindowsStartup {
        public static bool Enabled{get{bool? state=State;return state.HasValue&&state.Value;}}
        public static bool? State {
            get{try{using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+ServiceRegistration.Name)){if(key==null)return false;object value=key.GetValue("Start");return value==null?(bool?)null:Convert.ToInt32(value)==2;}}
                catch(System.Security.SecurityException){return null;}catch(UnauthorizedAccessException){return null;}catch(IOException){return null;}}
        }
        public static void SetEnabled(bool enabled, string executable) {
            if(!ServiceRegistration.IsInstalled)throw new InvalidOperationException("Instale o AquaLite para iniciar a tela antes do login.");
            ServiceRegistration.SetAutoStart(enabled);RemoveLegacyEntry();
        }
        public static void EnsureRunning() {
            using(var service=new ServiceController(ServiceRegistration.Name)){
                service.Refresh();if(service.Status==ServiceControllerStatus.Running)return;
                if(service.Status==ServiceControllerStatus.StopPending)service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(10));
                service.Refresh();if(service.Status==ServiceControllerStatus.Stopped)service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(15));
            }
        }
        public static void RemoveLegacyEntry(){using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true))if(key!=null)key.DeleteValue("AquaLite",false);}
    }
}
