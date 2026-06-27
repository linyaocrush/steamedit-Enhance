#nullable enable
using System;
using System.Globalization;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamEdit;

namespace SteamEditTests;

[TestClass]
public class I18nTests
{
    [TestMethod]
    public void CurrentCulture_Initially_NotEmpty()
    {
        Assert.IsNotNull(I18n.CurrentCulture);
        Assert.IsFalse(string.IsNullOrWhiteSpace(I18n.CurrentCulture.Name));
    }

    [TestMethod]
    public void SetLanguage_ChangesCurrentCulture()
    {
        var original = I18n.CurrentCulture.Name;
        try
        {
            I18n.SetLanguage("zh-CN");
            Assert.AreEqual("zh-CN", I18n.CurrentCulture.Name);
            Assert.AreEqual("zh-CN", Strings.Culture.Name);
        }
        finally
        {
            I18n.SetLanguage(original);
        }
    }

    [TestMethod]
    public void SetLanguage_SameCulture_DoesNotFireEvent()
    {
        var original = I18n.CurrentCulture.Name;
        bool eventFired = false;
        EventHandler handler = (_, _) => eventFired = true;
        try
        {
            I18n.LanguageChanged += handler;
            I18n.SetLanguage(original);
            Assert.IsFalse(eventFired, "LanguageChanged should not fire when setting the same language");
        }
        finally
        {
            I18n.LanguageChanged -= handler;
        }
    }

    [TestMethod]
    public void GetEnumDisplayName_ReturnsNonEmptyString()
    {
        string name = I18n.GetEnumDisplayName(AppInfoEntryType.Game);
        Assert.IsFalse(string.IsNullOrWhiteSpace(name));
    }

    [TestMethod]
    public void GetEnumDisplayName_UnknownFallback_ReturnsToString()
    {
        // For an undefined value, GetEnumDisplayName falls back to enumValue.ToString()
        // Use a simple, predictable value that doesn't match any Flags combination
        var undefinedValue = (AppInfoEntryType)int.MaxValue;
        string name = I18n.GetEnumDisplayName(undefinedValue);
        Assert.AreEqual(undefinedValue.ToString(), name);
    }
}
