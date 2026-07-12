#nullable enable
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class PropertyTableTests
{
    [TestMethod]
    public void AddPropertyValue_CanReadBack()
    {
        var table = new PropertyTable();
        table.AddPropertyValue("name", PropertyType.String, "Hello");
        Assert.AreEqual("Hello", table.GetPropertyValue<string>("name"));
    }

    [TestMethod]
    public void SetPropertyValue_OverwritesExisting()
    {
        var table = new PropertyTable();
        table.AddPropertyValue("name", PropertyType.String, "Old");
        table.SetPropertyValue("name", PropertyType.String, "New");
        Assert.AreEqual("New", table.GetPropertyValue<string>("name"));
    }

    [TestMethod]
    public void SetPropertyValue_CreatesNew()
    {
        var table = new PropertyTable();
        table.SetPropertyValue("name", PropertyType.String, "Created");
        Assert.AreEqual("Created", table.GetPropertyValue<string>("name"));
        Assert.AreEqual(1, table.Count);
    }

    [TestMethod]
    public void RemoveProperty_RemovesIt()
    {
        var table = new PropertyTable();
        table.AddPropertyValue("name", PropertyType.String, "Hello");
        table.RemoveProperty("name");
        Assert.IsFalse(table.HasProperty("name"));
        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void RemoveProperty_NestedPath_CleansEmptyParents()
    {
        var table = new PropertyTable();
        var inner = new PropertyTable();
        inner.AddPropertyValue("name", PropertyType.String, "Hello");
        table.AddPropertyValue("common", PropertyType.Table, inner);
        table.RemoveProperty("common", "name");
        Assert.IsFalse(table.HasProperty("common"));
    }

    [TestMethod]
    public void HasProperty_Exists_ReturnsTrue()
    {
        var table = new PropertyTable();
        table.AddPropertyValue("name", PropertyType.String, "Hello");
        Assert.IsTrue(table.HasProperty("name"));
    }

    [TestMethod]
    public void HasProperty_NotExists_ReturnsFalse()
    {
        var table = new PropertyTable();
        Assert.IsFalse(table.HasProperty("name"));
    }

    [TestMethod]
    public void HasProperty_NestedPath()
    {
        var table = new PropertyTable();
        var inner = new PropertyTable();
        inner.AddPropertyValue("name", PropertyType.String, "Hello");
        table.AddPropertyValue("common", PropertyType.Table, inner);
        Assert.IsTrue(table.HasProperty("common", "name"));
        Assert.IsFalse(table.HasProperty("common", "missing"));
    }

    [TestMethod]
    public void GetPropertyValue_ReturnsCorrectType()
    {
        var table = new PropertyTable();
        table.AddPropertyValue("str", PropertyType.String, "text");
        table.AddPropertyValue("int", PropertyType.Int32, 42);
        table.AddPropertyValue("uint", PropertyType.Uint64, (ulong)123);
        table.AddPropertyValue("flt", PropertyType.Float, 3.14f);

        Assert.AreEqual("text", table.GetPropertyValue<string>("str"));
        Assert.AreEqual(42, table.GetPropertyValue<int>("int"));
        Assert.AreEqual((ulong)123, table.GetPropertyValue<ulong>("uint"));
        Assert.AreEqual(3.14f, table.GetPropertyValue<float>("flt"));
    }

    [TestMethod]
    public void GetPropertyValue_NotExists_ReturnsDefault()
    {
        var table = new PropertyTable();
        Assert.IsNull(table.GetPropertyValue<string>("missing"));
        Assert.AreEqual(0, table.GetPropertyValue<int>("missing"));
    }

    [TestMethod]
    public void NestedPropertyTable_ReadWrite()
    {
        var table = new PropertyTable();
        var inner = new PropertyTable();
        inner.AddPropertyValue("name", PropertyType.String, "Inner");
        inner.AddPropertyValue("value", PropertyType.Int32, 99);
        table.AddPropertyValue("parent", PropertyType.Table, inner);

        var readBack = table.GetPropertyValue<PropertyTable>("parent");
        Assert.IsNotNull(readBack);
        Assert.AreEqual("Inner", readBack.GetPropertyValue<string>("name"));
        Assert.AreEqual(99, readBack.GetPropertyValue<int>("value"));
    }

    [TestMethod]
    public void CopyConstructor_DeepCopy()
    {
        var original = new PropertyTable();
        original.AddPropertyValue("name", PropertyType.String, "Original");
        var inner = new PropertyTable();
        inner.AddPropertyValue("key", PropertyType.String, "val");
        original.AddPropertyValue("nested", PropertyType.Table, inner);

        var copy = new PropertyTable(original);
        Assert.AreEqual("Original", copy.GetPropertyValue<string>("name"));

        copy.SetPropertyValue("name", PropertyType.String, "Modified");
        Assert.AreEqual("Original", original.GetPropertyValue<string>("name"));
    }

    [TestMethod]
    public void Equals_SameContent_ReturnsTrue()
    {
        var a = new PropertyTable();
        a.AddPropertyValue("name", PropertyType.String, "Hello");
        a.AddPropertyValue("id", PropertyType.Int32, 42);

        var b = new PropertyTable();
        b.AddPropertyValue("name", PropertyType.String, "Hello");
        b.AddPropertyValue("id", PropertyType.Int32, 42);

        Assert.IsTrue(a.Equals(b));
    }

    [TestMethod]
    public void Equals_DifferentContent_ReturnsFalse()
    {
        var a = new PropertyTable();
        a.AddPropertyValue("name", PropertyType.String, "Hello");

        var b = new PropertyTable();
        b.AddPropertyValue("name", PropertyType.String, "World");

        Assert.IsFalse(a.Equals(b));
    }

    [TestMethod]
    public void ModsRoundtrip_AllPropertyTypes()
    {
        var original = new PropertyTable();
        original.AddPropertyValue("str", PropertyType.String, "Hello World");
        original.AddPropertyValue("int", PropertyType.Int32, -42);
        original.AddPropertyValue("uint", PropertyType.Uint64, (ulong)999);
        original.AddPropertyValue("flt", PropertyType.Float, 2.718f);
        original.AddPropertyValue("wstr", PropertyType.WString, "WideString");

        var nested = new PropertyTable();
        nested.AddPropertyValue("inner", PropertyType.String, "value");
        original.AddPropertyValue("table", PropertyType.Table, nested);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            PropertyTableIO.WritePropertyTable_0x00000000(writer, original);
        }

        ms.Position = 0;
        using var reader = new BinaryReader(ms);
        var loaded = PropertyTableIO.ReadPropertyTable_0x00000000(reader);

        Assert.AreEqual("Hello World", loaded.GetPropertyValue<string>("str"));
        Assert.AreEqual(-42, loaded.GetPropertyValue<int>("int"));
        Assert.AreEqual((ulong)999, loaded.GetPropertyValue<ulong>("uint"));
        Assert.AreEqual(2.718f, loaded.GetPropertyValue<float>("flt"));
        Assert.AreEqual("WideString", loaded.GetPropertyValue<string>("wstr"));
        var loadedNested = loaded.GetPropertyValue<PropertyTable>("table");
        Assert.IsNotNull(loadedNested);
        Assert.AreEqual("value", loadedNested.GetPropertyValue<string>("inner"));
    }
}
