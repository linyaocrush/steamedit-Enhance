#nullable enable
namespace SteamEdit.Data;

internal static class AppInfoPaths
{
    // appinfo/common/*
    public static readonly string[] Name = { "appinfo", "common", "name" };
    public static readonly string[] SortAs = { "appinfo", "common", "sortas" };
    public static readonly string[] Type = { "appinfo", "common", "type" };
    public static readonly string[] ControllerSupport = { "appinfo", "common", "controller_support" };
    public static readonly string[] OpenVrSupport = { "appinfo", "common", "openvrsupport" };
    public static readonly string[] OsList = { "appinfo", "common", "oslist" };
    public static readonly string[] Developers = { "appinfo", "common", "developers" };
    public static readonly string[] Publishers = { "appinfo", "common", "publishers" };
    public static readonly string[] NameLocalized = { "appinfo", "common", "name_localized" };

    // appinfo/config/*
    public static readonly string[] InstallDir = { "appinfo", "config", "installdir" };
    public static readonly string[] Launch = { "appinfo", "config", "launch" };

    // appinfo/steam_edit/*
    public static readonly string[] IsHidden = { "appinfo", "steam_edit", "is_hidden" };
    public static readonly string[] IsCompleted = { "appinfo", "steam_edit", "is_completed" };
    public static readonly string[] BaseName = { "appinfo", "steam_edit", "base_name" };
    public static readonly string[] BaseType = { "appinfo", "steam_edit", "base_type" };
    public static readonly string[] BaseOpenVrSupport = { "appinfo", "steam_edit", "base_openvrsupport" };
    public static readonly string[] BaseNameLocalized = { "appinfo", "steam_edit", "base_name_localized" };
}
