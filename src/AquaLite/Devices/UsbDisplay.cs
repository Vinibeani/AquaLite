using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AquaLite {
    // USB LCD transport. This class does not modify firmware or cooling controls.
    // This class never flashes firmware, installs drivers or controls cooling.
    public sealed class UsbDisplay : IDisposable {
        SafeFileHandle file;
        IntPtr usb;
        byte input, output;
        readonly object gate = new object();
        public string DevicePath { get; private set; }
        public string Firmware { get; private set; }
        public string PipeDescription { get; private set; }
        public bool IsOpen { get { return usb != IntPtr.Zero; } }
        public long Frames { get; private set; }
        public long Bytes { get; private set; }
        public string LastResponse { get; private set; }
        public static List<string> Find() { return UsbDisplayDiscovery.Find(); }
        public void Open() {
            lock (gate) {
                if (IsOpen) return;
                List<string> devices = Find();
                if (devices.Count == 0) throw new IOException("Tela LCD 2,1″ não encontrada. Confira o cabo USB.");
                if (devices.Count != 1) throw new IOException("Há mais de uma tela compatível. Conecte somente a tela desejada.");
                DevicePath = devices[0];
                file = WinUsbNative.CreateFile(DevicePath, 0xc0000000, 3, IntPtr.Zero, 3, 0x40000080, IntPtr.Zero);
                if (file.IsInvalid) { int error = Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(error, "Não foi possível abrir a tela. Feche outros aplicativos que estejam usando o visor."); }
                try {
                    if (!WinUsbNative.WinUsb_Initialize(file, out usb)) throw new Win32Exception(Marshal.GetLastWin32Error(), "O driver WinUSB não permitiu a conexão.");
                    WinUsbNative.UsbInterface info;
                    if (!WinUsbNative.WinUsb_QueryInterfaceSettings(usb, 0, out info)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    StringBuilder description = new StringBuilder();
                    for (byte i = 0; i < info.Endpoints; i++) {
                        WinUsbNative.PipeInfo pipe;
                        if (!WinUsbNative.WinUsb_QueryPipe(usb, 0, i, out pipe)) throw new Win32Exception(Marshal.GetLastWin32Error());
                        description.AppendFormat("0x{0:X2}:tipo={1},pacote={2}; ", pipe.Id, pipe.Type, pipe.PacketSize);
                        if ((pipe.Type == 2 || pipe.Type == 3) && (pipe.Id & 128) != 0) input = pipe.Id;
                        if (pipe.Type == 2 && (pipe.Id & 128) == 0) output = pipe.Id;
                    }
                    PipeDescription = description.ToString();
                    if (input == 0 || output == 0) throw new IOException("A tela não expôs os canais USB esperados.");
                    SetTimeout(input, 1400); SetTimeout(output, 2500);
                } catch { Dispose(); throw; }
            }
        }
        void SetTimeout(byte pipe, uint value) {
            if (!WinUsbNative.WinUsb_SetPipePolicy(usb, pipe, 3, 4, ref value)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        void Write(byte[] data) {
            uint count;
            if (!WinUsbNative.WinUsb_WritePipe(usb, output, data, (uint)data.Length, out count, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Falha ao enviar dados para a tela.");
            if (count != data.Length) throw new IOException("A tela recebeu apenas parte dos dados. A conexão foi interrompida.");
            Bytes += count;
        }
        byte[] Exchange(byte command, byte[] arguments, byte[] image,bool requireSuccess=true,int minimumResponse=8) {
            lock (gate) {
                if (!IsOpen) throw new IOException("Conecte a tela antes de enviar.");
                SetTimeout(input, 5);
                try {
                    for(int stale=0;stale<12;stale++) {
                        uint staleCount;byte[] staleBuffer=new byte[512];
                        if(!WinUsbNative.WinUsb_ReadPipe(usb,input,staleBuffer,512,out staleCount,IntPtr.Zero))break;
                    }
                } finally {SetTimeout(input,1400);}
                byte[] packet = DisplayProtocol.Header(command, arguments, DisplayProtocol.Timestamp());
                // Send the header and its payload as one USB transfer.
                if (image != null) {
                    byte[] combined = new byte[packet.Length + image.Length];
                    Buffer.BlockCopy(packet, 0, combined, 0, packet.Length);
                    Buffer.BlockCopy(image, 0, combined, packet.Length, image.Length);
                    Write(combined);
                } else Write(packet);
                byte[] response = new byte[512]; uint count=0;
                for(int attempt=0;attempt<8;attempt++) {
                    if (!WinUsbNative.WinUsb_ReadPipe(usb, input, response, 512, out count, IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error(), "A tela não confirmou o comando. Tente reconectar.");
                    // A zero-length USB packet terminates some firmware replies.
                    // It is transport framing, not the next command's acknowledgement.
                    if(count>=8 && response[0]==command)break;
                }
                LastResponse = BitConverter.ToString(response, 0, (int)Math.Min(count, 32));
                if (count < minimumResponse || response[0] != command || (requireSuccess&&response[1] != 200))
                    throw new IOException("Resposta inesperada da tela: " + LastResponse);
                return response;
            }
        }
        public string Query() {
            byte[] response = Exchange(10, null, null);
            int end = Array.IndexOf(response, (byte)0, 8); if (end < 0) end = response.Length;
            Firmware = Encoding.UTF8.GetString(response, 8, end - 8).Trim();
            return Firmware;
        }
        public void Prepare() {
            // Volatile session only: leave on-device files, settings and firmware intact.
            SetSession(0);
            Exchange(123,null,null);
            System.Threading.Thread.Sleep(100);
            SetSession(2);
            Exchange(111, null, null);
            Exchange(52,new byte[]{0},null);
        }
        void SetSession(byte mode) {
            DateTime now=DateTime.Now;
            Exchange(51,new byte[]{(byte)(now.Year>>8),(byte)now.Year,(byte)now.Month,(byte)now.Day,(byte)now.Hour,(byte)now.Minute,(byte)now.Second,mode},null);
        }
        public void EndSession(){if(IsOpen){SetSession(0);Exchange(123,null,null);}}
        public int BeginH264(int frameRate,byte[] transparentPng) {
            if(frameRate<1||frameRate>120)throw new ArgumentOutOfRangeException("frameRate");
            if(transparentPng==null||transparentPng.Length<8||transparentPng[0]!=137||transparentPng[1]!=80)throw new ArgumentException("Camada PNG inválida.");
            Exchange(123,null,null);SetSession(2);ClearOverlay(transparentPng);
            Exchange(15,new byte[]{(byte)frameRate},null,false);
            byte[] response=Exchange(17,null,null,false,12);
            int size=(response[8]<<24)|(response[9]<<16)|(response[10]<<8)|response[11];
            return size>0&&size<=4*1024*1024?size:202752;
        }
        public int SendH264(byte[] bytes,bool last) {
            if(bytes==null||bytes.Length==0||bytes.Length>4*1024*1024)throw new ArgumentException("Bloco de vídeo inválido.");
            byte[] args=new byte[5];byte[] size=DisplayProtocol.ImageLength(bytes.Length);Buffer.BlockCopy(size,0,args,0,4);args[4]=(byte)(last?1:0);
            byte[] response=Exchange(121,args,bytes,false,9);return response[8];
        }
        public int VideoQueue(){return Exchange(122,null,null,false,9)[8];}
        public void StopH264(){if(IsOpen)Exchange(123,null,null,false);}
        public void Brightness(int percent) { Exchange(14, new byte[] {(byte)(Math.Max(0, Math.Min(100, percent)) * 255 / 100)}, null); }
        public void ClearOverlay(byte[] transparentPng) {
            if(transparentPng==null||transparentPng.Length<8||transparentPng[0]!=137||transparentPng[1]!=80)throw new ArgumentException("Camada PNG inválida.");
            Exchange(102,DisplayProtocol.ImageLength(transparentPng.Length),transparentPng);
        }
        public void SendJpeg(byte[] jpeg) {
            if (jpeg == null || jpeg.Length < 4 || jpeg[0] != 255 || jpeg[1] != 216) throw new ArgumentException("Imagem JPEG inválida.");
            // Prepare the image buffer before every complete frame. A previous
            // on-device scene otherwise survives an acknowledged image transfer.
            Exchange(52,new byte[]{0},null);
            Exchange(101, DisplayProtocol.ImageLength(jpeg.Length), jpeg); Frames++;
        }
        public void Dispose() {
            lock (gate) {
                if (usb != IntPtr.Zero) { WinUsbNative.WinUsb_Free(usb); usb = IntPtr.Zero; }
                if (file != null) { file.Dispose(); file = null; }
                input = output = 0;
            }
        }
    }
}
