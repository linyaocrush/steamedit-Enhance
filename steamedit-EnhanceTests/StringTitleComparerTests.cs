#nullable enable
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Common;

namespace steamedit_EnhanceTests;

[TestClass]
public class StringTitleComparerTests
{
    [TestMethod]
    public void Normalize_Null_ReturnsEmpty()
    {
        Assert.AreEqual("", StringTitleComparer.Normalize(null));
    }

    [TestMethod]
    public void Normalize_Whitespace_Trims()
    {
        Assert.AreEqual("hello", StringTitleComparer.Normalize("  hello  "));
    }

    [TestMethod]
    public void Normalize_ThePrefix_Strips()
    {
        Assert.AreEqual("Witcher", StringTitleComparer.Normalize("The Witcher"));
    }

    [TestMethod]
    public void Normalize_NoThePrefix_Unchanged()
    {
        Assert.AreEqual("Portal", StringTitleComparer.Normalize("Portal"));
    }

    [TestMethod]
    public void CompareStrings_SimpleOrder_Correct()
    {
        Assert.IsTrue(StringTitleComparer.CompareStrings("Alpha", "Beta") < 0);
        Assert.IsTrue(StringTitleComparer.CompareStrings("Beta", "Alpha") > 0);
    }

    [TestMethod]
    public void CompareStrings_ThePrefixIgnored()
    {
        int result = StringTitleComparer.CompareStrings("The Witcher", "Witcher");
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public void CompareStrings_CaseInsensitive()
    {
        Assert.AreEqual("witcher", StringTitleComparer.Normalize("The Witcher".ToLower()));
    }

    [TestMethod]
    public void CompareStrings_EqualStrings_ReturnsZero()
    {
        Assert.AreEqual(0, StringTitleComparer.CompareStrings("Hello", "Hello"));
    }
}
