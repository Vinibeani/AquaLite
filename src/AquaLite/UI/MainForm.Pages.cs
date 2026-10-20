using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AquaLite {
    public sealed partial class MainForm {
        Label LabelAt(Control parent,string text,int x,int y,int width,int height,float size=10,bool bold=false,Color? color=null) {
            Label label=new Label {Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=color??Palette.Text,Font=Palette.Font(size,bold),BackColor=Color.Transparent};parent.Controls.Add(label);return label;
        }
        T At<T>(Control parent,T control,int x,int y,int width,int height) where T:Control {control.SetBounds(x,y,width,height);parent.Controls.Add(control);return control;}
        ComboBox Combo(Control parent,string[] choices,int x,int y,int width) {
            ComboBox box=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=24,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(31,43,60),ForeColor=Palette.Text,Font=Palette.Font(10)};
            box.DrawItem+=delegate(object sender,DrawItemEventArgs args) {
                Color background=(args.State&DrawItemState.Selected)!=0?Color.FromArgb(34,73,77):Color.FromArgb(31,43,60);
                using(SolidBrush brush=new SolidBrush(background))args.Graphics.FillRectangle(brush,args.Bounds);
                if(args.Index>=0)TextRenderer.DrawText(args.Graphics,box.Items[args.Index].ToString(),box.Font,args.Bounds,Palette.Text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
            };
            box.Items.AddRange(choices);At(parent,box,x,y,width,30);return box;
        }
        CheckBox Check(Control parent,string text,int x,int y,int width,bool value) {return At(parent,new CheckBox {Text=text,Checked=value,ForeColor=Palette.Text,AutoSize=false},x,y,width,32);}
        void BuildSidebar() {
            sidebar=At(this,new Panel {BackColor=Palette.Sidebar,Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left},0,0,202,770);
            string logo=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","aqualite.png");
            if(File.Exists(logo))At(sidebar,new PictureBox {Image=Image.FromFile(logo),SizeMode=PictureBoxSizeMode.Zoom},23,27,43,43);
            LabelAt(sidebar,"AquaLite",75,31,119,34,17,true);LabelAt(sidebar,"CONTROLE DA TELA",25,97,170,23,8,true,Palette.Muted);
            string[] names={"Minha tela","Temas","Sensores do PC","Configurações"};
            for(int i=0;i<4;i++) {int index=i;nav[i]=At(sidebar,new FlatButton(names[i]),14,135+i*55,174,44);nav[i].TextAlign=ContentAlignment.MiddleLeft;nav[i].Padding=new Padding(15,0,0,0);nav[i].Click+=delegate {SelectPage(index);};}
            LabelAt(sidebar,"SEMPRE À MÃO",24,492,171,24,8,true,Palette.Accent);
            LabelAt(sidebar,"Feche a janela para manter\na tela funcionando.\nReabra pelo ícone perto\ndo relógio do Windows.",24,526,161,108,9,false,Palette.Muted);
            LabelAt(sidebar,"LCD 2,1″ • 480 × 480",24,650,174,23,8,false,Palette.Muted);
        }
        void BuildPages() {
            heading=LabelAt(this,"",232,29,600,42,23,true);subtitle=LabelAt(this,"",234,78,802,26,10,false,Palette.Muted);
            for(int i=0;i<4;i++)pages[i]=At(this,new Panel {BackColor=Palette.Background},230,125,850,573);
            BuildDisplayPage();BuildThemePage();BuildMonitorPage();BuildSettingsPage();
            resources=LabelAt(this,"Preparando...",233,724,840,27,9,false,Palette.Muted);
        }
        void SelectPage(int index) {
            string[] titles={"Sua tela, do seu jeito.","Um visual para cada momento.","Seu PC em tempo real.","Tudo sob seu controle."};
            string[] descriptions={"Confira a prévia, ajuste os detalhes e personalize seu watercooler.","Explore as prévias da biblioteca e aplique seu tema favorito.","Encontre cada leitura e saiba de onde ela vem.","Escolha como o AquaLite inicia e funciona em segundo plano."};
            heading.Text=titles[index];subtitle.Text=descriptions[index];
            for(int i=0;i<4;i++){pages[i].Visible=i==index;nav[i].BackColor=i==index?Color.FromArgb(26,62,65):Palette.Sidebar;nav[i].ForeColor=i==index?Palette.Accent:Palette.Muted;}
        }
        void BuildDisplayPage() {
            Panel page=pages[0];Card left=At(page,new Card(),0,0,416,573);
            LabelAt(left,"PRÉVIA DA TELA",22,20,260,24,9,true,Palette.Muted);
            LabelAt(left,"2,1″",352,20,45,24,9,true,Palette.Accent);
            preview=At(left,new PreviewControl(),15,57,386,386);
            status=LabelAt(left,"Preparando a prévia...",23,461,372,45,10,true);
            details=LabelAt(left,"480 × 480 pixels • rotação livre",23,511,372,42,9,false,Palette.Muted);
            Card rightCard=At(page,new Card {AutoScroll=true},434,0,416,573);
            Panel right=At(rightCard,new Panel {BackColor=Palette.Card},0,0,416,573);
            page.Resize+=delegate{int lw=Math.Min(416,Math.Max(310,page.ClientSize.Width-450));left.SetBounds(0,0,lw,page.ClientSize.Height);rightCard.SetBounds(lw+18,0,page.ClientSize.Width-lw-18,page.ClientSize.Height);right.Left=Math.Max(0,(rightCard.ClientSize.Width-right.Width)/2);int d=Math.Min(lw-30,Math.Max(230,left.Height-195));preview.SetBounds((lw-d)/2,57,d,d);status.SetBounds(23,left.Height-109,lw-46,45);details.SetBounds(23,left.Height-59,lw-46,44);};
            LabelAt(right,"CONTEÚDO",23,20,360,22,9,true,Palette.Muted);
            mode=Combo(right,new string[]{"Painel do PC","Relógio","Imagem ou GIF","Vídeo","Personalizado","Galeria de temas"},23,51,370);mode.SelectedItem=settings.Mode;
            mode.SelectedIndexChanged+=delegate {settings.Mode=mode.Text;Changed();};
            FlatButton gallery=At(right,new FlatButton("Explorar os temas"),23,92,179,29);gallery.Font=Palette.Font(9,true);gallery.Click+=delegate{SelectPage(1);};
            FlatButton customize=At(right,new FlatButton("Criar / editar tema"),214,92,179,29);customize.Font=Palette.Font(9,true);customize.Click+=EditCustom;
            FlatButton browse=At(right,new FlatButton("Imagem, GIF ou vídeo"),23,125,167,36);browse.Font=Palette.Font(9,true);browse.Click+=Browse;
            fileName=LabelAt(right,MediaName(),202,131,191,39,9,false,Palette.Muted);
            LabelAt(right,"Nome no painel / relógio",23,180,370,22,9,false,Palette.Muted);
            caption=At(right,new TextBox {Text=settings.Caption,MaxLength=24,BorderStyle=BorderStyle.FixedSingle,BackColor=Color.FromArgb(31,43,60),ForeColor=Palette.Text},23,207,273,29);
            caption.TextChanged+=delegate {settings.Caption=caption.Text;Changed();};
            color=At(right,new FlatButton("Cor"),310,204,83,33);color.Click+=delegate {using(ColorDialog dialog=new ColorDialog()){try{dialog.Color=ColorTranslator.FromHtml(settings.Accent);}catch{}if(dialog.ShowDialog(this)==DialogResult.OK){settings.Accent=ColorTranslator.ToHtml(dialog.Color);Changed();}}};
            LabelAt(right,"Brilho",23,255,220,25,10,true);brightnessText=LabelAt(right,settings.Brightness+"%",327,255,68,25,10,true,Palette.Accent);
            brightness=At(right,new TrackBar {Minimum=0,Maximum=100,Value=settings.Brightness,TickStyle=TickStyle.None,BackColor=Palette.Card},18,285,380,35);
            brightness.ValueChanged+=delegate {settings.Brightness=brightness.Value;brightnessText.Text=settings.Brightness+"%";Changed();};
            rotationText=LabelAt(right,"Rotação livre",23,330,220,25,10,true);
            angle=At(right,new NumericUpDown {Minimum=0,Maximum=359,Value=settings.Rotation,BackColor=Color.FromArgb(31,43,60),ForeColor=Palette.Text,BorderStyle=BorderStyle.FixedSingle},303,329,68,29);
            LabelAt(right,"°",376,330,20,25,11,true,Palette.Muted);
            rotation=At(right,new TrackBar {AutoSize=false,Minimum=0,Maximum=359,Value=settings.Rotation,TickStyle=TickStyle.None,BackColor=Palette.Card},18,363,380,30);
            rotation.ValueChanged+=delegate {if(angle.Value!=rotation.Value)angle.Value=rotation.Value;settings.Rotation=rotation.Value;Changed();};
            angle.ValueChanged+=delegate {if(rotation.Value!=(int)angle.Value)rotation.Value=(int)angle.Value;settings.Rotation=(int)angle.Value;Changed();};
            LabelAt(right,"Ajuste de 1 em 1 grau.\nA prévia mostra o ângulo real.",23,399,370,36,9,false,Palette.Muted);
            LabelAt(right,"Uso de recursos",23,438,150,25,10,true);
            performance=Combo(right,new string[]{"Econômico","Equilibrado","Fluido"},205,435,188);performance.SelectedItem=settings.Performance;
            performance.SelectedIndexChanged+=delegate {settings.Performance=performance.Text;Changed();};
            LabelAt(right,"Perfil de animação. A fluidez depende do arquivo e da USB.",23,472,370,22,8,false,Palette.Muted);
            connect=At(right,new FlatButton("Conectando à tela...",true),23,499,246,47);connect.Click+=delegate {if(engine!=null){engine.SetConnection(!engine.Wanted);connect.Text=engine.Wanted?"Conectando...":"Retomar envio à tela";}};
            FlatButton export=At(right,new FlatButton("Salvar prévia"),281,499,112,47);export.Font=Palette.Font(9,true);export.Click+=Export;
        }
        void BuildThemePage(){
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=Padding.Empty,Padding=Padding.Empty};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,82));pages[1].Controls.Add(layout);
            themeBrowser=new ThemeBrowser(settings.LibraryThemeId);themeBrowser.Dock=DockStyle.Fill;themeBrowser.Margin=Padding.Empty;layout.Controls.Add(themeBrowser,0,0);
            Panel playback=new Panel{Dock=DockStyle.Fill,Height=82,BackColor=Palette.Card,Margin=Padding.Empty};layout.Controls.Add(playback,0,1);
            CheckBox slideshow=Check(playback,"Alternar os temas da biblioteca",14,6,315,settings.ThemeSlideshow);
            CheckBox random=Check(playback,"Ordem aleatória",340,6,250,settings.RandomThemes);
            LabelAt(playback,"Trocar a cada (segundos)",14,44,230,28,9,false,Palette.Muted);
            NumericUpDown interval=At(playback,new NumericUpDown{Minimum=10,Maximum=3600,Value=Math.Max(10,Math.Min(3600,settings.ThemeIntervalSeconds)),BackColor=Palette.Card,ForeColor=Palette.Text},246,44,100,28);
            LabelAt(playback,"Ativo no modo Galeria de temas",365,45,360,28,9,false,Palette.Muted);
            slideshow.CheckedChanged+=delegate{settings.ThemeSlideshow=slideshow.Checked;if(slideshow.Checked){settings.Mode="Galeria de temas";mode.SelectedItem=settings.Mode;}Changed();};random.CheckedChanged+=delegate{settings.RandomThemes=random.Checked;Changed();};interval.ValueChanged+=delegate{settings.ThemeIntervalSeconds=(int)interval.Value;Changed();};
            themeBrowser.Applied+=delegate{settings.LibraryThemeId=themeBrowser.SelectedTheme;settings.Mode="Galeria de temas";mode.SelectedItem=settings.Mode;Changed();SelectPage(0);};themeBrowser.CreateNew+=EditCustom;
        }
        void BuildMonitorPage() {
            sensorPanel=new SensorPanel();sensorPanel.Dock=DockStyle.Fill;pages[2].Controls.Add(sensorPanel);
            sensorPanel.ActivateSensor+=delegate{settings.LocalSensorsEnabled=true;Changed();ActivateHardwareReader();};
        }
        void BuildSettingsPage() {
            Panel scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};pages[3].Controls.Add(scroll);
            Card panel=At(scroll,new Card(),0,0,850,780);
            scroll.Resize+=delegate{panel.Width=Math.Max(730,scroll.ClientSize.Width-20);foreach(Control c in panel.Controls){if((c is Label&&c.Width>300)||c is CheckBox)c.Width=panel.Width-c.Left-24;}int bw=(panel.Width-84)/3;foreach(Control c in panel.Controls)if(c is Button&&(c.Top==437||c.Top==513)){int col=c.Left<240?0:c.Left<460?1:2;c.Left=26+col*(bw+16);c.Width=bw;c.Font=Palette.Font(9,true);}};
            LabelAt(panel,"INICIALIZAÇÃO E SEGUNDO PLANO",26,24,780,28,10,true,Palette.Accent);
            startWindows=Check(panel,"Ligar a tela junto com o computador",26,72,780,Settings.StartupEnabled);
            if(!Settings.StartupState.HasValue){startWindows.Enabled=false;startWindows.Text="Inicialização automática: consulta bloqueada pelo Windows";}
            startWindows.CheckedChanged+=delegate {
                if(initial)return;
                try {Settings.SetStartup(startWindows.Checked,Application.ExecutablePath);}
                catch(Exception e){MessageBox.Show(this,"Não foi possível alterar a inicialização: "+e.Message,"Inicialização",MessageBoxButtons.OK,MessageBoxIcon.Warning);initial=true;startWindows.Checked=Settings.StartupEnabled;initial=false;}
            };
            LabelAt(panel,"O visor funciona antes do login. Abra o painel quando quiser ajustar.",48,109,751,25,9,false,Palette.Muted);
            keepRunning=Check(panel,"Manter o AquaLite na bandeja quando eu fechar a janela",26,154,780,settings.KeepRunning);
            keepRunning.CheckedChanged+=delegate {settings.KeepRunning=keepRunning.Checked;Changed();};
            LabelAt(panel,"Clique no ícone da bandeja para ajustar. Para parar, use “Encerrar aplicativo”.",48,191,751,26,9,false,Palette.Muted);
            reconnect=Check(panel,"Reconectar automaticamente se o cabo USB for desconectado",26,234,780,settings.Reconnect);
            reconnect.CheckedChanged+=delegate {settings.Reconnect=reconnect.Checked;Changed();};
            CheckBox crop=Check(panel,"Preencher a tela com a imagem ou GIF (recortar as bordas)",26,283,780,settings.Crop);
            crop.CheckedChanged+=delegate {settings.Crop=crop.Checked;Changed();};
            LabelAt(panel,"ÍCONE DE REMOÇÃO USB DO WINDOWS",26,345,790,25,9,true,Palette.Accent);
            LabelAt(panel,"Ajuste opcional do Windows, aplicado somente a esta tela e com backup para desfazer.\nPode exigir reconectar o cabo ou reiniciar o PC. O Windows solicitará permissão de administrador.",26,379,783,54,10,false,Palette.Muted);
            FlatButton hideUsb=At(panel,new FlatButton("Ocultar no menu USB"),26,437,205,35);hideUsb.Click+=delegate{AdjustUsb(true);};
            FlatButton restoreUsb=At(panel,new FlatButton("Restaurar menu USB"),247,437,205,35);restoreUsb.Click+=delegate{AdjustUsb(false);};
            FlatButton identity=At(panel,new FlatButton("Nome e ícone AquaLite no Windows"),468,437,354,35);identity.Click+=delegate{AdjustIdentity(false);};
            firmware=LabelAt(panel,"Tela: aguardando conexão",26,480,790,27,8,false,Palette.Muted);
            FlatButton exit=At(panel,new FlatButton(AppPaths.InstalledMode?"Fechar painel":"Encerrar aplicativo"),26,513,205,38);exit.Click+=delegate {exitRequested=true;Close();};
            FlatButton folder=At(panel,new FlatButton("Abrir meus arquivos"),247,513,205,38);folder.Click+=delegate {ApplicationActions.OpenFiles();};
            FlatButton restoreIdentity=At(panel,new FlatButton("Restaurar nome e ícone anteriores"),468,513,354,38);restoreIdentity.Click+=delegate{AdjustIdentity(true);};
            LabelAt(panel,"RECURSOS OPCIONAIS",26,587,780,28,10,true,Palette.Accent);
            CheckBox weather=Check(panel,"Mostrar clima local nos temas",26,626,780,settings.WeatherEnabled);
            LabelAt(panel,"Coordenadas da sua cidade. Consulta o clima pela internet quando ativado.",48,663,750,28,9,false,Palette.Muted);
            LabelAt(panel,"Latitude",48,702,120,24,9,false,Palette.Muted);LabelAt(panel,"Longitude",268,702,120,24,9,false,Palette.Muted);
            NumericUpDown latitude=At(panel,new NumericUpDown{Minimum=-90,Maximum=90,DecimalPlaces=4,Increment=0.01M,Value=(decimal)Math.Max(-90,Math.Min(90,settings.WeatherLatitude)),BackColor=Palette.Card,ForeColor=Palette.Text},48,730,190,30);
            NumericUpDown longitude=At(panel,new NumericUpDown{Minimum=-180,Maximum=180,DecimalPlaces=4,Increment=0.01M,Value=(decimal)Math.Max(-180,Math.Min(180,settings.WeatherLongitude)),BackColor=Palette.Card,ForeColor=Palette.Text},268,730,190,30);
            latitude.Enabled=longitude.Enabled=weather.Checked;weather.CheckedChanged+=delegate{settings.WeatherEnabled=weather.Checked;latitude.Enabled=longitude.Enabled=weather.Checked;Changed();};
            latitude.ValueChanged+=delegate{settings.WeatherLatitude=(double)latitude.Value;Changed();};longitude.ValueChanged+=delegate{settings.WeatherLongitude=(double)longitude.Value;Changed();};
            panel.Height=790;
        }
    }
}
