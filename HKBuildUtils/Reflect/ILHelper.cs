using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HKBuildUtils.Main.Reflect
{
    internal static class ILHelper
    {
        public static bool IsPublic(this TypeDefinition type) => type.IsPublic && (type.DeclaringType?.Resolve()
            .IsPublic() ?? true);
        public static bool IsPublic(this FieldDefinition member) => member.IsPublic && (member.DeclaringType?.Resolve()
            .IsPublic() ?? true);
        public static bool IsPublic(this MethodDefinition member) => member.IsPublic && (member.DeclaringType?.Resolve()
            .IsPublic() ?? true);
        public static bool IsPublic(this PropertyDefinition member) => (member.GetMethod?.Resolve().IsPublic() ?? true)
            && (member.SetMethod?.Resolve().IsPublic ?? true)
            && (member.DeclaringType?.Resolve()
            .IsPublic() ?? true);
        public static bool IsPublic(this EventDefinition member) => (member.AddMethod?.Resolve().IsPublic() ?? true)
            && (member.RemoveMethod?.Resolve().IsPublic ?? true)
            && (member.DeclaringType?.Resolve()
            .IsPublic() ?? true);
        public static readonly string InvalidChars = ".`~!@#$%^&*()+-=[]{}<>,;:'\"|\\";
        public static readonly string InvalidChars_GT = ".~!@#$%^&*()+-=[]{}<>,;:'\"|\\";
        public static bool IsInvalidName(this string name, bool isGT = false) => (isGT? InvalidChars_GT : InvalidChars).Any(x => name.IndexOf(x) != -1);
        public static MethodDefinition? SearchMethod(this TypeDefinition type, string name)
        {
            while(type != null)
            {
                var result = type.Methods.FirstOrDefault(m => m.Name == name);
                if(result != null) return result;
                type = type.BaseType?.Resolve()!;
            }
            return null;
        }
        
    }
}
