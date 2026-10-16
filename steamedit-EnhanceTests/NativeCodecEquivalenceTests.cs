#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

/// <summary>
/// 证明 Rust（原生）编解码器与托管实现完全等价：输出字节一致、解码结果一致。
/// 原生库不可用时整体跳过（回退托管），不阻塞无 Rust 工具链的环境。
/// </summary>
[TestClass]
[DoNotParallelize]
public class NativeCodecEquivalenceTests
{
    [TestInitialize]
    public void RequireNative()
    {
        if (!steamedit_Enhance.Data.Native.SteameditNative.Available)
            Assert.Inconclusive("原生 steamedit_core 库不可用，跳过等价性测试。");
        PropertyTableIO.UseNative = true;
    }

    [TestCleanup]
    public void Restore()
    {
        PropertyTableIO.UseNative = true;
    }

    private static PropertyTable BuildRichTable()
    {
        var root = new PropertyTable();
        root.AddPropertyValue("name", PropertyType.String, "游戏名称 / Game \"Name\"\\path");
        root.AddPropertyValue("empty", PropertyType.String, "");
        root.AddPropertyValue("unicode", PropertyType.WString, "日本語のタイトル \u00e9\u00e8\u00ea\ud83c\udfae");
        root.AddPropertyValue("appid", PropertyType.Int32, -123456);
        root.AddPropertyValue("pi", PropertyType.Float, 3.14159f);
        root.AddPropertyValue("zero", PropertyType.Float, 0f);
        root.AddPropertyValue("bignum", PropertyType.Uint64, 0xDEADBEEFCAFEBABEUL);
        root.AddPropertyValue("color", PropertyType.Color, Color.FromArgb(17, 34, 51));

        var common = new PropertyTable();
        common.AddPropertyValue("type", PropertyType.String, "game");
        var nested = new PropertyTable();
        nested.AddPropertyValue("english", PropertyType.Int32, 1);
        nested.AddPropertyValue("schinese", PropertyType.Int32, 0);
        common.AddPropertyValue("supported_languages", PropertyType.Table, nested);
        root.AddPropertyValue("common", PropertyType.Table, common);

        var deeper = new PropertyTable();
        deeper.AddPropertyValue("list", PropertyType.Table, new PropertyTable());
        root.AddPropertyValue("level1", PropertyType.Table, deeper);
        return root;
    }

    private static byte[] Encode(bool native, PropertyTable table)
    {
        PropertyTableIO.UseNative = native;
        return PropertyTableIO.WritePropertyTable_0x00000000(table);
    }

    private static PropertyTable Decode(bool native, byte[] data)
    {
        PropertyTableIO.UseNative = native;
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        return PropertyTableIO.ReadPropertyTable_0x00000000(br);
    }

    [TestMethod]
    public void Encode_0x00000000_NativeMatchesManaged()
    {
        var table = BuildRichTable();
        byte[] native = Encode(true, table);
        byte[] managed = Encode(false, table);
        CollectionAssert.AreEqual(managed, native, "原生与托管编码结果应逐字节一致。");
    }

    [TestMethod]
    public void Decode_0x00000000_NativeMatchesManaged()
    {
        var table = BuildRichTable();
        byte[] data = Encode(false, table);
        PropertyTable native = Decode(true, data);
        PropertyTable managed = Decode(false, data);
        Assert.IsTrue(managed.Equals(native), "原生与托管解码结果应相等。");
    }

    [TestMethod]
    public void RoundTrip_0x00000000_PreservesValues()
    {
        var table = BuildRichTable();
        byte[] data = Encode(true, table);
        PropertyTable decoded = Decode(true, data);
        Assert.IsTrue(table.Equals(decoded));
    }

    [TestMethod]
    public void Decode_0x07564429_NativeMatchesManaged()
    {
        var table = BuildRichTable();
        PropertyTableIO.UseNative = false;
        var stringTable = new List<string>();
        var lookup = new Dictionary<string, uint>();
        byte[] data = PropertyTableIO.WritePropertyTable_0x07564429(table, stringTable, lookup);

        PropertyTable native = Decode29(true, data, stringTable);
        PropertyTable managed = Decode29(false, data, stringTable);
        Assert.IsTrue(managed.Equals(native));
    }

    private static PropertyTable Decode29(bool native, byte[] data, IList<string> stringTable)
    {
        PropertyTableIO.UseNative = native;
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        return PropertyTableIO.ReadPropertyTable_0x07564429(br, stringTable);
    }
}
