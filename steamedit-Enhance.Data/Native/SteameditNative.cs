#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

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
    private static extern byte* ste_mods_to_flat(byte* src, nuint srcLen, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_mods_from_flat(byte* flat, nuint flatLen, out nuint outLen);

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

    // ── network kernel ──────────────────────────────────────────────────────

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_net_cover_download(int appId, byte* destUtf8);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_net_official_name(int appId, byte* localeUtf8, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_net_ai_chat(
        byte* system, byte* user, byte* url, byte* apiKey, byte* model,
        int maxTokens, float temperature, uint timeoutSeconds, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_net_ai_models(byte* url, byte* apiKey, uint timeoutSeconds, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_net_ai_error(out int outStatus);

    // ── steam cover resolver kernel ─────────────────────────────────────────

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ste_cover_resolver_new(byte* steamDir);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_cover_resolver_portrait(IntPtr h, int appId, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_cover_resolver_wide(IntPtr h, int appId, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_cover_resolver_free(IntPtr h);

    public static IntPtr CoverResolverNew(string steamDir)
    {
        fixed (byte* p = Utf8Z(steamDir)) return ste_cover_resolver_new(p);
    }

    public static string? CoverResolverPortrait(IntPtr h, int appId)
        => ReadUtf8OrNull(h, appId, ste_cover_resolver_portrait);

    public static string? CoverResolverWide(IntPtr h, int appId)
        => ReadUtf8OrNull(h, appId, ste_cover_resolver_wide);

    private delegate byte* CoverLookup(IntPtr h, int appId, out nuint outLen);

    private static string? ReadUtf8OrNull(IntPtr h, int appId, CoverLookup lookup)
    {
        if (h == IntPtr.Zero) return null;
        byte* outPtr = lookup(h, appId, out nuint len);
        if (outPtr == null || len == 0) return null;
        try { return Marshal.PtrToStringUTF8((IntPtr)outPtr, (int)len); }
        finally { ste_free(outPtr, len); }
    }

    public static void CoverResolverFree(IntPtr h) { if (h != IntPtr.Zero) ste_cover_resolver_free(h); }

    // ── localized-name cache kernel ─────────────────────────────────────────

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ste_namecache_new(byte* path);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_namecache_get(IntPtr h, int appId, byte* locale, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_namecache_contains(IntPtr h, int appId, byte* locale);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_namecache_set(IntPtr h, int appId, byte* locale, byte* name);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong ste_namecache_total(IntPtr h);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_namecache_save(IntPtr h);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_namecache_clear(IntPtr h);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_namecache_free(IntPtr h);

    public static IntPtr NameCacheNew(string path)
    {
        fixed (byte* p = Utf8Z(path)) return ste_namecache_new(p);
    }

    public static string? NameCacheGet(IntPtr h, int appId, string locale)
    {
        if (h == IntPtr.Zero) return null;
        fixed (byte* p = Utf8Z(locale))
        {
            byte* outPtr = ste_namecache_get(h, appId, p, out nuint len);
            if (outPtr == null || len == 0) return null;
            try { return Marshal.PtrToStringUTF8((IntPtr)outPtr, (int)len); }
            finally { ste_free(outPtr, len); }
        }
    }

    public static bool NameCacheContains(IntPtr h, int appId, string locale)
    {
        if (h == IntPtr.Zero) return false;
        fixed (byte* p = Utf8Z(locale)) return ste_namecache_contains(h, appId, p) != 0;
    }

    public static bool NameCacheSet(IntPtr h, int appId, string locale, string name)
    {
        if (h == IntPtr.Zero) return false;
        fixed (byte* pl = Utf8Z(locale))
        fixed (byte* pn = Utf8Z(name))
        {
            return ste_namecache_set(h, appId, pl, pn) != 0;
        }
    }

    public static ulong NameCacheTotal(IntPtr h) => h == IntPtr.Zero ? 0UL : ste_namecache_total(h);
    public static void NameCacheSave(IntPtr h) { if (h != IntPtr.Zero) ste_namecache_save(h); }
    public static void NameCacheClear(IntPtr h) { if (h != IntPtr.Zero) ste_namecache_clear(h); }
    public static void NameCacheFree(IntPtr h) { if (h != IntPtr.Zero) ste_namecache_free(h); }

    // ── translation glossary kernel ─────────────────────────────────────────

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ste_glossary_new();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_glossary_initialize(IntPtr h, byte* dataDir);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_glossary_has_app(IntPtr h, int appId);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_glossary_update(IntPtr h, int appId, byte* original, byte* translated);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong ste_glossary_count(IntPtr h);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_glossary_find(IntPtr h, byte* original, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_glossary_prompt(IntPtr h, nuint maxEntries, out nuint outLen);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_glossary_flush(IntPtr h);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_glossary_free(IntPtr h);

    public static IntPtr GlossaryNew() => ste_glossary_new();

    public static bool GlossaryInitialize(IntPtr h, string dataDirectory)
    {
        if (h == IntPtr.Zero) return false;
        fixed (byte* p = Utf8Z(dataDirectory)) return ste_glossary_initialize(h, p) != 0;
    }

    public static bool GlossaryHasApp(IntPtr h, int appId) => h != IntPtr.Zero && ste_glossary_has_app(h, appId) != 0;

    public static bool GlossaryUpdate(IntPtr h, int appId, string original, string translated)
    {
        if (h == IntPtr.Zero) return false;
        fixed (byte* po = Utf8Z(original))
        fixed (byte* pt = Utf8Z(translated))
        {
            return ste_glossary_update(h, appId, po, pt) != 0;
        }
    }

    public static ulong GlossaryCount(IntPtr h) => h == IntPtr.Zero ? 0UL : ste_glossary_count(h);

    public static string? GlossaryFind(IntPtr h, string original)
    {
        if (h == IntPtr.Zero) return null;
        fixed (byte* p = Utf8Z(original))
        {
            byte* outPtr = ste_glossary_find(h, p, out nuint len);
            if (outPtr == null || len == 0) return null;
            try { return Marshal.PtrToStringUTF8((IntPtr)outPtr, (int)len); }
            finally { ste_free(outPtr, len); }
        }
    }

    public static string? GlossaryPrompt(IntPtr h, int maxEntries)
    {
        if (h == IntPtr.Zero) return null;
        byte* outPtr = ste_glossary_prompt(h, (nuint)Math.Max(0, maxEntries), out nuint len);
        if (outPtr == null || len == 0) return null;
        try { return Marshal.PtrToStringUTF8((IntPtr)outPtr, (int)len); }
        finally { ste_free(outPtr, len); }
    }

    public static void GlossaryFlush(IntPtr h) { if (h != IntPtr.Zero) ste_glossary_flush(h); }
    public static void GlossaryFree(IntPtr h) { if (h != IntPtr.Zero) ste_glossary_free(h); }

    // ── logging kernel ──────────────────────────────────────────────────────

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_log_init(byte* path);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_log_reset(byte* path);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_log_configure(int debug, int info, int warning, int error);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ste_log_write(uint level, byte* ts, byte* line);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_log_flush();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ste_log_close();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_log_recent(nuint count, out nuint outLen);

    /// <summary>Opens the log file for append and activates the native logger.</summary>
    public static bool LogInit(string path)
    {
        fixed (byte* p = Utf8Z(path)) return ste_log_init(p) != 0;
    }

    /// <summary>Reopens the log file with truncation and enables all levels (test helper).</summary>
    public static bool LogReset(string path)
    {
        fixed (byte* p = Utf8Z(path)) return ste_log_reset(p) != 0;
    }

    public static void LogConfigure(bool debug, bool info, bool warning, bool error)
        => ste_log_configure(debug ? 1 : 0, info ? 1 : 0, warning ? 1 : 0, error ? 1 : 0);

    public static bool LogWrite(uint level, string timestamp, string line)
    {
        fixed (byte* pts = Utf8Z(timestamp))
        fixed (byte* pline = Utf8Z(line))
        {
            return ste_log_write(level, pts, pline) != 0;
        }
    }

    public static void LogFlush() => ste_log_flush();
    public static void LogClose() => ste_log_close();

    public static string[] LogRecent(int count)
    {
        byte* outPtr = ste_log_recent((nuint)Math.Max(0, count), out nuint len);
        if (outPtr == null || len == 0) return Array.Empty<string>();
        try
        {
            string joined = Marshal.PtrToStringUTF8((IntPtr)outPtr, (int)len) ?? "";
            return joined.Length == 0 ? Array.Empty<string>() : joined.Split('\n');
        }
        finally
        {
            ste_free(outPtr, len);
        }
    }

    // ── image kernel ────────────────────────────────────────────────────────

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern byte* ste_image_resize_bgra(
        byte* src, nuint srcLen, uint sw, uint sh, uint dw, uint dh, out nuint outLen);

    /// <summary>Resamples a BGRA8 buffer from (sw,sh) to (dw,dh); null on failure.</summary>
    public static byte[]? ResizeBgra(byte[] src, int sw, int sh, int dw, int dh)
    {
        fixed (byte* p = src)
        {
            byte* outPtr = ste_image_resize_bgra(
                p, (nuint)src.Length, (uint)sw, (uint)sh, (uint)dw, (uint)dh, out nuint len);
            return outPtr == null ? null : CopyAndFree(outPtr, len);
        }
    }

    /// <summary>Kinds reported by <see cref="AiError"/>; mirrors the Rust `AI_KIND_*` constants.</summary>
    public const int AiKindNone = -1;
    public const int AiKindNetwork = 0;
    public const int AiKindApi = 1;
    public const int AiKindParse = 2;
    public const int AiKindCircuit = 3;

    private static byte[] Utf8Z(string value) => Encoding.UTF8.GetBytes(value + "\0");

    /// <summary>Downloads the portrait cover to <paramref name="destFile"/>; false on any failure.</summary>
    public static bool CoverDownload(int appId, string destFile)
    {
        fixed (byte* p = Utf8Z(destFile))
        {
            return ste_net_cover_download(appId, p) != 0;
        }
    }

    /// <summary>Fetches the localized official store name, or null when unavailable.</summary>
    public static string? OfficialName(int appId, string locale)
    {
        fixed (byte* p = Utf8Z(locale))
        {
            byte* outPtr = ste_net_official_name(appId, p, out nuint len);
            if (outPtr == null || len == 0) return null;
            try
            {
                return Marshal.PtrToStringUTF8((IntPtr)outPtr, (int)len);
            }
            finally
            {
                ste_free(outPtr, len);
            }
        }
    }

    /// <summary>Runs a chat completion; returns the assistant content or null on failure.</summary>
    public static string? AiChat(
        string system, string user, string url, string apiKey, string model,
        int maxTokens, float temperature, int timeoutSeconds)
    {
        fixed (byte* ps = Utf8Z(system))
        fixed (byte* pu = Utf8Z(user))
        fixed (byte* pUrl = Utf8Z(url))
        fixed (byte* pKey = Utf8Z(apiKey))
        fixed (byte* pModel = Utf8Z(model))
        {
            byte* outPtr = ste_net_ai_chat(
                ps, pu, pUrl, pKey, pModel, maxTokens, temperature,
                (uint)Math.Max(1, timeoutSeconds), out nuint len);
            if (outPtr == null) return null;
            try
            {
                return Marshal.PtrToStringUTF8((IntPtr)outPtr, (int)len);
            }
            finally
            {
                ste_free(outPtr, len);
            }
        }
    }

    /// <summary>Lists AI model ids; null on failure.</summary>
    public static string[]? AiModels(string url, string apiKey, int timeoutSeconds)
    {
        fixed (byte* pUrl = Utf8Z(url))
        fixed (byte* pKey = Utf8Z(apiKey))
        {
            byte* outPtr = ste_net_ai_models(pUrl, pKey, (uint)Math.Max(1, timeoutSeconds), out nuint len);
            if (outPtr == null) return null;
            try
            {
                string joined = len == 0 ? "" : Marshal.PtrToStringUTF8((IntPtr)outPtr, (int)len) ?? "";
                return joined.Length == 0 ? Array.Empty<string>() : joined.Split('\n');
            }
            finally
            {
                ste_free(outPtr, len);
            }
        }
    }

    /// <summary>Classification of the most recent AI failure: (kind, statusCode).</summary>
    public static (int Kind, int Status) AiError()
    {
        int kind = ste_net_ai_error(out int status);
        return (kind, status);
    }

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

    /// <summary>Decodes a <c>mods.dat</c> file into a flat mods container (null on failure).</summary>
    public static byte[]? ModsToFlat(ReadOnlySpan<byte> src)
    {
        fixed (byte* p = src)
        {
            byte* outPtr = ste_mods_to_flat(p, (nuint)src.Length, out nuint len);
            return outPtr == null ? null : CopyAndFree(outPtr, len);
        }
    }

    /// <summary>Encodes a flat mods container into <c>mods.dat</c> bytes (null on failure).</summary>
    public static byte[]? ModsFromFlat(ReadOnlySpan<byte> flat)
    {
        fixed (byte* p = flat)
        {
            byte* outPtr = ste_mods_from_flat(p, (nuint)flat.Length, out nuint len);
            return outPtr == null ? null : CopyAndFree(outPtr, len);
        }
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
