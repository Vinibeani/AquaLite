using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AquaLite {
    static class Program {
        [STAThread]
        static int Main(string[] args) {
            if(args.Length==1&&args[0]=="--service"){AppPaths.ServiceProcess=true;System.ServiceProcess.ServiceBase.Run(new DisplayService());return 0;}
            if(args.Length==1&&args[0]=="--sensor-host"){CpuBridge.Host();return 0;}
            if(args.Length==1&&(args[0]=="--install-service"||args[0]=="--uninstall-service"||args[0]=="--stop-service")){
                try{if(args[0]=="--install-service")ServiceRegistration.Install(System.Reflection.Assembly.GetExecutingAssembly().Location);else if(args[0]=="--stop-service")ServiceRegistration.Stop();else ServiceRegistration.Uninstall();return 0;}
                catch(Exception error){try{Directory.CreateDirectory(AppPaths.SharedFolder);File.WriteAllText(Path.Combine(AppPaths.SharedFolder,"instalacao-erro.txt"),error.ToString());}catch{}return 1;}
            }
            if(args.Length==1&&args[0]=="--shutdown"){try{using(var signal=EventWaitHandle.OpenExisting(@"Local\AquaLite.Exit"))signal.Set();}catch(WaitHandleCannotBeOpenedException){}return 0;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==1&&args[0]=="--setup-sensor"){try{CpuBridge.Configure();return 0;}catch(Exception e){MessageBox.Show(e.Message,"AquaLite • Sensor da CPU");return 1;}}
            if(args.Length==2&&args[0]=="--usb-menu") {
                try {string message=UsbMenu.Apply(args[1]=="hide");File.WriteAllText(Path.Combine(Settings.Folder,"resultado-menu-usb.txt"),message);MessageBox.Show(message,"AquaLite • Menu USB",MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;}
                catch(Exception error){Directory.CreateDirectory(Settings.Folder);File.WriteAllText(Path.Combine(Settings.Folder,"resultado-menu-usb.txt"),error.ToString());MessageBox.Show(error.Message,"AquaLite • Ajuste USB não aplicado",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
            }
            if(args.Length==2&&args[0]=="--device-identity") {
                try {string message=DeviceIdentity.Apply(args[1]=="restore",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","aqualite.ico"));MessageBox.Show(message,"AquaLite • Identidade da tela",MessageBoxButtons.OK,MessageBoxIcon.Information);return 0;}
                catch(Exception error){MessageBox.Show(error.Message,"AquaLite • Identidade não atualizada",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
            }
            bool preview=args.Length==2 && (args[0]=="--preview"||args[0]=="--preview-editor");
            if(preview) {
                using(MainForm form=new MainForm(new Settings(),true,false)) {
                    form.Show();DateTime until=DateTime.UtcNow.AddSeconds(3);
                    while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(25);}
                    form.UpdateState();Application.DoEvents();
                    if(args[0]=="--preview-editor")form.OpenEditorForPreview(args[1]);
                    else using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}
                }
                return 0;
            }
            bool created;
            using(Mutex mutex=new Mutex(true,@"Local\AquaLite.Instance",out created))
            using(EventWaitHandle show=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\AquaLite.Show"))
            using(EventWaitHandle shutdown=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\AquaLite.Exit")) {
                if(!created){show.Set();return 0;}
                Settings config;string warning=null;
                try {config=Settings.Load();}catch(Exception e){config=new Settings();warning="Não foi possível ler suas preferências. Iniciamos com os valores padrão. O arquivo anterior foi preservado.\n\n"+e.Message;}
                if(Array.IndexOf(args,"--sensors")>=0)config.LocalSensorsEnabled=true;
                int themeArgument=Array.IndexOf(args,"--theme");
                if(themeArgument>=0&&themeArgument+1<args.Length){LibraryTheme.Load(args[themeArgument+1]);config.Mode="Galeria de temas";config.LibraryThemeId=args[themeArgument+1];}
                bool background=Array.IndexOf(args,"--background")>=0;
                using(MainForm form=new MainForm(config,false,background)) {
                    form.StartupError=warning;
                    RegisteredWaitHandle listener=ThreadPool.RegisterWaitForSingleObject(show,delegate {
                        if(!form.IsDisposed&&form.IsHandleCreated)try{form.BeginInvoke((Action)form.ShowControls);}catch(InvalidOperationException){}
                    },null,-1,false);
                    RegisteredWaitHandle exitListener=ThreadPool.RegisterWaitForSingleObject(shutdown,delegate{
                        if(!form.IsDisposed&&form.IsHandleCreated)try{form.BeginInvoke((Action)form.RequestExit);}catch(InvalidOperationException){}
                    },null,-1,false);
                    Application.Run(form);listener.Unregister(null);exitListener.Unregister(null);
                }
                mutex.ReleaseMutex();
            }
            return 0;
        }
    }
}
