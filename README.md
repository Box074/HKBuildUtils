
# **H**ollow **K**night Mod **B**uild **Utils**

 [![Build](https://github.com/HKLab/HKBuildUtils/actions/workflows/dotnet.yml/badge.svg)](https://github.com/HKLab/HKBuildUtils/actions/workflows/dotnet.yml) ![Nuget](https://img.shields.io/nuget/v/HKBuildUtils)

A nuget package to help make the Hollow Knight Mod

## Features

### Merger with HKMirror

Use ILRepack to merge mods with [HKMirror](https://github.com/TheMulhima/HKMirror) and eliminate unused types in [HKMirror](https://github.com/TheMulhima/HKMirror).

#### How to enable

Add `<MergeHKMirror>true</MergeHKMirror>` to `PropertyGroup` and reference [HKMirror](https://github.com/TheMulhima/HKMirror).
The rest will take care of it for you.

### Mod Resources

Using `<ModResource></ModResource>` instead of `<EmbeddedResource></EmbeddedResource>` automatically generates the type `ModResources` for use

#### Example

Add the following to the project file

```xml
<ItemGroup>
    <ModResource Include="Test1.txt"></ModResource>
    <ModResource Include="a/Test2.txt"></ModResource>
    <ModResource Include="b/c/Test3.txt"></ModResource>
</ItemGroup>
```

Then you can get them like this in your code

```c#
Modding.Logger.Log(ModResources.TEST1.Length);

Modding.Logger.Log(ModResources.TEST2.Length);

Modding.Logger.Log(ModResources.TEST3.Length);
```

You can add `Default="true"` to `<ModResource>` to use user-defined content.Like this

```xml
<ItemGroup>
    <ModResource Include="a/b/c" Default="true"></ModResource>
</ItemGroup>
```

You can get it through `ModResources.C`, just like mentioned above.

If `ModResources.C` is used, the file `$(ModDir)/a/b/c` is checked for existence first. If present, the file contents are returned, and if they are not, read from the assembly and written to the `$(ModDir)/a/b/c` file
