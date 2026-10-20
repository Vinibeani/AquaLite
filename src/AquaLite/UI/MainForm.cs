using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AquaLite {
    public sealed partial class MainForm : Form {
        Settings settings;
        IDisplayEngine engine;
        readonly Panel[] pages=new Panel[4];
        readonly Button[] nav=new Button[4];
        SensorPanel sensorPanel;
        ThemeBrowser themeBrowser;
        Panel sidebar;
        Button minimizeButton,closeButton;
        PreviewControl preview;
        Label status,details,resources,firmware,fileName,rotationText,brightnessText,heading,subtitle;
        ComboBox mode,performance;
        TrackBar rotation,brightness;
        NumericUpDown angle;
        TextBox caption;
        FlatButton connect,color;
        CheckBox startWindows,keepRunning,reconnect;
        readonly Timer uiTimer=new Timer();
        readonly Timer saveTimer=new Timer();
        NotifyIcon tray;
        ContextMenuStrip trayMenu;
        ToolStripMenuItem trayPause;
        bool initial=true,exitRequested,previewOnly;
        DateTime nextResourceUpdate=DateTime.MinValue;
        public string StartupError;
        public MainForm(Settings config,bool previewMode,bool background) {
            settings=config;previewOnly=previewMode;
            Text="AquaLite • Sua tela, do seu jeito";FormBorderStyle=FormBorderStyle.None;ClientSize=new Size(1180,810);MaximizeBox=false;
            StartPosition=FormStartPosition.CenterScreen;BackColor=Palette.Background;ForeColor=Palette.Text;Font=Palette.Font(10);
            AutoScaleMode=AutoScaleMode.Dpi;DoubleBuffered=true;ResizeRedraw=true;
            string icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","aqualite.ico");if(File.Exists(icon))Icon=new Icon(icon);
            BuildSidebar();BuildPages();BuildWindowControls();Resize+=delegate{LayoutWindow();};LayoutWindow();if(!previewOnly)BuildTray();SelectPage(0);initial=false;
            Size lockedSize=Size;MinimumSize=lockedSize;MaximumSize=lockedSize;
            saveTimer.Interval=650;saveTimer.Tick+=delegate {saveTimer.Stop();Persist();};
            uiTimer.Interval=100;uiTimer.Tick+=delegate {UpdateState();};
            Shown+=delegate {
                engine=previewOnly?new DisplayEngine(settings,false):(AppPaths.InstalledMode?(IDisplayEngine)new RemoteDisplayEngine(settings):new DisplayEngine(settings,true));uiTimer.Start();
                if(!previewOnly&&settings.LocalSensorsEnabled)BeginInvoke((Action)ActivateHardwareReader);
                if(background)Hide();
                if(!string.IsNullOrEmpty(StartupError))MessageBox.Show(this,StartupError,"Configuração recuperada",MessageBoxButtons.OK,MessageBoxIcon.Information);
            };
            FormClosing+=OnClosing;
            VisibleChanged+=delegate {uiTimer.Interval=Visible?100:2500;};
        }
        void ActivateHardwareReader(){
            try{ApplicationActions.ActivateHardwareReader(Application.ExecutablePath);
            }catch(System.ComponentModel.Win32Exception e){if(e.NativeErrorCode!=1223)MessageBox.Show(this,e.Message,"Sensores do PC");}
        }
        void AdjustUsb(bool hide) {
            try {ApplicationActions.AdjustUsb(Application.ExecutablePath,hide);}
            catch(System.ComponentModel.Win32Exception error){if(error.NativeErrorCode!=1223)MessageBox.Show(this,error.Message,"Permissão do Windows");}
        }
        void AdjustIdentity(bool restore) {
            try{ApplicationActions.AdjustIdentity(Application.ExecutablePath,restore);}
            catch(System.ComponentModel.Win32Exception error){if(error.NativeErrorCode!=1223)MessageBox.Show(this,error.Message,"Permissão do Windows");}
        }
        string MediaName() {return string.IsNullOrEmpty(settings.MediaPath)?"Nenhum arquivo escolhido":Path.GetFileName(settings.MediaPath);}
        public void OpenEditorForPreview(string output){
            using(ThemeEditor editor=new ThemeEditor(settings,delegate{using(EngineState state=engine.Snapshot(false))return state.Reading;})) {
                editor.Show();Application.DoEvents();using(Bitmap bitmap=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(bitmap,new Rectangle(Point.Empty,editor.Size));bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);}
            }
        }
        void EditCustom(object sender,EventArgs e){
            using(ThemeEditor editor=new ThemeEditor(settings,delegate{if(engine==null)return new Reading();using(EngineState state=engine.Snapshot(false))return state.Reading;})) {
                if(editor.ShowDialog(this)==DialogResult.OK){settings=editor.Draft.Copy();mode.SelectedItem="Personalizado";fileName.Text=MediaName();Changed();}
            }
        }
        void Browse(object sender,EventArgs e) {
            using(OpenFileDialog dialog=new OpenFileDialog {Title="Escolher imagem, GIF ou vídeo",Filter="Imagens e vídeos|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.mp4|Imagens|*.png;*.jpg;*.jpeg;*.gif;*.bmp|Vídeo MP4|*.mp4|Todos os arquivos|*.*"}) {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try {
                    string extension=Path.GetExtension(dialog.FileName).ToLowerInvariant();
                    string destination=ApplicationActions.ImportMedia(dialog.FileName,"imagem-");
                    settings.MediaPath=destination;settings.Mode=extension==".mp4"?"Vídeo":"Imagem ou GIF";mode.SelectedItem=settings.Mode;fileName.Text=MediaName();Changed();
                } catch(Exception error){MessageBox.Show(this,error.Message,"Não foi possível abrir a imagem",MessageBoxButtons.OK,MessageBoxIcon.Information);}
            }
        }
        void Export(object sender,EventArgs e) {
            if(engine==null)return;
            using(EngineState state=engine.Snapshot(true)) {
                if(state.Preview==null)return;
                using(SaveFileDialog dialog=new SaveFileDialog {Title="Salvar prévia da tela",FileName="Minha tela.png",Filter="Imagem PNG|*.png"})
                    if(dialog.ShowDialog(this)==DialogResult.OK)try{state.Preview.Save(dialog.FileName,System.Drawing.Imaging.ImageFormat.Png);}catch(Exception error){MessageBox.Show(this,error.Message,"Salvar prévia");}
            }
        }
        void Changed() {if(initial)return;if(engine!=null)engine.Update(settings);saveTimer.Stop();saveTimer.Start();}
        void Persist() {try{var remote=engine as RemoteDisplayEngine;if(remote!=null)remote.Save(settings);else settings.Save();}catch(Exception error){MessageBox.Show(this,"Não foi possível salvar as preferências: "+error.Message,"Configuração",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
        public void UpdateState() {
            if(engine==null)return;
            uiTimer.Interval=Visible?(pages[0].Visible?(settings.Performance=="Fluido"?33:settings.Performance=="Equilibrado"?50:100):900):2500;
            using(EngineState state=engine.Snapshot(Visible&&pages[0].Visible)) {
                if(state.Preview!=null){preview.SetImage(state.Preview);state.Preview=null;}
                status.Text=state.Status;status.ForeColor=state.Connected?Palette.Accent:Palette.Muted;
                details.Text="480 × 480 pixels • rotação de "+settings.Rotation+"°";
                connect.Text=engine.Wanted?"Pausar envio à tela":"Retomar envio à tela";
                Reading r=state.Reading;
                if(pages[2].Visible)sensorPanel.UpdateReading(r);
                firmware.Text=(string.IsNullOrEmpty(state.Firmware)?"Tela não conectada":"Tela LCD conectada")+"   •   "+r.TemperatureSource;
                if(DateTime.UtcNow>=nextResourceUpdate){nextResourceUpdate=DateTime.UtcNow.AddSeconds(2);resources.Text="AquaLite • "+(settings.Mode=="Galeria de temas"?(string.IsNullOrEmpty(state.ThemeId)?"Biblioteca de temas":"Tema "+state.ThemeId)+(settings.ThemeSlideshow?" • troca automática ligada":""):settings.Mode);}
            }
        }
        static string Temperature(double? value) {return value.HasValue?value.Value.ToString("0")+" °C":"Temperatura indisponível";}
        public void ShowControls() {if(IsDisposed)return;Show();WindowState=FormWindowState.Normal;Activate();UpdateState();}
        public void RequestExit(){exitRequested=true;Close();}
        void OnClosing(object sender,FormClosingEventArgs e) {
            if(e.CloseReason==CloseReason.UserClosing&&!exitRequested&&!previewOnly&&settings.KeepRunning) {e.Cancel=true;Persist();Hide();return;}
            uiTimer.Stop();saveTimer.Stop();if(!previewOnly)Persist();if(engine!=null){engine.Dispose();engine=null;}
        }
        protected override void Dispose(bool disposing) {if(disposing){if(tray!=null){tray.Visible=false;tray.Dispose();tray=null;}if(trayMenu!=null)trayMenu.Dispose();uiTimer.Dispose();saveTimer.Dispose();if(engine!=null){engine.Dispose();engine=null;}}base.Dispose(disposing);}
    }
}
