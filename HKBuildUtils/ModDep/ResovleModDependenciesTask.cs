using HKBuildUtils.ModLink;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Serialization;

namespace HKBuildUtils.ModDep
{
    public class ResovleModDependenciesTask : Task
    {
        [Required]
        public string LibraryCache { get; set; } = "";
        public string ModNames { get; set; } = "";
        public override bool Execute()
        {
            var modNames = ModNames.Split(';');
            Log.LogMessage("Fetch Modlinks");
            var modlinksResponse = WebRequest.Create(@"https://github.com/hk-modding/modlinks/raw/main/ModLinks.xml")
                .GetResponse();
            ModLinks modlinks;
            Log.LogMessage("Parsing Modlinks");
            using (var reader = new StreamReader(modlinksResponse.GetResponseStream()))
            {
                var des = new XmlSerializer(typeof(ModLinks));
                modlinks = (ModLinks)des.Deserialize(reader);
            }
            Log.LogMessage("Checking mod dependencies");
            var mods = modlinks.Manifests.ToDictionary(x => x.Name);
            var tasks = new List<System.Threading.Tasks.Task>();
            foreach (var v in modNames)
            {
                if (!mods.TryGetValue(v, out var mod))
                {
                    Log.LogError($"The specified mods were not found: {v}");
                    return false;
                }
                var moddir = Path.GetFullPath(Path.Combine(LibraryCache, v));
                Log.LogMessage("Moddir: " + moddir);
                if(Directory.Exists(moddir))
                {
                    Log.LogMessage("Exists Moddir: " + moddir);
                    if (Directory.GetFiles(moddir, "*.dll").Length != 0)
                    {
                        Log.LogMessage($"Skip '{v}'");
                        continue;
                    }
                }

                var uri = new Uri(mod.Links.Windows.URL);
                Log.LogMessage($"Downloading '{v}' from '{uri}'");
                tasks.Add(WebRequest.Create(uri)
                    .GetResponseAsync()
                    .ContinueWith(task =>
                    {
                        var result = task.Result;
                        Log.LogMessage($"'{v}' was downloaded");
                        var ext = Path.GetExtension(uri.PathAndQuery);
                        
                        Directory.CreateDirectory(moddir);
                        byte[] data;
                        using (BinaryReader reader = new(result.GetResponseStream()))
                            data = reader.ReadBytes((int)result.ContentLength);
                        if (ext.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            File.WriteAllBytes(Path.Combine(v + ".dll"), data);
                            return;
                        }
                        Log.LogMessage($"Decompressing '{v}' to '{moddir}");
                        using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
                        {
                            foreach (var v in zip.Entries)
                            {
                                var path = Path.Combine(moddir, v.FullName);
                                var dir = Path.GetDirectoryName(path);
                                Directory.CreateDirectory(dir);

                                v.ExtractToFile(path, true);
                            }
                        }
                        Log.LogMessage($"'{v}' was decompressed");
                    }));
            }
            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
            return true;
        }
    }
}
