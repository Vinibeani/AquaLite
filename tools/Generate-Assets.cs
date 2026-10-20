using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
class GenerateAssets {
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    static Bitmap Logo(int size) {
        var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bitmap)) {
            g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);g.ScaleTransform(size/128f,size/128f);
            using(var back=new SolidBrush(Color.FromArgb(12,18,29)))g.FillEllipse(back,4,4,120,120);
            using(var ring=new Pen(Color.FromArgb(59,224,206),7))g.DrawEllipse(ring,22,22,84,84);
            using(var path=new GraphicsPath()) {
                path.AddBezier(64,29,54,45,39,59,39,75);path.AddBezier(39,75,39,108,89,108,89,75);path.AddBezier(89,75,89,59,74,45,64,29);path.CloseFigure();
                using(var fill=new SolidBrush(Color.FromArgb(59,224,206)))g.FillPath(fill,path);
            }
        }
        return bitmap;
    }
    static void Text(Graphics g,string text,float size,Color color,float y) {
        using(var font=new Font("Segoe UI",size,FontStyle.Bold,GraphicsUnit.Pixel))
        using(var brush=new SolidBrush(color))
        using(var format=new StringFormat{Alignment=StringAlignment.Center})g.DrawString(text,font,brush,new PointF(240,y),format);
    }
    static int Main(string[] args) {
        if(args.Length!=1)throw new ArgumentException("Informe a pasta de recursos gerados.");
        string target=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.Combine(target,"assets"));
        using(var logo=Logo(128))logo.Save(Path.Combine(target,"assets","aqualite.png"),ImageFormat.Png);
        using(var logo=Logo(64)) {
            IntPtr handle=logo.GetHicon();
            try{using(var icon=(Icon)Icon.FromHandle(handle).Clone())using(var stream=File.Create(Path.Combine(target,"assets","aqualite.ico")))icon.Save(stream);}finally{DestroyIcon(handle);}
        }
        string[] ids={"aqua","brasa","oceano"};string[] names={"AQUA","BRASA","OCEANO"};string[] colors={"#3BE0CE","#FF965B","#6CA8FF"};
        for(int i=0;i<ids.Length;i++) {
            Color accent=ColorTranslator.FromHtml(colors[i]);string folder=Path.Combine(target,"themes",ids[i]);Directory.CreateDirectory(folder);
            using(var bitmap=new Bitmap(480,480))using(var g=Graphics.FromImage(bitmap)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var gradient=new LinearGradientBrush(new Rectangle(0,0,480,480),Color.FromArgb(8,14,24),Color.FromArgb(24,accent.R/5,accent.G/5),45))g.FillRectangle(gradient,0,0,480,480);
                using(var ring=new Pen(Color.FromArgb(40,accent),3))g.DrawEllipse(ring,35,35,410,410);
                using(var ring=new Pen(accent,5)){ring.StartCap=ring.EndCap=LineCap.Round;g.DrawArc(ring,35,35,410,410,135,105);}
                bitmap.Save(Path.Combine(folder,"background.png"),ImageFormat.Png);
                Text(g,names[i],24,accent,85);Text(g,"TEMPERATURA DA CPU",16,Color.SlateGray,147);Text(g,"— °C",78,Color.White,185);Text(g,"12:00",35,accent,280);Text(g,"SEU PC, SEU ESTILO",15,Color.SlateGray,375);
                bitmap.Save(Path.Combine(folder,"preview.png"),ImageFormat.Png);
            }
        }
        return 0;
    }
}
