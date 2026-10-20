using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AquaLite;
class ValidateWindow {
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr command,IntPtr data);
 static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("OK: "+name);}
 static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(Control nested in Descendants(child))yield return nested;}}
 static void Capture(Form form,string target){using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(target,System.Drawing.Imaging.ImageFormat.Png);}}
 [STAThread] static int Main(string[] args){try{
  Application.EnableVisualStyles();string folder=args.Length>0?args[0]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tests");Directory.CreateDirectory(folder);
  var config=new Settings{Mode="Painel do PC",ThemeSlideshow=false,KeepRunning=false,LocalSensorsEnabled=false,WeatherEnabled=false};
  using(var form=new MainForm(config,true,false)){
   form.Show();Application.DoEvents();Size original=form.Size;
   Check(form.MinimumSize==original&&form.MaximumSize==original&&!form.MaximizeBox,"Janela tem tamanho mínimo/máximo fixo e maximização desativada");
   SendMessage(form.Handle,0x112,new IntPtr(0xf030),IntPtr.Zero);Application.DoEvents();Check(form.Size==original&&form.WindowState==FormWindowState.Normal,"WM_SYSCOMMAND maximizar é bloqueado");
   SendMessage(form.Handle,0x112,new IntPtr(0xf000),IntPtr.Zero);Application.DoEvents();Check(form.Size==original,"WM_SYSCOMMAND redimensionar é bloqueado");
   form.Size=new Size(original.Width+150,original.Height+150);Check(form.Size==original,"Mudança programática de tamanho respeita limites");
   var engine=(DisplayEngine)typeof(MainForm).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);var ready=System.Diagnostics.Stopwatch.StartNew();bool hasPreview=false;
   while(ready.ElapsedMilliseconds<4000&&!hasPreview){Application.DoEvents();using(var snapshot=engine.Snapshot(true))hasPreview=snapshot.Preview!=null;if(!hasPreview)System.Threading.Thread.Sleep(40);}
   Check(hasPreview,"Prévia real é renderizada com USB desativada");form.UpdateState();Application.DoEvents();
   Capture(form,Path.Combine(folder,"Janela-minha-tela.png"));
   typeof(MainForm).GetMethod("SelectPage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{1});Application.DoEvents();
   CheckBox slideshow=null;ThemeBrowser browser=null;
   foreach(Control control in Descendants(form)){if(control is CheckBox&&control.Text=="Alternar os temas da biblioteca")slideshow=(CheckBox)control;if(control is ThemeBrowser)browser=(ThemeBrowser)control;}
   Check(slideshow!=null&&slideshow.Visible,"Controle Alternar é encontrado pelo texto e está visível");
   Rectangle checkBounds=form.RectangleToClient(slideshow.RectangleToScreen(slideshow.ClientRectangle));Rectangle footerBounds=form.RectangleToClient(slideshow.Parent.RectangleToScreen(slideshow.Parent.ClientRectangle));Rectangle browserBounds=form.RectangleToClient(browser.RectangleToScreen(browser.ClientRectangle));
   Check(form.ClientRectangle.Contains(checkBounds)&&footerBounds.Contains(checkBounds),"Checkbox fica dentro da janela e do rodapé");
   Check(!footerBounds.IntersectsWith(browserBounds)&&footerBounds.Top>=browserBounds.Bottom,"Rodapé da troca automática não se sobrepõe à galeria");
   slideshow.Checked=true;((Timer)typeof(MainForm).GetField("saveTimer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form)).Stop();
   var updated=(Settings)typeof(MainForm).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
   Check(updated.ThemeSlideshow&&updated.Mode=="Galeria de temas","Habilitar troca automática ativa o modo Galeria de temas");
   Capture(form,Path.Combine(folder,"Janela-temas.png"));
   Console.WriteLine("Dimensões: "+original+"; galeria="+browserBounds+"; rodapé="+footerBounds+"; checkbox="+checkBounds);
   form.Close();
  }
  return 0;
 }catch(Exception error){Console.WriteLine(error);return 1;}}
}
