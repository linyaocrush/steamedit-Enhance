using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Common;
using Microsoft.Win32;
using NAppUpdate.Framework;
using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Sources;
using SteamEdit.Data;

namespace SteamEdit;

internal static class Program
{
	private static MainWindow MainWindow_;

	private static bool ApplyUpdatesOnExit = false;

	private static bool IsUpdateRunning_ = false;

	private static object IsUpdateRunningLock_ = new object();

	private static bool IsUpdateRunning
	{
		get
		{
			lock (IsUpdateRunningLock_)
			{
				return IsUpdateRunning_;
			}
		}
		set
		{
			lock (IsUpdateRunningLock_)
			{
				IsUpdateRunning_ = value;
			}
		}
	}

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
			WaitForUpdateToFinish(3u);
			if (ApplyUpdatesOnExit)
			{
				return false;
			}
			MessageBoxWithDetails.Show("Unknown version while trying to load appinfo.vdf. Either we're trying to load the wrong file, or maybe Steam has updated something, and a fix is needed for SteamEdit", "SteamEdit: Critical Error", $"AppInfo Path: {ex2.AppInfoPath}\r\nDetected Version: {ex2.AppInfoVersion:X8}");
			return false;
		}
		catch (AppInfoLoadException ex3)
		{
			WaitForUpdateToFinish(3u);
			if (ApplyUpdatesOnExit)
			{
				return false;
			}
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
			if (Settings.Default.EnableAutoUpdate)
			{
				StartUpdateCheck();
			}
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
		if (!ApplyUpdatesOnExit)
		{
			return;
		}
		Log.WriteLine("[Update] Applying updates.");
		UpdateManager instance = UpdateManager.Instance;
		try
		{
			Log.WriteLine("[Update] Update applied!");
			instance.ApplyUpdates(relaunchApplication: true, updaterDoLogging: true, updaterShowConsole: false);
		}
		catch (Exception ex)
		{
			MessageBoxWithDetails.Show("Error while trying to install software updates.", "SteamEdit Update failed", ex.ToString());
			instance.RollbackUpdates();
		}
		finally
		{
			instance.CleanUp();
		}
	}

	private static void StartUpdateCheck()
	{
		UpdateManager instance = UpdateManager.Instance;
		if (Settings.Default.AutoUpdateFeed.Length > 0)
		{
			string autoUpdateFeed = Settings.Default.AutoUpdateFeed;
			if (!autoUpdateFeed.EndsWith("/"))
			{
				if (autoUpdateFeed.EndsWith(".xml", StringComparison.InvariantCultureIgnoreCase))
				{
					int num = autoUpdateFeed.LastIndexOf('/');
					autoUpdateFeed = ((num <= 0) ? "" : autoUpdateFeed.Substring(0, num + 1));
				}
				else
				{
					autoUpdateFeed += "/";
				}
				Settings.Default.AutoUpdateFeed = autoUpdateFeed;
				Settings.Default.Save();
			}
		}
		string text = ((Settings.Default.AutoUpdateFeed.Length <= 0) ? "http://files.tg-software.com/steam_edit/" : Settings.Default.AutoUpdateFeed);
		string feedUrl = text + "updates.xml";
		instance.UpdateSource = new SimpleWebSource(feedUrl);
		instance.Config.TempFolder = Path.Combine(Path.GetTempPath(), "SteamEdit\\Updates");
		instance.ReinstateIfRestarted();
		if (instance.State != UpdateManager.UpdateProcessState.AfterRestart)
		{
			try
			{
				Log.WriteLine("[Update] Checking for updates...");
				IsUpdateRunning = true;
				instance.BeginCheckForUpdates(OnCheckForUpdatesCompleted, null);
			}
			catch (NAppUpdateException)
			{
				instance.CleanUp();
			}
			catch (InvalidOperationException)
			{
				instance.CleanUp();
			}
		}
	}

	private static void OnCheckForUpdatesCompleted(IAsyncResult asyncResult)
	{
		if (asyncResult.IsCompleted)
		{
			UpdateManager instance = UpdateManager.Instance;
			try
			{
				((UpdateProcessAsyncResult)asyncResult).EndInvoke();
			}
			catch (Exception ex)
			{
				Log.WriteLine("[Update] Update check failed: {0}", ex.ToString());
				instance.CleanUp();
				IsUpdateRunning = false;
				return;
			}
			if (instance.UpdatesAvailable == 0)
			{
				Log.WriteLine("[Update] Nothing to update!");
				instance.CleanUp();
				IsUpdateRunning = false;
			}
			else
			{
				Log.WriteLine("[Update] Update available! Preparing {0} files...", instance.UpdatesAvailable);
				instance.BeginPrepareUpdates(OnPrepareUpdatesCompleted, null);
			}
		}
	}

	private static void OnPrepareUpdatesCompleted(IAsyncResult asyncResult)
	{
		UpdateManager instance = UpdateManager.Instance;
		try
		{
			((UpdateProcessAsyncResult)asyncResult).EndInvoke();
		}
		catch (Exception ex)
		{
			MessageBoxWithDetails.Show("Updates preperation failed. You can continue using SteamEdit.", "Update failed", ex.ToString());
			instance.CleanUp();
			IsUpdateRunning = false;
			return;
		}
		Log.WriteLine("[Update] Update prepared.");
		if (MessageBox.Show("Updates are ready to install. Do you wish to install them now?", "SteamEdit updates ready", MessageBoxButtons.YesNo) != DialogResult.Yes)
		{
			IsUpdateRunning = false;
			if (MainWindow_ != null)
			{
				MainWindow_.BeginInvoke((MethodInvoker)delegate
				{
					MainWindow_.Activate();
					MainWindow_.BringToFront();
				});
			}
		}
		else
		{
			ApplyUpdatesOnExit = true;
			IsUpdateRunning = false;
			Application.Exit();
		}
	}

	private static void WaitForUpdateToFinish(uint TimeoutSeconds)
	{
		if (IsUpdateRunning)
		{
			UpdateManager instance = UpdateManager.Instance;
			Log.WriteLine("Waiting for auto-update to finish...");
			DateTime dateTime = DateTime.Now.AddSeconds(TimeoutSeconds);
			while (IsUpdateRunning && (!instance.IsWorking || !(DateTime.Now >= dateTime)))
			{
				Thread.Sleep(1);
			}
		}
	}

	private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		WaitForUpdateToFinish(3u);
		MessageBoxWithDetails.Show("Unhandled exception. Not sure what went wrong, but you might be able to continue working.", "SteamEdit: Unhandled exception", e.ExceptionObject.ToString());
	}

	private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
	{
		WaitForUpdateToFinish(3u);
		MessageBoxWithDetails.Show("Unhandled exception. Not sure what went wrong, but you might be able to continue working.", "SteamEdit: Unhandled exception", e.Exception.ToString());
	}
}
