
using HKBuildUtils.Main.Reflect;
using Mono.Cecil;

var asm = AssemblyDefinition.CreateAssembly(new("Test1", new()), "Test1", ModuleKind.Dll);
var generator = new ReflectHelperGenerator(
    AssemblyDefinition.ReadAssembly(@"E:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Assembly-CSharp.dll"),
    asm.MainModule);
generator.Generate();

asm.Write("Test1.dll");