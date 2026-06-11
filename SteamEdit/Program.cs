using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;
using Microsoft.Win32;
using SteamEdit.Data;

namespace SteamEdit;

internal static class Program
{
    private static MainWindow MainWindow_;

    private static string ResolveAppInfoPath(bool trySavedPath, bool allowManual)
    {
        string path = trySavedPath ? Settings.Default.AppInfoPath : "";
        if (path.Length == 0)
        {
            Log.WriteLine("Missing AppInfoPath. Looking for default location.");
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
            if (registryKey != null)
            {
                try
                {
                    string fullPath = Path.GetFullPath((string)registryKey.GetValue("SteamPath"));
                    Log.WriteLine("  Got steam path from registry: {0}", fullPath);
                    path = Path.GetFullPath(Path.Combine(fullPath, "appcache", "appinfo.vdf"));
                }
                catch (Exception ex)
                {
                    Log.WriteLine("  Error reading SteamPath from registry:\n{0}", ex);
                    path = "";
                }
            }
        }
        if (path.Length == 0)
        {
            string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            Log.WriteLine("  Using default steam path: {0}", fallback);
            path = Path.GetFullPath(Path.Combine(fallback, "appcache", "appinfo.vdf"));
        }
        if (!File.Exists(path))
        {
            if (!allowManual) return null;
            return ShowPathDialog(path);
        }
        return path;
    }

    private static string ShowPathDialog(string initialPath = "")
    {
        using OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.FileName = initialPath;
        openFileDialog.Title = Strings.Msg_SelectAppInfoTitle;
        openFileDialog.ValidateNames = true;
        openFileDialog.CheckFileExists = true;
        openFileDialog.ReadOnlyChecked = true;
        if (openFileDialog.ShowDialog() == DialogResult.Cancel)
        {
            return null;
        }
        return openFileDialog.FileName;
    }

    private static bool LoadSteamData(ref SteamDataInitProps dataInit, string appInfoPath)
    {
        dataInit.AppInfoPath = appInfoPath;
        var initCopy = dataInit;
        Exception loadError = null;

        using (var loading = new WaitingDialog(Strings.WaitingDialog_Waiting))
        {
            var task = Task.Run(() =>
            {
                try { SteamData.Initialize(ref initCopy); }
                catch (Exception ex) { loadError = ex; }
            });
            if (!loading.ShowAndWait(() => !task.IsCompleted))
            {
                Log.WriteLine("Load cancelled by user or timed out");
                task.Wait(TimeSpan.FromSeconds(5));
                return false;
            }
        }

        if (loadError is UnknownAppInfoVersionException ux)
        {
            MessageBoxWithDetails.Show(Strings.Msg_UnknownVersion, Strings.Msg_CriticalError, $"AppInfo Path: {ux.AppInfoPath}\r\nDetected Version: {ux.AppInfoVersion:X8}");
            return false;
        }
        if (loadError is AppInfoLoadException al)
        {
            MessageBoxWithDetails.Show(Strings.Msg_LoadFailed, Strings.Msg_CriticalError, al.ToString());
            return false;
        }
        if (loadError != null) throw loadError;
        return true;
    }

    private static bool ResolveAndLoadAsync(SteamDataInitProps dataInit)
    {
        string appInfoPath = ResolveAppInfoPath(trySavedPath: true, allowManual: true);
        if (appInfoPath == null) return false;

        if (!LoadSteamData(ref dataInit, appInfoPath))
        {
            return ResolveAndLoadAsyncFallback(dataInit);
        }

        if (appInfoPath != Settings.Default.AppInfoPath)
        {
            Settings.Default.AppInfoPath = appInfoPath;
            Settings.Default.Save();
        }
        return true;
    }

    private static bool ResolveAndLoadAsyncFallback(SteamDataInitProps dataInit)
    {
        while (true)
        {
            string appInfoPath = ShowPathDialog();
            if (appInfoPath == null) return false;

            if (LoadSteamData(ref dataInit, appInfoPath))
            {
                Settings.Default.AppInfoPath = appInfoPath;
                Settings.Default.Save();
                return true;
            }
        }
    }

    private static bool InitializeSteamData(ref SteamDataInitProps dataInit, bool tryCurrentPath, bool silent)
    {
        bool firstAttempt = true;
        while (true)
        {
            string appInfoPath = ResolveAppInfoPath(trySavedPath: firstAttempt && tryCurrentPath, allowManual: !silent);
            if (appInfoPath == null) return false;

            if (LoadSteamData(ref dataInit, appInfoPath))
            {
                if (appInfoPath != Settings.Default.AppInfoPath)
                {
                    Settings.Default.AppInfoPath = appInfoPath;
                    Settings.Default.Save();
                }
                return true;
            }

            if (silent) return false;
            firstAttempt = false;
        }
    }

    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += Application_ThreadException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        string dataDirectory = ((!Path.IsPathRooted(Settings.Default.ModsLocation)) ? Path.GetFullPath(Path.Combine(AppHelper.GetAppDirectory(), Settings.Default.ModsLocation)) : Settings.Default.ModsLocation);
        SteamDataInitProps dataInit = new SteamDataInitProps
        {
            DataDirectory = dataDirectory,
            EnableCompleted = Settings.Default.EnableCompleted,
            EnableHidden = Settings.Default.EnableHidden
        };
        bool flag = AppHelper.HasArgument("autofix", Environment.GetCommandLineArgs());
        if (flag)
        {
            if (InitializeSteamData(ref dataInit, tryCurrentPath: true, silent: true))
            {
                bool flag2 = AppHelper.HasArgument("forcestart", Environment.GetCommandLineArgs());
                if (SteamData.NeedsApply)
                {
                    Log.WriteLine("Changes detected, applying!");
                    SteamData.SaveAndRestartSteam(flag2);
                    Log.WriteLine("Appcache updated");
                }
                else if (flag2)
                {
                    SteamData.EnsureSteamRunning(focusIfRunning: true);
                }
                else
                {
                    Log.WriteLine("No data changes, so nothing to do!");
                }
                SteamData.Shutdown();
                Log.Flush();
            }
        }
        else
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(defaultValue: false);
            if (ResolveAndLoadAsync(dataInit))
            {
                MainWindow_ = new MainWindow();
                Application.Run(MainWindow_);
                SteamData.Shutdown();
                Log.Flush();
            }
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBoxWithDetails.Show(Strings.Msg_UnhandledException, Strings.Msg_UnhandledExceptionTitle, e.ExceptionObject.ToString());
    }

    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        MessageBoxWithDetails.Show(Strings.Msg_UnhandledException, Strings.Msg_UnhandledExceptionTitle, e.Exception.ToString());
    }
}
