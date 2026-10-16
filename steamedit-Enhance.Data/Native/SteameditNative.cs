#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace steamedit_Enhance.Data.Native;

/// <summary>
/// P/Invoke wrapper over the native <c>steamedit_core</c> Rust library, which implements
/// the hot <c>appinfo.vdf</c> property-table codec.
/// </summary>
/// <remarks>
/// The native kernel is <b>mandatory</b>: this build ships no managed fallback. Call
/// <see cref="EnsureLoaded"/> during static initialization to fail fast with a clear,
/// actionable message if the library cannot be loaded.
/// </remarks>
internal static unsafe class SteameditNative
{
    private const string Dll = "steamedit_core";

    /// <summary>
    /// Verifies the native kernel can be loaded and an expected export resolves.
    /// Throws an actionable exception otherwise — there is no managed fallback.
    /// </summary>
    public static void EnsureLoaded()
    {
        try
        {
            if (!NativeLibrary.TryLoad(Dll, out IntPtr handle) || handle == IntPtr.Zero)
                throw new DllNotFoundException($"无法加载 {Dll}.dll");
            // Resolve a known entry point to guard against a stale/foreign DLL of the same name.
            _ = ste_last_error();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            throw new InvalidOperationException(
                $"原生内核 \"{Dll}.dll\" 不可用，且本版本不再提供托管回退，无法继续。" +
                $"请确认该 DLL 与程序位于同一目录。" +
                $"手动构建：cargo build --release --manifest-path native/steamedit-core/Cargo.toml",
                ex);
        }
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_vdf_to_flat(byte* src, nuint srcLen, out nuint outLen, out nuint outUsed);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_vdf_to_flat_07564429(
        byte* src, nuint srcLen, IntPtr strtab, out nuint outLen, out nuint outUsed);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_vdf_to_flat_sparse(
        byte* src, nuint srcLen, uint format29, IntPtr strtab, IntPtr pathset,
        out nuint outLen, out nuint outUsed);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ste_pathset_new(byte* paths, nuint len);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_pathset_free(IntPtr pathset);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_flat_to_vdf(byte* flat, nuint flatLen, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_flat_to_vdf_07564429(byte* flat, nuint flatLen, IntPtr strtab, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ste_strtab_new();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ste_strtab_parse(byte* dump, nuint dumpLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_strtab_free(IntPtr strtab);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_strtab_dump(IntPtr strtab, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_free(byte* ptr, nuint len);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_last_error();

    /// <summary>The message recorded by the most recent native failure (empty if none).</summary>
    public static string LastError
    {
        get
        {
            byte* p = ste_last_error();
            return p == null ? string.Empty : Marshal.PtrToStringUTF8((IntPtr)p) ?? string.Empty;
        }
    }

    public static byte[]? VdfToFlat(ReadOnlySpan<byte> src, out int consumed)
    {
        consumed = 0;
        fixed (byte* p = src)
        {
            byte* outPtr = ste_vdf_to_flat(p, (nuint)src.Length, out nuint len, out nuint used);
            if (outPtr == null) return null;
            consumed = (int)used;
            return CopyAndFree(outPtr, len);
        }
    }

    public static byte[]? VdfToFlat07564429(ReadOnlySpan<byte> src, IntPtr strtab, out int consumed)
    {
        consumed = 0;
        fixed (byte* p = src)
        {
            byte* outPtr = ste_vdf_to_flat_07564429(p, (nuint)src.Length, strtab, out nuint len, out nuint used);
            if (outPtr == null) return null;
            consumed = (int)used;
            return CopyAndFree(outPtr, len);
        }
    }

    public static byte[]? FlatToVdf(ReadOnlySpan<byte> flat)
    {
        fixed (byte* p = flat)
        {
            byte* outPtr = ste_flat_to_vdf(p, (nuint)flat.Length, out nuint len);
            return outPtr == null ? null : CopyAndFree(outPtr, len);
        }
    }

    public static byte[]? FlatToVdf07564429(ReadOnlySpan<byte> flat, IntPtr strtab)
    {
        fixed (byte* p = flat)
        {
            byte* outPtr = ste_flat_to_vdf_07564429(p, (nuint)flat.Length, strtab, out nuint len);
            return outPtr == null ? null : CopyAndFree(outPtr, len);
        }
    }

    public static byte[]? VdfToFlatSparse(
        ReadOnlySpan<byte> src, bool format29, IntPtr strtab, IntPtr pathset, out int consumed)
    {
        consumed = 0;
        fixed (byte* p = src)
        {
            byte* outPtr = ste_vdf_to_flat_sparse(
                p, (nuint)src.Length, format29 ? 1u : 0u, strtab, pathset, out nuint len, out nuint used);
            if (outPtr == null) return null;
            consumed = (int)used;
            return CopyAndFree(outPtr, len);
        }
    }

    public static IntPtr StrTabNew() => ste_strtab_new();
    public static IntPtr StrTabParse(ReadOnlySpan<byte> dump)
    {
        fixed (byte* p = dump)
        {
            return ste_strtab_parse(p, (nuint)dump.Length);
        }
    }

    public static void StrTabFree(IntPtr strtab)
    {
        if (strtab != IntPtr.Zero) ste_strtab_free(strtab);
    }

    public static byte[]? StrTabDump(IntPtr strtab)
    {
        if (strtab == IntPtr.Zero) return null;
        byte* outPtr = ste_strtab_dump(strtab, out nuint len);
        return outPtr == null ? null : CopyAndFree(outPtr, len);
    }

    public static IntPtr PathSetNew(ReadOnlySpan<byte> paths)
    {
        fixed (byte* p = paths)
        {
            return ste_pathset_new(p, (nuint)paths.Length);
        }
    }

    public static void PathSetFree(IntPtr pathset)
    {
        if (pathset != IntPtr.Zero) ste_pathset_free(pathset);
    }

    private static byte[] CopyAndFree(byte* ptr, nuint len)
    {
        try
        {
            var result = new byte[(int)len];
            if (len > 0) Marshal.Copy((IntPtr)ptr, result, 0, (int)len);
            return result;
        }
        finally
        {
            ste_free(ptr, len);
        }
    }
}

/// <summary>
/// RAII handle around a native string table. Used both while reading a
/// <c>0x07564429</c> appinfo file (built once from the file's string table and reused
/// for every entry) and while writing one (built up across all entries).
/// </summary>
internal sealed class NativeStringTable : IDisposable
{
    private IntPtr _handle;

    private NativeStringTable(IntPtr handle) => _handle = handle;

    public static NativeStringTable CreateEmpty() => new(SteameditNative.StrTabNew());

    public static NativeStringTable FromDump(byte[] dump) => new(SteameditNative.StrTabParse(dump));

    public IntPtr Handle => _handle;

    public byte[]? Dump() => SteameditNative.StrTabDump(_handle);

    /// <summary>Exports the table as an index-ordered string list (used for index-resolving decodes/tests).</summary>
    public List<string> ToList()
    {
        var list = new List<string>();
        byte[]? dump = Dump();
        if (dump == null) return list;
        int pos = 0;
        uint count = BitConverter.ToUInt32(dump, pos);
        pos += 4;
        for (uint i = 0; i < count; i++)
        {
            uint len = BitConverter.ToUInt32(dump, pos);
            pos += 4;
            list.Add(System.Text.Encoding.UTF8.GetString(dump, pos, (int)len));
            pos += (int)len;
        }
        return list;
    }

    public void Dispose()
    {
        SteameditNative.StrTabFree(_handle);
        _handle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    ~NativeStringTable()
    {
        SteameditNative.StrTabFree(_handle);
        _handle = IntPtr.Zero;
    }
}
