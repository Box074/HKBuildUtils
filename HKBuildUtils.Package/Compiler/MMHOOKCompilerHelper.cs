using MonoMod.RuntimeDetour.HookGen;
using MonoMod.RuntimeDetour;
using System.Reflection;
using System.Collections.Generic;
using System;

namespace HKBuildUtils.Compiler
{

    internal static class MMHOOKCompilerHelper
    {
        public static void Hook_Add(System.Delegate hook, System.RuntimeMethodHandle method) =>
            MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Add(System.Reflection.MethodBase.GetMethodFromHandle(method), hook);
        public static void Hook_Remove(System.Delegate hook, System.RuntimeMethodHandle method) =>
            MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Remove(System.Reflection.MethodBase.GetMethodFromHandle(method), hook);
        public static void Hook_Modify(System.Delegate hook, System.RuntimeMethodHandle method) =>
            MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Modify(System.Reflection.MethodBase.GetMethodFromHandle(method), hook);
        public static void Hook_Unmodify(System.Delegate hook, System.RuntimeMethodHandle method) =>
            MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Unmodify(MethodBase.GetMethodFromHandle(method), hook);
    }
}