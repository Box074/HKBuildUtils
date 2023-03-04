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
    public class SetupModdingAPITask : Task
    {
        [Required]
        public string LibraryCache { get; set; } = "";
        [Output]
        public ITaskItem[] OutputAllDlls { get; set; } = new ITaskItem[0];
        public ITaskItem[] IgnoreFiles { get; set; } = null!;
        public string VanillaURL { get; set; } = "";
        public override bool Execute()
        {
            Directory.CreateDirectory(LibraryCache);
            Log.LogMessage("Fetch ModdingAPI");
            if (File.Exists(Path.Combine(LibraryCache, "Assembly-CSharp.dll")))
            {
                Log.LogMessage("Skip download ModdingAPI");
                return true;
            }
            List<ITaskItem> outputDlls = new();
            var apilinkReponse = WebRequest.Create(@"https://github.com/hk-modding/modlinks/raw/main/ApiLinks.xml")
                .GetResponse();
            ApiLinks apiLinks;
            Log.LogMessage("Parsing Modlinks");
            using (var reader = new StreamReader(apilinkReponse.GetResponseStream()))
            {
                var des = new XmlSerializer(typeof(ApiLinks));
                apiLinks = (ApiLinks)des.Deserialize(reader);
            }
            Log.LogMessage("Downloading Modding API");
            var task_mapi = WebRequest.Create(apiLinks.Manifest.Links.Windows.URL)
                .GetResponseAsync();
            var vanillaURL = string.IsNullOrWhiteSpace(VanillaURL) ?
                "https://files.catbox.moe/i4sdl6.zip" : VanillaURL;
            {
                Log.LogMessage($"Downloading Vanilla from {vanillaURL}");
                var task_van = WebRequest.Create(vanillaURL).GetResponse();
                Log.LogMessage("Decompress Vanilla");

                byte[] data;
                using (BinaryReader reader = new(task_van.GetResponseStream()))
                    data = reader.ReadBytes((int)task_van.ContentLength);
                using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
                {
                    foreach (var v in zip.Entries)
                    {
                        var fn = Path.GetFileName(v.FullName);
                        
                        var path = Path.GetFullPath(Path.Combine(LibraryCache, v.FullName));
                        var dir = Path.GetDirectoryName(path);
                        Directory.CreateDirectory(dir);

                        v.ExtractToFile(path, true);
                        
                        if(IgnoreFiles != null)
                        {
                            if (IgnoreFiles.Any(x => x.ItemSpec?.Equals(fn, StringComparison.OrdinalIgnoreCase) ?? false))
                            {
                                continue;
                            }
                        }
                        if(path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            outputDlls.Add(new TaskItem(path));
                        }
                    }
                }
            }
            {
                task_mapi.Wait();
                Log.LogMessage("Decompress MAPI");

                byte[] data;
                using (BinaryReader reader = new(task_mapi.Result.GetResponseStream()))
                    data = reader.ReadBytes((int)task_mapi.Result.ContentLength);
                using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
                {
                    foreach (var v in zip.Entries)
                    {
                        var path = Path.Combine(LibraryCache, v.FullName);
                        var dir = Path.GetDirectoryName(path);
                        Directory.CreateDirectory(dir);

                        v.ExtractToFile(path, true);
                    }
                }
            }

            return true;
        }
    }
}
