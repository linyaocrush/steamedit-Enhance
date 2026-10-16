#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.Data.Native;

namespace steamedit_EnhanceTests;

/// <summary>
/// 编解码器微基准：直接在“大属性表”上压测原生（Rust）内核的编解码吞吐。
/// </summary>
[TestClass]
[TestCategory("Perf")]
[DoNotParallelize]
public class CodecPerfProbe
{
    private static PropertyTable BuildTable(int entries)
    {
        var root = new PropertyTable();
        for (int i = 0; i < entries; i++)
        {
            var entry = new PropertyTable();
            var common = new PropertyTable();
            common.AddPropertyValue("name", PropertyType.String, $"Game Title Number {i}");
            common.AddPropertyValue("type", PropertyType.String, "game");
            common.AddPropertyValue("installdir", PropertyType.String, $"GameDir{i}");
            var langs = new PropertyTable();
            langs.AddPropertyValue("english", PropertyType.Int32, 1);
            langs.AddPropertyValue("schinese", PropertyType.Int32, i % 2);
            common.AddPropertyValue("supported_languages", PropertyType.Table, langs);
            entry.AddPropertyValue("common", PropertyType.Table, common);
            entry.AddPropertyValue("appid", PropertyType.Int32, i);
            entry.AddPropertyValue("hash", PropertyType.String, $"hash-{i:x8}-{i * 2654435761u:x8}");
            root.AddPropertyValue($"app{i}", PropertyType.Table, entry);
        }
        return root;
    }

    private static long Time(Action action, int iterations)
    {
        action(); // warm up
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++) action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [TestMethod]
    public void CodecThroughput()
    {
        var table = BuildTable(2000);
        const int iterations = 30;

        byte[] encoded = Array.Empty<byte>();
        long encodeMs = Time(() => encoded = PropertyTableIO.WritePropertyTable_0x00000000(table), iterations);
        long decodeMs = Time(() =>
        {
            using var ms = new MemoryStream(encoded);
            using var br = new BinaryReader(ms);
            PropertyTableIO.ReadPropertyTable_0x00000000(br);
        }, iterations);
        Console.WriteLine($"0x00000000 encoded={encoded.Length} bytes  encode={encodeMs} ms  decode={decodeMs} ms  ({iterations} iters)");

        var table29 = BuildTable(2000);
        using var strtab = NativeStringTable.CreateEmpty();
        byte[] encoded29 = PropertyTableIO.WritePropertyTable_0x07564429(table29, strtab);
        var strings = strtab.ToList();
        long decode29Ms = Time(() =>
        {
            using var ms = new MemoryStream(encoded29);
            using var br = new BinaryReader(ms);
            PropertyTableIO.ReadPropertyTable_0x07564429(br, strings);
        }, iterations);
        Console.WriteLine($"0x07564429 encoded={encoded29.Length} bytes  stringTable={strings.Count}  decode={decode29Ms} ms  ({iterations} iters)");
    }
}
