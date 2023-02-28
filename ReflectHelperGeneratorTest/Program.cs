
using HKBuildUtils.Main.Reflect;
using Mono.Cecil;

var searcher = new DefaultAssemblyResolver();
searcher.AddSearchDirectory(@"E:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\");
var asm = AssemblyDefinition.ReadAssembly(@"E:\SteamLibrary\steamapps\common\Hollow Knight\hollow_knight_Data\Managed\Assembly-CSharp.dll", new()
{
    AssemblyResolver = searcher,
});

var origName = new AssemblyNameReference(asm.Name.Name, asm.Name.Version);

asm.Name.Name = "Test1";

var generator = new ReflectHelperGenerator(asm);
generator.Generate("", origName);

asm.Write("Test1.dll");