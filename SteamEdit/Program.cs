using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Common;
using Microsoft.Win32;
using SteamEdit.Data;

namespace SteamEdit;

internal static class Program
{
    private static MainWindow MainWindow_;

    private static bool InitializeSteamData(ref SteamDataInitProps dataInit, bool tryCurrentPath, bool silent)
    {
        string text = (tryCurrentPath ? Settings.Default.AppInfoPath : "");
        if (text.Length == 0)
        {
            Log.WriteLine("Missing AppInfoPath. Looking for default location.");
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
            if (registryKey != null)
            {
                try
                {
                    string fullPath = Path.GetFullPath((string)registryKey.GetValue("SteamPath"));
                    Log.WriteLine("  Got steam path from registry: {0}", fullPath);
                    text = Path.GetFullPath(Path.Combine(fullPath, "appcache", "appinfo.vdf"));
                }
                catch (Exception ex)
                {
                    Log.WriteLine("  Error reading SteamPath from registry:\n{0}", ex);
                    text = "";
                }
            }
        }
        if (text.Length == 0)
        {
            string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            Log.WriteLine("  Using default steam path: {0}", text2);
            text = Path.GetFullPath(Path.Combine(text2, "appcache", "appinfo.vdf"));
        }
        if (!silent && (!tryCurrentPath || text.Length == 0))
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.FileName = text;
            openFileDialog.Title = "Select Steam's appinfo.vdf file";
            openFileDialog.ValidateNames = true;
            openFileDialog.CheckFileExists = true;
            openFileDialog.ReadOnlyChecked = true;
            if (openFileDialog.ShowDialog() == DialogResult.Cancel)
            {
                return false;
            }
            text = openFileDialog.FileName;
        }
        dataInit.AppInfoPath = text;
        try
        {
            SteamData.Initialize(ref dataInit);
        }
        catch (UnknownAppInfoVersionException ex2)
        {
            MessageBoxWithDetails.Show("Unknown version while trying to load appinfo.vdf. Either we're trying to load the wrong file, or maybe Steam has updated something, and a fix is needed for SteamEdit", "SteamEdit: Critical Error", $"AppInfo Path: {ex2.AppInfoPath}\r\nDetected Version: {ex2.AppInfoVersion:X8}");
            return false;
        }
        catch (AppInfoLoadException ex3)
        {
            MessageBoxWithDetails.Show("Failed to load appinfo.vdf. Possibly corrupted. Please locate your appinfo.vdf file.", "SteamEdit: Critical Error", ex3.ToString());
            return InitializeSteamData(ref dataInit, tryCurrentPath: false, silent);
        }
        if (text != Settings.Default.AppInfoPath)
        {
            Settings.Default.AppInfoPath = text;
            Settings.Default.Save();
        }
        return true;
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
        if (!flag)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(defaultValue: false);
        }
        if (InitializeSteamData(ref dataInit, tryCurrentPath: true, flag))
        {
            if (flag)
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
            }
            else
            {
                MainWindow_ = new MainWindow();
                Application.Run(MainWindow_);
            }
            SteamData.Shutdown();
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBoxWithDetails.Show("Unhandled exception. Not sure what went wrong, but you might be able to continue working.", "SteamEdit: Unhandled exception", e.ExceptionObject.ToString());
    }

    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        MessageBoxWithDetails.Show("Unhandled exception. Not sure what went wrong, but you might be able to continue working.", "SteamEdit: Unhandled exception", e.Exception.ToString());
    }
}
