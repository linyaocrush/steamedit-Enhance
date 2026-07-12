#nullable enable
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class ModifiedEntryTests
{
    [TestMethod]
    public void Constructor_WithId_SetsId()
    {
        var entry = new ModifiedEntry(12345);
        Assert.AreEqual(12345, entry.ID);
    }

    [TestMethod]
    public void Constructor_WithId_ChangesIsNull()
    {
        // The int-only constructor does not initialize Changes
        var entry = new ModifiedEntry(1);
        Assert.IsNull(entry.Changes);
    }

    [TestMethod]
    public void AddChange_SetsProperty()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();

        entry.AddChange(PropertyType.String, "New Name", AppInfoPaths.Name);

        Assert.IsTrue(entry.Changes.HasProperty("appinfo", "common", "name"));
        Assert.AreEqual("New Name", entry.Changes.GetPropertyValue<string>(null, AppInfoPaths.Name));
    }

    [TestMethod]
    public void AddChange_ReturnsTrue_WhenChanged()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();

        bool result = entry.AddChange(PropertyType.String, "Value", AppInfoPaths.Name);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void AddChange_ReturnsFalse_WhenSameValue()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();

        entry.AddChange(PropertyType.String, "Value", AppInfoPaths.Name);
        bool result = entry.AddChange(PropertyType.String, "Value", AppInfoPaths.Name);
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ApplyChanges_AppliesToTable()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();
        entry.AddChange(PropertyType.String, "Modified", AppInfoPaths.Name);

        // Build a target with the matching nested structure
        var target = new PropertyTable();
        var appinfo = new PropertyTable();
        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, "Original");
        appinfo.AddPropertyValue("common", PropertyType.Table, common);
        target.AddPropertyValue("appinfo", PropertyType.Table, appinfo);

        bool changed = entry.ApplyChanges(target);
        Assert.IsTrue(changed);
        Assert.AreEqual("Modified", target.GetPropertyValue<string>(null, AppInfoPaths.Name));
    }

    [TestMethod]
    public void ApplyChanges_CreatesNestedTables_WhenMissing()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();
        entry.AddChange(PropertyType.String, "Created", AppInfoPaths.Name);

        // Target has no nested structure at all
        var target = new PropertyTable();
        bool changed = entry.ApplyChanges(target);
        Assert.IsTrue(changed);
        Assert.AreEqual("Created", target.GetPropertyValue<string>(null, AppInfoPaths.Name));
    }

    [TestMethod]
    public void ApplyChanges_MultipleProperties()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();
        entry.AddChange(PropertyType.String, "Renamed", AppInfoPaths.Name);
        entry.AddChange(PropertyType.String, "custom_sort", AppInfoPaths.SortAs);

        var target = new PropertyTable();
        bool changed = entry.ApplyChanges(target);

        Assert.IsTrue(changed);
        Assert.AreEqual("Renamed", target.GetPropertyValue<string>(null, AppInfoPaths.Name));
        Assert.AreEqual("custom_sort", target.GetPropertyValue<string>(null, AppInfoPaths.SortAs));
    }

    [TestMethod]
    public void ApplyChanges_WhenChangesIsNull_ReturnsFalse()
    {
        var entry = new ModifiedEntry(1);
        // Changes is null by default with int constructor

        var target = new PropertyTable();
        bool changed = entry.ApplyChanges(target);
        Assert.IsFalse(changed);
    }

    [TestMethod]
    public void ApplyChanges_WhenChangesEmpty_ReturnsFalse()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();

        var target = new PropertyTable();
        bool changed = entry.ApplyChanges(target);
        Assert.IsFalse(changed);
    }

    [TestMethod]
    public void ApplyChanges_SameValue_ReturnsFalse()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();
        entry.AddChange(PropertyType.String, "Same", AppInfoPaths.Name);

        var target = new PropertyTable();
        var appinfo = new PropertyTable();
        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, "Same");
        appinfo.AddPropertyValue("common", PropertyType.Table, common);
        target.AddPropertyValue("appinfo", PropertyType.Table, appinfo);

        bool changed = entry.ApplyChanges(target);
        Assert.IsFalse(changed);
    }

    [TestMethod]
    public void OriginalData_Set_ClonesDefensively()
    {
        var entry = new ModifiedEntry(1);
        byte[] data = { 1, 2, 3 };
        entry.OriginalData = data;
        data[0] = 99;
        Assert.AreEqual(1, entry.OriginalData[0]);
    }

    [TestMethod]
    public void OriginalData_SetNull_ReturnsNull()
    {
        var entry = new ModifiedEntry(1);
        byte[] data = { 1, 2, 3 };
        entry.OriginalData = data;
        entry.OriginalData = null;
        Assert.IsNull(entry.OriginalData);
    }

    [TestMethod]
    public void GetOriginalName_ReturnsNull_WhenNoData()
    {
        var entry = new ModifiedEntry(1);
        Assert.IsNull(entry.GetOriginalName());
    }

    [TestMethod]
    public void GetOriginalName_ReadsFromBinaryData()
    {
        // Build a binary blob using PropertyTableIO.WritePropertyTable_0x00000000
        var table = new PropertyTable();
        var appinfo = new PropertyTable();
        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, "Test Game");
        appinfo.AddPropertyValue("common", PropertyType.Table, common);
        table.AddPropertyValue("appinfo", PropertyType.Table, appinfo);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            PropertyTableIO.WritePropertyTable_0x00000000(writer, table);
        }

        var entry = new ModifiedEntry(42);
        entry.OriginalData = ms.ToArray();

        Assert.AreEqual("Test Game", entry.GetOriginalName());
    }

    [TestMethod]
    public void AddChange_Int32Property()
    {
        var entry = new ModifiedEntry(1);
        entry.Changes = new PropertyTable();

        entry.AddChange(PropertyType.Int32, 42, AppInfoPaths.IsHidden);

        Assert.AreEqual(42, entry.Changes.GetPropertyValue<int>(0, AppInfoPaths.IsHidden));
    }

    [TestMethod]
    public void OriginalDataVersion_And_ChangeNumber_GetSet()
    {
        var entry = new ModifiedEntry(1);
        entry.OriginalDataVersion = 0x12345678;
        entry.OriginalDataChangeNumber = 999;

        Assert.AreEqual((uint)0x12345678, entry.OriginalDataVersion);
        Assert.AreEqual((uint)999, entry.OriginalDataChangeNumber);
    }
}
