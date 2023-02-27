using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using MonoMod;
using MonoMod.RuntimeDetour.HookGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HKBuildUtils
{
    public class GenerateMonoHookTask : Task
    {
        [Required]
        public string AllReference { get; set; } = "";
        [Required]
        public string OutFiles { get; set; } = "";
        [Required]
        public string ReferencePath { get; set; } = "";
        [Required]
        public string AssemblyRoot { get; set; } = "";
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
                if (!File.Exists(v)) continue;
                resovler.AddSearchDirectory(Path.GetDirectoryName(v));
            }
            Environment.SetEnvironmentVariable("MONOMOD_HOOKGEN_PRIVATE", "1");
            Environment.SetEnvironmentVariable("MONOMOD_HOOKGEN_ORIG", "1");
            Environment.SetEnvironmentVariable("MONOMOD_DEPENDENCY_MISSING_THROW", "0");
            foreach (var outfile in outfiles)
            {
                if (string.IsNullOrEmpty(outfile)) continue;

                var asmName = Path.GetFileNameWithoutExtension(outfile);
                if (!asmName.StartsWith("MMHOOK.")) continue;
                asmName = asmName.Substring("MMHOOK.".Length);

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
                Directory.CreateDirectory(Path.GetDirectoryName(outfile));
                using(var modder = new HookModder()
                { 
                    InputPath = origAsmPath,
                    OutputPath = outfile,
                    ReadingMode = ReadingMode.Deferred
                })
                {
                    foreach(var v in ReferencePath.Split(';'))
                    {
                        if(Directory.Exists(v))
                        {
                            modder.DependencyDirs.Add(v);
                        }
                    }
                    modder.DependencyDirs.Add(AssemblyRoot);
                    modder.Read();
                    modder.MapDependencies();
                    HookGenerator hookGenerator = new HookGenerator(modder, Path.GetFileName(outfile));
                    using (ModuleDefinition outputModule = hookGenerator.OutputModule)
                    {
                        hookGenerator.Generate();
                        outputModule.Write(outfile);
                    }
                }
            }
            return true;
        }

        private class HookModder : MonoModder
        {
            private ModuleDefinition? mscorlib;
            public override TypeReference FindType(string name)
            {
                if(mscorlib == null)
                {
                    mscorlib = AssemblyResolver.Resolve(new("mscorlib", new()))?.MainModule;
                }
                if (mscorlib == null) return FindType(name);
                
                return mscorlib.GetType(name) ?? base.FindType(name);
            }
        }
    }
}
