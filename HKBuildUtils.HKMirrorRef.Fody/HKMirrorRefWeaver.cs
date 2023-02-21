using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;

public class ModuleWeaver : BaseModuleWeaver
{
    public override void Execute()
    {
        var methods = ModuleDefinition.GetAllTypes().SelectMany(x => x.Methods);
        var typeMapping = new Dictionary<string, TypeReference>();
        foreach (var method in methods)
        {
            if (!method.HasBody) continue;
            var body = method.Body;
            var il = body.Instructions[0];
            var nextIL = il;
            while ((il = nextIL) != null)
            {
                nextIL = nextIL.Next;

                if (il.OpCode != OpCodes.Call && il.OpCode != OpCodes.Callvirt) continue;
                var mr = (MethodReference)il.Operand;
                var type = mr.DeclaringType.FullName;
                if (!type.StartsWith("HKMirror")) continue;
                if (type == "HKMirror.Reflection.Extensions")
                {
                    il.OpCode = OpCodes.Nop;
                    var srcType = mr.Parameters[0].ParameterType;
                    var dstType = mr.ReturnType.FullName;
                    typeMapping[dstType] = srcType;
                    continue;
                }
                if (type.StartsWith("HKMirror.Reflection"))
                {
                    var typeD = mr.DeclaringType.Resolve();
                    var wrapper = typeD.BaseType as GenericInstanceType;
                    if (wrapper == null)
                    {
                        //Not supported
                        continue;
                    }
                    var srcType = wrapper.GenericArguments[0].Resolve();
                    typeMapping[type] = srcType;

                    if (mr.Name.StartsWith("get_") || mr.Name.StartsWith("set_"))
                    {

                        var propName = mr.Name.Substring(4);
                        //Property
                        var field = srcType.Fields.FirstOrDefault(x => x.Name == propName);
                        if (field != null)
                        {
                            if (mr.Name.StartsWith("get_"))
                            {
                                //Getter
                                il.OpCode = OpCodes.Ldfld;
                                il.Operand = ModuleDefinition.ImportReference(field);
                            }
                            else
                            {
                                //Setter
                                il.OpCode = OpCodes.Stfld;
                                il.Operand = ModuleDefinition.ImportReference(field);
                            }
                        }
                        else
                        {
                            var prop = srcType.Properties.FirstOrDefault(x => x.Name == propName);
                            if (prop != null)
                            {
                                var m = mr.Name.StartsWith("get_") ? prop.GetMethod : prop.SetMethod;
                                il.Operand = ModuleDefinition.ImportReference(m);
                                continue;
                            }
                            throw new WeavingException($"No field or property named {propName} found in type {srcType.FullName}");
                        }
                    }
                    else
                    {
                        //Call Method
                        var m = srcType.Methods.FirstOrDefault(x =>
                        {
                            var result = x.Name == mr.Name && x.Parameters.Count == mr.Parameters.Count;
                            if (!result) return false;
                            for (int i = 0; i < mr.Parameters.Count; i++)
                            {
                                var spt = mr.Parameters[i].ParameterType.FullName;
                                var dpt = mr.Parameters[i].ParameterType.FullName;
                                if (spt != dpt) return false;
                            }
                            return true;
                        });

                        if (m == null) throw new WeavingException($"The original method corresponding to method {mr} was not found");

                        il.Operand = ModuleDefinition.ImportReference(m);
                    }
                }

            }
        }

        //Redirect TypeReference

        var assemblyNameCache = new Dictionary<string, AssemblyNameReference>();
        foreach (var v in ModuleDefinition.GetTypeReferences())
        {
            if (!typeMapping.TryGetValue(v.FullName, out var type)) continue;
            v.Name = type.Name;
            v.Namespace = type.Namespace;
            var an = type.Scope as AssemblyNameReference;
            if (an != null)
            {
                if (!assemblyNameCache.TryGetValue(an.Name, out var dan))
                {
                    dan = new AssemblyNameReference(an.Name, an.Version);
                    assemblyNameCache[an.Name] = dan;
                    ModuleDefinition.AssemblyReferences.Add(dan);
                }
                v.Scope = dan;
            }

        }
    }

    public override IEnumerable<string> GetAssembliesForScanning()
    {
        yield return "HKMirror";
        yield return "Assembly-CSharp";
        yield return "PlayMaker";
    }

    public override bool ShouldCleanReference => true;
}

