#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.Data.Native;

namespace steamedit_EnhanceTests;

/// <summary>
/// mods.dat 编解码微基准：对比旧版 v6（Changes 为二进制 VDF，需转码）与
/// 新版 v7（全长度前缀 + Changes 直接存扁平树，纯拷贝）的 Rust 读写吞吐。
/// </summary>
[TestClass]
[TestCategory("Perf")]
[DoNotParallelize]
public class ModsPerfProbe
{
    private const int Entries = 2000;
    private const int Iterations = 20;

    private static PropertyTable BuildChanges(int i)
    {
        var changes = new PropertyTable();
        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, $"Modified Game {i}");
        common.AddPropertyValue("type", PropertyType.String, "game");
        changes.AddPropertyValue("common", PropertyType.Table, common);
        var steamEdit = new PropertyTable();
        steamEdit.AddPropertyValue("is_hidden", PropertyType.Int32, i % 2);
        changes.AddPropertyValue("steam_edit", PropertyType.Table, steamEdit);
        return changes;
    }

    private static byte[] BuildOriginalData(int i)
    {
        var table = new PropertyTable();
        var appinfo = new PropertyTable();
        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, $"Original Game {i}");
        common.AddPropertyValue("type", PropertyType.String, "game");
        appinfo.AddPropertyValue("common", PropertyType.Table, common);
        var config = new PropertyTable();
        config.AddPropertyValue("installdir", PropertyType.String, $"Game{i}");
        appinfo.AddPropertyValue("config", PropertyType.Table, config);
        table.AddPropertyValue("appinfo", PropertyType.Table, appinfo);
        return PropertyTableIO.WritePropertyTable_0x00000000(table);
    }

    /// <summary>构造与 v7 文件同构的扁平容器（版本字段决定写出哪种格式）。</summary>
    private static byte[] BuildContainer(int version)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        w.Write((uint)version);
        w.Write((uint)Entries);
        for (int i = 0; i < Entries; i++)
        {
            w.Write(1000 + i);
            byte[] changes = FlatTableCodec.ToFlat(BuildChanges(i));
            w.Write((uint)changes.Length);
            w.Write(changes);
            w.Write(0x07564429u);
            w.Write((uint)i);
            byte[] data = BuildOriginalData(i);
            w.Write((uint)data.Length);
            w.Write(data);
        }
        w.Flush();
        return ms.ToArray();
    }

    private static long Time(Action action)
    {
        action();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++) action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [TestMethod]
    public void ModsCodecThroughput()
    {
        byte[] containerV6 = BuildContainer(6);
        byte[] containerV7 = BuildContainer(7);

        byte[] fileV6 = SteameditNative.ModsFromFlat(containerV6)!;
        byte[] fileV7 = SteameditNative.ModsFromFlat(containerV7)!;

        long writeV6 = Time(() => SteameditNative.ModsFromFlat(containerV6));
        long writeV7 = Time(() => SteameditNative.ModsFromFlat(containerV7));
        long readV6 = Time(() => SteameditNative.ModsToFlat(fileV6));
        long readV7 = Time(() => SteameditNative.ModsToFlat(fileV7));

        Console.WriteLine($"entries={Entries} iters={Iterations}");
        Console.WriteLine($"  v6 file={fileV6.Length} B   write(encode VDF)={writeV6} ms   read(decode VDF)={readV6} ms");
        Console.WriteLine($"  v7 file={fileV7.Length} B   write(copy)      ={writeV7} ms   read(copy)      ={readV7} ms");
    }
}
