using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Xml;
using SteamEdit.Data;

namespace Common;

internal class Settings
{
	public static readonly Settings Default = new Settings();

	private readonly string _settingsFilePath;

	private readonly Dictionary<string, object> _values;

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
		}
	}

	private Settings()
	{
		_values = new Dictionary<string, object>();
		PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			DefaultValueAttribute defaultValueAttribute = (DefaultValueAttribute)Attribute.GetCustomAttribute(propertyInfo, typeof(DefaultValueAttribute));
			if (defaultValueAttribute != null)
			{
				propertyInfo.SetValue(this, defaultValueAttribute.Value, null);
			}
		}
		_settingsFilePath = Path.Combine(AppHelper.GetAppDirectory(), "Settings.xml");
		if (!File.Exists(_settingsFilePath))
		{
			return;
		}
		using XmlReader reader = XmlReader.Create(_settingsFilePath);
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.Load(reader);
		XmlElement documentElement = xmlDocument.DocumentElement;
		array = properties;
		foreach (PropertyInfo propertyInfo2 in array)
		{
			GetType().GetMethod("ReadXml", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(propertyInfo2.PropertyType).Invoke(this, new object[2] { documentElement, propertyInfo2.Name });
		}
	}

	public void Save()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath));
		XmlWriterSettings settings = new XmlWriterSettings
		{
			Indent = true
		};
		using XmlWriter xmlWriter = XmlWriter.Create(_settingsFilePath, settings);
		xmlWriter.WriteStartDocument();
		xmlWriter.WriteStartElement("Settings");
		PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
		foreach (PropertyInfo propertyInfo in properties)
		{
			xmlWriter.WriteStartElement(propertyInfo.Name);
			xmlWriter.WriteValue(propertyInfo.GetValue(this, null).ToString());
			xmlWriter.WriteEndElement();
		}
		xmlWriter.WriteEndElement();
		xmlWriter.WriteEndDocument();
	}

	private void ReadXml<T>(XmlElement root, string name)
	{
		PropertyInfo property = GetType().GetProperty(name);
		if (property != null)
		{
			property.SetValue(this, ParseXml(root, name, (T)property.GetValue(this, null)), null);
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
			MethodInfo method = typeof(T).GetMethod("TryParse", types);
			if (method != null)
			{
				object[] array = new object[2]
				{
					root[name].InnerText,
					null
				};
				if ((bool)method.Invoke(null, array))
				{
					result = (T)array[1];
				}
			}
			else if (typeof(T).IsEnum)
			{
				try
				{
					result = (T)Enum.Parse(typeof(T), root[name].InnerText);
				}
				catch (Exception)
				{
				}
			}
			else if (typeof(T).IsAssignableFrom(typeof(string)))
			{
				result = (T)(object)root[name].InnerText;
			}
		}
		return result;
	}
}
