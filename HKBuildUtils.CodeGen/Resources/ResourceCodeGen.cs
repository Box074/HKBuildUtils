using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace HKBuildUtils.CodeGen.Resources
{
    [Generator]
    internal class ResourceCodeGen : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("HKBuildUtils.g.cs", $@"
                internal static partial class ModResources {{
                    public static int _____1 = 0;
                }}
                ");
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            
        }
    }
}
