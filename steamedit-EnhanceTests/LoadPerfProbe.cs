#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

/// <summary>
/// 开发期性能探针：加载真实的 appinfo.vdf 并打印耗时。
/// 路径与产品一致地自动检测（注册表 → 兜底路径），可用环境变量
/// <c>STEAMEDIT_APPINFO</c> 覆盖；找不到文件时测试跳过，不影响 CI。
/// </summary>
[TestClass]
[TestCategory("Perf")]
public class LoadPerfProbe
{
    private static string? ResolveAppInfoPath()
    {
        string? overridePath = Environment.GetEnvironmentVariable("STEAMEDIT_APPINFO");
        if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            return overridePath;

        using var key = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
        string? steamPath = key?.GetValue("SteamPath") as string;
        if (!string.IsNullOrEmpty(steamPath))
        {
            string candidate = Path.GetFullPath(Path.Combine(steamPath, "appcache", "appinfo.vdf"));
            if (File.Exists(candidate)) return candidate;
        }

        string fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        string fallbackPath = Path.GetFullPath(Path.Combine(fallback, "appcache", "appinfo.vdf"));
        return File.Exists(fallbackPath) ? fallbackPath : null;
    }

    [TestMethod]
    public void ProbeRealAppInfo()
    {
        string? path = ResolveAppInfoPath();
        if (path == null) { Assert.Inconclusive("未找到 appinfo.vdf（非 Steam 机器），跳过。"); return; }

        Console.WriteLine($"appinfo.vdf = {path}");
        foreach (bool native in new[] { false, true })
        {
            PropertyTableIO.UseNative = native;
            Console.WriteLine($"--- UseNative={native} (active={PropertyTableIO.Native}) ---");
            for (int i = 0; i < 3; i++)
            {
                var sw = Stopwatch.StartNew();
                var data = AppInfoFactory.Load(path);
                sw.Stop();
                var entries = data.Entries.ToList();
                Console.WriteLine($"  Load#{i}: {sw.ElapsedMilliseconds} ms, entries={entries.Count}");
                if (i == 0) Assert.IsTrue(entries.Count > 0);
            }
        }
        PropertyTableIO.UseNative = true;
    }
}
