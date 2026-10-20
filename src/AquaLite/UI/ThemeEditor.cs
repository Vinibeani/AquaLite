using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AquaLite {
    public sealed class ThemeCanvas : Control {
        public Settings Draft;
        public Reading Reading=new Reading();
        public int Selected=-1;
        public event EventHandler SelectionChanged,PositionChanged;
        readonly ScreenRenderer renderer=new ScreenRenderer();
        bool dragging;Point start;int originalX,originalY;
        public ThemeCanvas(Settings value){Draft=value;DoubleBuffered=true;Size=new Size(480,480);Cursor=Cursors.Cross;}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);
            try {
                renderer.Load(Draft.CustomBackground?Draft.MediaPath:"");
                Settings copy=Draft.Copy();copy.Rotation=0;copy.Mode="Personalizado";
                using(Bitmap bitmap=renderer.Render(copy,Reading))e.Graphics.DrawImageUnscaled(bitmap,0,0);
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                using(Pen ring=new Pen(Color.FromArgb(100,150,169),1)){ring.DashStyle=DashStyle.Dash;e.Graphics.DrawEllipse(ring,1,1,478,478);}
                if(Selected>=0&&Selected<Draft.Widgets.Count){using(Pen selection=new Pen(Palette.Accent,2))e.Graphics.DrawRectangle(selection,Rectangle.Round(ScreenRenderer.WidgetBounds(e.Graphics,Draft.Widgets[Selected],Reading)));}
            } catch(Exception error){TextRenderer.DrawText(e.Graphics,"Não foi possível carregar o fundo.\n"+error.Message,Font,ClientRectangle,Palette.Text);}
        }
        protected override void OnMouseDown(MouseEventArgs e){
            base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;
            int hit=-1;using(Graphics g=CreateGraphics())for(int i=Draft.Widgets.Count-1;i>=0;i--)if(Draft.Widgets[i].Visible&&ScreenRenderer.WidgetBounds(g,Draft.Widgets[i],Reading).Contains(e.Location)){hit=i;break;}
            Selected=hit;if(SelectionChanged!=null)SelectionChanged(this,EventArgs.Empty);
            if(hit>=0){dragging=true;Capture=true;start=e.Location;originalX=Draft.Widgets[hit].X;originalY=Draft.Widgets[hit].Y;}
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging||Selected<0)return;Widget item=Draft.Widgets[Selected];item.X=Math.Max(0,Math.Min(480,originalX+e.X-start.X));item.Y=Math.Max(0,Math.Min(480,originalY+e.Y-start.Y));if(PositionChanged!=null)PositionChanged(this,EventArgs.Empty);Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;}
        protected override void Dispose(bool disposing){if(disposing)renderer.Dispose();base.Dispose(disposing);}
    }
    public sealed class ThemeEditor : Form {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam);
        public Settings Draft {get;private set;}
        public ThemeCanvas Canvas {get;private set;}
        readonly Func<Reading> getReading;
        readonly Timer timer=new Timer();
        ListBox layers;ComboBox kind,sensor;TextBox text;NumericUpDown x,y,size,width;CheckBox visible,bold;Label colorValue,propertyTitle;
        sealed class SensorChoice{public string Id,Name;public override string ToString(){return Name;}}
        bool loading;
        public ThemeEditor(Settings config,Func<Reading> reader){
            Draft=config.Copy();Draft.Mode="Personalizado";getReading=reader;
            Text="AquaLite • Editor personalizado";FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;ClientSize=new Size(908,700);StartPosition=FormStartPosition.CenterParent;BackColor=Palette.Background;ForeColor=Palette.Text;Font=Palette.Font(10);
            Label title=LabelAt("Crie uma tela com a sua cara.",23,20,830,35,20,true,Palette.Text);title.MouseDown+=delegate(object sender,MouseEventArgs e){if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}};
            LabelAt("Arraste os elementos. Use o painel ao lado para ajustar os detalhes.",25,63,830,25,10,false,Palette.Muted);
            Canvas=new ThemeCanvas(Draft);At(Canvas,23,112,480,480);
            Canvas.SelectionChanged+=delegate{layers.SelectedIndex=Canvas.Selected;LoadProperties();};
            Canvas.PositionChanged+=delegate{loading=true;if(Canvas.Selected>=0){x.Value=Draft.Widgets[Canvas.Selected].X;y.Value=Draft.Widgets[Canvas.Selected].Y;}loading=false;};
            LabelAt("O círculo indica a área visível. Edição em 0°; a rotação geral é aplicada no visor.",23,606,480,48,9,false,Palette.Muted);
            kind=At(new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,FlatStyle=FlatStyle.Flat,BackColor=Palette.Card,ForeColor=Palette.Text},530,111,242,30);
            kind.Items.AddRange(new string[]{"Texto","Hora","Data","Uso da CPU","Temperatura da CPU","Clock da CPU","Potência da CPU","Tensão da CPU","Uso da RAM","Temperatura da GPU","Uso da GPU","Clock da GPU","Potência da GPU","Ventoinha da GPU","VRAM usada","Download","Upload","Volume","FPS","Clima","AM/PM","Sensor","Barra da CPU","Barra da RAM"});kind.SelectedIndex=0;
            Button add=At(new FlatButton("Adicionar"),783,109,104,34);add.Click+=delegate{if(Draft.Widgets.Count>=40){MessageBox.Show(this,"Use até 40 elementos por tema.","Limite do tema");return;}Draft.Widgets.Add(new Widget{Kind=kind.Text,X=240,Y=240,Color=Draft.Accent});RefreshLayers(Draft.Widgets.Count-1);};
            layers=At(new ListBox {BackColor=Palette.Card,ForeColor=Palette.Text,BorderStyle=BorderStyle.None,IntegralHeight=false,Font=Palette.Font(10)},530,157,357,126);
            layers.SelectedIndexChanged+=delegate{Canvas.Selected=layers.SelectedIndex;LoadProperties();Canvas.Invalidate();};
            Button remove=At(new FlatButton("Excluir"),530,293,109,31);remove.Click+=delegate{int index=layers.SelectedIndex;if(index>=0){Draft.Widgets.RemoveAt(index);RefreshLayers(Math.Min(index,Draft.Widgets.Count-1));}};
            Button duplicate=At(new FlatButton("Duplicar"),650,293,112,31);duplicate.Click+=delegate{if(layers.SelectedIndex>=0&&Draft.Widgets.Count<40){Widget item=Draft.Widgets[layers.SelectedIndex].Copy();item.Y=Math.Min(480,item.Y+25);Draft.Widgets.Add(item);RefreshLayers(Draft.Widgets.Count-1);}};
            Button up=At(new FlatButton("À frente"),774,293,113,31);up.Click+=delegate{int index=layers.SelectedIndex;if(index>=0&&index<Draft.Widgets.Count-1){Widget item=Draft.Widgets[index];Draft.Widgets.RemoveAt(index);Draft.Widgets.Insert(index+1,item);RefreshLayers(index+1);}};
            propertyTitle=LabelAt("Texto livre",530,342,330,24,9,false,Palette.Muted);
            text=At(new TextBox{BackColor=Palette.Card,ForeColor=Palette.Text,BorderStyle=BorderStyle.FixedSingle,MaxLength=120},530,369,357,28);text.TextChanged+=delegate{Apply();};
            sensor=At(new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Palette.Card,ForeColor=Palette.Text,FlatStyle=FlatStyle.Flat,Visible=false,DropDownWidth=520},530,369,357,30);
            Reading current=getReading==null?new Reading():getReading();if(current.Sensors!=null)foreach(SensorReading value in current.Sensors)sensor.Items.Add(new SensorChoice{Id=value.Id,Name=value.Group+" · "+value.Name+(string.IsNullOrEmpty(value.Unit)?"":" ("+value.Unit+")")});sensor.SelectedIndexChanged+=delegate{if(loading||layers.SelectedIndex<0)return;SensorChoice choice=sensor.SelectedItem as SensorChoice;if(choice!=null){Draft.Widgets[layers.SelectedIndex].SensorId=choice.Id;Canvas.Invalidate();}};
            LabelAt("Centro X",530,410,80,24,9,false,Palette.Muted);LabelAt("Centro Y",650,410,80,24,9,false,Palette.Muted);LabelAt("Tamanho",772,410,109,24,9,false,Palette.Muted);
            x=Number(530,438,0,480);y=Number(650,438,0,480);size=Number(772,438,10,140);
            LabelAt("Largura da barra",530,479,150,24,9,false,Palette.Muted);width=Number(772,477,20,460);
            visible=At(new CheckBox{Text="Visível",ForeColor=Palette.Text},530,518,115,29);bold=At(new CheckBox{Text="Negrito",ForeColor=Palette.Text},650,518,117,29);
            visible.CheckedChanged+=delegate{Apply();};bold.CheckedChanged+=delegate{Apply();};
            Button chooseColor=At(new FlatButton("Cor do elemento"),530,559,165,35);chooseColor.Click+=delegate{if(layers.SelectedIndex<0)return;using(ColorDialog dialog=new ColorDialog()){try{dialog.Color=ColorTranslator.FromHtml(Draft.Widgets[layers.SelectedIndex].Color);}catch{}if(dialog.ShowDialog(this)==DialogResult.OK){Draft.Widgets[layers.SelectedIndex].Color=ColorTranslator.ToHtml(dialog.Color);LoadProperties();Canvas.Invalidate();}}};
            colorValue=LabelAt("",708,568,180,24,9,false,Palette.Muted);
            Button background=At(new FlatButton("Fundo: imagem / GIF"),530,606,218,35);background.Click+=ChooseBackground;
            Button clear=At(new FlatButton("Sem fundo"),760,606,127,35);clear.Click+=delegate{Draft.CustomBackground=false;Canvas.Invalidate();};
            Button cancel=At(new FlatButton("Cancelar"),629,655,118,35);cancel.Click+=delegate{DialogResult=DialogResult.Cancel;Close();};
            Button save=At(new FlatButton("Aplicar meu tema",true),760,655,127,35);save.Font=Palette.Font(9,true);save.Click+=delegate{DialogResult=DialogResult.OK;Close();};
            LabelAt("Até 40 elementos • alterações só são aplicadas ao salvar",24,661,585,27,9,false,Palette.Muted);
            RefreshLayers(Draft.Widgets.Count>0?0:-1);
            timer.Interval=config.Performance=="Fluido"?33:config.Performance=="Equilibrado"?50:100;DateTime nextReading=DateTime.MinValue;timer.Tick+=delegate{if(getReading!=null&&DateTime.UtcNow>=nextReading){Canvas.Reading=getReading();nextReading=DateTime.UtcNow.AddSeconds(1);}Canvas.Invalidate();};timer.Start();
            if(getReading!=null)Canvas.Reading=getReading();
        }
        T At<T>(T control,int left,int top,int w,int h) where T:Control {control.SetBounds(left,top,w,h);Controls.Add(control);return control;}
        Label LabelAt(string text,int left,int top,int w,int h,float size,bool bold,Color color){return At(new Label{Text=text,Font=Palette.Font(size,bold),ForeColor=color},left,top,w,h);}
        NumericUpDown Number(int left,int top,int minimum,int maximum){NumericUpDown number=At(new NumericUpDown{Minimum=minimum,Maximum=maximum,BackColor=Palette.Card,ForeColor=Palette.Text,BorderStyle=BorderStyle.FixedSingle},left,top,110,28);number.ValueChanged+=delegate{Apply();};return number;}
        void RefreshLayers(int selected){loading=true;layers.Items.Clear();foreach(Widget item in Draft.Widgets)layers.Items.Add(item);layers.SelectedIndex=selected;Canvas.Selected=selected;loading=false;LoadProperties();Canvas.Invalidate();}
        void LoadProperties(){
            if(x==null)return;loading=true;int index=layers.SelectedIndex;bool enabled=index>=0;
            foreach(Control control in new Control[]{text,x,y,size,width,visible,bold})control.Enabled=enabled;
            sensor.Visible=enabled&&Draft.Widgets[index].Kind=="Sensor";text.Visible=!sensor.Visible;propertyTitle.Text=sensor.Visible?"Sensor do PC (lista disponível neste computador)":"Texto livre";
            if(enabled){Widget item=Draft.Widgets[index];text.Text=item.Text;text.Enabled=item.Kind=="Texto";x.Value=item.X;y.Value=item.Y;size.Value=item.Size;width.Value=item.Width;visible.Checked=item.Visible;bold.Checked=item.Bold;colorValue.Text=item.Color;width.Enabled=item.Kind.StartsWith("Barra");if(item.Kind=="Sensor"){sensor.SelectedIndex=-1;for(int i=0;i<sensor.Items.Count;i++)if(((SensorChoice)sensor.Items[i]).Id==item.SensorId){sensor.SelectedIndex=i;break;}}}
            loading=false;
        }
        void Apply(){if(loading||layers.SelectedIndex<0||x==null)return;Widget item=Draft.Widgets[layers.SelectedIndex];item.Text=text.Text;item.X=(int)x.Value;item.Y=(int)y.Value;item.Size=(int)size.Value;item.Width=(int)width.Value;item.Visible=visible.Checked;item.Bold=bold.Checked;Canvas.Invalidate();}
        void ChooseBackground(object sender,EventArgs e){
            using(OpenFileDialog dialog=new OpenFileDialog{Title="Escolher fundo",Filter="Imagem ou GIF|*.png;*.jpg;*.jpeg;*.gif;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK){
                try{string target=ApplicationActions.ImportMedia(dialog.FileName,"fundo-");Draft.MediaPath=target;Draft.CustomBackground=true;Canvas.Invalidate();}
                catch(Exception error){MessageBox.Show(this,error.Message,"Não foi possível abrir o fundo");}
            }
        }
        protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
    }
}
