#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using Common;

namespace SteamEdit.Data;

internal static class SteamProcessManager
{
    /// <summary>
    /// 获取已验证的 Steam 可执行文件路径。
    /// 验证文件存在且 CompanyName 为 "Valve Corporation"。
    /// </summary>
    public static string? GetSteamExePath(string steamDir)
    {
        if (string.IsNullOrEmpty(steamDir)) return null;

        string exePath = Path.Combine(steamDir, "steam.exe");
        if (!File.Exists(exePath))
        {
            Log.WriteLine("Steam exe not found: {0}", exePath);
            return null;
        }

        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.Equals(versionInfo.CompanyName, "Valve Corporation", StringComparison.OrdinalIgnoreCase))
            {
                Log.WriteLine("Steam exe company mismatch: {0} (expected Valve Corporation)", versionInfo.CompanyName ?? "(null)");
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.WriteLine("Failed to verify steam exe: {0}", ex.Message);
            return null;
        }

        return exePath;
    }

    public static bool IsSteamRunning(string steamDir)
    {
        string? expectedPath = GetSteamExePath(steamDir);
        if (expectedPath == null)
        {
            // 无法验证路径时回退到进程名匹配
            var procs = Process.GetProcessesByName("steam");
            foreach (var p in procs) p.Dispose();
            return procs.Length != 0;
        }

        string normalizedExpected = Path.GetFullPath(expectedPath);
        foreach (var proc in Process.GetProcessesByName("steam"))
        {
            try
            {
                string? fileName = proc.MainModule?.FileName;
                if (fileName != null && string.Equals(Path.GetFullPath(fileName), normalizedExpected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (System.ComponentModel.Win32Exception) { /* 无权限访问，跳过 */ }
            finally { proc.Dispose(); }
        }
        return false;
    }

    public static void KillSteam(string steamDir, string waitingMessage, Action<string, Func<bool>> showAndWait)
    {
        string? expectedPath = GetSteamExePath(steamDir);
        string? normalizedExpected = expectedPath != null ? Path.GetFullPath(expectedPath) : null;
        if (normalizedExpected == null)
            Log.WriteLine("WARNING: Steam exe validation failed, falling back to name-only process matching");

        Process[] processesByName = Process.GetProcessesByName("steam");
        Log.WriteLine("Steam candidate processes: {0}", processesByName.Length);
        if (processesByName.Length == 0) return;

        int killed = 0;
        Log.WriteLine("Closing Steam");
        foreach (var proc in processesByName)
        {
            try
            {
                if (normalizedExpected != null)
                {
                    string procPath;
                    try { procPath = Path.GetFullPath(proc.MainModule?.FileName ?? ""); }
                    catch (System.ComponentModel.Win32Exception) { continue; } // 无权限，非目标进程

                    if (!string.Equals(procPath, normalizedExpected, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.WriteLine("Skipping non-Steam process: {0}", procPath);
                        continue;
                    }
                }
                proc.Kill();
                killed++;
            }
            catch (Exception ex) { Log.WriteLine("Failed to kill steam process: {0}", ex.Message); }
            finally { proc.Dispose(); }
        }

        if (killed > 0)
            showAndWait(waitingMessage, () => IsSteamRunning(steamDir));
        else
            Log.WriteLine("No matching Steam processes found to kill");
    }

    public static void EnsureSteamRunning(string steamDir, bool focusIfRunning)
    {
        if (focusIfRunning || !IsSteamRunning(steamDir))
        {
            string? exePath = GetSteamExePath(steamDir);
            if (exePath == null)
            {
                Log.WriteLine("Cannot start Steam: exe path validation failed");
                return;
            }

            string[] args = Environment.GetCommandLineArgs();
            string text = AppHelper.GetPassthroughArguments(args);
            if (AppHelper.HasArgument("silent", args))
            {
                text += "-silent ";
            }
            Log.WriteLine("Starting Steam: {0}", exePath);
            Process.Start(exePath, text)?.Dispose();
        }
    }
}
