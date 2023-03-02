using HKBuildUtils.Checker;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
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
        private MethodReference extAttrCtor;
        private TypeDefinition extType = null!;
        public ReflectHelperGenerator(AssemblyDefinition assembly)
        {
            this.assembly = assembly;
            this.module = assembly.MainModule;
            extAttrCtor = module.ImportReference(
                (assembly.Name.Name == "mscorlib" ? assembly : assembly.MainModule.AssemblyResolver
                .Resolve(new AssemblyNameReference("mscorlib", new())))
                .MainModule.GetType("System.Runtime.CompilerServices.ExtensionAttribute")
                .Methods.First(x => x.Name == ".ctor"));
        }
        public void Generate(string origSHA, AssemblyNameReference origAssembly)
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

            module.AssemblyReferences.Add(origAssembly);

            extType = new TypeDefinition("", "ReflectHelperExt", TypeAttributes.Public |
                                                                    TypeAttributes.Class |
                                                                    TypeAttributes.Abstract);
            extType.CustomAttributes.Add(new(extAttrCtor));

            foreach (var v in assembly.MainModule.Types)
            {
                ProcessingType(v, null!, origAssembly);
            }

            module.Types.Add(extType);
            module.Types.Add(md);

        }

        private void ProcessingType(TypeDefinition td, TypeReference parent, AssemblyNameReference origAssembly)
        {
            var srcType = module.ImportReference(new TypeReference(td.Namespace, td.Name, null, origAssembly
                    )
            {
                DeclaringType = parent
            });
            var orig2R = new MethodDefinition("Reflect", MethodAttributes.Public | MethodAttributes.Static,
                td)
            {
                CustomAttributes =
                    {
                        new(extAttrCtor)
                    },
                Parameters =
                    {
                        new(srcType)
                    }
            };
            if(td.HasGenericParameters)
            {
                var grs = new GenericInstanceType(srcType);
                var grd = new GenericInstanceType(td);
                for(int i = 0; i < td.GenericParameters.Count; i++)
                {
                    var gt = new GenericParameter(orig2R);
                    orig2R.GenericParameters.Add(gt);
                    grs.GenericArguments.Add(gt);
                    grd.GenericArguments.Add(gt);
                }
                orig2R.Parameters[0].ParameterType = grs;
                orig2R.ReturnType = grd;
            }
            var R2orig = new MethodDefinition("ToOriginal", MethodAttributes.Public | MethodAttributes.Static,
               srcType)
            {
                CustomAttributes =
                    {
                        new(extAttrCtor)
                    },
                Parameters =
                    {
                        new(td)
                    }
            };
            if (td.HasGenericParameters)
            {
                var grs = new GenericInstanceType(srcType);
                var grd = new GenericInstanceType(td);
                for (int i = 0; i < td.GenericParameters.Count; i++)
                {
                    var gt = new GenericParameter(R2orig);
                    R2orig.GenericParameters.Add(gt);
                    grs.GenericArguments.Add(gt);
                    grd.GenericArguments.Add(gt);
                }
                R2orig.Parameters[0].ParameterType = grd;
                R2orig.ReturnType = grs;
            }

            extType.Methods.Add(R2orig);
            extType.Methods.Add(orig2R);
            td.CustomAttributes.Clear();
            td.IsPublic = true;
            if (!td.HasGenericParameters)
            {
                td.Name += "R";
            }
            else
            {
                td.Name = td.Name.Split('`')[0] + "R`" + td.GenericParameters.Count;
            }

            foreach (var m in td.Methods)
            {
                m.CustomAttributes.Clear();
                m.IsNative = true;
                m.IsVirtual = false;
                m.Overrides.Clear();
                if (m.Name == ".cctor") continue;
                m.IsPublic = true;
            }
            foreach (var f in td.Fields)
            {
                f.CustomAttributes.Clear();
                if (f.Name.IsInvalidName()) continue;
                f.IsPublic = true;
            }

            var md = new MethodDefinition("__HKBU__ORIGINAL_TYPE__", MethodAttributes.Private | MethodAttributes.Static,
                module.TypeSystem.Void)
            {
                Body = {
                    Instructions =
                    {
                        Instruction.Create(OpCodes.Ldtoken, srcType),
                        Instruction.Create(OpCodes.Ret)
                    }
                }
            };

            td.Methods.Add(md);

            foreach (var nt in td.NestedTypes)
            {
                ProcessingType(nt, srcType, origAssembly);
            }
        }
    }
}
