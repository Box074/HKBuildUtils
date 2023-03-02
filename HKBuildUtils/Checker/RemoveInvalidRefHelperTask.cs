using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HKBuildUtils.Checker
{
    public class RemoveInvalidRefHelperTask : Task
    {
        public const int CURRENT_REFHELPER_GENERATOR_VER = 11;

        [Required]
        public string AllReference { get; set; } = "";
        [Required]
        public string OutFiles { get; set; } = "";
        public override bool Execute()
        {
            var references = AllReference.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (references.Length == 0)
            {
                Log.LogWarning("You don't seem to be referencing any assemblies!");
                return true;
            }
            var outfiles = OutFiles.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            if (outfiles.Length == 0)
            {
                return true;
            }
            foreach (var outfile in outfiles)
            {
                if (string.IsNullOrEmpty(outfile) || !File.Exists(outfile)) continue;

                var asmName = Path.GetFileNameWithoutExtension(outfile);
                if (!asmName.StartsWith("ReflectHelper.")) continue;
                asmName = asmName.Substring("ReflectHelper.".Length);
                var origAsmPath = "";
                foreach (var v in references)
                {
                    var aname = Path.GetFileNameWithoutExtension(v);
                    if (aname.Equals(asmName, StringComparison.OrdinalIgnoreCase))
                    {
                        origAsmPath = v;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(origAsmPath))
                {
                    Log.LogWarning("Unable to find original assembly: " + asmName);
                    File.Delete(outfile);
                    continue;
                }
                try
                {
                    using var ms = new MemoryStream(File.ReadAllBytes(outfile));
                    using (var ad = AssemblyDefinition.ReadAssembly(ms))
                    {
                        var md = ad.MainModule.GetType("<HKBUMD>.Reflect");
                        if (md == null)
                        {
                            Log.LogWarning($"Reflect Metadata not found.({outfile})");
                            File.Delete(outfile);
                            continue;
                        }
                        var ver = md.Fields.FirstOrDefault(x => x.Name == "GEN_VER" && x.IsLiteral)?.Constant as int?;
                        if (ver == null)
                        {
                            Log.LogWarning($"Reflect Helper is too old.({outfile})");
                            File.Delete(outfile);
                            continue;
                        }
                        if (ver.Value < CURRENT_REFHELPER_GENERATOR_VER)
                        {
                            Log.LogWarning($"Reflect Helper is too old({ver.Value}->{CURRENT_REFHELPER_GENERATOR_VER}).({outfile})");
                            File.Delete(outfile);
                            continue;
                        }
                        var oSHA = md.Fields.FirstOrDefault(x => x.Name == "ORIG_SHA256" && x.IsLiteral)?.Constant as string;
                        if (oSHA != null)
                        {
                            var data = File.ReadAllBytes(origAsmPath);
                            if (!BitConverter.ToString(SHA256.Create().ComputeHash(data))
                                .Equals(oSHA, StringComparison.OrdinalIgnoreCase))
                            {
                                Log.LogWarning($"Reflect Helper is not match orig assembly({ver.Value}->{CURRENT_REFHELPER_GENERATOR_VER}).({outfile})");
                                File.Delete(outfile);
                                continue;
                            }
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    Log.LogWarning($"Failed to load assembly {outfile}");
                    File.Delete(outfile);
                }
                catch (Exception ex)
                {
                    Log.LogErrorFromException(ex);
                    File.Delete(outfile);
                }
            }
            return true;
        }
    }
}
