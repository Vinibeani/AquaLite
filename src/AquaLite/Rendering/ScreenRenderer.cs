using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;

namespace AquaLite {
    public sealed class ScreenRenderer : IDisposable {
        Image media;
        MemoryStream mediaStream;
        string loadedPath;
        LibraryRenderer library;
        VideoPlayer video;
        int frames;
        int[] delays;
        readonly Stopwatch mediaClock = new Stopwatch();
        readonly Font title = new Font("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Pixel);
        readonly Font large = new Font("Segoe UI", 91, FontStyle.Bold, GraphicsUnit.Pixel);
        readonly Font medium = new Font("Segoe UI", 26, FontStyle.Regular, GraphicsUnit.Pixel);
        readonly Font small = new Font("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Pixel);
        readonly Font clockFont = new Font("Segoe UI", 79, FontStyle.Bold, GraphicsUnit.Pixel);
        readonly Font metric = new Font("Segoe UI", 28, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly CultureInfo Portuguese = new CultureInfo("pt-BR");
        readonly ImageCodecInfo jpeg;
        public ScreenRenderer() { foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders()) if (codec.MimeType == "image/jpeg") { jpeg = codec; break; } }
        public void Load(string path) {
            if (path == loadedPath) return;
            if(!string.IsNullOrEmpty(path)&&Path.GetExtension(path).Equals(".mp4",StringComparison.OrdinalIgnoreCase)){
                VideoPlayer next=new VideoPlayer(path);if(video!=null)video.Dispose();video=next;if(media!=null){media.Dispose();media=null;}if(mediaStream!=null){mediaStream.Dispose();mediaStream=null;}loadedPath=path;return;
            }
            if(video!=null){video.Dispose();video=null;}
            Image replacement = null; MemoryStream replacementStream = null; int count = 1; int[] durations = null;
            if (!string.IsNullOrWhiteSpace(path)) {
                FileInfo file = new FileInfo(path);
                if (!file.Exists) throw new FileNotFoundException("O arquivo de imagem não está mais disponível.", path);
                if (file.Length > 64 * 1024 * 1024) throw new IOException("Escolha uma imagem ou GIF de até 64 MB.");
                byte[] bytes = File.ReadAllBytes(path);
                replacementStream = new MemoryStream(bytes,false);
                try {replacement=Image.FromStream(replacementStream,true,true);
                    if ((long)replacement.Width * replacement.Height > 24000000) throw new IOException("A imagem ultrapassa 24 megapixels. Reduza a resolução antes de importar.");
                } catch {if(replacement!=null)replacement.Dispose();replacementStream.Dispose();throw;}
                try {
                    if (Array.IndexOf(replacement.FrameDimensionsList, FrameDimension.Time.Guid) >= 0) {
                        count = replacement.GetFrameCount(FrameDimension.Time);
                        if (count > 2000 || (long)count * replacement.Width * replacement.Height > 180000000)
                            throw new IOException("Este GIF é muito grande para o modo leve. Reduza a duração ou a resolução.");
                        durations = new int[count];
                        PropertyItem property = replacement.GetPropertyItem(0x5100);
                        for (int i = 0; i < count; i++) durations[i] = property.Value.Length >= (i+1)*4 ? Math.Max(20, BitConverter.ToInt32(property.Value, i*4)*10) : 100;
                    }
                } catch (ArgumentException) { durations = new int[count]; for (int i=0;i<count;i++) durations[i]=100; }
                catch { replacement.Dispose(); replacementStream.Dispose(); throw; }
            }
            if (media != null) media.Dispose();
            if (mediaStream != null) mediaStream.Dispose();
            media = replacement; mediaStream = replacementStream; frames = count; delays = durations; loadedPath = path; mediaClock.Restart();
        }
        public void ValidateMedia(string path) { Load(path); }
        public bool Animated { get { return video!=null || media != null && frames > 1 || library!=null && library.Animated; } }
        public static byte[] EmptyOverlay() {
            using(Bitmap bitmap=new Bitmap(480,480,PixelFormat.Format32bppArgb))
            using(MemoryStream stream=new MemoryStream()) {
                using(Graphics g=Graphics.FromImage(bitmap))g.Clear(Color.Transparent);
                bitmap.Save(stream,ImageFormat.Png);return stream.ToArray();
            }
        }
        static void Center(Graphics g, string text, Font font, Brush brush, float x, float y, float width, float height) {
            using (StringFormat format = new StringFormat()) { format.Alignment = StringAlignment.Center; format.LineAlignment = StringAlignment.Center; g.DrawString(text, font, brush, new RectangleF(x,y,width,height),format); }
        }
        public Bitmap Render(Settings settings, Reading reading) {
            if(settings.Mode=="Galeria de temas"){if(library==null)library=new LibraryRenderer();library.Load(settings.LibraryThemeId);}else if(library!=null){library.Dispose();library=null;}
            Bitmap result = new Bitmap(480,480, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(result)) {
                g.Clear(Color.FromArgb(8,14,24)); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.TranslateTransform(240,240); g.RotateTransform(settings.Rotation); g.TranslateTransform(-240,-240);
                Color accent;
                try { accent = ColorTranslator.FromHtml(settings.Accent); } catch { accent = Color.Turquoise; }
                using (SolidBrush cyan = new SolidBrush(accent))
                using (SolidBrush light = new SolidBrush(Color.FromArgb(238,246,255)))
                using (SolidBrush muted = new SolidBrush(Color.FromArgb(136,158,179)))
                using (Pen track = new Pen(Color.FromArgb(27,42,57), 13))
                using (Pen arc = new Pen(accent,13)) {
                    arc.StartCap = arc.EndCap = LineCap.Round;
                    if(video!=null&&(settings.Mode=="Vídeo"||settings.Mode=="Imagem ou GIF"||settings.Mode=="Personalizado"&&settings.CustomBackground))using(var frame=video.Snapshot()){if(frame!=null)g.DrawImage(frame,0,0,480,480);}
                    if ((settings.Mode == "Imagem ou GIF" || settings.Mode=="Personalizado" && settings.CustomBackground) && media != null) {
                        if (frames > 1 && delays != null) {
                            long total = 0; for (int i=0;i<delays.Length;i++) total += delays[i];
                            long current = total == 0 ? 0 : mediaClock.ElapsedMilliseconds % total;
                            int frame=0; while (frame < frames-1 && current >= delays[frame]) {current -= delays[frame]; frame++;}
                            media.SelectActiveFrame(FrameDimension.Time,frame);
                        }
                        double scale = settings.Crop ? Math.Max(480.0/media.Width,480.0/media.Height) : Math.Min(480.0/media.Width,480.0/media.Height);
                        float width=(float)(media.Width*scale), height=(float)(media.Height*scale);
                        g.DrawImage(media, (480-width)/2,(480-height)/2,width,height);
                    }
                    if(settings.Mode=="Galeria de temas") {
                        library.Draw(g,reading);
                    } else if(settings.Mode=="Personalizado") {
                        foreach(Widget widget in settings.Widgets??Widget.Defaults())DrawWidget(g,widget,reading);
                    } else if (settings.Mode == "Relógio") {
                        g.DrawEllipse(track,34,34,412,412);
                        g.DrawArc(arc,34,34,412,412,-90,Math.Max(0.2f,reading.Time.Second*6));
                        Center(g,settings.Caption.ToUpper(Portuguese),title,cyan,40,105,400,28);
                        Center(g,reading.Time.ToString("HH:mm"),clockFont,light,30,157,420,110);
                        Center(g,reading.Time.ToString("dddd",Portuguese),medium,muted,30,286,420,45);
                        Center(g,reading.Time.ToString("dd 'de' MMMM",Portuguese),small,muted,40,332,400,30);
                    } else if(settings.Mode!="Imagem ou GIF"&&settings.Mode!="Vídeo") {
                        double value = reading.CpuTemperature??0;
                        string unit = "°C";
                        string name = "TEMPERATURA DA CPU";
                        g.DrawArc(track,38,38,404,404,135,270);
                        if(reading.CpuTemperature.HasValue)g.DrawArc(arc,38,38,404,404,135,Math.Max(0.3f,(float)(Math.Min(100,value)/100*270)));
                        Center(g,settings.Caption.ToUpper(Portuguese),title,cyan,50,68,380,26);
                        Center(g,name,title,muted,40,129,400,24);
                        Center(g,reading.CpuTemperature.HasValue?Math.Round(value).ToString("0"):"—",large,light,70,158,290,113);
                        Center(g,unit,medium,cyan,331,194,75,48);
                        using (Pen divider = new Pen(Color.FromArgb(39,56,72),1)) g.DrawLine(divider,131,302,349,302);
                        Center(g,"MEMÓRIA",title,muted,80,322,150,22);
                        Center(g,reading.GpuTemperature.HasValue ? "GPU" : "HORÁRIO",title,muted,250,322,150,22);
                        Center(g,reading.MemoryPercent.ToString("0")+"%",metric,light,80,348,150,45);
                        Center(g,reading.GpuTemperature.HasValue ? reading.GpuTemperature.Value.ToString("0")+" °C" : reading.Time.ToString("HH:mm"),metric,light,245,348,155,45);
                    }
                }
            }
            return result;
        }
        public static string WidgetText(Widget widget,Reading reading) {
            switch(widget.Kind) {
                case "Clock da CPU":return "CPU "+Metric(reading.CpuClockMHz,"MHz");
                case "Potência da CPU":return "CPU "+Metric(reading.CpuPowerWatts,"W");
                case "Tensão da CPU":return "CPU "+Metric(reading.CpuVoltage,"V","0.00");
                case "Clock da GPU":return "GPU "+Metric(reading.GpuClockMHz,"MHz");
                case "Potência da GPU":return "GPU "+Metric(reading.GpuPowerWatts,"W");
                case "Ventoinha da GPU":return "GPU "+Metric(reading.GpuFanRpm,"RPM");
                case "VRAM usada":return "VRAM "+Metric(reading.GpuMemoryUsedGB,"GB","0.0");
                case "Volume":return "Volume "+Metric(reading.GetValue("Volume"),"%");
                case "FPS":return Metric(reading.GetValue("FPS"),"FPS");
                case "Download":return "Download "+Throughput(reading.NetworkDownloadBytesPerSecond);
                case "Upload":return "Upload "+Throughput(reading.NetworkUploadBytesPerSecond);
                case "Sensor":
                    if(reading.Sensors!=null)foreach(SensorReading sensor in reading.Sensors)
                        if(!string.IsNullOrEmpty(widget.SensorId)&&string.Equals(sensor.Id,widget.SensorId,StringComparison.OrdinalIgnoreCase))return Metric(sensor.Value,sensor.Unit,"0.##");
                    return "—";
                case "Hora":return reading.Time.ToString("HH:mm");
                case "AM/PM":return reading.Time.Hour>=12?"PM":"AM";
                case "Clima":return reading.WeatherText??"—";
                case "Data":return reading.Time.ToString("dd 'de' MMMM",Portuguese);
                case "Uso da CPU":return "CPU "+reading.Cpu.ToString("0")+"%";
                case "Temperatura da CPU":return "CPU "+(reading.CpuTemperature.HasValue?reading.CpuTemperature.Value.ToString("0")+" °C":"— °C");
                case "Uso da RAM":return "RAM "+reading.MemoryPercent.ToString("0")+"%";
                case "Temperatura da GPU":return "GPU "+(reading.GpuTemperature.HasValue?reading.GpuTemperature.Value.ToString("0")+" °C":"— °C");
                case "Uso da GPU":return "GPU "+(reading.GpuLoad.HasValue?reading.GpuLoad.Value.ToString("0")+"%":"—%");
                default:return widget.Text??"";
            }
        }
        static string Metric(double? value,string unit,string format="0") {
            return (value.HasValue&&SensorReading.IsFinite(value.Value)?value.Value.ToString(format,Portuguese):"—")+(string.IsNullOrEmpty(unit)?"":" "+unit);
        }
        static string Throughput(double? value) {
            if(!value.HasValue||!SensorReading.IsFinite(value.Value))return "— B/s";
            double amount=Math.Max(0,value.Value);string unit="B/s";
            if(amount>=1048576){amount/=1048576;unit="MB/s";}else if(amount>=1024){amount/=1024;unit="KB/s";}
            return Metric(amount,unit,"0.##");
        }
        public static RectangleF WidgetBounds(Graphics g,Widget widget,Reading reading) {
            if(widget.Kind.StartsWith("Barra"))return new RectangleF(widget.X-widget.Width/2f,widget.Y-widget.Size/2f,widget.Width,widget.Size);
            using(Font font=new Font("Segoe UI",widget.Size,widget.Bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel)) {
                SizeF size=g.MeasureString(WidgetText(widget,reading),font);
                return new RectangleF(widget.X-size.Width/2,widget.Y-size.Height/2,size.Width,size.Height);
            }
        }
        static void DrawWidget(Graphics g,Widget widget,Reading reading) {
            if(!widget.Visible)return;
            Color color;try{color=ColorTranslator.FromHtml(widget.Color);}catch{color=Color.White;}
            using(SolidBrush brush=new SolidBrush(color)) {
                RectangleF bounds=WidgetBounds(g,widget,reading);
                if(widget.Kind.StartsWith("Barra")) {
                    using(SolidBrush track=new SolidBrush(Color.FromArgb(51,65,80)))g.FillRectangle(track,bounds);
                    double percentage=widget.Kind=="Barra da RAM"?reading.MemoryPercent:reading.Cpu;
                    g.FillRectangle(brush,bounds.X,bounds.Y,bounds.Width*(float)Math.Min(1,Math.Max(0,percentage/100)),bounds.Height);
                } else using(Font font=new Font("Segoe UI",widget.Size,widget.Bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel))Center(g,WidgetText(widget,reading),font,brush,bounds.X,bounds.Y,bounds.Width,bounds.Height);
            }
        }
        public byte[] Encode(Bitmap bitmap, long quality) {
            using (MemoryStream stream = new MemoryStream())
            using (EncoderParameters parameters = new EncoderParameters(1)) {
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,quality);
                bitmap.Save(stream,jpeg,parameters); return stream.ToArray();
            }
        }
        public void Dispose() { if(video!=null)video.Dispose();if(library!=null)library.Dispose();if(media != null)media.Dispose();if(mediaStream!=null)mediaStream.Dispose(); title.Dispose();large.Dispose();medium.Dispose();small.Dispose();clockFont.Dispose();metric.Dispose(); }
    }
}
