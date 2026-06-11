using System;
using System.Globalization;
using Common;

namespace SteamEdit;

internal static class I18n
{
    public static event EventHandler LanguageChanged;

    public static CultureInfo CurrentCulture { get; private set; }

    static I18n()
    {
        var saved = Settings.Default.Language;
        if (string.IsNullOrEmpty(saved))
        {
            CurrentCulture = CultureInfo.CurrentUICulture;
        }
        else
        {
            CurrentCulture = new CultureInfo(saved);
        }
        CultureInfo.CurrentUICulture = CurrentCulture;
    }

    public static void SetLanguage(string cultureName)
    {
        CurrentCulture = new CultureInfo(cultureName);
        CultureInfo.CurrentUICulture = CurrentCulture;
        Settings.Default.Language = cultureName;
        Settings.Default.Save();
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string GetEnumDisplayName<T>(T enumValue) where T : Enum
    {
        string key = "Enum_" + enumValue.ToString();
        return Strings.ResourceManager.GetString(key, CurrentCulture) ?? enumValue.ToString();
    }
}
