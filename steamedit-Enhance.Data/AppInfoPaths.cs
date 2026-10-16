#nullable enable
using System;

namespace steamedit_Enhance.Data;

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

    /// <summary>
    /// 加载时预取的“热路径”子树。列表/筛选/排序/批量操作只读取这些前缀下的属性；
    /// 其余部分（描述、DLC 列表、启动项等）在首次真正访问该条目的完整属性时才惰性物化。
    /// 单一事实来源：既用于向原生解码器下发热路径，也用于判断某访问是否能命中
    /// “摘要表”（<see cref="IsHotPath"/>）。
    /// </summary>
    public static readonly string[][] HotPaths =
    {
        new[] { "appinfo", "common" },
        new[] { "appinfo", "steam_edit" },
        new[] { "appinfo", "config", "installdir" },
    };

    /// <summary>判断属性路径是否被某个热路径前缀覆盖（即可用摘要表满足）。</summary>
    public static bool IsHotPath(ReadOnlySpan<string> path)
    {
        foreach (string[] prefix in HotPaths)
        {
            if (path.Length < prefix.Length) continue;
            bool match = true;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (!string.Equals(path[i], prefix[i], StringComparison.Ordinal)) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }
}
