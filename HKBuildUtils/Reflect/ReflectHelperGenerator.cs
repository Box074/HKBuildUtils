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
                assembly.MainModule.AssemblyResolver.Resolve(new AssemblyNameReference("mscorlib", new()))
                .MainModule.GetType("System.Runtime.CompilerServices.ExtensionAttribute")
                .Methods.First(x => x.Name == ".ctor"));
        }
        public void Generate()
        {
            extType = new TypeDefinition("", "ReflectHelperExt", TypeAttributes.Public | 
                                                                    TypeAttributes.Class |
                                                                    TypeAttributes.Abstract);
            extType.CustomAttributes.Add(new(extAttrCtor));
            module.Types.Add(extType);

            foreach (var v in assembly.MainModule.Types)
            {
                if ((v.IsValueType || v.IsEnum) || v.HasGenericParameters || 
                    v.Name.IsInvalidName() || v.IsInterface) continue;
                var type = GenerateType(v);
                module.Types.Add(type);
            }
        }
        private void GetMappedType(string type, Action<TypeDefinition> action)
        {
            if(typeMap.TryGetValue(type, out var t))
            {
                action(t);
                return;
            }
            if(!waitForType.TryGetValue(type, out var wl))
            {
                wl = new();
                waitForType.Add(type, wl);
            }
            wl.Add(action);
        }
        private void NoticeWaitForMappedType(string type, TypeDefinition action) {
            typeMap[type] = action;
            if (!waitForType.TryGetValue(type, out var list)) return;
            foreach (var v in list) v(action);
        }
        private void ModifyType(TypeReference srcType, Action<TypeReference> cb)
        {
            if(srcType.IsGenericParameter)
            {
                cb(srcType);
                return;
            }
            if(srcType.IsGenericInstance)
            {
                var gt = (GenericInstanceType)srcType;
                ModifyType(gt.GetElementType(), elType =>
                {
                    var rgt = new GenericInstanceType(elType);
                    var galist = new List<TypeReference>();
                    foreach(var v in gt.GenericArguments)
                    {
                        ModifyType(v, elType =>
                        {
                            galist.Add(elType);
                            if (galist.Count < gt.GenericArguments.Count) return;
                            foreach(var t in galist)
                            {
                                rgt.GenericArguments.Add(t);
                            }
                            cb(module.ImportReference(rgt));
                        });
                    }
                });
                return;
            }
            try
            {
                var srcT = srcType.Resolve();
                if (srcT.IsPublic())
                {
                    cb(module.ImportReference(srcType));
                    return;
                }
                GetMappedType(srcT.FullName, cb);
            }catch(Exception)
            {
                cb(module.ImportReference(srcType));
            }
        }
        private MethodDefinition CreateDummyMethod(MethodDefinition md)
        {
            var str = md.ReturnType.IsGenericInstance ? module.TypeSystem.Object : md.ReturnType;
            var method = new MethodDefinition(md.Name, md.Attributes, module.ImportReference(str));
            ModifyType(md.ReturnType, r => method.ReturnType = r);
            method.IsPublic = true;
            foreach (var p in md.Parameters)
            {
                var st = p.ParameterType.IsGenericInstance ? module.TypeSystem.Object : p.ParameterType;
                var pd = new ParameterDefinition(p.Name, p.Attributes, module.ImportReference(st));
                ModifyType(p.ParameterType, tr => pd.ParameterType = tr);
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
            var result = type.Methods.FirstOrDefault(x => x.Name == md.Name);
            if(result != null) return result;
            result = CreateDummyMethod(md);
            type.Methods.Add(result);
            return result;
        }
        private TypeDefinition GenerateType(TypeDefinition srcType)
        {
            var typeDef = new TypeDefinition(srcType.Namespace, srcType.Name + "R", TypeAttributes.Public
                | TypeAttributes.AutoLayout);

            var extMethod = new MethodDefinition("Reflect", MethodAttributes.Public | MethodAttributes.Static,
                typeDef);
            extMethod.Parameters.Add(new(module.ImportReference(srcType)));
            extMethod.CustomAttributes.Add(new(extAttrCtor));
            extType.Methods.Add(extMethod);

            NoticeWaitForMappedType(srcType.FullName, typeDef);

            if (!srcType.IsValueType && !srcType.IsEnum)
            {
                typeDef.BaseType = module.ImportReference(srcType);
            }
            if(srcType.IsEnum)
            {
                typeDef.BaseType = module.ImportReference(new TypeReference("System", "Enum", null,
                    module.TypeSystem.CoreLibrary));
            }
            bool insertAutoCtor = true;

            foreach(var fd in srcType.Fields)
            {
                if (fd.IsPublic() || fd.Name.IsInvalidName()) continue;
                var st = fd.FieldType.IsGenericInstance ? module.TypeSystem.Object : fd.FieldType;
                var field = new FieldDefinition(fd.Name, fd.Attributes, module.ImportReference(st));
                ModifyType(fd.FieldType, r => field.FieldType = r);
                field.IsPublic = true;
                //if(fd.IsLiteral && srcType.IsEnum) field.Constant = fd.Constant;
                typeDef.Fields.Add(field);
            }
            foreach(var md in srcType.Methods)
            { 
                if (md.HasGenericParameters) continue;
                if ((md.IsPublic() || md.Name.IsInvalidName()) && md.Name != ".ctor") continue;
                if (md.Name == ".ctor") insertAutoCtor = false;
               
                typeDef.Methods.Add(CreateDummyMethod(md));
            }
            foreach(var pd in srcType.Properties)
            {
                if (pd.IsPublic()) continue;
                var st = pd.PropertyType.IsGenericInstance ? module.TypeSystem.Object : pd.PropertyType;
                var pr = new PropertyDefinition(pd.Name, pd.Attributes, module.ImportReference(st));
                ModifyType(pd.PropertyType, r => pr.PropertyType = r);
                typeDef.Properties.Add(pr);

                if(pd.GetMethod != null)
                {
                    pr.GetMethod = FindOrCreateDummyMethod(pd.GetMethod, typeDef);
                }
                if(pd.SetMethod != null)
                {
                    pr.SetMethod = FindOrCreateDummyMethod(pd.SetMethod, typeDef);
                }
            }
            foreach (var ev in srcType.Events)
            {
                if (ev.IsPublic()) continue;
                var st = ev.EventType.IsGenericInstance ? module.TypeSystem.Object : ev.EventType;
                var er = new EventDefinition(ev.Name, ev.Attributes, module.ImportReference(st));
                ModifyType(ev.EventType, r => er.EventType = r);
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
            foreach(var nt in srcType.NestedTypes)
            {
                if ((nt.IsValueType || nt.IsEnum) || nt.HasGenericParameters ||
                    nt.Name.IsInvalidName() || nt.IsInterface) continue;
                if (nt.IsPublic() || nt.Name.IsInvalidName()) continue;
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
