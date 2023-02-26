using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
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
            if (!context.AnalyzerConfigOptions.GlobalOptions
                .TryGetValue("build_property.hkbu_use_compiler_helper", out string? useHelper) || useHelper != "true") return;
            context.AddSource("HKBuildUtils.CompilerHelper.g.cs",
                """
using MonoMod.RuntimeDetour.HookGen;
using MonoMod.RuntimeDetour;
using System.Reflection;
using System.Collections.Generic;
using System;

namespace HKBuildUtils.Compiler
{

internal static class MMHOOKCompilerHelper
{
    public static void Hook_Add(System.Delegate hook, System.RuntimeMethodHandle method) => 
        MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Add(System.Reflection.MethodBase.GetMethodFromHandle(method), hook);
    public static void Hook_Remove(System.Delegate hook, System.RuntimeMethodHandle method) => 
        MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Remove(System.Reflection.MethodBase.GetMethodFromHandle(method), hook);
    public static void Hook_Modify(System.Delegate hook, System.RuntimeMethodHandle method) => 
        MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Modify(System.Reflection.MethodBase.GetMethodFromHandle(method), hook);
    public static void Hook_Unmodify(System.Delegate hook, System.RuntimeMethodHandle method) => 
        MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Unmodify(MethodBase.GetMethodFromHandle(method), hook);
}
}
""");
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            
        }
    }
}
