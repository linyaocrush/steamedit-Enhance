#nullable enable
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class AppInfoFactoryTests
{
    [TestMethod]
    public void Load_FileNotFound_Throws()
    {
        Assert.ThrowsException<FileNotFoundException>(
            () => AppInfoFactory.Load(@"C:\nonexistent\appinfo.vdf"));
    }

    [TestMethod]
    public void Load_UnknownVersion_Throws()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            using (var bw = new BinaryWriter(File.Create(tempFile)))
            {
                bw.Write(0xDEADBEEFu); // unknown magic
            }
            Assert.ThrowsException<UnknownAppInfoVersionException>(
                () => AppInfoFactory.Load(tempFile));
        }
        finally { File.Delete(tempFile); }
    }

    [TestMethod]
    public void Load_EmptyFile_Throws()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            // Empty file — can't create memory mapping or read magic number
            Assert.ThrowsException<ArgumentException>(
                () => AppInfoFactory.Load(tempFile));
        }
        finally { File.Delete(tempFile); }
    }
}
