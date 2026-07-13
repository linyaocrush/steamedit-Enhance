#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

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
        PropertyTableIO.WritePropertyTable_0x00000000(writer, table);
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
                PropertyTableIO.WritePropertyTable_0x00000000(bw, entry.Changes);
                bw.Write(entry.OriginalDataVersion);
                bw.Write(entry.OriginalDataChangeNumber);
                bw.Write(entry.OriginalData.Length);
                bw.Write(entry.OriginalData);
            }

            using var br = new BinaryReader(File.OpenRead(tempFile));
            Assert.AreEqual(6u, br.ReadUInt32());
            Assert.AreEqual(1, br.ReadInt32());
            Assert.AreEqual(12345, br.ReadInt32());

            var loadedChanges = PropertyTableIO.ReadPropertyTable_0x00000000(br);
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
                    PropertyTableIO.WritePropertyTable_0x00000000(bw, e.Changes);
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
                PropertyTableIO.ReadPropertyTable_0x00000000(br);
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
                PropertyTableIO.WritePropertyTable_0x00000000(bw, new PropertyTable());
                bw.Write(0u);
                bw.Write(0u);
                bw.Write(0);
            }

            using var br = new BinaryReader(File.OpenRead(tempFile));
            br.ReadUInt32();
            br.ReadInt32();
            br.ReadInt32();
            PropertyTableIO.ReadPropertyTable_0x00000000(br);
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
    public void Load_UnsupportedVersion_DoesNotLoadEntries()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(99u); // unsupported version
                bw.Write(1);   // 1 entry (should be ignored)
                bw.Write(12345);
            }

            var appInfo = new TestAppInfo();
            var mods = Mods.Load(tempFile, appInfo, RealFileSystem.Instance);
            Assert.AreEqual(0, appInfo.Entries.Count());
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private class TestAppInfo : IAppInfo
    {
        private readonly List<IAppInfoEntry> _entries = new();
        public IEnumerable<IAppInfoEntry> Entries => _entries;
        public event System.EventHandler Modified;
        public IAppInfoEntry FindEntry(uint AppID) => null;
        public void Save(string path, IFileSystem? fileSystem = null) { }
        public bool RevertEntry(IAppInfoEntry entry) => false;
        public void RemoveEntry(IAppInfoEntry entry) { }
        public void AddEntry(IAppInfoEntry entry) => _entries.Add(entry);
    }

    private class MockAppInfoEntry : IAppInfoEntry
    {
        public int ID { get; }
        public uint AppInfoVersion => 0x07564429;
        public uint ChangeNumber => 0;
        public ModifiedEntry? Modification { get; set; }
        public bool IsModified => Modification != null;
        public bool IsHidden { get; set; }
        public bool IsCompleted { get; set; }
        public string Name { get; set; } = "";
        public bool HasSortAs => false;
        public string SortAs { get; set; } = "";
        public string NormalizedSortAs => Common.StringTitleComparer.Normalize(SortAs);
        public string InstallPath => "";
        public AppInfoEntryType Type { get; set; } = AppInfoEntryType.Game;
        public LaunchTable Launches { get; set; } = new LaunchTable(new PropertyTable());
        public event EventHandler? Modified;
        public MockAppInfoEntry(int id) { ID = id; }
        public byte[] GetOriginalDataCopy() => Array.Empty<byte>();
        public T GetPropertyValue<T>(T DefaultValue, params string[] PropertyPath) => DefaultValue;
        public void SetPropertyValue(PropertyType Type, object? Value, params string[] PropertyPath) { }
        public void RevertModifiedProperty(params string[] PropertyPath) { }
        public bool Revert() => false;
        public PropertyType? GetOriginalPropertyType(params string[] PropertyPath) => null;
        public object? GetOriginalPropertyValue(params string[] PropertyPath) => null;
    }

    [TestMethod]
    public void Save_CreatesBackup_WhenFileExists()
    {
        // 创建一个有效的 Mods 文件
        var originalData = CreateValidModsFile();
        var appInfo = new TestAppInfo();
        var entry = new MockAppInfoEntry(12345)
        {
            Modification = new ModifiedEntry(12345)
            {
                Changes = CreateSampleChanges(),
                OriginalData = CreateSampleOriginalData(),
                OriginalDataVersion = 0x07564429,
                OriginalDataChangeNumber = 100
            }
        };
        appInfo.AddEntry(entry);

        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, originalData);
            var mods = Mods.Load(tempFile, appInfo, RealFileSystem.Instance);
            var mockFs = new MockFileSystem();
            mockFs.Files[tempFile] = originalData;

            mods.Save(tempFile, mockFs);

            Assert.IsTrue(mockFs.Files.ContainsKey(tempFile + ".bak"));
            CollectionAssert.AreEqual(originalData, mockFs.Files[tempFile + ".bak"]);
        }
        finally
        {
            File.Delete(tempFile);
            if (File.Exists(tempFile + ".bak")) File.Delete(tempFile + ".bak");
        }
    }

    [TestMethod]
    public void Save_NoBackup_WhenFileDoesNotExist()
    {
        var appInfo = new TestAppInfo();
        var entry = new MockAppInfoEntry(12345)
        {
            Modification = new ModifiedEntry(12345)
            {
                Changes = CreateSampleChanges(),
                OriginalData = CreateSampleOriginalData(),
                OriginalDataVersion = 0x07564429,
                OriginalDataChangeNumber = 100
            }
        };
        appInfo.AddEntry(entry);

        string tempFile = Path.GetTempFileName();
        File.Delete(tempFile);
        try
        {
            var mods = Mods.Load(tempFile, appInfo, RealFileSystem.Instance);
            var mockFs = new MockFileSystem();

            mods.Save(tempFile, mockFs);

            Assert.IsFalse(mockFs.Files.ContainsKey(tempFile + ".bak"));
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    private byte[] CreateValidModsFile()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(6u); // version
        bw.Write(0);  // 0 entries
        return ms.ToArray();
    }
}
