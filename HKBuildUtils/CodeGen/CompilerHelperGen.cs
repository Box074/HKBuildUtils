using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace HKBuildUtils.CodeGen
{
    [Generator]
    internal class CompilerHelperGen : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var file in context.AdditionalFiles)
            {
                if (!context.AnalyzerConfigOptions.GetOptions(file)
                    .TryGetValue("build_metadata.additionalfiles.hkbu_importcode", out var val) || string.IsNullOrEmpty(val)) continue;
                context.AddSource(Path.GetFileNameWithoutExtension(file.Path) + ".g.cs", File.ReadAllText(file.Path));
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            
        }
    }
}
