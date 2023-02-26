using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Fody.ReflectHelper
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        private bool IsDefinedInReflectHelper(TypeReference type)
        {
            if (type == null) return false;
            if (type.Scope is not AssemblyNameReference asmName)
            {
                if (type.DeclaringType != null) return IsDefinedInReflectHelper(type.DeclaringType);
                return false;
            }
            return asmName.Name.StartsWith("ReflectHelper.");
        }
        public override void Execute()
        {
            //var lib = ModuleDefinition.Assembly; //ModuleDefinition.AssemblyResolver.Resolve(new("HKBuildUtils.Lib", new()))
            // ?? throw new NotSupportedException();
            var setSkipVisibeCheck = 
                ModuleDefinition.GetType("HKBuildUtils.Compiler.CReflectHelper")?.Methods
                .First(x => x.Name == "SetSkipVisibeCheck");
            
            foreach(var type in ModuleDefinition.GetAllTypes())
            {
                foreach(var m in type.Methods.ToArray())
                {
                    var body = m.Body;
                    if(body != null && body.Instructions.Count > 0)
                    {
                        bool shouldWrap = false;
                        foreach(var il in body.Instructions)
                        {
                            if(il.OpCode == OpCodes.Call && il.Operand is MethodReference mr)
                            {
                                if(mr.Name == "Reflect")
                                {
                                    if(IsDefinedInReflectHelper(mr.DeclaringType) && 
                                        mr.DeclaringType.Name == "ReflectHelperExt")
                                    {
                                        il.OpCode = OpCodes.Nop;
                                        shouldWrap = true;
                                    }
                                }
                                
                            }
                            else if(!shouldWrap && il.Operand is MemberReference member)
                            {
                                if(member.DeclaringType != null)
                                {
                                    if(IsDefinedInReflectHelper(member.DeclaringType))
                                    {
                                        shouldWrap = true;
                                    }
                                }
                            }
                        }
                        if(shouldWrap && setSkipVisibeCheck != null)
                        {
                            var wrapDef = new MethodDefinition("$Wrap<" + m.Name + ">", m.Attributes, m.ReturnType);
                            wrapDef.IsPrivate = true;
                            wrapDef.Body = m.Body;
                            m.Body = new(m);

                            type.Methods.Add(wrapDef);
                            foreach (var pd in m.Parameters) wrapDef.Parameters.Add(pd);
                            foreach(var ptg in m.GenericParameters) wrapDef.GenericParameters.Add(ptg);
                            MethodReference mr;
                            if(m.IsGenericInstance)
                            {
                                var gmr = new GenericInstanceMethod(wrapDef);
                                foreach (var v in m.GenericParameters) gmr.GenericArguments.Add(v);
                                mr = ModuleDefinition.ImportReference(gmr);
                            }
                            else
                            {
                                mr = wrapDef;
                            }
                            var ilp = m.Body.GetILProcessor();
                            ilp.Emit(OpCodes.Ldtoken, mr);
                            ilp.Emit(OpCodes.Call, ModuleDefinition.ImportReference(setSkipVisibeCheck));
                            for(int i = 0; i < m.Parameters.Count + (m.HasThis ? 1 : 0); i++)
                            {
                                ilp.Emit(OpCodes.Ldarg, i);
                            }
                            ilp.Emit(OpCodes.Call, mr);
                            ilp.Emit(OpCodes.Ret);
                        }
                    }
                }
            }
            foreach(var tr in ModuleDefinition.GetTypeReferences())
            {
                if (!IsDefinedInReflectHelper(tr) || tr.IsArray || tr.IsGenericInstance || tr.IsPointer) continue;
                var td = tr.Resolve();
                if (td.BaseType != null)
                {
                    var srcType = ModuleDefinition.ImportReference(td.BaseType);
                    tr.Name = srcType.Name;
                    tr.Scope = srcType.Scope;
                    tr.Namespace = srcType.Namespace;
                    tr.DeclaringType = srcType.DeclaringType;
                }
            }
            foreach (var ar in ModuleDefinition.AssemblyReferences.ToArray())
            {
                if (ar.Name.StartsWith("ReflectHelper."))
                {
                    ModuleDefinition.AssemblyReferences.Remove(ar);
                }
            }
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
        }
    }
}
