using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AquaLite {
    public static class Palette {
        public static Color Background=Color.FromArgb(12,18,29),Sidebar=Color.FromArgb(9,14,23),Card=Color.FromArgb(20,29,43),Border=Color.FromArgb(39,53,71),Muted=Color.FromArgb(145,162,181),Text=Color.FromArgb(237,244,250),Accent=Color.FromArgb(59,224,206);
        public static Font Font(float size, bool bold=false) {return new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Point);}
    }
    public sealed class Card : Panel {
        public Card() { DoubleBuffered=true; BackColor=Palette.Card; }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using(Pen p=new Pen(Palette.Border))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1); }
    }
    public sealed class FlatButton : Button {
        public bool Primary;
        public FlatButton(string caption, bool primary=false) { Text=caption;Primary=primary;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=primary?Palette.Accent:Color.FromArgb(35,49,66);ForeColor=primary?Palette.Sidebar:Palette.Text;Font=Palette.Font(10,true);Cursor=Cursors.Hand;Height=39; }
    }
    public sealed class PreviewControl : Control {
        Bitmap image;
        public PreviewControl() { DoubleBuffered=true; BackColor=Palette.Card; }
        public void SetImage(Bitmap value) { Bitmap old=image; image=value; if(old!=null)old.Dispose();Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            int diameter=Math.Min(Width,Height)-28; Rectangle rectangle=new Rectangle((Width-diameter)/2,(Height-diameter)/2,diameter,diameter);
            using(SolidBrush shadow=new SolidBrush(Color.FromArgb(5,10,18)))g.FillEllipse(shadow,rectangle.X-9,rectangle.Y-9,diameter+18,diameter+18);
            using(Pen rim=new Pen(Color.FromArgb(45,65,83),3))g.DrawEllipse(rim,rectangle);
            using(GraphicsPath path=new GraphicsPath()) {
                path.AddEllipse(rectangle);GraphicsState state=g.Save();g.SetClip(path);
                if(image!=null)g.DrawImage(image,rectangle); else using(SolidBrush brush=new SolidBrush(Palette.Background))g.FillEllipse(brush,rectangle);
                g.Restore(state);
            }
        }
        protected override void Dispose(bool disposing) {if(disposing&&image!=null){image.Dispose();image=null;}base.Dispose(disposing);}
    }
}
