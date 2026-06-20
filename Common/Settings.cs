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

	private T Get<T>(string key) => (T)_values[key];

	private void Set<T>(string key, T value)
	{
		_values[key] = value!;
		MarkDirty();
	}

	[DefaultValue(true)]
	public bool EnableCompleted
	{
		get => Get<bool>("EnableCompleted");
		set => Set("EnableCompleted", value);
	}

	[DefaultValue(true)]
	public bool EnableHidden
	{
		get => Get<bool>("EnableHidden");
		set => Set("EnableHidden", value);
	}

	[DefaultValue(false)]
	public bool ReplaceLocalizedNames
	{
		get => Get<bool>("ReplaceLocalizedNames");
		set => Set("ReplaceLocalizedNames", value);
	}

	[DefaultValue("")]
	public string PassThroughArguments
	{
		get => Get<string>("PassThroughArguments");
		set => Set("PassThroughArguments", value);
	}

	[DefaultValue(AppInfoEntryType.Game | AppInfoEntryType.Application | AppInfoEntryType.Video)]
	public AppInfoEntryType Filters
	{
		get => Get<AppInfoEntryType>("Filters");
		set => Set("Filters", value);
	}

	[DefaultValue(ShowEntriesMode.All)]
	public ShowEntriesMode FiltersShowEntries
	{
		get => Get<ShowEntriesMode>("FiltersShowEntries");
		set => Set("FiltersShowEntries", value);
	}

	[DefaultValue("")]
	public string AppInfoPath
	{
		get => Get<string>("AppInfoPath");
		set => Set("AppInfoPath", value);
	}

	[DefaultValue("")]
	public string ModsLocation
	{
		get => Get<string>("ModsLocation");
		set => Set("ModsLocation", value);
	}

	[DefaultValue("")]
	public string ShownVersion
	{
		get => Get<string>("ShownVersion");
		set => Set("ShownVersion", value);
	}

	[DefaultValue(int.MaxValue)]
	public int WindowLocationX
	{
		get => Get<int>("WindowLocationX");
		set => Set("WindowLocationX", value);
	}

	[DefaultValue(int.MaxValue)]
	public int WindowLocationY
	{
		get => Get<int>("WindowLocationY");
		set => Set("WindowLocationY", value);
	}

	[DefaultValue(int.MaxValue)]
	public int WindowSizeX
	{
		get => Get<int>("WindowSizeX");
		set => Set("WindowSizeX", value);
	}

	[DefaultValue(int.MaxValue)]
	public int WindowSizeY
	{
		get => Get<int>("WindowSizeY");
		set => Set("WindowSizeY", value);
	}

	[DefaultValue(int.MaxValue)]
	public int MainWindowSplitter
	{
		get => Get<int>("MainWindowSplitter");
		set => Set("MainWindowSplitter", value);
	}

	[DefaultValue(false)]
	public bool AutoStart
	{
		get => Get<bool>("AutoStart");
		set => Set("AutoStart", value);
	}

	[DefaultValue(false)]
	public bool AutoStartAutoFix
	{
		get => Get<bool>("AutoStartAutoFix");
		set => Set("AutoStartAutoFix", value);
	}

	[DefaultValue(false)]
	public bool AutoStartForceStart
	{
		get => Get<bool>("AutoStartForceStart");
		set => Set("AutoStartForceStart", value);
	}

	[DefaultValue("")]
	public string Language
	{
		get => Get<string>("Language");
		set => Set("Language", value);
	}

	[DefaultValue("")]
	public string AiApiUrl
	{
		get => Get<string>("AiApiUrl");
		set => Set("AiApiUrl", value);
	}

	[DefaultValue("")]
	public string AiApiKey
	{
		get => Get<string>("AiApiKey");
		set => Set("AiApiKey", value);
	}

	[DefaultValue("gpt-4o-mini")]
	public string AiModel
	{
		get => Get<string>("AiModel");
		set => Set("AiModel", value);
	}

	[DefaultValue("Google")]
	public string SearchEngine
	{
		get => Get<string>("SearchEngine");
		set => Set("SearchEngine", value);
	}

	[DefaultValue("zh-CN")]
	public string AiTargetLanguage
	{
		get => Get<string>("AiTargetLanguage");
		set => Set("AiTargetLanguage", value);
	}

	[DefaultValue("")]
	public string AiCustomSystemPrompt
	{
		get => Get<string>("AiCustomSystemPrompt");
		set => Set("AiCustomSystemPrompt", value);
	}

	[DefaultValue("")]
	public string AiCustomUserMessage
	{
		get => Get<string>("AiCustomUserMessage");
		set => Set("AiCustomUserMessage", value);
	}

	[DefaultValue("")]
	public string AiTranslationRules
	{
		get => Get<string>("AiTranslationRules");
		set => Set("AiTranslationRules", value);
	}

	[DefaultValue(1)]
	public int AiBatchTranslateConcurrency
	{
		get => Get<int>("AiBatchTranslateConcurrency");
		set => Set("AiBatchTranslateConcurrency", Math.Max(value, 1));
	}

	[DefaultValue("Name")]
	public string SortField
	{
		get => Get<string>("SortField");
		set => Set("SortField", value);
	}

	[DefaultValue(false)]
	public bool SortDescending
	{
		get => Get<bool>("SortDescending");
		set => Set("SortDescending", value);
	}

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

		// SaveJson 将所有值存为字符串，所以需要先尝试字符串解析
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

			var readXmlMethod = GetType().GetMethod("ReadXml", BindingFlags.Instance | BindingFlags.NonPublic);
			if (readXmlMethod == null) return;
			foreach (PropertyInfo propertyInfo in _cachedProperties)
			{
				readXmlMethod.MakeGenericMethod(propertyInfo.PropertyType).Invoke(this, new object[2] { documentElement, propertyInfo.Name });
			}
		}
		catch (Exception ex)
		{
			Log.WriteLine("Settings: XML parse error, using defaults: {0}", ex.Message);
		}
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

	private void ReadXml<T>(XmlElement root, string name)
	{
		PropertyInfo? property = GetType().GetProperty(name);
		if (property != null)
		{
			property.SetValue(this, ParseXml(root, name, (T)property.GetValue(this, null)!), null);
		}
	}

	private static T ParseXml<T>(XmlElement root, string name, T defaultValue)
	{
		T result = defaultValue;
		if (root[name] != null)
		{
			Type[] types = new Type[2]
			{
				typeof(string),
				typeof(T).MakeByRefType()
			};
			MethodInfo? method = typeof(T).GetMethod("TryParse", types);
			if (method != null)
			{
				object?[] array = new object?[2]
				{
					root[name]!.InnerText,
					null
				};
				if (method.Invoke(null, array) is true)
				{
					result = (T)array[1]!;
				}
			}
			else if (typeof(T).IsEnum)
			{
				try
				{
					result = (T)Enum.Parse(typeof(T), root[name]!.InnerText);
				}
				catch (Exception ex) when (ex is ArgumentException or OverflowException)
				{
				}
			}
			else if (typeof(T).IsAssignableFrom(typeof(string)))
			{
				result = (T)(object)root[name]!.InnerText;
			}
		}
		return result;
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
