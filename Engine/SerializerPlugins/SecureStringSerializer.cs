//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace OpenTap.Plugins
{
    internal static class CryptographyHelper
    {
        private static readonly int iterations = 3;
        private static readonly int nofKeyBytes = 32; // 256/8
        private static readonly string hashMethod = "SHA1"; // 20B
        private static readonly byte[] iv = ASCIIEncoding.ASCII.GetBytes("dqg63928163gdg19"); // initialization vector
        private static readonly byte[] salt = ASCIIEncoding.ASCII.GetBytes("b37vrg37r83g8v36"); // salt as byte array
        
        internal static string Encrypt(string input, string password)
        {
            byte[] output;
            byte[] inputAsBytes = UTF8Encoding.UTF8.GetBytes(input);
            
            using (Aes aesAlgo = Aes.Create())
            {
                byte[] keyBytes = new PasswordDeriveBytes(password, salt, hashMethod, iterations).GetBytes(nofKeyBytes);
                aesAlgo.Mode = CipherMode.ECB;
                using (MemoryStream memoryStreamDestination = new MemoryStream())
                {
                    using (ICryptoTransform encryptor = aesAlgo.CreateEncryptor(keyBytes, iv))
                    {
                        using (CryptoStream writer = new CryptoStream(memoryStreamDestination, encryptor, CryptoStreamMode.Write))
                        {
                            writer.Write(inputAsBytes, 0, inputAsBytes.Length);
                            writer.FlushFinalBlock();
                            output = memoryStreamDestination.ToArray();
                        }
                    }
                }
                aesAlgo.Clear();
            }
            return Convert.ToBase64String(output);
        }

        internal static string Decrypt(string input, string password)
        {
            string result = String.Empty;
            int decryptedBytesCount = 0;
            byte[] output;
            byte[] inputAsBytes = Convert.FromBase64String(input);
            
            using (Aes aesAlgo = Aes.Create())
            {
                byte[] keyBytes = new PasswordDeriveBytes(password, salt, hashMethod, iterations).GetBytes(nofKeyBytes);
                aesAlgo.Mode = CipherMode.ECB;

                try
                {
                    using (MemoryStream inputStream = new MemoryStream(inputAsBytes))
                    {
                        using (ICryptoTransform decryptor = aesAlgo.CreateDecryptor(keyBytes, iv))
                        {
                            using (CryptoStream reader = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
                            {
                                output = new byte[inputAsBytes.Length];
                                decryptedBytesCount = reader.Read(output, 0, output.Length);
                                result = Encoding.UTF8.GetString(output, 0, decryptedBytesCount);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // swallow exception
                }
                aesAlgo.Clear();
            }
            return result;
        }

    }
    /// <summary> Serializer implementation for SecureStrings. </summary>
    public class SecureStringSerializer : TapSerializerPlugin
    {
        private const string password = "P4ssw0rdF0rS3r1al1z1ngS3cur3Str1ngs";

        /// <summary> Deserialization implementation for SecureString. </summary>
        /// <param name="node"></param>
        /// <param name="targetType"></param>
        /// <param name="setResult"></param>
        /// <returns></returns>
        public override bool Deserialize(XElement node, ITypeInfo targetType, Action<object> setResult)
        {
            if (targetType.IsA(typeof(System.Security.SecureString)) == false) return false;
            string valueString = node.Value;
            var result = new System.Security.SecureString();
            setResult(result);
            try
            {
                string encryptedString = CryptographyHelper.Decrypt(valueString, password);
                foreach (var c in encryptedString)
                    result.AppendChar(c);

                return true;
            }
            catch
            {
                return true;
            }
        }
        /// <summary>
        /// Serialization implementation for SecureString.
        /// </summary>
        /// <param name="elem"></param>
        /// <param name="obj"></param>
        /// <param name="expectedType"></param>
        /// <returns></returns>
        public override bool Serialize(XElement elem, object obj, ITypeInfo expectedType)
        {
            if (obj is System.Security.SecureString == false) return false;
            var sec = (System.Security.SecureString)obj;

            var unsec = sec.ConvertToUnsecureString();
            elem.Value = CryptographyHelper.Encrypt(unsec, password);

            return true;
        }
    }

}
