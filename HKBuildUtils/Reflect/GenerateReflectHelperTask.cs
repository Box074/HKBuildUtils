using HKBuildUtils.Main.Reflect;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HKBuildUtils.Reflect
{
    public class GenerateReflectHelperTask : Task
    {
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
            var resovler = new DefaultAssemblyResolver();
            foreach (var v in references)
            {
                if(Directory.Exists(v))
                {
                    resovler.AddSearchDirectory(v);
                    continue;
                }
                if (!File.Exists(v)) continue;
                resovler.AddSearchDirectory(Path.GetDirectoryName(v));
            }
            foreach (var outfile in outfiles)
            {
                if (string.IsNullOrEmpty(outfile)) continue;

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
                    continue;
                }

                using (var origAsm = AssemblyDefinition.ReadAssembly(origAsmPath, new()
                {
                    AssemblyResolver = resovler
                }))
                {
                    var origName = new AssemblyNameReference(origAsm.Name.Name, origAsm.Name.Version);
                    origAsm.Name.Name = "ReflectHelper." + origAsm.Name.Name;
                    var generator = new ReflectHelperGenerator(origAsm);
                    generator.Generate(BitConverter.ToString(
                        SHA256.Create().ComputeHash(File.ReadAllBytes(origAsmPath))
                        ), origName);
                    Directory.CreateDirectory(Path.GetDirectoryName(outfile));
                    origAsm.Write(outfile);

                }
            }

            return true;
        }
    }
}
