
using System.Reflection;
using System.Runtime.InteropServices;
using System;

namespace HKBuildUtils.Compiler;

internal static class Helper1
{
    public static unsafe void SetSkipVisibeCheck(RuntimeMethodHandle method)
    {
        MonoMethod1* m = (MonoMethod1*)method.Value;
        m->flag1 |= MonoMethod1.Flag1.skip_visibility;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct MonoMethod1
{
    public enum Flag0 : byte
    {
        inline_info = 1,
        inline_failure = 2,
        string_ctor = 1 << 7
    }
    public enum Flag1 : byte
    {
        save_lmf = 1,
        dynamic = 2,
        sre_method = 4,
        is_generic = 8,
        is_inflated = 16,
        skip_visibility = 32,
        verification_success = 64
    }
    public ushort flags; //method flags
    public ushort iflags; //method implementation flags
    public uint token;
    public void* klass;
    public void* signature;
    public byte* name;
    public Flag0 flag0;
    public Flag1 flag1;
    public short slot;
}