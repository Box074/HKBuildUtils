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
            foreach(var v in ModuleDefinition.GetAllTypes()
                .SelectMany(x => x.Methods)
                .Where(x => x.HasBody)
                .ToArray())
            {
                foreach(var il in v.Body.Instructions.ToArray())
                {
                    TryCheckIH(il, v.Body);
                }
            }
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
        }
    }
}
