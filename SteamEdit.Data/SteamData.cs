#nullable enable
using System;
using System.IO;
using Common;

namespace SteamEdit.Data;

internal static class SteamData
{
	public const string CompletedChar = "✓";

	private static SteamDataInitProps _initProps;

	private static Mods _modData = null!;

	private static bool _needsSave;

	private static bool _needsApply;

	public static string CompletedSuffix => " ✓";

	public static string SteamDir { get; private set; } = "";

	public static string AppInfoPath { get; private set; } = "";

	public static string ModInfoPath { get; private set; } = "";

	public static bool UseCompleted
	{
		get
		{
			return _initProps.EnableCompleted;
		}
		set
		{
			if (_initProps.EnableCompleted != value)
			{
				_initProps.EnableCompleted = value;
				NeedsSave = true;
			}
		}
	}

	public static bool UseHidden
	{
		get
		{
			return _initProps.EnableHidden;
		}
		set
		{
			if (_initProps.EnableHidden != value)
			{
				_initProps.EnableHidden = value;
				NeedsSave = true;
			}
		}
	}

	public static IAppInfo AppInfoData { get; private set; } = null!;

	public static bool NeedsSave
	{
		get
		{
			return _needsSave;
		}
		private set
		{
			if (_needsSave != value)
			{
				_needsSave = value;
				SteamData.OnNeedsSave?.Invoke(null, EventArgs.Empty);
				NeedsApply = true;
			}
		}
	}

	public static bool NeedsApply
	{
		get
		{
			return _needsApply;
		}
		private set
		{
			if (_needsApply != value)
			{
				_needsApply = value;
				SteamData.OnNeedsApply?.Invoke(null, EventArgs.Empty);
			}
		}
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
		catch (UnknownAppInfoVersionException)
		{
			throw;
		}
		catch (Exception e)
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
	}

	public static void Save()
	{
		Log.WriteLine("Saving Mods:");
		string text = ModInfoPath + ".bak";
		if (File.Exists(ModInfoPath))
		{
			File.Copy(ModInfoPath, text, overwrite: true);
		}
		try
		{
			_modData.Save(ModInfoPath);
		}
		catch (Exception ex)
		{
			Log.WriteLine("Save failed: {0}", ex.ToString());
			File.Copy(text, ModInfoPath, overwrite: true);
			throw;
		}
		Log.WriteLine("  Mods saved");
		NeedsSave = false;
	}

	public static void Apply()
	{
		Log.WriteLine("Saving SteamData:");
		AppInfoData.Save(AppInfoPath);
		Log.WriteLine("  AppInfo saved");
		NeedsApply = false;
	}
}
