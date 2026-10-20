using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace AquaLite {
 [DataContract] public sealed class LibraryTheme {
  [DataMember] public string Name,Id;
  [DataMember] public string NativeVideo;
  [DataMember] public string VideoReplacesImage;
  [DataMember] public int VideoFrameRate;
  [DataMember] public List<ThemeLayer> Layers;
  public static string Root {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"themes");}}
  public static LibraryTheme Load(string id){if(string.IsNullOrEmpty(id)||Path.GetFileName(id)!=id)throw new IOException("Tema inválido.");using(var f=File.OpenRead(Path.Combine(Root,id,"theme.json")))return (LibraryTheme)new DataContractJsonSerializer(typeof(LibraryTheme)).ReadObject(f);}
 }
 [DataContract] public sealed class ThemeLayer {
  [DataMember] public string Kind,Data,Sub,Text,Font,Color,Image,Back,Front;
  [DataMember] public float X,Y,Size,Width,Height,CenterX,CenterY,Angle;
  [DataMember] public bool Unit,Bold;
  [DataMember] public int Align;
 }
 public sealed class LibraryRenderer:IDisposable {
  LibraryTheme theme; string id; readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
  static readonly System.Drawing.Text.PrivateFontCollection fonts=LoadFonts();
  static System.Drawing.Text.PrivateFontCollection LoadFonts(){var result=new System.Drawing.Text.PrivateFontCollection();string root=Path.Combine(LibraryTheme.Root,"fonts");if(Directory.Exists(root))foreach(string p in Directory.GetFiles(root))try{result.AddFontFile(p);}catch(ArgumentException){}return result;}
  static Font MakeFont(ThemeLayer l){FontFamily family=null;foreach(var f in fonts.Families)if(string.Equals(f.Name,l.Font,StringComparison.OrdinalIgnoreCase)){family=f;break;}float size=Math.Max(6,l.Size);FontStyle style=l.Bold?FontStyle.Bold:FontStyle.Regular;try{return family==null?new Font(string.IsNullOrEmpty(l.Font)?"Segoe UI":l.Font,size,style,GraphicsUnit.Point):new Font(family,size,style,GraphicsUnit.Point);}catch(ArgumentException){return new Font("Segoe UI",size,style,GraphicsUnit.Point);}}
  readonly Dictionary<string,ScreenRenderer> animations=new Dictionary<string,ScreenRenderer>();
  public bool Animated {get{return animations.Count>0;}}
  public void Load(string value){Load(value,false);}
  public void Load(string value,bool overlayOnly){if(id==value)return;Dispose();id=value;if(string.IsNullOrEmpty(id))return;theme=LibraryTheme.Load(id);foreach(var l in theme.Layers){if(overlayOnly&&l.Kind=="Animation")continue;if(string.IsNullOrEmpty(l.Image)||images.ContainsKey(l.Image)||animations.ContainsKey(l.Image))continue;string p=Path.GetFullPath(Path.Combine(LibraryTheme.Root,id,l.Image));if(!p.StartsWith(Path.GetFullPath(LibraryTheme.Root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Imagem de tema inválida.");if(l.Image.EndsWith(".gif",StringComparison.OrdinalIgnoreCase)||l.Image.EndsWith(".mp4",StringComparison.OrdinalIgnoreCase)){var r=new ScreenRenderer();r.Load(p);animations.Add(l.Image,r);}else using(var f=Image.FromFile(p))images.Add(l.Image,new Bitmap(f));}}
  static Color ColorOf(string s){try{if(s.Length==9&&s[0]=='#')return Color.FromArgb(unchecked((int)uint.Parse(s.Substring(1),NumberStyles.HexNumber)));return ColorTranslator.FromHtml(s);}catch{return Color.White;}}
  static double? Number(ThemeLayer l,Reading r){
   double? value=r.GetValue(l.Data,l.Sub,0);if(value.HasValue)return value;
   switch(l.Data){case "CPUTEMP":return r.CpuTemperature;case "GPUTEMP":return r.GpuTemperature;case "CPULOAD":return r.Cpu;case "GPULOAD":return r.GpuLoad;case "RAMLOAD":return r.MemoryPercent;case "RAM":return r.MemoryUsedGB*1024;case "RAMVALID":return (r.MemoryTotalGB-r.MemoryUsedGB)*1024;default:return null;}
  }
  public static string TextOf(ThemeLayer l,Reading r){
   var c=new CultureInfo("pt-BR");if(l.Data=="StaticText"||l.Kind=="Text")return l.Text??"";
   if(l.Data=="TIME"){string f=l.Sub=="h:m:s"?"HH:mm:ss":l.Sub=="s"?"ss":l.Sub=="m"?"mm":l.Sub=="h_12"?"hh":l.Sub=="h"?"HH":"HH:mm";return r.Time.ToString(f,c);}
   if(l.Data=="APM")return r.Time.Hour<12?"AM":"PM";
   if(l.Data=="DAY")return r.Time.ToString("ddd",c);
   if(l.Data=="DATE"){string f=l.Sub=="M"?"MM":l.Sub=="D"?"dd":l.Sub=="Y"?"yyyy":l.Sub=="MMM"?"MMM":"dd/MM/yyyy";return r.Time.ToString(f,c);}
   string text=r.GetText(l.Data);if(text!=null)return text;
   var n=Number(l,r);if(!n.HasValue||!SensorReading.IsFinite(n.Value))return "—";
   string data=l.Data??"",unit="";
   if(l.Unit){if(data.Contains("TEMP"))unit=" °C";else if(data.EndsWith("LOAD")||data=="DRVLOAD"||data=="Volume")unit="%";else if(data.EndsWith("_GB"))unit=" GB";else if(data.Contains("RAM")&&!data.EndsWith("MODEL"))unit=" MB";else if(data.Contains("CLOCK"))unit=data.EndsWith("_G")?" GHz":" MHz";else if(data.EndsWith("PWR"))unit=" W";else if(data.Contains("VOLTAGE"))unit=" V";else if(data.Contains("FAN")||data.Contains("PUMP"))unit=" RPM";else if(data=="UPSPEED"||data=="DOWNDSPEED")unit=" KB/s";else if(data=="FPS")unit=" FPS";}
   return n.Value.ToString(data.Contains("VOLTAGE")||data.EndsWith("_G")||data.EndsWith("_GB")?"0.##":"0",c)+unit;
  }
  public void Draw(Graphics g,Reading reading){Draw(g,reading,false);}
  public void Draw(Graphics g,Reading reading,bool skipAnimation){if(theme==null)return;foreach(var l in theme.Layers){
   if(!string.IsNullOrEmpty(theme.VideoReplacesImage)&&l.Image==theme.VideoReplacesImage)continue;
   if(skipAnimation&&l.Kind=="Animation")continue;
   Image img;ScreenRenderer animation;
   if(!string.IsNullOrEmpty(l.Image)&&animations.TryGetValue(l.Image,out animation)){using(Bitmap frame=animation.Render(new Settings{Mode="Imagem ou GIF",Crop=false},reading))g.DrawImage(frame,l.X,l.Y,480,480);continue;}
   if(!string.IsNullOrEmpty(l.Image)&&images.TryGetValue(l.Image,out img)){
    if(l.Kind=="Clock"){GraphicsState state=g.Save();float a=l.Sub=="s"?reading.Time.Second*6:l.Sub=="m"?(reading.Time.Minute+reading.Time.Second/60f)*6:(reading.Time.Hour%12+reading.Time.Minute/60f)*30;g.TranslateTransform(l.CenterX,l.CenterY);g.RotateTransform(a);g.DrawImage(img,l.X,l.Y,img.Width,img.Height);g.Restore(state);}else g.DrawImage(img,l.X,l.Y,img.Width,img.Height);continue;
   }
   if(l.Kind=="StatuBar"){float w=Math.Max(1,l.Width),h=Math.Max(1,l.Height),ratio=(float)Math.Max(0,Math.Min(1,(Number(l,reading)??0)/100));using(var back=new SolidBrush(ColorOf(l.Back)))g.FillRectangle(back,l.X,l.Y,w,h);using(var front=new SolidBrush(ColorOf(l.Front)))g.FillRectangle(front,l.X,l.Y,w*ratio,h);continue;}
   if(l.Kind=="Data"||l.Kind=="Text")using(var f=MakeFont(l))using(var brush=new SolidBrush(ColorOf(l.Color)))using(var format=new StringFormat{Alignment=(StringAlignment)Math.Max(0,Math.Min(2,l.Align))})g.DrawString(TextOf(l,reading),f,brush,new PointF(l.X,l.Y),format);
  }}
  public void Dispose(){foreach(var i in images.Values)i.Dispose();images.Clear();foreach(var a in animations.Values)a.Dispose();animations.Clear();theme=null;id=null;}
  public byte[] Overlay(Reading reading,int rotation){using(var bitmap=new Bitmap(480,480,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bitmap))using(var stream=new MemoryStream()){
   g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   g.TranslateTransform(240,240);g.RotateTransform(rotation);g.TranslateTransform(-240,-240);Draw(g,reading,true);bitmap.Save(stream,ImageFormat.Png);return stream.ToArray();
  }}
 }
 public sealed class ThemeGallery:Form {
  public string SelectedTheme;
  public ThemeGallery(string current){Text="Galeria de temas • AquaLite";FormBorderStyle=FormBorderStyle.None;Size=new Size(870,680);StartPosition=FormStartPosition.CenterParent;BackColor=Palette.Background;ForeColor=Palette.Text;Font=Palette.Font(10);MinimizeBox=false;MaximizeBox=false;
   var title=new Label{Text="Escolha o seu próximo visual",Font=Palette.Font(19,true),Dock=DockStyle.Top,Height=58,Padding=new Padding(20,12,0,0)};Controls.Add(title);
   var footer=new Panel{Dock=DockStyle.Bottom,Height=62};var note=new Label{Text="Prévias do pacote. Sensores e horário são atualizados ao aplicar.\nVídeos completos. A fluidez depende do perfil e da conexão USB.",ForeColor=Palette.Muted,AutoSize=false,Size=new Size(620,52),Location=new Point(18,6),Font=Palette.Font(9)};footer.Controls.Add(note);var close=new FlatButton("Voltar"){Location=new Point(695,12),Width=130};close.Click+=delegate{Close();};footer.Controls.Add(close);Controls.Add(footer);
   var grid=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(12),BackColor=Palette.Background};Controls.Add(grid);grid.BringToFront();
   if(!Directory.Exists(LibraryTheme.Root))return;
   foreach(string dir in Directory.GetDirectories(LibraryTheme.Root)){string id=Path.GetFileName(dir);if(!File.Exists(Path.Combine(dir,"theme.json")))continue;var tile=new Panel{Width=192,Height=236,Margin=new Padding(6),BackColor=Palette.Card};var picture=new PictureBox{Location=new Point(7,7),Size=new Size(178,178),SizeMode=PictureBoxSizeMode.Zoom,Cursor=Cursors.Hand};string path=Path.Combine(dir,"preview.png");if(File.Exists(path))using(var img=Image.FromFile(path))picture.Image=new Bitmap(img);tile.Controls.Add(picture);var apply=new FlatButton((id==current?"✓ ":"")+"Tema "+id){Location=new Point(7,191),Size=new Size(178,36)};EventHandler choose=delegate{SelectedTheme=id;DialogResult=DialogResult.OK;Close();};apply.Click+=choose;picture.Click+=choose;tile.Controls.Add(apply);grid.Controls.Add(tile);}
  }
  protected override void Dispose(bool disposing){if(disposing)foreach(Control grid in Controls)foreach(Control tile in grid.Controls)foreach(Control child in tile.Controls){var p=child as PictureBox;if(p!=null&&p.Image!=null){p.Image.Dispose();p.Image=null;}}base.Dispose(disposing);}
 }
}
