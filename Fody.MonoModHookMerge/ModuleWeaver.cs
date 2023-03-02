using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Fody.MonoModHookMerge
{
    public partial class ModuleWeaver : BaseModuleWeaver
    {
        public override void Execute()
        {
            foreach (var t in ModuleDefinition.GetAllTypes().ToArray())
            {
                foreach (var v in t.Methods)
                {
                    foreach (var p in v.Parameters)
                    {
                        p.ParameterType = ConvertHookDelegate(p.ParameterType, out _);
                    }
                    v.ReturnType = ConvertHookDelegate(v.ReturnType, out _);
                    if (v.HasBody)
                    {
                        foreach (var il in v.Body.Instructions.ToArray())
                        {
                            TryCheckIH(il, v.Body);
                        }
                    }
                }
                foreach(var f in t.Fields)
                {
                    f.FieldType = ConvertHookDelegate(f.FieldType, out _);
                }
                foreach(var ev in t.Events)
                {
                    ev.EventType = ConvertHookDelegate(ev.EventType, out _);
                }
                foreach(var p in t.Properties)
                {
                    p.PropertyType = ConvertHookDelegate(p.PropertyType, out _);
                }
            }
            foreach(var v in ModuleDefinition.AssemblyReferences.ToArray())
            {
                if(v.Name.StartsWith("MMHOOK.")) ModuleDefinition.AssemblyReferences.Remove(v);
            }
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
        }
    }
}
