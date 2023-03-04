using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Fody.MonoModHookMerge
{
    public partial class ModuleWeaver : BaseModuleWeaver
    {
        private TypeReference GetRootType(TypeReference tr)
        {
            while(true)
            {
                if (tr.DeclaringType == null) return tr;
                tr = tr.DeclaringType;
            }
        }
        public override void Execute()
        {
            foreach (var t in ModuleDefinition.GetAllTypes().ToArray())
            {
                foreach (var v in t.Methods)
                {
                    if (v.HasBody)
                    {
                        foreach (var il in v.Body.Instructions.ToArray())
                        {
                            TryCheckIH(il, v.Body);
                        }
                    }
                }
            }
            
            AssemblyNameReference refself = null; 
            _RE_CHECK:
            foreach(var rt0 in ModuleDefinition.GetTypeReferences().ToArray())
            {
                if (rt0 is TypeSpecification || rt0.HasGenericParameters) continue;
                var rt = GetRootType(rt0);
                var asmr = rt.Scope as AssemblyNameReference;
                if(asmr is null) continue;
                var asmn = asmr.Name;
                if (!asmn.StartsWith("MMHOOK_", StringComparison.OrdinalIgnoreCase) &&
                    !asmn.StartsWith("MMHOOK.", StringComparison.OrdinalIgnoreCase)) continue;
                var ctd = ConvertHookDelegate(rt0, out var replaced);
                if (!replaced) continue;

                if(refself == null)
                {
                    refself = new AssemblyNameReference(ModuleDefinition.Assembly.Name.Name, 
                                ModuleDefinition.Assembly.Name.Version);
                    ModuleDefinition.AssemblyReferences.Add(refself);
                }
               
                rt0.DeclaringType = null;
                rt0.Scope = refself;
                rt0.Namespace = ctd.Namespace;
                rt0.Name = ctd.Name;
            }
            foreach(var rt0 in ModuleDefinition.GetTypeReferences())
            {
                if (rt0 is TypeSpecification || rt0.HasGenericParameters) continue;
                if (!rt0.Name.StartsWith("hook_") && 
                    !rt0.Name.StartsWith("orig_")) continue;

                var rt = GetRootType(rt0);
                var asmr = rt.Scope as AssemblyNameReference;
                if (asmr is null) continue;
                var asmn = asmr.Name;
                if (!asmn.StartsWith("MMHOOK_", StringComparison.OrdinalIgnoreCase) &&
                    !asmn.StartsWith("MMHOOK.", StringComparison.OrdinalIgnoreCase)) continue;
                goto _RE_CHECK;
            }
            foreach (var v in ModuleDefinition.AssemblyReferences.ToArray())
            {
                if (v.Name.StartsWith("MMHOOK.") || v.Name.StartsWith("MMHOOK_"))
                {
                    v.Culture = "neutral";
                    v.PublicKey = null;
                    v.HasPublicKey = false;
                    v.Name = ModuleDefinition.Assembly.Name.Name;
                    v.Version = ModuleDefinition.Assembly.Name.Version;
                }
            }
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
        }
    }
}
