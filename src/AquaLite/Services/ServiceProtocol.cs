using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace AquaLite {
    [DataContract] public sealed class ServiceRequest {
        [DataMember] public string Operation;
        [DataMember] public Settings Settings;
        [DataMember] public bool Enabled,IncludePreview;
    }
    [DataContract] public sealed class ServiceReply {
        [DataMember] public string Error,Status,Firmware,ThemeId,PreviewPng;
        [DataMember] public bool Connected,Wanted;
        [DataMember] public Reading Reading;
        [DataMember] public long Frames,Bytes;
        [DataMember] public double FrameMilliseconds;
    }
    public static class ServiceWire {
        public const int MaximumBytes=4*1024*1024;
        static void ReadExact(Stream stream,byte[] data) {
            int offset=0;
            while(offset<data.Length) {
                IAsyncResult pending=stream.BeginRead(data,offset,data.Length-offset,null,null);
                int count;
                using(var wait=pending.AsyncWaitHandle){if(!wait.WaitOne(5000)){stream.Dispose();throw new IOException("O serviço da tela não respondeu a tempo.");}count=stream.EndRead(pending);}
                if(count==0)throw new EndOfStreamException();offset+=count;
            }
        }
        public static T Read<T>(Stream stream) {
            byte[] header=new byte[4];ReadExact(stream,header);int length=BitConverter.ToInt32(header,0);
            if(length<=0||length>MaximumBytes)throw new InvalidDataException("Mensagem do serviço fora do limite.");
            byte[] bytes=new byte[length];ReadExact(stream,bytes);
            using(var body=new MemoryStream(bytes))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(body);
        }
        public static void Write<T>(Stream stream,T value) {
            byte[] bytes;using(var body=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(body,value);bytes=body.ToArray();}
            if(bytes.Length==0||bytes.Length>MaximumBytes)throw new InvalidDataException("Mensagem do serviço fora do limite.");
            byte[] header=BitConverter.GetBytes(bytes.Length);stream.Write(header,0,4);stream.Write(bytes,0,bytes.Length);stream.Flush();
        }
    }
    public static class ServiceClient {
        public const string PipeName="AquaLite.Display.v1";
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint processId);
        public static ServiceReply Send(ServiceRequest request) { return Send(request,PipeName); }
        public static ServiceReply Send(ServiceRequest request,string pipeName) {
            using(var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous)) {
                pipe.Connect(2000);
                if(pipeName==PipeName) {
                    uint actual;uint expected=ServiceRegistration.ProcessId;
                    if(expected==0||!GetNamedPipeServerProcessId(pipe.SafePipeHandle,out actual)||actual!=expected)throw new IOException("A conexão não pertence ao serviço instalado.");
                }
                ServiceWire.Write(pipe,request);ServiceReply reply=ServiceWire.Read<ServiceReply>(pipe);
                if(reply==null)throw new IOException("Resposta vazia do serviço.");
                if(!string.IsNullOrEmpty(reply.Error))throw new IOException(reply.Error);
                return reply;
            }
        }
    }
}
