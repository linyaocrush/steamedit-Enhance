#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

/// <summary>
/// 编解码器微基准：在“大属性表”上对比原生（Rust）与托管实现，
/// 排除条目级对象图构建带来的噪声。
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
    public void CodecNativeVsManaged()
    {
        var table = BuildTable(2000);
        const int iterations = 30;

        foreach (bool native in new[] { false, true })
        {
            PropertyTableIO.UseNative = native;
            bool active = PropertyTableIO.Native;

            byte[] encoded = Array.Empty<byte>();
            long encodeMs = Time(() => encoded = PropertyTableIO.WritePropertyTable_0x00000000(table), iterations);

            long decodeMs = Time(() =>
            {
                using var ms = new MemoryStream(encoded);
                using var br = new BinaryReader(ms);
                PropertyTableIO.ReadPropertyTable_0x00000000(br);
            }, iterations);

            Console.WriteLine($"0x00000000 UseNative={native} (active={active}) encoded={encoded.Length} bytes  " +
                              $"encode={encodeMs} ms  decode={decodeMs} ms  ({iterations} iters)");
        }

        // 0x07564429 (string-table format): the real-world encoding for modern appinfo.vdf.
        var stringTable = new System.Collections.Generic.List<string>();
        var stringLookup = new System.Collections.Generic.Dictionary<string, uint>();
        var table29 = BuildTable(2000);
        foreach (bool native in new[] { false, true })
        {
            PropertyTableIO.UseNative = native;
            bool active = PropertyTableIO.Native;

            stringTable.Clear();
            stringLookup.Clear();
            byte[] encoded = PropertyTableIO.WritePropertyTable_0x07564429(table29, stringTable, stringLookup);

            long decodeMs = Time(() =>
            {
                using var ms = new MemoryStream(encoded);
                using var br = new BinaryReader(ms);
                PropertyTableIO.ReadPropertyTable_0x07564429(br, stringTable);
            }, iterations);

            Console.WriteLine($"0x07564429 UseNative={native} (active={active}) encoded={encoded.Length} bytes  " +
                              $"stringTable={stringTable.Count}  decode={decodeMs} ms  ({iterations} iters)");
        }
        PropertyTableIO.UseNative = true;
    }
}
