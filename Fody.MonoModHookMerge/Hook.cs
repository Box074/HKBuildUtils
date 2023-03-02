
using Mono.Cecil.Cil;
using Mono.Cecil;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Fody.MonoModHookMerge;

public partial class ModuleWeaver
{
    public static int id = 0;
    public static Dictionary<string, TypeReference> delegateMap = new();
    private TypeDefinition compilerHelper;
    private void TryCheckIH(Instruction il, MethodBody body)
    {
        if (il.Operand is MethodReference mr)
        {
            if (!mr.DeclaringType.FullName.StartsWith("On.") && !mr.DeclaringType.FullName.StartsWith("IL.")) return;

            mr.DeclaringType = ConvertHookDelegate(mr.DeclaringType, out var repalced);
            if (repalced)
            {
                il.Operand = mr.DeclaringType.Resolve().Methods.First(x => x.Name == mr.Name);
                return;
            }
            if (mr.Name.StartsWith("add_") || mr.Name.StartsWith("remove_")) CheckIH(mr, il, body);
        }
    }
    private void CheckIH(MethodReference mr, Instruction il, MethodBody body)
    {
        var rmd = mr.Resolve();
        if (rmd?.Body == null) return;
        compilerHelper ??= ModuleDefinition.GetType("HKBuildUtils.Compiler.MMHOOKCompilerHelper");

        var method = (MethodReference)rmd.Body.Instructions.First(x => x.OpCode == OpCodes.Ldtoken).Operand;
        var callMethod = (MethodReference)rmd.Body.Instructions.First(x => x.Operand is MethodReference and
        {
            DeclaringType.FullName: "MonoMod.RuntimeDetour.HookGen.HookEndpointManager"
        }).Operand;
        var helperMethod = "Hook_" + callMethod.Name;
        il.OpCode = OpCodes.Ldtoken;
        il.Operand = ModuleDefinition.ImportReference(method);
        if (!method.Resolve().HasBody)
        {
            throw new NotSupportedException($"Body-less method {mr.FullName}");
        }
        body.GetILProcessor().InsertAfter(il,
            Instruction.Create(OpCodes.Call, ModuleDefinition.ImportReference(
                compilerHelper.Methods.First(x => x.Name == helperMethod))));
    }
    private TypeReference ConvertHookDelegate(TypeReference tr, out bool replaced)
    {
        replaced = false;
        try
        {
            tr = ModuleDefinition.ImportReference(tr);
        }catch(Exception)
        {

        }

        if (!tr.FullName.StartsWith("On")) return tr;
        var td = tr.Resolve();


        if (td.BaseType.FullName != "System.MulticastDelegate") return tr;
        if (delegateMap.TryGetValue(tr.FullName, out var val))
        {
            replaced = true;
            return val;
        }


        var invoke = td.Methods.First(x => x.Name == "Invoke");
        var rt = GenerateDelegate(invoke);
        delegateMap.Add(tr.FullName, rt);
        replaced = true;
        return rt;
    }
    private TypeDefinition GenerateDelegate(MethodDefinition invokeMethod)
    {
        var md = ModuleDefinition;
        TypeDefinition del = new TypeDefinition(
            null, "MD_" + invokeMethod.DeclaringType.Name + "|" + (id++),
            TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
            md.ImportReference(FindTypeDefinition("System.MulticastDelegate"))
        );
        md.Types.Add(del);
        MethodDefinition ctor = new MethodDefinition(
            ".ctor",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.ReuseSlot,
            md.TypeSystem.Void
        )
        {
            ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed,
            HasThis = true
        };
        ctor.Parameters.Add(new ParameterDefinition(md.TypeSystem.Object));
        ctor.Parameters.Add(new ParameterDefinition(md.TypeSystem.IntPtr));
        ctor.Body = new MethodBody(ctor);
        del.Methods.Add(ctor);

        MethodDefinition invoke = new MethodDefinition(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            ConvertHookDelegate(invokeMethod.ReturnType, out _)
        )
        {
            ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed,
            HasThis = true
        };
        foreach (ParameterDefinition param in invokeMethod.Parameters)
            invoke.Parameters.Add(new ParameterDefinition(
                param.Name,
                param.Attributes,
                ConvertHookDelegate(param.ParameterType, out _)
            ));
        invoke.Body = new MethodBody(invoke);
        del.Methods.Add(invoke);

        MethodDefinition invokeBegin = new MethodDefinition(
            "BeginInvoke",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            md.ImportReference(FindTypeDefinition("System.IAsyncResult"))
        )
        {
            ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed,
            HasThis = true
        };
        foreach (ParameterDefinition param in invoke.Parameters)
            invokeBegin.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, ConvertHookDelegate(param.ParameterType, out _)));
        invokeBegin.Parameters.Add(new ParameterDefinition("callback", ParameterAttributes.None, md.ImportReference(FindTypeDefinition("System.AsyncCallback"))));
        invokeBegin.Parameters.Add(new ParameterDefinition(null, ParameterAttributes.None, md.TypeSystem.Object));
        invokeBegin.Body = new MethodBody(invokeBegin);
        del.Methods.Add(invokeBegin);

        MethodDefinition invokeEnd = new MethodDefinition(
            "EndInvoke",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            md.TypeSystem.Object
        )
        {
            ImplAttributes = MethodImplAttributes.Runtime | MethodImplAttributes.Managed,
            HasThis = true
        };
        invokeEnd.Parameters.Add(new ParameterDefinition("result", ParameterAttributes.None, md.ImportReference(FindTypeDefinition("System.IAsyncResult"))));
        invokeEnd.Body = new MethodBody(invokeEnd);
        del.Methods.Add(invokeEnd);

        return del;
    }
}