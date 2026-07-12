#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common;

internal class Settings : ISettings
{
	private static readonly PropertyInfo[] _cachedProperties = typeof(Settings).GetProperties(BindingFlags.Instance | BindingFlags.Public);
	public static readonly Settings Default = new Settings();

	private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _values;

	private readonly object _saveLock = new();
	private volatile bool _isDirty;
	private bool _suppressDirtyTracking;

	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

	private T Get<T>([System.Runtime.CompilerServices.CallerMemberName] string key = "") =>
		_values.TryGetValue(key, out var val) ? (T)val : default!;

	private void Set<T>(T value, [System.Runtime.CompilerServices.CallerMemberName] string key = "")
	{
		if (_values.TryGetValue(key, out var existing) && existing is T typed && EqualityComparer<T>.Default.Equals(typed, value))
			return;
		_values[key] = value!;
		MarkDirty();
	}

	// -- 显示设置 --
	[DefaultValue(true)]
	public bool EnableCompleted { get => Get<bool>(); set => Set(value); }

	[DefaultValue(true)]
	public bool EnableHidden { get => Get<bool>(); set => Set(value); }

	[DefaultValue(false)]
	public bool ReplaceLocalizedNames { get => Get<bool>(); set => Set(value); }

	[DefaultValue("")]
	public string PassThroughArguments { get => Get<string>(); set => Set(value); }

	[DefaultValue(AppInfoEntryType.Game | AppInfoEntryType.Application | AppInfoEntryType.Video)]
	public AppInfoEntryType Filters { get => Get<AppInfoEntryType>(); set => Set(value); }

	[DefaultValue(ShowEntriesMode.All)]
	public ShowEntriesMode FiltersShowEntries { get => Get<ShowEntriesMode>(); set => Set(value); }

	[DefaultValue("")]
	public string AppInfoPath { get => Get<string>(); set => Set(value); }

	[DefaultValue("")]
	public string ModsLocation { get => Get<string>(); set => Set(value); }

	[DefaultValue("")]
	public string ShownVersion { get => Get<string>(); set => Set(value); }

	// -- 窗口设置 --
	[DefaultValue(int.MaxValue)]
	public int WindowLocationX { get => Get<int>(); set => Set(value); }

	[DefaultValue(int.MaxValue)]
	public int WindowLocationY { get => Get<int>(); set => Set(value); }

	[DefaultValue(int.MaxValue)]
	public int WindowSizeX { get => Get<int>(); set => Set(value); }

	[DefaultValue(int.MaxValue)]
	public int WindowSizeY { get => Get<int>(); set => Set(value); }

	[DefaultValue(int.MaxValue)]
	public int MainWindowSplitter { get => Get<int>(); set => Set(value); }

	[DefaultValue(false)]
	public bool AutoStart { get => Get<bool>(); set => Set(value); }

	[DefaultValue(false)]
	public bool AutoStartAutoFix { get => Get<bool>(); set => Set(value); }

	[DefaultValue(false)]
	public bool AutoStartForceStart { get => Get<bool>(); set => Set(value); }

	[DefaultValue("")]
	public string Language { get => Get<string>(); set => Set(value); }

	// -- AI 翻译设置 --
	[DefaultValue("")]
	public string AiApiUrl { get => Get<string>(); set => Set(value); }

	[DefaultValue("")]
	public string AiApiKey { get => Get<string>(); set => Set(value); }

	[DefaultValue("gpt-4o-mini")]
	public string AiModel { get => Get<string>(); set => Set(value); }

	[DefaultValue("Google")]
	public string SearchEngine { get => Get<string>(); set => Set(value); }

	[DefaultValue("zh-CN")]
	public string AiTargetLanguage { get => Get<string>(); set => Set(value); }

	[DefaultValue("")]
	public string AiCustomSystemPrompt { get => Get<string>(); set => Set(value); }

	[DefaultValue("")]
	public string AiCustomUserMessage { get => Get<string>(); set => Set(value); }

	[DefaultValue("")]
	public string AiTranslationRules { get => Get<string>(); set => Set(value); }

	[DefaultValue(120)]
	public int AiTimeoutSeconds { get => Get<int>(); set => Set(Math.Max(value, 10)); }

	[DefaultValue(3)]
	public int AiBatchTranslateConcurrency { get => Get<int>(); set => Set(Math.Max(value, 1)); }

	// -- 日志设置 --
	[DefaultValue(Log.LogLevel.Info)]
	public Log.LogLevel LogLevel
	{
		get => Get<Log.LogLevel>();
		set { Set(value); Log.SetMinimumLevel(value); }
	}
	
	[DefaultValue(false)]
	public bool EnableDebugLogging { get => Get<bool>(); set => Set(value); }

	// -- 排序设置 --
	[DefaultValue("Name")]
	public string SortField { get => Get<string>(); set => Set(value); }

	[DefaultValue(false)]
	public bool SortDescending { get => Get<bool>(); set => Set(value); }

	private Settings()
	{
		_values = new System.Collections.Concurrent.ConcurrentDictionary<string, object>();
		_suppressDirtyTracking = true;

		// 第一步：应用 [DefaultValue] 默认值
		foreach (PropertyInfo propertyInfo in _cachedProperties)
		{
			DefaultValueAttribute? defaultValueAttribute = Attribute.GetCustomAttribute(propertyInfo, typeof(DefaultValueAttribute)) as DefaultValueAttribute;
			if (defaultValueAttribute != null)
			{
				propertyInfo.SetValue(this, defaultValueAttribute.Value, null);
			}
		}

		// 第二步：尝试加载配置文件
		string jsonPath = GetJsonFilePath();
		if (File.Exists(jsonPath))
		{
			LoadFromJson(jsonPath);
		}

		_suppressDirtyTracking = false;
		_isDirty = false;
	}

	private void LoadFromJson(string jsonPath)
	{
		try
		{
			string json = File.ReadAllText(jsonPath);
			// 优先尝试新嵌套格式
			var data = JsonSerializer.Deserialize<SettingsData>(json, _jsonOptions);
			if (data != null)
				ApplySettingsData(data);
			else
				LoadFlatJson(json);
		}
		catch (JsonException)
		{
			try { LoadFlatJson(File.ReadAllText(jsonPath)); } catch { }
		}
		catch (Exception ex)
		{
			Log.WriteLine("Settings: JSON parse error, using defaults: {0}", ex.Message);
		}
	}

	private void LoadFlatJson(string json)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			DeserializeOldFormat(doc, this);
		}
		catch (Exception ex)
		{
			Log.WriteLine("Settings: Flat JSON parse error: {0}", ex.Message);
		}
	}

	private static void DeserializeOldFormat(JsonDocument doc, Settings target)
	{
		var root = doc.RootElement;
		foreach (PropertyInfo prop in _cachedProperties)
		{
			if (!root.TryGetProperty(prop.Name, out JsonElement element))
				continue;
			try
			{
				object? value = DeserializeJsonElement(element, prop.PropertyType);
				if (value != null)
					prop.SetValue(target, value, null);
			}
			catch (Exception ex)
			{
				Log.WriteLine("Settings: Failed to load property '{0}': {1}", prop.Name, ex.Message);
			}
		}
	}

	private static object? DeserializeJsonElement(JsonElement element, Type targetType)
	{
		if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
			return null;
		if (element.ValueKind == JsonValueKind.String)
		{
			string? str = element.GetString();
			if (targetType == typeof(bool))
				return bool.TryParse(str, out var b) ? b : null;
			if (targetType == typeof(int))
				return int.TryParse(str, out var i) ? i : null;
			if (targetType == typeof(string))
				return str ?? "";
			if (targetType.IsEnum)
			{
				try { return Enum.Parse(targetType, str!); }
				catch (ArgumentException) { return null; }
			}
			return null;
		}
		if (targetType == typeof(bool) && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
			return element.GetBoolean();
		if (targetType == typeof(int) && element.ValueKind == JsonValueKind.Number)
			return element.GetInt32();
		if (targetType == typeof(string))
			return element.GetRawText();
		return null;
	}

	private void ApplySettingsData(SettingsData data)
	{
		var d = data.Display;
		EnableCompleted = d.EnableCompleted;
		EnableHidden = d.EnableHidden;
		ReplaceLocalizedNames = d.ReplaceLocalizedNames;
		PassThroughArguments = d.PassThroughArguments;
		if (Enum.TryParse<AppInfoEntryType>(d.Filters, out var filters)) Filters = filters;
		if (Enum.TryParse<ShowEntriesMode>(d.FiltersShowEntries, out var show)) FiltersShowEntries = show;
		AppInfoPath = d.AppInfoPath;
		ModsLocation = d.ModsLocation;
		ShownVersion = d.ShownVersion;
		AutoStart = d.AutoStart;
		AutoStartAutoFix = d.AutoStartAutoFix;
		AutoStartForceStart = d.AutoStartForceStart;
		Language = d.Language;
		if (Enum.TryParse<Log.LogLevel>(d.LogLevel, out var logLevel)) LogLevel = logLevel;
		EnableDebugLogging = d.EnableDebugLogging;
		SortField = d.SortField;
		SortDescending = d.SortDescending;
		SearchEngine = d.SearchEngine;

		var w = data.Window;
		WindowLocationX = w.LocationX;
		WindowLocationY = w.LocationY;
		WindowSizeX = w.SizeX;
		WindowSizeY = w.SizeY;
		MainWindowSplitter = w.Splitter;

		var a = data.Ai;
		AiApiUrl = a.ApiUrl;
		AiApiKey = a.ApiKey;
		AiModel = a.Model;
		AiTargetLanguage = a.TargetLanguage;
		AiCustomSystemPrompt = a.CustomSystemPrompt;
		AiCustomUserMessage = a.CustomUserMessage;
		AiTranslationRules = a.TranslationRules;
		AiTimeoutSeconds = a.TimeoutSeconds;
		AiBatchTranslateConcurrency = a.BatchTranslateConcurrency;
	}

	private SettingsData ToSettingsData()
	{
		return new SettingsData
		{
			Display = new DisplaySettingsData
			{
				EnableCompleted = EnableCompleted,
				EnableHidden = EnableHidden,
				ReplaceLocalizedNames = ReplaceLocalizedNames,
				PassThroughArguments = PassThroughArguments,
				Filters = Filters.ToString(),
				FiltersShowEntries = FiltersShowEntries.ToString(),
				AppInfoPath = AppInfoPath,
				ModsLocation = ModsLocation,
				ShownVersion = ShownVersion,
				AutoStart = AutoStart,
				AutoStartAutoFix = AutoStartAutoFix,
				AutoStartForceStart = AutoStartForceStart,
				Language = Language,
				LogLevel = LogLevel.ToString(),
				EnableDebugLogging = EnableDebugLogging,
				SortField = SortField,
				SortDescending = SortDescending,
				SearchEngine = SearchEngine,
			},
			Window = new WindowSettingsData
			{
				LocationX = WindowLocationX,
				LocationY = WindowLocationY,
				SizeX = WindowSizeX,
				SizeY = WindowSizeY,
				Splitter = MainWindowSplitter,
			},
			Ai = new AiSettingsData
			{
				ApiUrl = AiApiUrl,
				ApiKey = AiApiKey,
				Model = AiModel,
				TargetLanguage = AiTargetLanguage,
				CustomSystemPrompt = AiCustomSystemPrompt,
				CustomUserMessage = AiCustomUserMessage,
				TranslationRules = AiTranslationRules,
				TimeoutSeconds = AiTimeoutSeconds,
				BatchTranslateConcurrency = AiBatchTranslateConcurrency,
			},
		};
	}

	private void SaveJson(string path)
	{
		string? dir = Path.GetDirectoryName(path);
		if (dir != null) Directory.CreateDirectory(dir);
		string json = JsonSerializer.Serialize(ToSettingsData(), _jsonOptions);
		string tempPath = path + ".tmp";
		File.WriteAllText(tempPath, json);
		File.Move(tempPath, path, overwrite: true);
	}

	public void Save()
	{
		if (!_isDirty) return;
		lock (_saveLock)
		{
			if (!_isDirty) return;
			try
			{
				SaveJson(GetJsonFilePath());
				_isDirty = false;
			}
			catch (Exception ex)
			{
				Log.WriteLine("Settings: Save failed: {0}", ex.Message);
			}
		}
	}

	/// <summary>
	/// 重新从 JSON 文件加载设置（仅用于测试，避免测试间状态泄漏）
	/// </summary>
	[System.Diagnostics.Conditional("DEBUG")]
	public void Reload()
	{
		_suppressDirtyTracking = true;
		_values.Clear();
		foreach (PropertyInfo propertyInfo in _cachedProperties)
		{
			DefaultValueAttribute? defaultValueAttribute = Attribute.GetCustomAttribute(propertyInfo, typeof(DefaultValueAttribute)) as DefaultValueAttribute;
			if (defaultValueAttribute != null)
				propertyInfo.SetValue(this, defaultValueAttribute.Value, null);
		}
		string jsonPath = GetJsonFilePath();
		if (File.Exists(jsonPath))
			LoadFromJson(jsonPath);
		_suppressDirtyTracking = false;
		_isDirty = false;
	}

	private string GetJsonFilePath()
	{
		return Path.Combine(AppHelper.GetAppDirectory(), "Settings.json");
	}

	private void MarkDirty()
	{
		if (!_suppressDirtyTracking)
			_isDirty = true;
	}
}
