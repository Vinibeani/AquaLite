using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AquaLite {
    public sealed partial class MainForm {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam);
        protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);try{int dark=1;DwmSetWindowAttribute(Handle,20,ref dark,4);}catch(DllNotFoundException){} }
        protected override void WndProc(ref Message m) {
            if(m.Msg==0x112){int command=m.WParam.ToInt32()&0xfff0;if(command==0xf000||command==0xf030)return;}
            base.WndProc(ref m);
        }
        void LayoutWindow(){if(heading==null)return;sidebar.Height=ClientSize.Height;heading.Width=Math.Max(420,ClientSize.Width-400);subtitle.Width=ClientSize.Width-270;foreach(Panel p in pages)p.SetBounds(230,125,ClientSize.Width-260,ClientSize.Height-187);resources.SetBounds(233,ClientSize.Height-47,ClientSize.Width-260,30);if(minimizeButton!=null){minimizeButton.Left=ClientSize.Width-98;closeButton.Left=ClientSize.Width-54;}}
        void DragWindow(object sender,MouseEventArgs e) {if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}}
        void BuildWindowControls() {
            Panel drag=At(this,new Panel {BackColor=Palette.Background},204,0,800,24);drag.MouseDown+=DragWindow;
            heading.MouseDown+=DragWindow;subtitle.MouseDown+=DragWindow;
            minimizeButton=At(this,new FlatButton("—"),1013,12,35,30);minimizeButton.BackColor=Palette.Background;minimizeButton.AccessibleName="Minimizar para a bandeja";minimizeButton.Click+=delegate {Hide();};
            closeButton=At(this,new FlatButton("×"),1057,12,35,30);closeButton.Font=Palette.Font(15);closeButton.BackColor=Palette.Background;closeButton.AccessibleName="Fechar janela";closeButton.FlatAppearance.MouseOverBackColor=Color.FromArgb(150,44,64);closeButton.Click+=delegate {Close();};
        }
        void BuildTray() {
            trayMenu=new ContextMenuStrip();
            trayMenu.Items.Add("Abrir AquaLite",null,delegate{ShowControls();});
            trayMenu.Items.Add("Configurações",null,delegate{ShowControls();SelectPage(3);});
            trayPause=new ToolStripMenuItem("Pausar envio à tela");trayPause.Click+=delegate{if(engine!=null)engine.SetConnection(!engine.Wanted);};trayMenu.Items.Add(trayPause);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(AppPaths.InstalledMode?"Fechar painel":"Encerrar aplicativo",null,delegate{exitRequested=true;Close();});
            trayMenu.Opening+=delegate{trayPause.Text=engine!=null&&engine.Wanted?"Pausar envio à tela":"Retomar envio à tela";};
            tray=new NotifyIcon {Text="AquaLite • Controle da tela",Icon=Icon,ContextMenuStrip=trayMenu,Visible=true};
            tray.MouseClick+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left)ShowControls();};
        }
    }
}
