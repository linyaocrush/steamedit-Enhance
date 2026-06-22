#nullable enable
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamEdit.Data;

namespace SteamEditTests;

[TestClass]
public class PropertyTableIOTests
{
    private PropertyTable CreateSampleTable()
    {
        var table = new PropertyTable();
        table.AddPropertyValue("str", PropertyType.String, "Hello");
        table.AddPropertyValue("int", PropertyType.Int32, 42);
        table.AddPropertyValue("uint", PropertyType.Uint64, (ulong)999);
        table.AddPropertyValue("flt", PropertyType.Float, 3.14f);
        table.AddPropertyValue("wstr", PropertyType.WString, "Wide");
        table.AddPropertyValue("clr", PropertyType.Color, System.Drawing.Color.FromArgb(255, 128, 64));
        var nested = new PropertyTable();
        nested.AddPropertyValue("inner", PropertyType.String, "value");
        table.AddPropertyValue("table", PropertyType.Table, nested);
        return table;
    }

    [TestMethod]
    public void Roundtrip_0x00000000_AllTypes()
    {
        var original = CreateSampleTable();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x00000000(writer, original);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x00000000(reader);
        Assert.AreEqual("Hello", loaded.GetPropertyValue<string>("str"));
        Assert.AreEqual(42, loaded.GetPropertyValue<int>("int"));
        Assert.AreEqual((ulong)999, loaded.GetPropertyValue<ulong>("uint"));
        Assert.AreEqual(3.14f, loaded.GetPropertyValue<float>("flt"));
        Assert.AreEqual("Wide", loaded.GetPropertyValue<string>("wstr"));
        var nested = loaded.GetPropertyValue<PropertyTable>("table");
        Assert.IsNotNull(nested);
        Assert.AreEqual("value", nested.GetPropertyValue<string>("inner"));
    }

    [TestMethod]
    public void Roundtrip_0x00000000_EmptyTable()
    {
        var original = new PropertyTable();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x00000000(writer, original);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x00000000(reader);
        Assert.AreEqual(0, loaded.Count);
    }

    [TestMethod]
    public void Roundtrip_0x07564429_AllTypes()
    {
        var original = CreateSampleTable();
        var stringTable = new List<string>();
        var stringLookup = new Dictionary<string, uint>();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x07564429(writer, original, stringTable, stringLookup);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x07564429(reader, stringTable);
        Assert.AreEqual("Hello", loaded.GetPropertyValue<string>("str"));
        Assert.AreEqual(42, loaded.GetPropertyValue<int>("int"));
        var nested = loaded.GetPropertyValue<PropertyTable>("table");
        Assert.IsNotNull(nested);
        Assert.AreEqual("value", nested.GetPropertyValue<string>("inner"));
    }

    [TestMethod]
    public void Roundtrip_0x07564429_EmptyTable()
    {
        var original = new PropertyTable();
        var stringTable = new List<string>();
        var stringLookup = new Dictionary<string, uint>();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x07564429(writer, original, stringTable, stringLookup);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x07564429(reader, stringTable);
        Assert.AreEqual(0, loaded.Count);
    }

    [TestMethod]
    public void Roundtrip_Mods_AllTypes()
    {
        var original = CreateSampleTable();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x00000000(writer, original);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x00000000(reader);
        Assert.AreEqual("Hello", loaded.GetPropertyValue<string>("str"));
        Assert.AreEqual(42, loaded.GetPropertyValue<int>("int"));
    }

    [TestMethod]
    public void Roundtrip_Mods_EmptyTable()
    {
        var original = new PropertyTable();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x00000000(writer, original);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x00000000(reader);
        Assert.AreEqual(0, loaded.Count);
    }

    [TestMethod]
    public void Roundtrip_0x07564429_LargeStringTable()
    {
        var original = new PropertyTable();
        for (int i = 0; i < 100; i++)
            original.AddPropertyValue($"prop{i}", PropertyType.String, $"value{i}");

        var stringTable = new List<string>();
        var stringLookup = new Dictionary<string, uint>();
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x07564429(writer, original, stringTable, stringLookup);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x07564429(reader, stringTable);
        for (int i = 0; i < 100; i++)
            Assert.AreEqual($"value{i}", loaded.GetPropertyValue<string>($"prop{i}"));
    }

    [TestMethod]
    public void WritePropertyTable_PreservesPropertyOrder()
    {
        var original = new PropertyTable();
        original.AddPropertyValue("z_last", PropertyType.String, "last");
        original.AddPropertyValue("a_first", PropertyType.String, "first");
        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x00000000(writer, original);
        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x00000000(reader);
        var names = new List<string>(loaded.PropertyNames);
        Assert.AreEqual("z_last", names[0]);
        Assert.AreEqual("a_first", names[1]);
    }
}
