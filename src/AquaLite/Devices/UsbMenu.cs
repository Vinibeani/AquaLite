using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace AquaLite {
    // This changes one per-device Windows property, never USB class policies.
    // No firmware, storage, driver package or other device is modified.
    public static class UsbMenu {
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format;public uint Id;public PropertyKey(uint id){Format=new Guid("afd97640-86a3-4210-b67c-289c41aabe55");Id=id;} }
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Locate_DevNodeW(out uint node,string instance,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_DevNode_PropertyW(uint node,ref PropertyKey key,out uint type,byte[] buffer,ref uint size,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Set_DevNode_PropertyW(uint node,ref PropertyKey key,uint type,byte[] buffer,uint size,uint flags);
        [DataContract]sealed class Backup {
            [DataMember]public string Instance;
            [DataMember]public bool Existed;
            [DataMember]public byte Original;
        }
        static string BackupPath {get{return Path.Combine(Settings.Folder,"politica-usb-anterior.json");}}
        static void Check(uint status,string action){if(status!=0)throw new IOException(action+" (código PnP 0x"+status.ToString("X")+").");}
        public static string Apply(bool hide) {
            Directory.CreateDirectory(Settings.Folder);
            List<string> paths=UsbDisplay.Find();
            if(paths.Count!=1)throw new IOException("Conecte somente uma tela LCD compatível para ajustar o menu USB.");
            string[] parts=paths[0].Split('#');
            if(parts.Length!=4||!parts[1].Equals("vid_1cbe&pid_0021",StringComparison.OrdinalIgnoreCase))throw new IOException("A identificação da tela não corresponde ao dispositivo esperado.");
            string instance="USB\\VID_1CBE&PID_0021\\"+parts[2];uint node;
            Check(CM_Locate_DevNodeW(out node,instance,0),"Não foi possível localizar a tela");
            PropertyKey key=new PropertyKey(3);Backup backup;
            if(File.Exists(BackupPath))using(FileStream stream=File.OpenRead(BackupPath))backup=(Backup)new DataContractJsonSerializer(typeof(Backup)).ReadObject(stream);
            else {
                if(!hide) return "Nenhum ajuste anterior do AquaLite foi encontrado. Nada foi alterado.";
                uint size=1,type;byte[] bytes=new byte[1];uint result=CM_Get_DevNode_PropertyW(node,ref key,out type,bytes,ref size,0);
                if(result!=0 && result!=0x25)Check(result,"Não foi possível ler a política atual");
                if(result==0&&(type!=17||size!=1))throw new IOException("A propriedade USB atual possui um formato inesperado. Nada foi alterado.");
                backup=new Backup {Instance=instance,Existed=result==0,Original=bytes[0]};
                using(FileStream stream=new FileStream(BackupPath,FileMode.CreateNew,FileAccess.Write,FileShare.None))new DataContractJsonSerializer(typeof(Backup)).WriteObject(stream,backup);
            }
            if(!backup.Instance.Equals(instance,StringComparison.OrdinalIgnoreCase))throw new IOException("O backup pertence a outra tela. Nenhuma alteração foi feita.");
            if(hide)Check(CM_Set_DevNode_PropertyW(node,ref key,17,new byte[]{0},1,0),"O Windows não permitiu ajustar a identificação USB");
            else Check(CM_Set_DevNode_PropertyW(node,ref key,backup.Existed?17u:0u,backup.Existed?new byte[]{backup.Original}:null,backup.Existed?1u:0u,0),"Não foi possível restaurar a política anterior");
            uint verifySize=1,verifyType;byte[] verify=new byte[1];uint verifyResult=CM_Get_DevNode_PropertyW(node,ref key,out verifyType,verify,ref verifySize,0);
            if(hide && (verifyResult!=0||verifyType!=17||verify[0]!=0))throw new IOException("O Windows não confirmou o ajuste. O backup foi preservado.");
            if(!hide && (backup.Existed ? verifyResult!=0||verify[0]!=backup.Original : verifyResult!=0x25))throw new IOException("O Windows não confirmou a restauração. O backup foi preservado.");
            return hide?"Ajuste salvo somente para esta tela. O Windows pode atualizar o menu USB após reconectar a tela ou reiniciar o PC. O driver e os outros dispositivos não foram alterados.":"Política USB anterior restaurada. Reconecte a tela ou reinicie o PC para atualizar o menu.";
        }
    }
}
