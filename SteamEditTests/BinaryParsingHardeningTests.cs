using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamEdit.Data;

namespace SteamEditTests;

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

    private class TestAppInfo : IAppInfo
    {
        private readonly System.Collections.Generic.List<IAppInfoEntry> _entries = new();
        public System.Collections.Generic.IEnumerable<IAppInfoEntry> Entries => _entries;
        public event EventHandler Modified;
        public IAppInfoEntry FindEntry(uint AppID) => null;
        public void Save(string path) { }
        public void RevertEntry(IAppInfoEntry entry) { }
        public void RemoveEntry(IAppInfoEntry entry) { }
    }
}
