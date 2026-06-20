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

	[DefaultValue(true)]
	public bool EnableCompleted
	{
		get
		{
			return (bool)_values["EnableCompleted"];
		}
		set
		{
			_values["EnableCompleted"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(true)]
	public bool EnableHidden
	{
		get
		{
			return (bool)_values["EnableHidden"];
		}
		set
		{
			_values["EnableHidden"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(false)]
	public bool ReplaceLocalizedNames
	{
		get
		{
			return (bool)_values["ReplaceLocalizedNames"];
		}
		set
		{
			_values["ReplaceLocalizedNames"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string PassThroughArguments
	{
		get
		{
			return (string)_values["PassThroughArguments"];
		}
		set
		{
			_values["PassThroughArguments"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(AppInfoEntryType.Game | AppInfoEntryType.Application | AppInfoEntryType.Video)]
	public AppInfoEntryType Filters
	{
		get
		{
			return (AppInfoEntryType)_values["Filters"];
		}
		set
		{
			_values["Filters"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(ShowEntriesMode.All)]
	public ShowEntriesMode FiltersShowEntries
	{
		get
		{
			return (ShowEntriesMode)_values["FiltersShowEntries"];
		}
		set
		{
			_values["FiltersShowEntries"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string AppInfoPath
	{
		get
		{
			return (string)_values["AppInfoPath"];
		}
		set
		{
			_values["AppInfoPath"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string ModsLocation
	{
		get
		{
			return (string)_values["ModsLocation"];
		}
		set
		{
			_values["ModsLocation"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string ShownVersion
	{
		get
		{
			return (string)_values["ShownVersion"];
		}
		set
		{
			_values["ShownVersion"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(int.MaxValue)]
	public int WindowLocationX
	{
		get
		{
			return (int)_values["WindowLocationX"];
		}
		set
		{
			_values["WindowLocationX"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(int.MaxValue)]
	public int WindowLocationY
	{
		get
		{
			return (int)_values["WindowLocationY"];
		}
		set
		{
			_values["WindowLocationY"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(int.MaxValue)]
	public int WindowSizeX
	{
		get
		{
			return (int)_values["WindowSizeX"];
		}
		set
		{
			_values["WindowSizeX"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(int.MaxValue)]
	public int WindowSizeY
	{
		get
		{
			return (int)_values["WindowSizeY"];
		}
		set
		{
			_values["WindowSizeY"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(int.MaxValue)]
	public int MainWindowSplitter
	{
		get
		{
			return (int)_values["MainWindowSplitter"];
		}
		set
		{
			_values["MainWindowSplitter"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(false)]
	public bool AutoStart
	{
		get
		{
			return (bool)_values["AutoStart"];
		}
		set
		{
			_values["AutoStart"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(false)]
	public bool AutoStartAutoFix
	{
		get
		{
			return (bool)_values["AutoStartAutoFix"];
		}
		set
		{
			_values["AutoStartAutoFix"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(false)]
	public bool AutoStartForceStart
	{
		get
		{
			return (bool)_values["AutoStartForceStart"];
		}
		set
		{
			_values["AutoStartForceStart"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string Language
	{
		get
		{
			return (string)_values["Language"];
		}
		set
		{
			_values["Language"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string AiApiUrl
	{
		get
		{
			return (string)_values["AiApiUrl"];
		}
		set
		{
			_values["AiApiUrl"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string AiApiKey
	{
		get
		{
			return (string)_values["AiApiKey"];
		}
		set
		{
			_values["AiApiKey"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("gpt-4o-mini")]
	public string AiModel
	{
		get
		{
			return (string)_values["AiModel"];
		}
		set
		{
			_values["AiModel"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("Google")]
	public string SearchEngine
	{
		get
		{
			return (string)_values["SearchEngine"];
		}
		set
		{
			_values["SearchEngine"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("zh-CN")]
	public string AiTargetLanguage
	{
		get
		{
			return (string)_values["AiTargetLanguage"];
		}
		set
		{
			_values["AiTargetLanguage"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string AiCustomSystemPrompt
	{
		get
		{
			return (string)_values["AiCustomSystemPrompt"];
		}
		set
		{
			_values["AiCustomSystemPrompt"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string AiCustomUserMessage
	{
		get
		{
			return (string)_values["AiCustomUserMessage"];
		}
		set
		{
			_values["AiCustomUserMessage"] = value;
			MarkDirty();
		}
	}

	[DefaultValue("")]
	public string AiTranslationRules
	{
		get
		{
			return (string)_values["AiTranslationRules"];
		}
		set
		{
			_values["AiTranslationRules"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(1)]
	public int AiBatchTranslateConcurrency
	{
		get
		{
			return (int)_values["AiBatchTranslateConcurrency"];
		}
		set
		{
			_values["AiBatchTranslateConcurrency"] = Math.Max(value, 1);
			MarkDirty();
		}
	}

	[DefaultValue("Name")]
	public string SortField
	{
		get
		{
			return (string)_values["SortField"];
		}
		set
		{
			_values["SortField"] = value;
			MarkDirty();
		}
	}

	[DefaultValue(false)]
	public bool SortDescending
	{
		get
		{
			return (bool)_values["SortDescending"];
		}
		set
		{
			_values["SortDescending"] = value;
			MarkDirty();
		}
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
		var dict = new Dictionary<string, string>();
		foreach (PropertyInfo prop in _cachedProperties)
		{
			object? value = prop.GetValue(this, null);
			dict[prop.Name] = value?.ToString() ?? "";
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
