#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;
using Microsoft.Win32;
using SteamEdit.Data;
using SteamEdit.Data.Impl;

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
                try
                {
                    var service = new SteamData();
                    service.Initialize(ref initCopy);
                    SteamData.Instance = service;
                }
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
        string[] cmdArgs = Environment.GetCommandLineArgs();
        bool flag = AppHelper.HasArgument("autofix", cmdArgs);
        if (flag)
        {
            if (InitializeSteamData(ref dataInit, tryCurrentPath: true, silent: true))
            {
                var svc = SteamData.Instance;
                bool flag2 = AppHelper.HasArgument("forcestart", cmdArgs);
                if (svc.NeedsApply)
                {
                    Log.WriteLine("Changes detected, applying!");
                    bool shouldRestart = flag2 || SteamProcessManager.IsSteamRunning(svc.SteamDir);
                    if (shouldRestart)
                        SteamProcessManager.KillSteam(svc.SteamDir, SteamEdit.Strings.SteamData_WaitingForClose, (msg, waitWhile) =>
                        {
                            using var dlg = new WaitingDialog(msg);
                            dlg.ShowAndWait(() => waitWhile());
                        });
                    svc.Save();
                    svc.Apply();
                    if (shouldRestart)
                        SteamProcessManager.EnsureSteamRunning(svc.SteamDir, focusIfRunning: true);
                    Log.WriteLine("Appcache updated");
                }
                else if (flag2)
                {
                    SteamProcessManager.EnsureSteamRunning(svc.SteamDir, focusIfRunning: true);
                }
                else
                {
                    Log.WriteLine("No data changes, so nothing to do!");
                }
                svc.Shutdown();
                Log.Close();
            }
        }
        else
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(defaultValue: false);
            if (InitializeSteamData(ref dataInit, tryCurrentPath: true, silent: false))
            {
                TranslationGlossary.Initialize(dataInit.DataDirectory);
                Task.Run(() => SyncGlossaryFromEntries());
                try
                {
                    MainWindow_ = new MainWindow(SteamData.Instance);
                    Application.Run(MainWindow_);
                }
                finally
                {
                    SteamData.Instance.Shutdown();
                    Log.Close();
                }
            }
        }
    }

    private static void SyncGlossaryFromEntries()
    {
        try
        {
            var adapters = SteamData.Instance.AppInfoData.Entries
                .Where(e => e.IsModified && e.Modification.Changes.HasProperty(AppInfoPaths.Name))
                .Select(e => new GlossaryAdapter(e))
                .ToList();
            TranslationGlossary.SyncFromMods(adapters);
        }
        catch (Exception ex)
        {
            Log.WriteLine("Glossary sync failed: {0}", ex.Message);
        }
    }

    private class GlossaryAdapter : IAppInfoEntryAdapter
    {
        private readonly SteamEdit.Data.IAppInfoEntry _entry;
        private string _originalName;
        private bool _originalNameParsed;
        public int ID => _entry.ID;
        public string TranslatedName => _entry.Name;
        public string OriginalName
        {
            get
            {
                if (!_originalNameParsed)
                {
                    _originalNameParsed = true;
                    _originalName = GetOriginalName(_entry);
                }
                return _originalName;
            }
        }

        public GlossaryAdapter(SteamEdit.Data.IAppInfoEntry entry)
        {
            _entry = entry;
        }

        private static string GetOriginalName(SteamEdit.Data.IAppInfoEntry entry)
        {
            return entry.Modification?.GetOriginalName();
        }
    }

    private static string WriteCrashFile(Exception ex)
    {
        try
        {
            string dir = AppHelper.GetAppDirectory();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(dir, $"crash-{timestamp}.log");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== SteamEdit Crash Report ===");
            sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"操作系统: {Environment.OSVersion}");
            sb.AppendLine($".NET 版本: {Environment.Version}");
            sb.AppendLine($"命令行: {Environment.CommandLine}");
            sb.AppendLine();
            sb.AppendLine("=== 异常信息 ===");
            sb.AppendLine(ex.ToString());

            File.WriteAllText(path, sb.ToString());
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        string crashPath = ex != null ? WriteCrashFile(ex) : null;
        string details = e.ExceptionObject.ToString();
        if (crashPath != null)
        {
            details = $"{Strings.CrashDialog_SavedTo}\r\n{crashPath}\r\n\r\n{details}";
        }
        MessageBoxWithDetails.ShowWithExit(Strings.Msg_UnhandledException, Strings.Msg_UnhandledExceptionTitle, details);
    }

    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        string crashPath = WriteCrashFile(e.Exception);
        string details = e.Exception.ToString();
        if (crashPath != null)
        {
            details = $"{Strings.CrashDialog_SavedTo}\r\n{crashPath}\r\n\r\n{details}";
        }
        if (MessageBoxWithDetails.ShowWithExit(Strings.Msg_UnhandledException, Strings.Msg_UnhandledExceptionTitle, details))
        {
            Environment.Exit(1);
        }
    }
}
