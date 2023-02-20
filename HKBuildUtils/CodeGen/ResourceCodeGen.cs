using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HKBuildUtils.CodeGen.Resources
{
    [Generator]
    internal partial class ResourceCodeGen : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (!context.AnalyzerConfigOptions.GlobalOptions
                .TryGetValue("build_property.projectdir", out string? projectFile) 
                || string.IsNullOrEmpty(projectFile)) return;
            context.AnalyzerConfigOptions.GlobalOptions
                .TryGetValue("build_property.rootnamespace", out string? rootNamespace);

            var root = Path.GetFullPath(Path.GetDirectoryName(projectFile));

            StringBuilder code = new();
            code.AppendLine("""
                using System.IO;
                using System;
                """);
            code.AppendLine("internal static partial class ModResources {");
            code.AppendLine($@"
                private static byte[] GetResourceBytesImpl(string name)
                {{
                    using(Stream stream = typeof(ModResources).Assembly
                                        .GetManifestResourceStream({
                                    (string.IsNullOrEmpty(rootNamespace) ? "name" : $"\"{rootNamespace}.\" + name")
                                    }))
                    {{
                        byte[] buffer = new byte[stream.Length];
                        stream.Read(buffer, 0, buffer.Length);
                        return buffer;
                    }}
                }}
                private static byte[] GetResourceBytes(string name)
                {{
                    string path = Path.Combine(Path.GetDirectoryName(typeof(ModResources).Assembly.Location), name);
                    if(File.Exists(path)) return File.ReadAllBytes(path);
                    byte[] data = GetResourceBytesImpl(name.Replace('/', '.'));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, data);
                    return data;
                }}
                ");

            foreach (var file in context.AdditionalFiles)
            {
                
                if (!context.AnalyzerConfigOptions.GetOptions(file)
                    .TryGetValue("build_metadata.additionalfiles.modresourcesitemgroup", out var type)) continue;

                var name = Path.GetFileNameWithoutExtension(file.Path);
                var pl = Path.GetFullPath(file.Path);
                var codename = name.Replace('.', '_')
                                    .Replace(' ', '_')
                                    .ToUpper();
                if(!pl.StartsWith(root))
                {
                    code.AppendLine($"[Obsolete(\"Incorrect resource file\", true)]public static byte[] {codename} => throw new NotSupportedException();");
                    continue;
                }
                var rp = pl.Substring(root.Length + 1).Replace('\\', '/');
                var np = rp.Replace('/', '.');

                
                if(type == "unpack")
                {
                    code.AppendLine($"public static byte[] {codename} => GetResourceBytes(\"{rp}\");");
                } else if(type == "embedded")
                {
                    code.AppendLine($"public static byte[] {codename} => GetResourceBytesImpl(\"{np}\");");
                } else
                {
                    code.AppendLine($"[Obsolete(\"Unknown resource type '{type}'\", true)]public static byte[] {codename} => throw new NotSupportedException();");
                }
            }

            code.AppendLine("}");
            context.AddSource("HKBuildUtils.Resources.g.cs", code.ToString());
        }
        [Obsolete("Incorrect resource file")]
        public void Initialize(GeneratorInitializationContext context)
        {
            
        }
    }
}
