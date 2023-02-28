using HKBuildUtils.Checker;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HKBuildUtils.Main.Reflect
{
    public class ReflectHelperGenerator
    {
        private AssemblyDefinition assembly;
        private ModuleDefinition module;
        private Dictionary<string, TypeDefinition> typeMap = new();
        private Dictionary<string, List<Action<TypeDefinition>>> waitForType = new();
        private MethodReference extAttrCtor;
        private TypeDefinition extType = null!;
        public ReflectHelperGenerator(AssemblyDefinition assembly, ModuleDefinition module)
        {
            this.assembly = assembly;
            this.module = module;
            extAttrCtor = module.ImportReference(
                (assembly.Name.Name == "mscorlib" ? assembly : assembly.MainModule.AssemblyResolver
                .Resolve(new AssemblyNameReference("mscorlib", new())))
                .MainModule.GetType("System.Runtime.CompilerServices.ExtensionAttribute")
                .Methods.First(x => x.Name == ".ctor"));
        }
        public void Generate(string origSHA)
        {
            var md = new TypeDefinition("<HKBUMD>", "Reflect", TypeAttributes.NotPublic)
            {
                Fields =
                {
                    new("GEN_VER", FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal,
                        module.TypeSystem.Int32)
                    {
                        Constant = RemoveInvalidRefHelperTask.CURRENT_REFHELPER_GENERATOR_VER
                    },
                    new(string.IsNullOrEmpty(origSHA) ? "_SHA" : "ORIG_SHA256",
                         FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal,
                        module.TypeSystem.String)
                    {
                        Constant = origSHA
                    }
                }
            };
            module.Types.Add(md);

            extType = new TypeDefinition("", "ReflectHelperExt", TypeAttributes.Public |
                                                                    TypeAttributes.Class |
                                                                    TypeAttributes.Abstract);
            extType.CustomAttributes.Add(new(extAttrCtor));
            module.Types.Add(extType);

            foreach (var v in assembly.MainModule.Types)
            {
                if ((v.IsValueType || v.IsEnum) || v.HasGenericParameters ||
                    v.Name.IsInvalidName(true) || v.IsInterface) continue;
                var type = GenerateType(v);
                module.Types.Add(type);
            }

            foreach(var f in waitForType.ToArray())
            {
                waitForType = null!;
                foreach(var cb in f.Value)
                {
                    cb(null!);
                }
            }
        }
        private void GetMappedType(string type, Action<TypeDefinition> action)
        {
            if (typeMap.TryGetValue(type, out var t))
            {
                action(t);
                return;
            }
            if (waitForType == null)
            {
                action(null!);
                return;
            }
            if (!waitForType.TryGetValue(type, out var wl))
            {
                wl = new();
                waitForType.Add(type, wl);
            }
            wl.Add(action);
        }
        private void NoticeWaitForMappedType(string type, TypeDefinition action)
        {
            typeMap[type] = action;
            if (!waitForType.TryGetValue(type, out var list)) return;
            foreach (var v in list) v(action);
            list.Clear();
        }
        private TypeReference ImportSafe(TypeReference type, IGenericParameterProvider provider)
        {
            /*if (type.IsGenericParameter || (type.Scope == null && (type is not TypeSpecification)))
            {
                return type;
            }
            var et = type.GetElementType();
            if(et != null)
            {
                if (et.IsGenericParameter || et.Scope == null)
                {
                    return type;
                }
            }*/
            var bt = type.GetElementType() ?? type;
            if(type.IsGenericInstance || bt.IsGenericInstance)
            {
                var gtype = (type as GenericInstanceType) ?? (bt as GenericInstanceType);
                if (gtype == null) gtype = (GenericInstanceType)bt;
                var result = new GenericInstanceType(ImportSafe(bt, provider));
                foreach (var v in gtype.GenericArguments) result.GenericArguments.Add(ImportSafe(v, provider));
                if (type.IsArray) return new ArrayType(result);
                if (type.IsPointer) return new PointerType(result);
                if (type.IsByReference) return new ByReferenceType(result);
                return result;
            }
            if (bt.IsGenericParameter) return type;
            provider = null!;
            if (type.IsDefinition && (type.Module == module || type.Module == null)) return type;
            return module.ImportReference(type, (provider?.Module == module) ? provider : null);
        }
        private void ModifyType(TypeReference srcType, IGenericParameterProvider provider, Action<TypeReference> cb)
        {
            if (srcType is GenericParameter gp)
            {
                if (gp.Owner is TypeReference td)
                {
                    ModifyType(td, provider, r =>
                    {
                        cb(r.GenericParameters[gp.Position]);
                    });
                    return;
                }
                cb(srcType);
                return;
            }
            if (srcType.IsGenericInstance)
            {
                var gt = (GenericInstanceType)srcType;
                ModifyType(gt.GetElementType(), provider, elType =>
                {
                    var rgt = new GenericInstanceType(elType);
                    var galist = new List<TypeReference>();
                    foreach (var v in gt.GenericArguments)
                    {
                        ModifyType(v, provider, elType =>
                        {
                            galist.Add(elType);
                            if (galist.Count < gt.GenericArguments.Count) return;
                            foreach (var t in galist)
                            {
                                rgt.GenericArguments.Add(t);
                            }
                            cb(ImportSafe(rgt, provider));
                        });
                    }
                });
                return;
            }
            if (srcType.IsByReference || srcType.IsArray || srcType.IsPointer)
            {
                if (srcType.IsByReference)
                {
                    ModifyType(srcType.GetElementType(), provider, el =>
                    {
                        cb(new ByReferenceType(el));
                    });
                }
                if (srcType.IsArray)
                {
                    ModifyType(srcType.GetElementType(), provider, el =>
                    {
                        cb(new ArrayType(el, (srcType as ArrayType)!.Rank));
                    });
                }
                if (srcType.IsPointer)
                {
                    ModifyType(srcType.GetElementType(), provider, el =>
                    {
                        cb(new PointerType(el));
                    });
                }
                return;
            }
            try
            {
                var srcT = srcType.Resolve();
                if (srcT.IsPublic())
                {
                    cb(ImportSafe(srcType, provider));
                    return;
                }
                GetMappedType(srcT.FullName, rt =>
                {
                    if (rt == null) cb(ImportSafe(srcType, provider));
                    else cb(rt);
                });
            }
            catch (Exception)
            {
                cb(ImportSafe(srcType, provider));
            }
        }
        private MethodDefinition CreateDummyMethod(MethodDefinition md)
        {

            var str = md.ReturnType.IsGenericParameter ? module.TypeSystem.Object : md.ReturnType;
            var method = new MethodDefinition(md.Name, md.Attributes, str);
            
            foreach (var tp in md.GenericParameters)
            {
                method.GenericParameters.Add(new(tp.Name, method));
            }
            ModifyType(md.ReturnType, method, r => method.ReturnType = r);
            method.IsPublic = true;

            


            foreach (var p in md.Parameters)
            {
                var st = p.ParameterType.IsGenericParameter ? module.TypeSystem.Object : p.ParameterType;
                var pd = new ParameterDefinition(p.Name, p.Attributes, st ?? module.ImportReference(st));
                ModifyType(p.ParameterType, method, tr => pd.ParameterType = tr);
                method.Parameters.Add(pd);
            }

            /*method.Body = new(method);
            var ilp = method.Body.GetILProcessor();
            ilp.Emit(OpCodes.Ldtoken, module.ImportReference(md));
            ilp.Emit(OpCodes.Ret);*/
            return method;
        }
        private MethodDefinition FindOrCreateDummyMethod(MethodDefinition md, TypeDefinition type)
        {
            //var result = type.Methods.FirstOrDefault(x => x.Name == md.Name);
            //if (result != null) return result;
            var result = CreateDummyMethod(md);
            type.Methods.Add(result);
            return result;
        }
        private TypeDefinition GenerateType(TypeDefinition srcType)
        {
            var typeDef = new TypeDefinition(srcType.Namespace, srcType.Name + "R", TypeAttributes.Public
                | TypeAttributes.AutoLayout);

            foreach (var gp in srcType.GenericParameters)
            {
                var g = new GenericParameter(gp.Name, typeDef);
                typeDef.GenericParameters.Add(g);
            }


            NoticeWaitForMappedType(srcType.FullName, typeDef);

            if (!srcType.IsValueType && !srcType.IsEnum)
            {
                TypeReference bt = ImportSafe(srcType, null!);
                if (srcType.HasGenericParameters)
                {
                    bt = new GenericInstanceType(bt);
                    foreach (var ga in typeDef.GenericParameters) ((GenericInstanceType)bt).GenericArguments.Add(ga);
                }
                typeDef.BaseType = bt;
            }
            if (srcType.IsEnum)
            {
                typeDef.BaseType = ImportSafe(new TypeReference("System", "Enum", null,
                    module.TypeSystem.CoreLibrary), null!);
            }

            var extMethod = new MethodDefinition("Reflect", MethodAttributes.Public | MethodAttributes.Static,
                                typeDef);
            extMethod.CustomAttributes.Add(new(extAttrCtor));
            extType.Methods.Add(extMethod);
            if (!srcType.HasGenericParameters)
            {
                extMethod.Parameters.Add(new(ImportSafe(srcType, srcType)));
            }
            else
            {
                var gi = new GenericInstanceType(ImportSafe(srcType, null!));
                var rgi = new GenericInstanceType(typeDef);
                foreach (var tga in srcType.GenericParameters)
                {
                    var pd = new GenericParameter(tga.Name, extMethod);
                    gi.GenericArguments.Add(pd);
                    rgi.GenericArguments.Add(pd);
                }
                extMethod.Parameters.Add(new(ImportSafe(gi, srcType)));
                extMethod.ReturnType = rgi;
            }

            bool insertAutoCtor = true;

            foreach (var fd in srcType.Fields)
            {
                if (fd.IsPublic() || fd.Name.IsInvalidName()) continue;
                var field = new FieldDefinition(fd.Name, fd.Attributes, ImportSafe(fd.FieldType, typeDef));
                ModifyType(fd.FieldType, typeDef, r => field.FieldType = r);
                field.IsPublic = true;
                //if(fd.IsLiteral && srcType.IsEnum) field.Constant = fd.Constant;
                typeDef.Fields.Add(field);
            }
            foreach (var md in srcType.Methods)
            {
                if ((md.IsPublic() || md.Name.IsInvalidName()) && md.Name != ".ctor") continue;
                if (md.Name == ".ctor") insertAutoCtor = false;

                typeDef.Methods.Add(CreateDummyMethod(md));
            }
            foreach (var pd in srcType.Properties)
            {
                if (pd.IsPublic()) continue;
                var pr = new PropertyDefinition(pd.Name, pd.Attributes, ImportSafe(pd.PropertyType, typeDef));
                ModifyType(pd.PropertyType, typeDef, r => pr.PropertyType = r);
                typeDef.Properties.Add(pr);

                if (pd.GetMethod != null)
                {
                    pr.GetMethod = FindOrCreateDummyMethod(pd.GetMethod, typeDef);
                }
                if (pd.SetMethod != null)
                {
                    pr.SetMethod = FindOrCreateDummyMethod(pd.SetMethod, typeDef);
                }
            }
            foreach (var ev in srcType.Events)
            {
                if (ev.IsPublic()) continue;
                var st = ev.EventType.IsGenericInstance ? module.TypeSystem.Object : ev.EventType;
                var er = new EventDefinition(ev.Name, ev.Attributes, ImportSafe(ev.EventType, typeDef));
                ModifyType(ev.EventType, typeDef, r => er.EventType = r);
                typeDef.Events.Add(er);

                if (ev.AddMethod != null)
                {
                    er.AddMethod = FindOrCreateDummyMethod(ev.AddMethod, typeDef);
                }
                if (ev.RemoveMethod != null)
                {
                    er.RemoveMethod = FindOrCreateDummyMethod(ev.RemoveMethod, typeDef);
                }
            }
            foreach (var nt in srcType.NestedTypes)
            {
                if ((nt.IsValueType || nt.IsEnum) || nt.HasGenericParameters ||
                    nt.Name.IsInvalidName() || nt.IsInterface) continue;
                if (nt.IsPublic() || nt.Name.IsInvalidName(true)) continue;
                typeDef.NestedTypes.Add(GenerateType(nt));
            }

            if (insertAutoCtor && !srcType.IsEnum)
            {
                var ctor = new MethodDefinition(".ctor", MethodAttributes.Private, module.TypeSystem.Void);
                typeDef.Methods.Add(ctor);
            }

            return typeDef;
        }
    }
}
