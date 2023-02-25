using ILRepacking;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HKBuildUtils
{
    public class MergeHKBULibTask : Task
    {
        [Required]
        public string ModOutput { get; set; } = "";
        public override bool Execute()
        {
            var libFile = Path.Combine(Path.GetDirectoryName(ModOutput), "HKBuildUtils.Lib.dll");
            if(!File.Exists(libFile))
            {
                Log.LogWarning("Not found HKBuildUtils.Lib.dll");
                return true;
            }
            var origMod = Path.ChangeExtension(ModOutput, ".orig.dll");
            File.Copy(ModOutput, origMod, true);
            var ilrepack = new ILRepack(new RepackOptions()
            {
                InputAssemblies = new[] {
                    origMod, libFile
                },
                OutputFile = ModOutput,
                DebugInfo = true,
                TargetKind = ILRepack.Kind.Dll,
                SearchDirectories = new List<string>()
                { 
                    Path.GetDirectoryName(ModOutput)
                }
            });
            ilrepack.Repack();
            File.Delete(origMod);
            return true;
        }
    }
}
