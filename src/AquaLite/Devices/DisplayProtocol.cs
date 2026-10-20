using System;
using System.Security.Cryptography;

namespace AquaLite {
    public static class DisplayProtocol {
        public static byte[] Header(byte command, byte[] arguments, uint timestamp) {
            byte[] plain = new byte[500];
            plain[0] = command; plain[2] = 0x1a; plain[3] = 0x6d;
            Buffer.BlockCopy(BitConverter.GetBytes(timestamp), 0, plain, 4, 4);
            if (arguments != null) {
                if (arguments.Length > 492) throw new ArgumentOutOfRangeException("arguments");
                Buffer.BlockCopy(arguments, 0, plain, 8, arguments.Length);
            }
            byte[] packet = new byte[512];
            using (DES des = DES.Create()) {
                des.Mode = CipherMode.CBC; des.Padding = PaddingMode.PKCS7;
                // Fixed wire-format bytes for the supported LCD; not an application credential.
                des.Key = new byte[]{0x73,0x6c,0x76,0x33,0x74,0x75,0x7a,0x78}; des.IV = des.Key;
                using (ICryptoTransform encrypt = des.CreateEncryptor()) {
                    byte[] encrypted = encrypt.TransformFinalBlock(plain, 0, plain.Length);
                    Buffer.BlockCopy(encrypted, 0, packet, 0, encrypted.Length);
                }
            }
            packet[510] = 0xa1; packet[511] = 0x1a; return packet;
        }
        public static uint Timestamp() {
            return unchecked((uint)Math.Round((DateTime.UtcNow - DateTime.Today.AddDays(-1)).TotalMilliseconds));
        }
        public static byte[] ImageLength(int length) {
            if (length < 1 || length > 8 * 1024 * 1024) throw new ArgumentOutOfRangeException("length");
            return new byte[] {(byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length};
        }
    }

}
