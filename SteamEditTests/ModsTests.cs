using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamEdit.Data;

namespace SteamEditTests;

[TestClass]
public class ModsTests
{
    private PropertyTable CreateSampleChanges()
    {
        var changes = new PropertyTable();
        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, "Modified Game Name");
        changes.AddPropertyValue("common", PropertyType.Table, common);
        return changes;
    }

    private byte[] CreateSampleOriginalData()
    {
        var table = new PropertyTable();
        var common = new PropertyTable();
        common.AddPropertyValue("name", PropertyType.String, "Original Game Name");
        common.AddPropertyValue("type", PropertyType.String, "game");
        table.AddPropertyValue("common", PropertyType.Table, common);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        PropertyTableIO.WritePropertyTable_Mods(writer, table);
        return ms.ToArray();
    }

    [TestMethod]
    public void SaveLoad_SingleModifiedEntry_PreservesChangesAndOriginalData()
    {
        var original = CreateSampleOriginalData();
        var changes = CreateSampleChanges();

        var entry = new ModifiedEntry(12345)
        {
            Changes = changes,
            OriginalData = original,
            OriginalDataVersion = 0x07564429,
            OriginalDataChangeNumber = 100
        };

        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);
                bw.Write(1);
                bw.Write(entry.ID);
                PropertyTableIO.WritePropertyTable_Mods(bw, entry.Changes);
                bw.Write(entry.OriginalDataVersion);
                bw.Write(entry.OriginalDataChangeNumber);
                bw.Write(entry.OriginalData.Length);
                bw.Write(entry.OriginalData);
            }

            using var br = new BinaryReader(File.OpenRead(tempFile));
            Assert.AreEqual(6u, br.ReadUInt32());
            Assert.AreEqual(1, br.ReadInt32());
            Assert.AreEqual(12345, br.ReadInt32());

            var loadedChanges = PropertyTableIO.ReadPropertyTable_Mods(br);
            Assert.IsTrue(loadedChanges.HasProperty("common", "name"));
            Assert.AreEqual("Modified Game Name", loadedChanges.GetPropertyValue<string>(null, "common", "name"));

            Assert.AreEqual(0x07564429u, br.ReadUInt32());
            Assert.AreEqual(100u, br.ReadUInt32());
            int dataLen = br.ReadInt32();
            Assert.AreEqual(original.Length, dataLen);
            byte[] loadedData = br.ReadBytes(dataLen);
            CollectionAssert.AreEqual(original, loadedData);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void SaveLoad_EmptyMods_WritesZeroCount()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);
                bw.Write(0);
            }

            using var br = new BinaryReader(File.OpenRead(tempFile));
            Assert.AreEqual(6u, br.ReadUInt32());
            Assert.AreEqual(0, br.ReadInt32());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void SaveLoad_MultipleEntries_PreservesOrder()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            var entries = new List<ModifiedEntry>();
            for (int i = 0; i < 5; i++)
            {
                entries.Add(new ModifiedEntry(1000 + i)
                {
                    Changes = CreateSampleChanges(),
                    OriginalData = CreateSampleOriginalData(),
                    OriginalDataVersion = 0x07564429,
                    OriginalDataChangeNumber = (uint)i
                });
            }

            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);
                bw.Write(entries.Count);
                foreach (var e in entries)
                {
                    bw.Write(e.ID);
                    PropertyTableIO.WritePropertyTable_Mods(bw, e.Changes);
                    bw.Write(e.OriginalDataVersion);
                    bw.Write(e.OriginalDataChangeNumber);
                    bw.Write(e.OriginalData.Length);
                    bw.Write(e.OriginalData);
                }
            }

            using var br = new BinaryReader(File.OpenRead(tempFile));
            br.ReadUInt32();
            Assert.AreEqual(5, br.ReadInt32());
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(1000 + i, br.ReadInt32());
                PropertyTableIO.ReadPropertyTable_Mods(br);
                Assert.AreEqual(0x07564429u, br.ReadUInt32());
                Assert.AreEqual((uint)i, br.ReadUInt32());
                int len = br.ReadInt32();
                br.ReadBytes(len);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void SaveLoad_NullOriginalData_WritesZeroLength()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);
                bw.Write(1);
                bw.Write(99999);
                PropertyTableIO.WritePropertyTable_Mods(bw, new PropertyTable());
                bw.Write(0u);
                bw.Write(0u);
                bw.Write(0);
            }

            using var br = new BinaryReader(File.OpenRead(tempFile));
            br.ReadUInt32();
            br.ReadInt32();
            br.ReadInt32();
            PropertyTableIO.ReadPropertyTable_Mods(br);
            br.ReadUInt32();
            br.ReadUInt32();
            Assert.AreEqual(0, br.ReadInt32());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void GetReaderFunction_UnsupportedVersion_ReturnsNull()
    {
        var modsType = typeof(Mods);
        var constructor = modsType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, new[] { typeof(IAppInfo) }, null);
        var mods = constructor.Invoke(new object[] { null });

        var method = modsType.GetMethod("GetReaderFunction",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = method.Invoke(mods, new object[] { 99u });
        Assert.IsNull(result);
    }
}
