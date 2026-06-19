using System;
using System.Diagnostics;
using System.IO;
using Common;

namespace SteamEdit.Data;

internal static class SteamProcessManager
{
    public static bool IsSteamRunning()
    {
        var procs = Process.GetProcessesByName("steam");
        foreach (var p in procs) p.Dispose();
        return procs.Length != 0;
    }

    public static void KillSteam()
    {
        Process[] processesByName = Process.GetProcessesByName("steam");
        Log.WriteLine("Steam processes: " + processesByName.Length);
        if (processesByName.Length == 0) return;

        Log.WriteLine("Closing Steam");
        foreach (var proc in processesByName)
        {
            try { proc.Kill(); }
            catch (Exception ex) { Log.WriteLine("Failed to kill steam process: {0}", ex.Message); }
            finally { proc.Dispose(); }
        }
        using (var dlg = new WaitingDialog(SteamEdit.Strings.SteamData_WaitingForClose))
            dlg.ShowAndWait(() => IsSteamRunning());
    }

    public static void EnsureSteamRunning(bool focusIfRunning)
    {
        if (focusIfRunning || !IsSteamRunning())
        {
            string[] args = Environment.GetCommandLineArgs();
            string text = AppHelper.GetPassthroughArguments(args);
            if (AppHelper.HasArgument("silent", args))
            {
                text += "-silent ";
            }
            Log.WriteLine("Starting Steam");
            Process.Start(Path.Combine(SteamData.SteamDir, "steam.exe"), text)?.Dispose();
        }
    }
}
