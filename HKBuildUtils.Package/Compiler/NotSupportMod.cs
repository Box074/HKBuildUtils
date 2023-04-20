using System;
using System.Linq;
using System.Reflection;
using Modding;

internal class NotSupportMod : Mod
{
    public NotSupportMod() : base(typeof(NotSupportMod).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(x => x.Key == "ModName").Value) { }
    public override string GetVersion() => "<color=red>Current platform is not supported</color>";
}

