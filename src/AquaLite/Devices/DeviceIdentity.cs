using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Security.Cryptography;

namespace AquaLite {
    public static class DeviceIdentity {
        [StructLayout(LayoutKind.Sequential)]struct Key {public Guid Format;public uint Id;public Key(string guid,uint id){Format=new Guid(guid);Id=id;}}
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Locate_DevNodeW(out uint node,string instance,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_DevNode_PropertyW(uint node,ref Key key,out uint type,byte[] buffer,ref uint size,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Set_DevNode_PropertyW(uint node,ref Key key,uint type,byte[] buffer,uint size,uint flags);
        [DllImport("shell32.dll")]static extern void SHChangeNotify(uint change,uint flags,IntPtr item,IntPtr other);
        [DataContract]sealed class Entry {
            [DataMember]public string Format;
            [DataMember]public uint Id,Type;
            [DataMember]public byte[] Bytes;
            [DataMember]public bool Existed;
        }
        [DataContract]sealed class Snapshot {
            [DataMember]public string Instance;
            [DataMember]public List<Entry> Entries=new List<Entry>();
        }
        static Key Name {get{return new Key("a45c254e-df1c-4efd-8020-67d146a850e0",14);}}
        static Key Model {get{return new Key("7d0300e4-4fb7-493d-8a95-656f00e6a271",2);}}
        static Key Icon {get{return new Key("7d0300e4-4fb7-493d-8a95-656f00e6a271",5);}}
        static Entry Read(uint node,Key key){uint size=16384,type;byte[] buffer=new byte[size];uint result=CM_Get_DevNode_PropertyW(node,ref key,out type,buffer,ref size,0);if(result!=0&&result!=0x25)throw new IOException("Não foi possível ler a identidade da tela (0x"+result.ToString("X")+").");if(result==0)Array.Resize(ref buffer,(int)size);return new Entry{Format=key.Format.ToString(),Id=key.Id,Type=type,Bytes=result==0?buffer:null,Existed=result==0};}
        static uint Set(uint node,Key key,string value){byte[] bytes=Encoding.Unicode.GetBytes(value+"\0");return CM_Set_DevNode_PropertyW(node,ref key,18,bytes,(uint)bytes.Length,0);}
        public static string Apply(bool restore,string iconPath){
            List<string> paths=UsbDisplay.Find();if(paths.Count!=1)throw new IOException("Conecte somente uma tela LCD compatível.");
            string[] parts=paths[0].Split('#');if(parts.Length!=4||!parts[1].Equals("vid_1cbe&pid_0021",StringComparison.OrdinalIgnoreCase))throw new IOException("A tela encontrada não corresponde ao modelo esperado.");
            string instance="USB\\VID_1CBE&PID_0021\\"+parts[2];uint node;uint result=CM_Locate_DevNodeW(out node,instance,0);if(result!=0)throw new IOException("A tela não pôde ser localizada.");
            Directory.CreateDirectory(Settings.Folder);string backupPath=Path.Combine(Settings.Folder,"identidade-windows-anterior.json");Snapshot backup;
            if(File.Exists(backupPath))using(FileStream stream=File.OpenRead(backupPath))backup=(Snapshot)new DataContractJsonSerializer(typeof(Snapshot)).ReadObject(stream);
            else {
                if(restore)return "Nenhum ajuste anterior de identidade foi encontrado.";
                backup=new Snapshot{Instance=instance};foreach(Key key in new Key[]{Name,Model,Icon})backup.Entries.Add(Read(node,key));
                using(FileStream stream=new FileStream(backupPath,FileMode.CreateNew,FileAccess.Write,FileShare.None))new DataContractJsonSerializer(typeof(Snapshot)).WriteObject(stream,backup);
            }
            if(!backup.Instance.Equals(instance,StringComparison.OrdinalIgnoreCase))throw new IOException("O backup pertence a outra tela. Nada foi alterado.");
            if(restore){foreach(Entry entry in backup.Entries){Key key=new Key(entry.Format,entry.Id);result=CM_Set_DevNode_PropertyW(node,ref key,entry.Existed?entry.Type:0,entry.Bytes,entry.Existed?(uint)entry.Bytes.Length:0,0);if(result!=0)throw new IOException("O Windows não permitiu restaurar uma propriedade. Backup preservado (0x"+result.ToString("X")+").");}SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);return "Nome e metadados anteriores restaurados. Reabra Dispositivos e Impressoras.";}
            result=Set(node,Name,"AquaLite LCD");if(result!=0)throw new IOException("O Windows não permitiu alterar o nome da tela (0x"+result.ToString("X")+").");
            Entry verify=Read(node,Name);if(!verify.Existed||Encoding.Unicode.GetString(verify.Bytes).TrimEnd('\0')!="AquaLite LCD")throw new IOException("A alteração do nome não foi confirmada pelo Windows.");
            uint modelResult=Set(node,Model,"AquaLite LCD");
            string resource=Path.Combine(Path.GetDirectoryName(iconPath),"AquaLite.DeviceIcons.dll");
            if(!File.Exists(resource))throw new FileNotFoundException("O recurso de ícone do AquaLite está ausente. Mantenha a pasta assets junto do aplicativo.",resource);
            string digest;using(SHA256 sha=SHA256.Create())using(FileStream stream=File.OpenRead(resource))digest=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").Substring(0,16);
            string permanentIcon=Path.Combine(Settings.Folder,"AquaLite.Icon-"+digest+".dll");if(!File.Exists(permanentIcon))File.Copy(resource,permanentIcon,false);
            uint iconResult=Set(node,Icon,permanentIcon+",-32512");
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
            return "Nome da tela alterado para AquaLite LCD. "+(iconResult==0?"Ícone específico da tela registrado.":"O Windows não permitiu alterar o ícone específico (0x"+iconResult.ToString("X")+").")+" Reabra Dispositivos e Impressoras para atualizar a visualização."+(modelResult!=0?" O nome de modelo do firmware permanece original.":"");
        }
    }
}
