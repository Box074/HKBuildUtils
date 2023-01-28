using ILRepacking;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace HKBuildUtils
{
    public class MergeHKMirrorTask : Task
    {
        [Required]
        public string ModOutput { get; set; } = "";
        public string HKMirrorPath { get; set; } = "";
        public override bool Execute()
        {
            var root = Path.GetDirectoryName(ModOutput);
            var hkmp = string.IsNullOrEmpty(HKMirrorPath) ? Path.Combine(
                root, "HKMirror.dll"
                ) : HKMirrorPath;
            var hkmOP = Path.Combine(root, "HKMirror.modified.dll");
            var ar = new DefaultAssemblyResolver();
            ar.AddSearchDirectory(Path.GetDirectoryName(ModOutput));
            Version modVer;
            using (var hkm = AssemblyDefinition.ReadAssembly(hkmp, new()
            {
                AssemblyResolver = ar
            }))
            {
                var masm = hkm.MainModule.AssemblyResolver.Resolve(new("Assembly-CSharp", new()));
                var logger = masm.MainModule.GetType("Modding.Logger");
                var logError = logger.Methods.First(x => x.Name == "LogError" &&
                    x.Parameters.Count == 1 &&
                    x.Parameters[0].ParameterType == masm.MainModule.TypeSystem.Object);
                List<string> keepTypes = new()
                {
                    "HKMirror.HKMirrorMod"
                };
                List<string> useExt = new();
                HashSet<string> useMethods = new();
                ar.ResolveFailure += (sender, r) =>
                {
                    if (r.Name == "HKMirror") return hkm;
                    return null;
                };
                using (var mod = AssemblyDefinition.ReadAssembly(ModOutput, new()
                {
                    AssemblyResolver = ar
                }))
                {
                    modVer = mod.Name.Version;
                    var rmirror = mod.MainModule.GetTypeReferences().Where(x => x.Scope is AssemblyNameReference
                    {
                        Name: "HKMirror"
                    }).ToArray();

                    if (rmirror.Length == 0) return true;

                    keepTypes.AddRange(rmirror.Select(x => x.GetElementType().FullName));

                    useExt.AddRange(mod.MainModule.GetMemberReferences()
                        .OfType<MethodReference>()
                        .Where(x => x.DeclaringType.FullName == "HKMirror.Reflection.Extensions")
                        .Select(x => x.Resolve().Parameters[0].ParameterType.FullName)
                    );
                    foreach(var v in mod.MainModule.GetMemberReferences()
                        .OfType<MethodReference>()
                        .Where(x => x.DeclaringType.FullName.StartsWith("HKMirror."))
                        .Select(x => x.FullName)) useMethods.Add(v);
                }
                if (keepTypes.Any(x => x.StartsWith("HKMirror.Reflection")))
                {
                    keepTypes.Add("HKMirror.Reflection.InstanceClasses.InstanceClassWrapper`1");
                }
                foreach (var type in hkm.MainModule.Types.ToArray())
                {
                    bool keep = keepTypes.Contains(type.FullName);
                    if (type.FullName == "HKMirror.Reflection.Extensions" && keep)
                    {
                        foreach (var v in type.Methods.ToArray()
                            .Where(x => x.Name == "Reflect"))
                        {
                            if (useExt.Contains(v.Parameters[0].ParameterType.FullName)) continue;
                            type.Methods.Remove(v);
                        }
                    }
                    
                    type.IsPublic = false;

                    if (!keep)
                    {
                        hkm.MainModule.Types.Remove(type);
                        continue;
                    }

                    //Reserved, otherwise an exception is thrown at runtime
                    if (type.FullName == "HKMirror.HKMirrorMod")
                    {
                        type.IsAbstract = true;
                        type.BaseType = null;
                        type.IsSealed = true;
                        type.Namespace = "";
                        type.Name = "____Reserved____";
                        type.Methods.Clear();
                        continue;
                    }

                    if (type.FullName != "HKMirror.Reflection.InstanceClasses.InstanceClassWrapper`1"
                        && (type.FullName.StartsWith("HKMirror.Reflection.") ||
                        type.FullName.StartsWith("HKMirror.Hooks.ILHooks") ||
                        type.FullName == "HKMirror.PlayerDataAccess"
                        ))
                    {
                        foreach (var md in type.Methods.ToArray())
                        {
                            if (md.IsConstructor) continue;
                            if (!useMethods.Contains(md.FullName)) type.Methods.Remove(md);
                        }

                    }
                    if (type.FullName.StartsWith("HKMirror.Hooks.OnHooks"))
                    {
                        
                        var beforeOrAfter = new TypeDefinition[]
                        {
                            type.NestedTypes.First(x => x.Name == "AfterOrig"),
                            type.NestedTypes.First(x => x.Name == "BeforeOrig"),
                            type.NestedTypes.First(x => x.Name == "WithOrig")
                        };
                        foreach (var vt in beforeOrAfter)
                        {
                            foreach (var md in vt.Methods.ToArray())
                            {
                                if(md.IsConstructor) continue;
                                if(!useMethods.Contains(md.FullName))
                                {
                                    vt.Methods.Remove(md);
                                    continue;
                                }
                                foreach (var v in md.Body.Instructions
                                    .Where(x => x.OpCode == OpCodes.Call)
                                    .Select(x => ((MethodReference)x.Operand).FullName)) useMethods.Add(v);
                            }
                            vt.Events.Clear();
                        }
                        var handler = type.NestedTypes.First(x => x.Name == "HookHandler");
                        foreach (var v in handler.Methods)
                        {
                            if (!useMethods.Contains(v.FullName)) continue;
                            foreach (var mf in v.Body.Instructions
                                .Where(x => x.OpCode == OpCodes.Ldftn)
                                .Select(x => ((MethodReference)x.Operand).FullName)) useMethods.Add(mf);
                        }
                        foreach(var v in handler.Events)
                        {
                            if (!new[] { v.AddMethod, v.RemoveMethod }
                                .All(x => useMethods.Contains(x.FullName))) continue;
                            var field = (FieldDefinition)v.AddMethod.Body.Instructions
                                .First(x => x.OpCode == OpCodes.Ldsfld).Operand;
                            useMethods.Add(field.FullName);
                        }
                        foreach(var v in handler.Methods.ToArray())
                        {
                            if(v.IsConstructor) continue;
                            if(!useMethods.Contains(v.FullName))
                            {
                                handler.Methods.Remove(v);
                                continue;
                            }
                            var unusedPart = v.Body.Instructions
                                .Where(x => x.OpCode == OpCodes.Ldsfld && x.Operand is FieldDefinition)
                                .Select(x => (Item1: x, (FieldDefinition)x.Operand))
                                .Where(x => x.Item2.DeclaringType == handler && x.Item2.Name.StartsWith("_"))
                                .Where(x => !keepTypes.Contains(x.Item2.FieldType.FullName))
                                .FirstOrDefault(x => 
                                    x.Item1.Next.OpCode == OpCodes.Brfalse_S ||
                                    x.Item1.Next.OpCode == OpCodes.Brfalse);
                            if(unusedPart.Item2 != null)
                            {
                                var end = (Instruction) unusedPart.Item1.Next.Operand;
                                var il = unusedPart.Item1;
                                while(il != end)
                                {
                                    var next = il.Next;
                                    v.Body.Instructions.Remove(il);
                                    var e = v.Body.ExceptionHandlers.FirstOrDefault(x => x.TryStart == il);
                                    if(e != null)
                                    {
                                        v.Body.ExceptionHandlers.Remove(e);
                                    }
                                    il = next;
                                }
                            }
                            foreach(var il in v.Body.Instructions.ToArray())
                            {
                                if(il.OpCode != OpCodes.Call || il.Operand == null) continue;
                                if (il.Operand is not MethodDefinition cm) continue;
                                if (cm.Name != "DoLogError") continue;
                                il.Operand = v.Module.ImportReference(logError);
                                il.OpCode = OpCodes.Call;
                            }
                        }
                        foreach(var f in handler.Fields.ToArray())
                        {
                            if(!keepTypes.Contains(f.FieldType.FullName) && 
                                f.Name != "HookedList" &&
                                f.FieldType is TypeDefinition)
                            {
                                handler.Fields.Remove(f);
                                continue;
                            }
                        }
                        handler.Events.Clear();

                        foreach (var vt in new TypeDefinition[]
                        {
                            type.NestedTypes.First(x => x.Name == "Delegates"),
                        })
                        {
                            foreach (var dt in vt.NestedTypes.ToArray())
                            {
                                if (!keepTypes.Contains(dt.FullName)) vt.NestedTypes.Remove(dt);
                            }
                            vt.Events.Clear();
                        }
                    }
                    type.Properties.Clear();
                    type.Events.Clear();
                }
                
                hkm.CustomAttributes.Clear();
                hkm.Write(hkmOP);
            }

            var origMod = Path.ChangeExtension(ModOutput, ".orig.dll");
            File.Copy(ModOutput, origMod, true);
            var ilrepack = new ILRepack(new RepackOptions()
            {
                InputAssemblies = new[] {
                    origMod, hkmOP
                },
                OutputFile = ModOutput,
                DebugInfo = true,
                TargetKind = ILRepack.Kind.Dll,
                Version = modVer,
                SearchDirectories = ar.GetSearchDirectories()
            });
            ilrepack.Repack();
            File.Delete(origMod);
            File.Delete(hkmOP);
            return true;
        }
    }
}
