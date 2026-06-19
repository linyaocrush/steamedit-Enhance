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
