#nullable enable
using System;
using System.IO;
using System.Threading;
using Common;

namespace SteamEdit.Data;

internal class SteamData : ISteamDataService
{
	public const string CompletedChar = "✓";
	public static string CompletedSuffix => " ✓";

	/// <summary>
	/// 全局单例访问器。由 Program.Main 在初始化时赋值。
	/// </summary>
	public static SteamData Instance { get; set; } = null!;

	private readonly object _lock = new();
	private SteamDataInitProps _initProps;
	private Mods _modData = null!;
	private volatile bool _needsSave;
	private volatile bool _needsApply;

	/// <summary>
	/// 文件系统抽象，默认使用真实文件系统。测试时可替换为 mock。
	/// </summary>
	public IFileSystem FileSystem { get; set; } = RealFileSystem.Instance;

	public string SteamDir { get; private set; } = "";

	public string AppInfoPath { get; private set; } = "";

	public string ModInfoPath { get; private set; } = "";

	public bool UseCompleted
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

	public bool UseHidden
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

	public IAppInfo AppInfoData { get; private set; } = null!;

#pragma warning disable CS0420 // Volatile.Read/Write 正确处理 volatile 字段，此警告为误报
	public bool NeedsSave
	{
		get => Volatile.Read(ref _needsSave);
		private set
		{
			bool changed;
			lock (_lock) { changed = Volatile.Read(ref _needsSave) != value; if (changed) Volatile.Write(ref _needsSave, value); }
			if (changed) { OnNeedsSave?.Invoke(this, EventArgs.Empty); if (value) NeedsApply = true; }
		}
	}

	public bool NeedsApply
	{
		get => Volatile.Read(ref _needsApply);
		private set
		{
			bool changed;
			lock (_lock) { changed = Volatile.Read(ref _needsApply) != value; if (changed) Volatile.Write(ref _needsApply, value); }
			if (changed) OnNeedsApply?.Invoke(this, EventArgs.Empty);
		}
	}
#pragma warning restore CS0420

	public event EventHandler? OnNeedsSave;

	public event EventHandler? OnNeedsApply;

	public IEntryDisplaySettings DisplaySettings { get; } = new DisplaySettingsImpl();

	public IAppInfoDataStore DataStore { get; } = new DataStoreImpl();

	private sealed class DisplaySettingsImpl : IEntryDisplaySettings
	{
		public string SteamDir => Instance.SteamDir;
		public bool UseCompleted => Instance.UseCompleted;
		public string CompletedSuffix => SteamData.CompletedSuffix;
		public bool UseHidden => Instance.UseHidden;
	}

	private sealed class DataStoreImpl : IAppInfoDataStore
	{
		public IAppInfo AppInfo => Instance.AppInfoData;
	}

	public void Initialize(ref SteamDataInitProps props)
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
		SteamEdit.Data.Impl.AppInfoEntryBase.DisplaySettings = DisplaySettings;
	}

	private void OnEntryModified(object? sender, EventArgs e)
	{
		NeedsSave = true;
	}

	public void Shutdown()
	{
		if (AppInfoData != null)
		{
			AppInfoData.Modified -= OnEntryModified;
		}
	}

	public void Save()
	{
		Log.WriteLine("Saving Mods:");
		_modData.Save(ModInfoPath, FileSystem);
		Log.WriteLine("  Mods saved");
		NeedsSave = false;
	}

	public void Apply()
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
