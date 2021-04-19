using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;
using System.Reflection;

namespace SpcreatGunJamOne
{
    // Soundbank loading code, taken directly from https://mtgmodders.gitbook.io/etg-modding-guide/misc/using-custom-sounds
    public class AudioResourceLoader
    {
        public static readonly string ResourcesDirectoryName = "SpcreatGunJamOne";

        public static readonly string pathzip = GunJam1Module.ZipFilePath;
        public static readonly string pathfile = GunJam1Module.FilePath;


        public static void InitAudio()
        {
                LoadAllAutoloadResourcesFromModPath(pathzip);
        }

        public static void LoadAllAutoloadResourcesFromAssembly(Assembly assembly, string prefix) {
            ResourceLoaderSoundbanks LoaderSoundbanks = new ResourceLoaderSoundbanks();
            LoaderSoundbanks.AutoloadFromAssembly(assembly, prefix);
		}
        
		public static void LoadAllAutoloadResourcesFromPath(string path, string prefix) {
            ResourceLoaderSoundbanks LoaderSoundbanks = new ResourceLoaderSoundbanks();
            LoaderSoundbanks.AutoloadFromPath(path, prefix);
		}

        public static void LoadAllAutoloadResourcesFromModPath(string path)
        {
                ResourceLoaderSoundbanks LoaderSoundbanks = new ResourceLoaderSoundbanks();
                LoaderSoundbanks.AutoloadFromModZIPOrModFolder(path);
        }

  

    }
}
