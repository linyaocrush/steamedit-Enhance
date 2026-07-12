#nullable enable
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;
using steamedit_Enhance.Data.Impl;

namespace steamedit_EnhanceTests;

[TestClass]
public class BinaryParsingHardeningTests
{
    [TestMethod]
    public void ReadEntries_NegativeCount_ThrowsInvalidDataException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);    // version
                bw.Write(-1);    // negative count
            }

            var appInfo = new TestAppInfo();
            Assert.ThrowsException<InvalidDataException>(() => Mods.Load(tempFile, appInfo));
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void ReadEntries_CountExceedsHardLimit_ThrowsInvalidDataException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);
                bw.Write(100_001); // exceeds 100,000 limit
            }

            var appInfo = new TestAppInfo();
            Assert.ThrowsException<InvalidDataException>(() => Mods.Load(tempFile, appInfo));
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void ReadEntries_CountExceedsStreamData_ThrowsInvalidDataException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);
                bw.Write(50_000); // valid count, but file has no entry data
            }

            var appInfo = new TestAppInfo();
            Assert.ThrowsException<InvalidDataException>(() => Mods.Load(tempFile, appInfo));
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void ReadEntries_V5_DataSizeLessThan60_ThrowsInvalidDataException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(5u);    // version 5
                bw.Write(1);     // 1 entry
                bw.Write(12345); // appId
                // Empty property table (just end marker)
                bw.Write((byte)8); // PropertyType._EndOfTable_
                bw.Write(0u);    // OriginalDataVersion (v5 reads this)
                bw.Write(30);    // dataSize < 60 — INVALID
            }

            var appInfo = new TestAppInfo();
            Assert.ThrowsException<InvalidDataException>(() => Mods.Load(tempFile, appInfo));
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void ReadEntries_V5_DataSizeExceedsStream_ThrowsInvalidDataException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(5u);    // version 5
                bw.Write(1);     // 1 entry
                bw.Write(12345); // appId
                bw.Write((byte)8); // PropertyType._EndOfTable_
                bw.Write(0u);    // OriginalDataVersion
                bw.Write(int.MaxValue); // dataSize way too large
            }

            var appInfo = new TestAppInfo();
            Assert.ThrowsException<InvalidDataException>(() => Mods.Load(tempFile, appInfo));
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void ReadEntries_V6_NegativeDataSize_ThrowsInvalidDataException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);    // version 6
                bw.Write(1);     // 1 entry
                bw.Write(12345); // appId
                bw.Write((byte)8); // PropertyType._EndOfTable_
                bw.Write(0u);    // OriginalDataVersion
                bw.Write(0u);    // OriginalDataChangeNumber
                bw.Write(-1);    // dataSize < 0 — INVALID
            }

            var appInfo = new TestAppInfo();
            Assert.ThrowsException<InvalidDataException>(() => Mods.Load(tempFile, appInfo));
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void ReadEntries_V6_DataSizeExceedsStream_ThrowsInvalidDataException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(6u);
                bw.Write(1);
                bw.Write(12345);
                bw.Write((byte)8);
                bw.Write(0u);
                bw.Write(0u);
                bw.Write(int.MaxValue); // dataSize way too large
            }

            var appInfo = new TestAppInfo();
            Assert.ThrowsException<InvalidDataException>(() => Mods.Load(tempFile, appInfo));
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void ReadWideString_0x07564429_IndexOutOfBounds_ThrowsInvalidDataException()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write((byte)PropertyType.WString); // property type
            bw.Write((uint)0); // name index = 0 (valid)
            bw.Write((uint)5); // value index = 5 — OUT OF RANGE (only 3 entries)
            bw.Write((byte)8); // end of table
        }

        ms.Position = 0;
        using var br = new BinaryReader(ms);
        string[] stringTable = { "name1", "name2", "name3" };

        Assert.ThrowsException<InvalidDataException>(
            () => PropertyTableIO.ReadPropertyTable_0x07564429(br, stringTable));
    }

    [TestMethod]
    public void ReadWideString_0x07564429_MaxUIntIndex_ThrowsInvalidDataException()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write((byte)PropertyType.WString);
            bw.Write((uint)0); // name index
            bw.Write((uint)0xFFFFFFFF); // max uint
            bw.Write((byte)8);
        }

        ms.Position = 0;
        using var br = new BinaryReader(ms);
        string[] stringTable = { "name1" };

        Assert.ThrowsException<InvalidDataException>(
            () => PropertyTableIO.ReadPropertyTable_0x07564429(br, stringTable));
    }

    [TestMethod]
    public void AppInfo_0x07564429_StringTableSizeExceeds50000_ThrowsInvalidDataException()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(123094057u); // magic for 0x07564429
                bw.Write(0u);        // unknown value at start
                // String table position = header(16) + entry data(4) = 20
                bw.Write((long)20);
                // Entry data at position 16 (where reader resumes after reading string table)
                bw.Write(0);         // appId=0 entry terminator (no entries)
                // String table at position 20
                bw.Write(50_001u);   // exceeds 50,000 limit
                // Write 50,001 empty null-terminated strings so ReadZString would succeed
                for (int i = 0; i < 50_001; i++)
                    bw.Write((byte)0);
            }

            Assert.ThrowsException<InvalidDataException>(() =>
            {
                AppInfoFactory.Load(tempFile);
            });
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void ReadPropertyTableCore_DepthExceeds64_ThrowsInvalidDataException()
    {
        // Build binary data with 65 nested Table properties
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            for (int i = 0; i < 65; i++)
            {
                bw.Write((byte)PropertyType.Table);
                PropertyTableIO.WriteZString(bw, $"level{i}");
            }
            // 65th level tries to open another table but should hit depth limit
            bw.Write((byte)PropertyType.Table);
            PropertyTableIO.WriteZString(bw, "one_too_many");
            // End markers for all levels
            for (int i = 0; i <= 65; i++)
                bw.Write((byte)8);
        }

        ms.Position = 0;
        using var br = new BinaryReader(ms);

        Assert.ThrowsException<InvalidDataException>(
            () => PropertyTableIO.ReadPropertyTable_0x00000000(br));
    }

    [TestMethod]
    public void ReadPropertyTableCore_DepthExactly64_DoesNotThrow()
    {
        // Build binary data with exactly 64 nested Table properties (at the limit)
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            for (int i = 0; i < 64; i++)
            {
                bw.Write((byte)PropertyType.Table);
                PropertyTableIO.WriteZString(bw, $"level{i}");
            }
            // Innermost table has a string value
            bw.Write((byte)PropertyType.String);
            PropertyTableIO.WriteZString(bw, "leaf");
            PropertyTableIO.WriteZString(bw, "value");
            bw.Write((byte)8); // end innermost
            // End markers for remaining levels
            for (int i = 0; i < 64; i++)
                bw.Write((byte)8);
        }

        ms.Position = 0;
        using var br = new BinaryReader(ms);

        // Should NOT throw — depth 64 is the max allowed
        var table = PropertyTableIO.ReadPropertyTable_0x00000000(br);
        Assert.IsNotNull(table);
    }

    [TestMethod]
    public void ToStringInternal_DepthExceeds64_ThrowsInvalidDataException()
    {
        // Build a PropertyTable with 65 levels of nesting
        var table = new PropertyTable();
        var current = table;
        for (int i = 0; i < 65; i++)
        {
            var nested = new PropertyTable();
            current.AddPropertyValue($"level{i}", PropertyType.Table, nested);
            current = nested;
        }
        current.AddPropertyValue("leaf", PropertyType.String, "value");

        Assert.ThrowsException<InvalidDataException>(() => table.ToString());
    }

    [TestMethod]
    public void AppInfoEntry_0x07564427_FromReader_NegativeCount_ThrowsAppInfoLoadException()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(12345); // appId (non-zero, so not null)
            bw.Write(-1);    // negative count — INVALID
        }

        ms.Position = 0;
        using var br = new BinaryReader(ms);

        Assert.ThrowsException<AppInfoLoadException>(
            () => AppInfoEntryBase.ReadFromReader<AppInfoEntry_0x07564427>(br));
    }

    [TestMethod]
    public void AppInfoEntry_0x07564428_FromReader_NegativeCount_ThrowsAppInfoLoadException()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(12345);
            bw.Write(-1);
        }

        ms.Position = 0;
        using var br = new BinaryReader(ms);

        Assert.ThrowsException<AppInfoLoadException>(
            () => AppInfoEntryBase.ReadFromReader<AppInfoEntry_0x07564428>(br));
    }

    [TestMethod]
    public void AppInfoEntry_0x07564429_FromReader_NegativeCount_ThrowsAppInfoLoadException()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(12345);
            bw.Write(-1);
        }

        ms.Position = 0;
        using var br = new BinaryReader(ms);

        string[] stringTable = { "test" };
        Assert.ThrowsException<AppInfoLoadException>(
            () => AppInfoEntry_0x07564429.FromReader(br, stringTable));
    }

    [TestMethod]
    public void ApplyChanges_DepthExceeds64_ThrowsInvalidDataException()
    {
        // Build changes and entry tables with 65 levels of nesting
        var changes = new PropertyTable();
        var entry = new PropertyTable();

        var currentChanges = changes;
        var currentEntry = entry;
        for (int i = 0; i < 65; i++)
        {
            var nestedChanges = new PropertyTable();
            var nestedEntry = new PropertyTable();
            currentChanges.AddPropertyValue($"level{i}", PropertyType.Table, nestedChanges);
            currentEntry.AddPropertyValue($"level{i}", PropertyType.Table, nestedEntry);
            currentChanges = nestedChanges;
            currentEntry = nestedEntry;
        }
        currentChanges.AddPropertyValue("leaf", PropertyType.String, "modified");

        var modEntry = new ModifiedEntry(12345) { Changes = changes };
        Assert.ThrowsException<InvalidDataException>(() => modEntry.ApplyChanges(entry));
    }

    private class TestAppInfo : IAppInfo
    {
        private readonly System.Collections.Generic.List<IAppInfoEntry> _entries = new();
        public System.Collections.Generic.IEnumerable<IAppInfoEntry> Entries => _entries;
        public event EventHandler Modified;
        public IAppInfoEntry FindEntry(uint AppID) => null;
        public void Save(string path) { }
        public bool RevertEntry(IAppInfoEntry entry) => false;
        public void RemoveEntry(IAppInfoEntry entry) { }
    }
}
