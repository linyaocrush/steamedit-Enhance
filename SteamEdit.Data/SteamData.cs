#nullable enable
using System;
using System.IO;
using Common;

namespace SteamEdit.Data;

internal static class SteamData
{
	public const string CompletedChar = "✓";

	private static readonly object _lock = new();
	private static SteamDataInitProps _initProps;
	private static Mods _modData = null!;
	private static volatile bool _needsSave;
	private static volatile bool _needsApply;

	/// <summary>
	/// 文件系统抽象，默认使用真实文件系统。测试时可替换为 mock。
	/// </summary>
	internal static IFileSystem FileSystem { get; set; } = RealFileSystem.Instance;

	public static string CompletedSuffix => " ✓";

	public static string SteamDir { get; private set; } = "";

	public static string AppInfoPath { get; private set; } = "";

	public static string ModInfoPath { get; private set; } = "";

	public static bool UseCompleted
	{
		get
		{
			lock (_lock) return _initProps.EnableCompleted;
		}
		set
		{
			lock (_lock)
			{
				if (_initProps.EnableCompleted != value)
				{
					_initProps.EnableCompleted = value;
					NeedsSave = true;
				}
			}
		}
	}

	public static bool UseHidden
	{
		get
		{
			lock (_lock) return _initProps.EnableHidden;
		}
		set
		{
			lock (_lock)
			{
				if (_initProps.EnableHidden != value)
				{
					_initProps.EnableHidden = value;
					NeedsSave = true;
				}
			}
		}
	}

	public static IAppInfo AppInfoData { get; private set; } = null!;

	private static void SetDirtyFlag(ref bool field, EventHandler? handler, bool value, Action? onTrue = null)
	{
		bool changed;
		lock (_lock) { changed = field != value; if (changed) field = value; }
		if (changed) { handler?.Invoke(null, EventArgs.Empty); onTrue?.Invoke(); }
	}

	public static bool NeedsSave
	{
		get => _needsSave;
		private set => SetDirtyFlag(ref _needsSave, OnNeedsSave, value, value ? () => NeedsApply = true : null);
	}

	public static bool NeedsApply
	{
		get => _needsApply;
		private set => SetDirtyFlag(ref _needsApply, OnNeedsApply, value);
	}

	public static event EventHandler? OnNeedsSave;

	public static event EventHandler? OnNeedsApply;

	public static IEntryDisplaySettings DisplaySettings { get; } = new DisplaySettingsImpl();

	public static IAppInfoDataStore DataStore { get; } = new DataStoreImpl();

	private sealed class DisplaySettingsImpl : IEntryDisplaySettings
	{
		public string SteamDir => SteamData.SteamDir;
		public bool UseCompleted => SteamData.UseCompleted;
		public string CompletedSuffix => SteamData.CompletedSuffix;
		public bool UseHidden => SteamData.UseHidden;
	}

	private sealed class DataStoreImpl : IAppInfoDataStore
	{
		public IAppInfo AppInfo => SteamData.AppInfoData;
	}

	public static void Initialize(ref SteamDataInitProps props)
	{
		_initProps = props;
		AppInfoPath = _initProps.AppInfoPath;
		SteamDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(AppInfoPath) ?? "", "..\\"));
		Log.WriteLine("Initializing SteamData:");
		Log.WriteLine("  AppInfoPath: {0}", _initProps.AppInfoPath);
		Log.WriteLine("  DataDirectory: {0}", _initProps.DataDirectory);
		Log.WriteLine("  Loading AppInfo: {0}", AppInfoPath);
		try
		{
			AppInfoData = AppInfoFactory.Load(AppInfoPath);
		}
		catch (Exception e) when (e is not UnknownAppInfoVersionException)
		{
			throw new AppInfoLoadException($"Exception caught while loading appinfo.vdf from '{AppInfoPath}'", e);
		}
		NeedsApply = false;
		AppInfoData.Modified += OnEntryModified;
		ModInfoPath = Path.GetFullPath(Path.Combine(_initProps.DataDirectory, "mods.dat"));
		Log.WriteLine("  Loading Mods: {0}", ModInfoPath);
		_modData = Mods.Load(ModInfoPath, AppInfoData);
		Log.WriteLine("  SteamData initialized!");
		NeedsSave = false;
	}

	private static void OnEntryModified(object? sender, EventArgs e)
	{
		NeedsSave = true;
	}

	public static void Shutdown()
	{
		if (AppInfoData != null)
		{
			AppInfoData.Modified -= OnEntryModified;
		}
	}

	public static void Save()
	{
		Log.WriteLine("Saving Mods:");
		_modData.Save(ModInfoPath, FileSystem);
		Log.WriteLine("  Mods saved");
		NeedsSave = false;
	}

	public static void Apply()
	{
		Log.WriteLine("Saving SteamData:");
		string backupPath = AppInfoPath + ".bak";
		try
		{
			if (FileSystem.FileExists(AppInfoPath))
			{
				FileSystem.FileCopy(AppInfoPath, backupPath, overwrite: true);
			}
			AppInfoData.Save(AppInfoPath);
			Log.WriteLine("  AppInfo saved");
			NeedsApply = false;
		}
		catch (Exception ex)
		{
			Log.WriteLine("Apply failed: {0}", ex.ToString());
			try
			{
				if (FileSystem.FileExists(backupPath))
				{
					FileSystem.FileCopy(backupPath, AppInfoPath, overwrite: true);
					Log.WriteLine("  AppInfo restored from backup");
				}
			}
			catch (Exception restoreEx)
			{
				Log.WriteLine("  AppInfo backup restore also failed: {0}", restoreEx.ToString());
			}
			NeedsApply = true;
			throw;
		}
	}
}
