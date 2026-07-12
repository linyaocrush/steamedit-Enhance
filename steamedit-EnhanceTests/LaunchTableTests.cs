#nullable enable
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class LaunchTableTests
{
    [TestMethod]
    public void Constructor_Empty_CountIsZero()
    {
        var table = new LaunchTable();
        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void Add_SingleItem_CountIsOne()
    {
        var table = new LaunchTable();
        table.Add(new LaunchItem { Executable = "game.exe" });
        Assert.AreEqual(1, table.Count);
    }

    [TestMethod]
    public void Add_MultipleItems_CountMatches()
    {
        var table = new LaunchTable();
        table.Add(new LaunchItem { Executable = "a.exe" });
        table.Add(new LaunchItem { Executable = "b.exe" });
        table.Add(new LaunchItem { Executable = "c.exe" });
        Assert.AreEqual(3, table.Count);
    }

    [TestMethod]
    public void Items_ReturnsLaunchItems()
    {
        var table = new LaunchTable();
        table.Add(new LaunchItem { Label = "Play", Executable = "game.exe", Arguments = "-windowed", WorkingDir = "." });
        var items = table.Items.ToList();
        Assert.AreEqual(1, items.Count);
        Assert.AreEqual("Play", items[0].Label);
        Assert.AreEqual("game.exe", items[0].Executable);
        Assert.AreEqual("-windowed", items[0].Arguments);
    }

    [TestMethod]
    public void Clear_ResetsCount()
    {
        var table = new LaunchTable();
        table.Add(new LaunchItem { Executable = "game.exe" });
        table.Clear();
        Assert.AreEqual(0, table.Count);
    }

    [TestMethod]
    public void Constructor_FromItems_PreservesData()
    {
        var items = new List<LaunchItem>
        {
            new LaunchItem { Label = "Play", Executable = "game.exe" },
            new LaunchItem { Label = "Editor", Executable = "editor.exe" }
        };
        var table = new LaunchTable(items);
        Assert.AreEqual(2, table.Count);
        var readBack = table.Items.ToList();
        Assert.AreEqual(2, readBack.Count);
    }
}
