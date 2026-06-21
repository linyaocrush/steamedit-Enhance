#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace Common;

internal class Settings
{
	private static readonly PropertyInfo[] _cachedProperties = typeof(Settings).GetProperties(BindingFlags.Instance | BindingFlags.Public);
	public static readonly Settings Default = new Settings();

	private readonly Dictionary<string, object> _values;

	private bool _isDirty;
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
		_values[key] = value!;
		MarkDirty();
	}

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

	[DefaultValue(1)]
	public int AiBatchTranslateConcurrency { get => Get<int>(); set => Set(Math.Max(value, 1)); }

	[DefaultValue("Name")]
	public string SortField { get => Get<string>(); set => Set(value); }

	[DefaultValue(false)]
	public bool SortDescending { get => Get<bool>(); set => Set(value); }

	private Settings()
	{
		_values = new Dictionary<string, object>();
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
		string xmlPath = GetXmlFilePath();

		if (File.Exists(jsonPath))
		{
			LoadFromJson(jsonPath);
		}
		else if (File.Exists(xmlPath))
		{
			LoadFromXml(xmlPath);
			// 迁移：直接写出 JSON 并删除旧 XML
			try
			{
				SaveJson(jsonPath);
				File.Delete(xmlPath);
				Log.WriteLine("Settings: Migrated from XML to JSON");
			}
			catch (Exception ex)
			{
				Log.WriteLine("Settings: XML to JSON migration failed: {0}", ex.Message);
			}
		}

		_suppressDirtyTracking = false;
		_isDirty = false;
	}

	private void LoadFromJson(string jsonPath)
	{
		try
		{
			string json = File.ReadAllText(jsonPath);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			foreach (PropertyInfo prop in _cachedProperties)
			{
				if (!root.TryGetProperty(prop.Name, out JsonElement element))
					continue;

				try
				{
					object? value = DeserializeJsonElement(element, prop.PropertyType);
					if (value != null)
					{
						prop.SetValue(this, value, null);
					}
				}
				catch (Exception ex)
				{
					Log.WriteLine("Settings: Failed to load property '{0}': {1}", prop.Name, ex.Message);
				}
			}
		}
		catch (Exception ex)
		{
			Log.WriteLine("Settings: JSON parse error, using defaults: {0}", ex.Message);
		}
	}

	private static object? DeserializeJsonElement(JsonElement element, Type targetType)
	{
		if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
			return null;

		// 兼容旧版 JSON（值存为字符串）和新版（原生类型）
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

		// 原生 JSON 类型（直接加载的 JSON 或未来可能的原生序列化）
		if (targetType == typeof(bool) && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
			return element.GetBoolean();
		if (targetType == typeof(int) && element.ValueKind == JsonValueKind.Number)
			return element.GetInt32();
		if (targetType == typeof(string))
			return element.GetRawText();
		return null;
	}

	private void LoadFromXml(string xmlPath)
	{
		try
		{
			using XmlReader reader = XmlReader.Create(xmlPath);
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(reader);
			XmlElement? documentElement = xmlDocument.DocumentElement;
			if (documentElement == null) return;

			foreach (PropertyInfo propertyInfo in _cachedProperties)
			{
				XmlElement? element = documentElement[propertyInfo.Name];
				if (element == null) continue;

				string text = element.InnerText;
				object? currentValue = propertyInfo.GetValue(this, null);
				object? parsed = ParseXmlValue(text, propertyInfo.PropertyType, currentValue);
				if (parsed != null)
				{
					propertyInfo.SetValue(this, parsed, null);
				}
			}
		}
		catch (Exception ex)
		{
			Log.WriteLine("Settings: XML parse error, using defaults: {0}", ex.Message);
		}
	}

	private static object? ParseXmlValue(string text, Type targetType, object? defaultValue)
	{
		if (targetType == typeof(string))
			return text;

		if (targetType == typeof(bool) && bool.TryParse(text, out var b))
			return b;
		if (targetType == typeof(int) && int.TryParse(text, out var i))
			return i;

		if (targetType.IsEnum)
		{
			try { return Enum.Parse(targetType, text); }
			catch (Exception ex) when (ex is ArgumentException or OverflowException) { }
		}

		return defaultValue;
	}

	private void SaveJson(string path)
	{
		string? dir = Path.GetDirectoryName(path);
		if (dir != null) Directory.CreateDirectory(dir);
		var dict = new Dictionary<string, object?>();
		foreach (PropertyInfo prop in _cachedProperties)
		{
			dict[prop.Name] = prop.GetValue(this, null);
		}
		string json = JsonSerializer.Serialize(dict, _jsonOptions);
		string tempPath = path + ".tmp";
		File.WriteAllText(tempPath, json);
		File.Move(tempPath, path, overwrite: true);
	}

	public void Save()
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

	private string GetJsonFilePath()
	{
		return Path.Combine(AppHelper.GetAppDirectory(), "Settings.json");
	}

	private void MarkDirty()
	{
		if (!_suppressDirtyTracking)
			_isDirty = true;
	}

	private string GetXmlFilePath()
	{
		return Path.Combine(AppHelper.GetAppDirectory(), "Settings.xml");
	}
}
