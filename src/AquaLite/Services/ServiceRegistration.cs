using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace AquaLite {
    public static class ServiceRegistration {
        public const string Name="AquaLiteDisplay";
        [StructLayout(LayoutKind.Sequential)] struct NativeStatus{public uint Type,State,Controls,ExitCode,SpecificExit,CheckPoint,WaitHint,ProcessId,Flags;}
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenSCManager(string machine,string database,uint access);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr OpenService(IntPtr manager,string name,uint access);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateService(IntPtr manager,string name,string display,uint access,uint type,uint start,uint error,string binary,string group,IntPtr tag,string dependencies,string account,string password);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool ChangeServiceConfig(IntPtr service,uint type,uint start,uint error,string binary,string group,IntPtr tag,string dependencies,string account,string password,string display);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool QueryServiceStatusEx(IntPtr service,int level,out NativeStatus status,uint size,out uint needed);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool DeleteService(IntPtr service);
        [DllImport("advapi32.dll")] static extern bool CloseServiceHandle(IntPtr handle);
        public static string BinaryCommand(string executable){return "\""+Path.GetFullPath(executable)+"\" --service";}
        public static bool IsInstalled{get{IntPtr manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero)return false;try{IntPtr service=OpenService(manager,Name,4);if(service==IntPtr.Zero)return false;CloseServiceHandle(service);return true;}finally{CloseServiceHandle(manager);}}}
        public static uint ProcessId {
            get{IntPtr manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero)return 0;
                try{IntPtr service=OpenService(manager,Name,4);if(service==IntPtr.Zero)return 0;
                    try{NativeStatus status;uint needed;return QueryServiceStatusEx(service,0,out status,(uint)Marshal.SizeOf(typeof(NativeStatus)),out needed)?status.ProcessId:0;}
                    finally{CloseServiceHandle(service);}}
                finally{CloseServiceHandle(manager);}}
        }
        public static void Install(string executable) {
            if(!File.Exists(executable))throw new FileNotFoundException("Executável do AquaLite não encontrado.",executable);
            InitializeData();
            string sensorInstaller=Path.Combine(Path.GetDirectoryName(executable),"sensors","PawnIO_setup.exe");
            if(File.Exists(sensorInstaller)&&!CpuSensor.Installed)CpuBridge.InstallAccess();
            IntPtr manager=OpenSCManager(null,null,3);if(manager==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"O Windows precisa autorizar a instalação do serviço.");
            try{
                IntPtr service=OpenService(manager,Name,0xf01ff);
                if(service==IntPtr.Zero){int error=Marshal.GetLastWin32Error();if(error!=1060)throw new Win32Exception(error);service=CreateService(manager,Name,"AquaLite — Tela do watercooler",0xf01ff,0x10,2,1,BinaryCommand(executable),null,IntPtr.Zero,null,"LocalSystem",null);}
                if(service==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
                try{if(!ChangeServiceConfig(service,0xffffffff,2,0xffffffff,BinaryCommand(executable),null,IntPtr.Zero,null,null,null,"AquaLite — Tela do watercooler"))throw new Win32Exception(Marshal.GetLastWin32Error());}
                finally{CloseServiceHandle(service);}
            }finally{CloseServiceHandle(manager);}
            using(var process=Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"sc.exe"),"failure "+Name+" reset= 86400 actions= restart/5000/restart/10000/restart/30000"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){process.StandardOutput.ReadToEnd();process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new IOException("Não foi possível configurar a recuperação do serviço.");}
            WindowsStartup.RemoveLegacyEntry();WindowsStartup.EnsureRunning();
        }
        static void InitializeData() {
            Directory.CreateDirectory(AppPaths.SharedFolder);
            string target=Path.Combine(AppPaths.SharedFolder,"configuracao.json");if(File.Exists(target))return;
            string previous=Path.Combine(AppPaths.UserFolder,"configuracao.json");
            Settings settings=File.Exists(previous)?Settings.LoadFrom(previous):new Settings{Mode="Galeria de temas",LibraryThemeId="aqua",LocalSensorsEnabled=true};
            if(!string.IsNullOrEmpty(settings.MediaPath)&&File.Exists(settings.MediaPath)){
                string folder=Path.Combine(AppPaths.SharedFolder,"midia");Directory.CreateDirectory(folder);
                string media=Path.Combine(folder,"importado-"+Guid.NewGuid().ToString("N")+Path.GetExtension(settings.MediaPath));File.Copy(settings.MediaPath,media);settings.MediaPath=media;
            }
            settings.Reconnect=true;settings.SaveTo(target);
        }
        public static void SetAutoStart(bool enabled) {
            IntPtr manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            try{IntPtr service=OpenService(manager,Name,2);if(service==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
                try{if(!ChangeServiceConfig(service,0xffffffff,enabled?2u:3u,0xffffffff,null,null,IntPtr.Zero,null,null,null,null))throw new Win32Exception(Marshal.GetLastWin32Error());}
                finally{CloseServiceHandle(service);}}
            finally{CloseServiceHandle(manager);}
        }
        public static void Stop() {
            if(!IsInstalled)return;
            using(var service=new ServiceController(Name)){service.Refresh();if(service.Status==ServiceControllerStatus.Stopped)return;if(service.Status!=ServiceControllerStatus.StopPending)service.Stop();service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(30));}
        }
        public static void Uninstall() {
            Stop();IntPtr manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            try{IntPtr service=OpenService(manager,Name,0x10000);if(service==IntPtr.Zero){if(Marshal.GetLastWin32Error()==1060)return;throw new Win32Exception(Marshal.GetLastWin32Error());}
                try{if(!DeleteService(service))throw new Win32Exception(Marshal.GetLastWin32Error());}finally{CloseServiceHandle(service);}}
            finally{CloseServiceHandle(manager);}
        }
    }
}
