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
using steamedit_Enhance.Data;
using steamedit_Enhance.Data.Impl;

namespace steamedit_Enhance;

internal static class Program
{
    private static MainWindow MainWindow_ = null!;

    private static string ResolveAppInfoPath(bool trySavedPath, bool allowManual)
    {
        string path = trySavedPath ? Settings.Default.AppInfoPath : "";
        if (path.Length == 0)
        {
            Log.WriteLine("Missing AppInfoPath. Looking for default location.");
            using RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
            if (registryKey != null)
            {
                try
                {
                    string fullPath = Path.GetFullPath((string)registryKey.GetValue("SteamPath")!);
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
            if (!allowManual) return null!;
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
            return null!;
        }
        return openFileDialog.FileName;
    }

    private static SteamData? LoadSteamData(ref SteamDataInitProps dataInit, string appInfoPath)
    {
        dataInit.AppInfoPath = appInfoPath;
        var initCopy = dataInit;
        Exception? loadError = null;
        SteamData? result = null;
        var cts = new CancellationTokenSource();

        using (var loading = new WaitingDialog(Strings.WaitingDialog_Waiting))
        {
            var task = Task.Run(() =>
            {
                try
                {
                    cts.Token.ThrowIfCancellationRequested();
                    var service = new SteamData();
                    service.Initialize(ref initCopy);
                    result = service;
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { loadError = ex; }
            }, cts.Token);
            if (!loading.ShowAndWait(() => !task.IsCompleted))
            {
                Log.WriteLine("Load cancelled by user or timed out");
                cts.Cancel();
                try { task.Wait(TimeSpan.FromSeconds(5), cts.Token); } catch (OperationCanceledException) { }
                // 部分初始化的 SteamData 需要在后台回收
                result?.Shutdown();
                cts.Dispose();
                return null;
            }
        }

        cts.Dispose();
        if (loadError is UnknownAppInfoVersionException ux)
        {
            MessageBoxWithDetails.Show(Strings.Msg_UnknownVersion, Strings.Msg_CriticalError, $"AppInfo Path: {ux.AppInfoPath}\r\nDetected Version: {ux.AppInfoVersion:X8}");
            return null;
        }
        if (loadError is AppInfoLoadException al)
        {
            MessageBoxWithDetails.Show(Strings.Msg_LoadFailed, Strings.Msg_CriticalError, al.ToString());
            return null;
        }
        if (loadError != null) throw loadError;
        return result;
    }

    private static SteamData? InitializeSteamData(ref SteamDataInitProps dataInit, bool tryCurrentPath, bool silent)
    {
        bool firstAttempt = true;
        while (true)
        {
            string appInfoPath = ResolveAppInfoPath(trySavedPath: firstAttempt && tryCurrentPath, allowManual: !silent);
            if (appInfoPath == null) return null;

            var service = LoadSteamData(ref dataInit, appInfoPath);
            if (service != null)
            {
                if (appInfoPath != Settings.Default.AppInfoPath)
                {
                    Settings.Default.AppInfoPath = appInfoPath;
                    Settings.Default.Save();
                }
                return service;
            }

            if (silent) return null;
            firstAttempt = false;
        }
    }

    [STAThread]
    private static void Main()
    {
        // 强制 Log 静态构造函数在 Settings 之前运行，避免 Settings 构造函数中
        // 的 Log.WriteLine 调用触发隐式静态初始化顺序依赖
        _ = Log.FilePath;

        // 配置日志级别
#if DEBUG
        if (Settings.Default.EnableDebugLogging)
            Log.SetMinimumLevel(Log.LogLevel.Debug);
        else
            Log.SetMinimumLevel(Settings.Default.LogLevel);
#else
        Log.SetMinimumLevel(Settings.Default.LogLevel);
#endif
        
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
        bool isAutoFix = AppHelper.HasArgument("autofix", cmdArgs);
        if (isAutoFix)
        {
            var svc = InitializeSteamData(ref dataInit, tryCurrentPath: true, silent: true);
            if (svc != null)
            {
                bool isForceStart = AppHelper.HasArgument("forcestart", cmdArgs);
                if (svc.NeedsApply)
                {
                    Log.WriteLine("Changes detected, applying!");
                    bool shouldRestart = isForceStart || SteamProcessManager.IsSteamRunning(svc.SteamDir);
                    if (shouldRestart)
                        SteamProcessManager.KillSteam(svc.SteamDir, steamedit_Enhance.Strings.SteamData_WaitingForClose, (msg, waitWhile) =>
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
                else if (isForceStart)
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
            var svc = InitializeSteamData(ref dataInit, tryCurrentPath: true, silent: false);
            if (svc != null)
            {
                if (svc.NeedsApply)
                {
                    Log.WriteLine("Startup auto-apply: unapplied changes detected, applying...");
                    svc.Apply();
                }
                TranslationGlossary.Initialize(dataInit.DataDirectory);
                var mainWindow = new MainWindow(svc);
                // Load 事件后触发 glossary 同步（UI 初始化完成后再运行，避免竞态）
                mainWindow.Load += (_, _) => Task.Run(() => SyncGlossaryFromEntries(svc));
                try
                {
                    MainWindow_ = mainWindow;
                    Application.Run(MainWindow_);
                }
                finally
                {
                    svc.Shutdown();
                    Log.Close();
                }
            }
        }
    }

    private static void SyncGlossaryFromEntries(ISteamDataReadService svc)
    {
        try
        {
            var adapters = svc.AppInfoData.Entries
                .Where(e => e.IsModified && e.Modification!.Changes.HasProperty(AppInfoPaths.Name))
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
        private readonly steamedit_Enhance.Data.IAppInfoEntry _entry;
        private string _originalName = null!;
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

        public GlossaryAdapter(steamedit_Enhance.Data.IAppInfoEntry entry)
        {
            _entry = entry;
        }

        private static string GetOriginalName(steamedit_Enhance.Data.IAppInfoEntry entry)
        {
            return entry.Modification!.GetOriginalName()!;
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
            sb.AppendLine("=== steamedit_Enhance Crash Report ===");
            sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"操作系统: {Environment.OSVersion}");
            sb.AppendLine($".NET 版本: {Environment.Version}");
            sb.AppendLine($"命令行: {Environment.CommandLine}");
            sb.AppendLine();
            sb.AppendLine("=== 异常信息 ===");
            sb.AppendLine(ex.ToString());
            sb.AppendLine();
            sb.AppendLine("=== 最近日志 ===");
            var recentLog = Log.GetRecentLines(100);
            if (recentLog.Length > 0)
            {
                foreach (var line in recentLog)
                    sb.AppendLine(line);
            }
            else
            {
                sb.AppendLine("(无日志)");
            }

            File.WriteAllText(path, sb.ToString());
            return path;
        }
        catch
        {
            return null!;
        }
    }

    private static void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        Log.Flush();
        var ex = e.ExceptionObject as Exception;
        string? crashPath = ex != null ? WriteCrashFile(ex) : null;
        string? details = e.ExceptionObject.ToString();
        if (crashPath != null)
        {
            details = $"{Strings.CrashDialog_SavedTo}\r\n{crashPath}\r\n\r\n{details}";
        }
        MessageBoxWithDetails.ShowWithExit(Strings.Msg_UnhandledException, Strings.Msg_UnhandledExceptionTitle, details!);
    }

    private static void Application_ThreadException(object? sender, ThreadExceptionEventArgs e)
    {
        Log.Flush();
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
