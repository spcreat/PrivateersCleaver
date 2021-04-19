using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Ionic.Zip;
using UnityEngine;


namespace SpcreatGunJamOne
{
    // This is (mostly) unedited code from https://mtgmodders.gitbook.io/etg-modding-guide/misc/using-custom-sounds
    //            ^ go to lines 74 and 127 to see what I changed
    public class ResourceLoaderSoundbanks
    {
        public void AutoloadFromAssembly(Assembly assembly, string prefix) {
			bool flag = assembly == null;
			if (flag) { throw new ArgumentNullException("assembly", "Assembly cannot be null."); }
			bool flag2 = prefix == null;
			if (flag2) { throw new ArgumentNullException("prefix", "Prefix name cannot be null."); }
			prefix = prefix.Trim();
			bool flag3 = prefix == "";
			if (flag3) { throw new ArgumentException("Prefix name cannot be an empty (or whitespace only) string.", "prefix"); }
			List<string> list = new List<string>(assembly.GetManifestResourceNames());
			for (int i = 0; i < list.Count; i++) {
				string text = list[i];
				string text2 = text;
				text2 = text2.Replace('/', Path.DirectorySeparatorChar);
				text2 = text2.Replace('\\', Path.DirectorySeparatorChar);
				bool flag4 = text2.IndexOf(AudioResourceLoader.ResourcesDirectoryName) != 0;
				if (!flag4) {
					text2 = text2.Substring(text2.IndexOf(AudioResourceLoader.ResourcesDirectoryName) + AudioResourceLoader.ResourcesDirectoryName.Length);
					bool flag5 = text2.LastIndexOf(".bnk") != text2.Length - ".bnk".Length;
					if (!flag5) {
						text2 = text2.Substring(0, text2.Length - ".bnk".Length);
						bool flag6 = text2.IndexOf(Path.DirectorySeparatorChar) == 0;
						if (flag6) { text2 = text2.Substring(1); }
						text2 = prefix + ":" + text2;
                       
                           Debug.Log(string.Format("{0}: Soundbank found, attempting to autoload: name='{1}' resource='{2}'", typeof(ResourceLoaderSoundbanks), text2, text));
                        				
						using (Stream manifestResourceStream = assembly.GetManifestResourceStream(text)) {
							LoadSoundbankFromStream(manifestResourceStream, text2);
						}
					}
				}
			}
		}

        public void AutoloadFromModZIPOrModFolder(string path)
        {
            int FilesLoaded = 0;
            if (File.Exists(path))
            {
                Debug.Log("Zip Found");
                if (Directory.Exists("tempassets")) Directory.Delete("tempassets", true);
                Directory.CreateDirectory("tempassets");
                using (ZipFile ModZIP = ZipFile.Read(path))
                {
                    if (ModZIP != null && ModZIP.Entries.Count > 0)
                    {
                        foreach (ZipEntry entry in ModZIP.Entries)
                        {
                            if (entry.FileName.EndsWith(".bnk"))
                            {
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    entry.Extract(ms);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    LoadSoundbankFromStream(ms, entry.FileName.ToLower().Replace(".bnk", string.Empty));
                                    FilesLoaded++;
                                }
                            }
                            // These 2 ifs just look for skull.obj and bone.obj in the mod zip. If it finds them, it extracts them to a temporary folder then loads them
                            if (entry.FileName.EndsWith("skull.obj"))
                            {
                                entry.Extract("tempassets");
                                GunJam1Module.skullMesh = LoadMeshFromFile(Path.Combine("tempassets", "skull.obj"));
                            }
                            if (entry.FileName.EndsWith("bone.obj"))
                            {
                                entry.Extract("tempassets");
                                GunJam1Module.boneMesh = LoadMeshFromFile(Path.Combine("tempassets", "bone.obj"));
                            }
                        }
                        // I commented this line of code so that it will search through every zip file, since now I'm looking for multiple files instead of just one bank.
                        //if (FilesLoaded > 0) { return; }
                    }
                }
            }
            else
            {
                // Zip file wasn't found. Try to load from Mod folder instead.
                AutoloadFromPath(AudioResourceLoader.pathfile, "SpcreatGunJamOne");
            }
        }

        public void AutoloadFromPath(string path, string prefix)
        {
            if (string.IsNullOrEmpty(path)) { throw new ArgumentNullException("path", "Path cannot be null."); }
            if (string.IsNullOrEmpty(prefix)) { throw new ArgumentNullException("prefix", "Prefix name cannot be null."); }
            prefix = prefix.Trim();
            if (string.IsNullOrEmpty(prefix)) { throw new ArgumentException("Prefix name cannot be an empty (or whitespace only) string.", "prefix"); }
            path = path.Replace('/', Path.DirectorySeparatorChar);
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            if (!Directory.Exists(path))
            {

                Debug.Log(string.Format("{0}: No autoload directory in path, not autoloading anything. Path='{1}'.", typeof(ResourceLoaderSoundbanks), path));

            }
            else
            {
                List<string> list = new List<string>(Directory.GetFiles(path, "*.bnk", SearchOption.AllDirectories));
                for (int i = 0; i < list.Count; i++)
                {
                    string text = list[i];
                    string text2 = text;
                    text2 = text2.Replace('/', Path.DirectorySeparatorChar);
                    text2 = text2.Replace('\\', Path.DirectorySeparatorChar);
                    text2 = text2.Substring(text2.IndexOf(path) + path.Length);
                    text2 = text2.Substring(0, text2.Length - ".bnk".Length);
                    bool flag5 = text2.IndexOf(Path.DirectorySeparatorChar) == 0;
                    if (flag5) { text2 = text2.Substring(1); }
                    text2 = prefix + ":" + text2;
                    Debug.Log(string.Format("{0}: Soundbank found, attempting to autoload: name='{1}' file='{2}'", typeof(ResourceLoaderSoundbanks), text2, text));

                    using (FileStream fileStream = File.OpenRead(text)) { LoadSoundbankFromStream(fileStream, text2); }
                }
                // This does the exact same thing as the zip search, but searches for files in the mod directory instead.
                // (it also doesnt have to extract them, so it just loads directly from the file's path)
                List<string> list2 = new List<string>(Directory.GetFiles(path, "skull.obj", SearchOption.AllDirectories));
                if (list2.Count>0) GunJam1Module.skullMesh = LoadMeshFromFile(list2[0]);
                List<string> list3 = new List<string>(Directory.GetFiles(path, "bone.obj", SearchOption.AllDirectories));
                if (list3.Count > 0) GunJam1Module.boneMesh = LoadMeshFromFile(list3[0]);
            }
        }

        // This is just a small helper method that turns 2 lines into 1 in my code.
        // It just takes a path and returns the imported mesh.
        Mesh LoadMeshFromFile(string path)
        {
            ObjImporter newMesh = new ObjImporter();
            return newMesh.ImportFile(path);
        }

        private void LoadSoundbankFromStream(Stream stream, string name)
        {
            byte[] array = StreamToByteArray(stream);
            IntPtr intPtr = Marshal.AllocHGlobal(array.Length);
            try
            {
                Marshal.Copy(array, 0, intPtr, array.Length);
                uint num;
                AKRESULT akresult = AkSoundEngine.LoadAndDecodeBankFromMemory(intPtr, (uint)array.Length, false, name, false, out num);

                Debug.Log(string.Format("Result of soundbank load: {0}.", akresult));

            }
            finally
            {
                Marshal.FreeHGlobal(intPtr);
            }
        }

        public static byte[] StreamToByteArray(Stream input)
        {
            byte[] array = new byte[16384];
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                int count;
                while ((count = input.Read(array, 0, array.Length)) > 0) { memoryStream.Write(array, 0, count); }
                result = memoryStream.ToArray();
            }
            return result;
        }
    }
}
