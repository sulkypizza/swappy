using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace save_switcher
{
    internal static class RegistryHelper
    {
        private static ReadOnlyDictionary<string, RegistryHive> RegistryBases = new ReadOnlyDictionary<string, RegistryHive>(
            new Dictionary<string, RegistryHive>()
            {
                    {"HKEY_CLASSES_ROOT", RegistryHive.ClassesRoot },
                    {"HKEY_CURRENT_USER", RegistryHive.CurrentUser },
                    {"HKEY_LOCAL_MACHINE", RegistryHive.LocalMachine },
                    {"HKEY_USERS", RegistryHive.Users },
                    {"HKEY_CURRENT_CONFIG", RegistryHive.CurrentConfig },
            }
        );

        public static ReadOnlyDictionary<Type, RegistryValueKind> RegistryValueKinds = new ReadOnlyDictionary<Type, RegistryValueKind>(
            new Dictionary<Type, RegistryValueKind>()
            {
                {typeof(long), RegistryValueKind.QWord },
                {typeof(int), RegistryValueKind.DWord },
                {typeof(string), RegistryValueKind.String },
                {typeof(string[]), RegistryValueKind.MultiString },
                {typeof(byte[]), RegistryValueKind.Binary },
            }
        );


        public static RegistryHive? GetRegistryHive(string name)
        {
            RegistryHive parsedRegistryHive;
            RegistryBases.TryGetValue(name.Split('\\')[0], out parsedRegistryHive);

            return parsedRegistryHive;
        }

        public static RegistryKey GetKey(string keyName)
        {
            RegistryHive? parsedRegistryHive = GetRegistryHive(keyName);
            if (GetRegistryHive(keyName) != null)
            {
                string subKey = string.Join(@"\", keyName.Split('\\').Skip(1));

                return RegistryKey.OpenBaseKey(parsedRegistryHive.Value, RegistryView.Default).OpenSubKey(subKey, true);
            }
            else
                return null;
        }

        public static void CopyRegistryKey(RegistryKey sourceKey, RegistryKey destinationKey)
        {
            
            foreach (string value in sourceKey.GetValueNames())
            {
                if(sourceKey.GetValueKind(value) == RegistryValueKinds[sourceKey.GetValue(value).GetType()]) //key value kind matches the data we expect out of it
                    destinationKey.SetValue(value, sourceKey.GetValue(value), RegistryValueKinds[sourceKey.GetValue(value).GetType()]);
                else  //key is storing an invalid data type
                {
                    destinationKey.SetValue(value, registryKeyValeToByteArray(sourceKey, value), RegistryValueKind.Binary);
                }
            }

            foreach (string subkey in sourceKey.GetSubKeyNames())
            {
                RegistryKey createdSubKey = destinationKey.CreateSubKey(subkey, true);
                CopyRegistryKey(sourceKey.OpenSubKey(subkey), createdSubKey);
            }
        }

        public static void DeleteRegistryKey(RegistryKey key)
        {
            string parentKeyName = key.Name.Substring(0, key.Name.Trim('\\').LastIndexOf('\\'));
            string subKey = key.Name.Substring(key.Name.Trim('\\').LastIndexOf('\\')).Trim('\\');
            GetKey(parentKeyName).DeleteSubKeyTree(subKey);
        }

        public static void CreateRegistryKey(string fullKeyName)
        {
            RegistryHive? hive = GetRegistryHive(fullKeyName);

            if(hive != null)
            {
                string subKey = string.Join(@"\", fullKeyName.Split('\\').Skip(1));
                RegistryKey.OpenBaseKey(hive.Value, RegistryView.Default).CreateSubKey(subKey);
            }
        }

        public static void WriteRegistryKeyToFile(RegistryKey baseKey, string saveDirectory)
        {

            if (!Directory.Exists(saveDirectory))
                throw new DirectoryNotFoundException(saveDirectory);

            void writeKeyVales(RegistryKey key)
            {
                if (object.Equals(key, null))
                    throw new Exception("Key does not exist.");

                foreach (string value in key.GetValueNames())
                {
                    Stream fileStream = new FileStream(Path.GetFullPath(saveDirectory).Trim('\\') + @"\r_" + key.Name.Split('\\').Last() + "_" + value, FileMode.Create); ;
                    using (BinaryWriter writer = new BinaryWriter(fileStream))
                    {
                        RegistryValueKind valueKind = key.GetValueKind(value);
                        RegistryValueKind actualValueKind = RegistryValueKinds[key.GetValue(value).GetType()];

                        writer.Write(key.Name.Replace(baseKey.Name, "").TrimStart('\\'));
                        writer.Write(value);

                        if (valueKind == actualValueKind)  //registrykey is not storing data in the wrong data type
                        {
                            //special care needs to be taken with REG_SZ and REG_EXP_SZ:
                            if (valueKind == RegistryValueKind.String || valueKind == RegistryValueKind.ExpandString)
                            {
                                writer.Write((int)key.GetValueKind(value));
                            }
                            else
                                writer.Write((int)valueKind);

                            Console.WriteLine($"keyvalue: {key.GetValue(value)}    type: {key.GetValue(value).GetType()}");

                            switch (valueKind)
                            {
                                case RegistryValueKind.String:
                                case RegistryValueKind.ExpandString:
                                    writer.Write((string)key.GetValue(value));
                                    break;

                                case RegistryValueKind.MultiString:
                                    string[] valueStrings = (string[])key.GetValue(value);
                                    writer.Write(valueStrings.Length);

                                    foreach (string valueString in valueStrings)
                                    {
                                        writer.Write(valueString);
                                    }
                                    break;

                                case RegistryValueKind.DWord:
                                    writer.Write((int)key.GetValue(value));
                                    break;

                                case RegistryValueKind.QWord:
                                    writer.Write((long)key.GetValue(value));
                                    break;

                                case RegistryValueKind.Binary:

                                    byte[] bytes = (byte[])key.GetValue(value);


                                    writer.Write(bytes.Length);
                                    writer.Write(bytes);
                                    break;
                            }

                            writer.Close();
                        }
                        else   //registry key value kind is incorrect, so we store it as binary
                        {
                            writer.Write((int)RegistryValueKind.Binary);

                            byte[] bytes = registryKeyValeToByteArray(key, value);

                            writer.Write(bytes.Length);
                            writer.Write(bytes);

                            writer.Close();
                        }
                    }
                }
            }

            void recursiveIterate(RegistryKey key)
            {
                foreach (string subKey in key.GetSubKeyNames())
                {
                    RegistryKey regKey = key.OpenSubKey(subKey);
                    Console.WriteLine(key.Name + "   subkey: " + subKey + "   registryKey: " + regKey.Name);
                    writeKeyVales(regKey);
                    recursiveIterate(regKey);
                }
            }

            writeKeyVales(baseKey);
            recursiveIterate(baseKey);
        }

        public static void ReadRegistryFromFile(RegistryKey baseKey, Stream fileStream)
        {

            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                string subKeyName = reader.ReadString();
                string valueName = reader.ReadString();
                RegistryValueKind valueKind = (RegistryValueKind)reader.ReadInt32();

                RegistryKey workingKey = baseKey;

                if (!string.IsNullOrEmpty(subKeyName))
                {
                    workingKey = baseKey.OpenSubKey(subKeyName, true);
                    if (workingKey == null)
                        workingKey = baseKey.CreateSubKey(subKeyName, true);
                }

                object readValue = null;

                switch (valueKind)
                {
                    case RegistryValueKind.DWord:
                        readValue = reader.ReadInt32();
                        break;

                    case RegistryValueKind.QWord:
                        readValue = reader.ReadInt64();
                        break;

                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        readValue = reader.ReadString();
                        break;

                    case RegistryValueKind.MultiString:
                        string[] strings = new string[reader.ReadInt32()];
                        for (int i = 0; i < strings.Length; i++)
                        {
                            strings[i] = reader.ReadString();
                        }

                        readValue = strings;
                        break;

                    case RegistryValueKind.Binary:
                        byte[] bytes = new byte[reader.ReadInt32()];

                        for (int i = 0; i < bytes.Length; i++)
                        {
                            bytes[i] = reader.ReadByte();
                        }

                        readValue = bytes;
                        break;
                }

                if (readValue == null)
                    throw new Exception("Value read from file was null!");

                workingKey.SetValue(valueName, readValue, valueKind);
            }
        }

        private static byte[] registryKeyValeToByteArray (RegistryKey key, string value)
        {
            List<byte> bytes = new List<byte>();

            switch (key.GetValue(value).GetType().ToString())
            {
                case "System.Int32":
                    foreach (byte b in BitConverter.GetBytes((int)key.GetValue(value)))
                        bytes.Add(b);
                    break;

                case "System.Int64":
                    foreach (byte b in BitConverter.GetBytes((long)key.GetValue(value)))
                        bytes.Add(b);
                    break;

                case "System.String":
                    string s = (string)key.GetValue(value);

                    for (int i = 0; i < s.Length; i++)
                    {
                        foreach (byte b in BitConverter.GetBytes(s[i]))
                            bytes.Add(b);
                        
                    }
                    break;

                case "System.String[]":
                    string[] baseS = (string[])key.GetValue(value);

                    for (int j = 0; j < baseS.Length; j++)
                    {
                        string s1 = baseS[j];
                        for (int i = 0; i < s1.Length; i++)
                        {
                            foreach (byte b in BitConverter.GetBytes(s1[i]))
                                bytes.Add(b);
                        }
                    }
                    break;

                case "System.Byte[]":
                    foreach (byte b in (byte[])key.GetValue(value))
                        bytes.Add(b);
                    break;
            }

            return bytes.ToArray();
        }
    }
}
