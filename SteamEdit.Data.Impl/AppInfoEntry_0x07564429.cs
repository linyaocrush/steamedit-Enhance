using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Common;

namespace SteamEdit.Data.Impl;

internal class AppInfoEntry_0x07564429 : IAppInfoEntry
{
	private string _cachedName;

	private bool? _cachedHasSortAs;

	private string _cachedSortAs;

	private byte[] _stuffBeforeHash;

	private uint _changeNumber;

	private byte[] _originalData;

	private PropertyTable _properties;

	private ModifiedEntry _modifications;

	public uint AppInfoVersion => 123094057u;

	public int ID { get; private set; }

	public uint ChangeNumber => _changeNumber;

	public ModifiedEntry Modification
	{
		get
		{
			return _modifications;
		}
		set
		{
			if (value.OriginalData != null)
			{
				uint originalDataChangeNumber = value.OriginalDataChangeNumber;
				if (originalDataChangeNumber == _changeNumber)
				{
					_originalData = (byte[])value.OriginalData.Clone();
				}
				else
				{
					Log.WriteLine("{0:X8} {1} : ChangeNumber doesn't match. Assuming Steam updated, replacing data in Mod. Appinfo: {2:X8}; Mod: {3:X8}", ID, Name, _changeNumber, originalDataChangeNumber);
					value.OriginalDataVersion = AppInfoVersion;
					value.OriginalDataChangeNumber = ChangeNumber;
					value.OriginalData = _originalData;
				}
			}
			ClearCachedProps();
			_modifications = value;
			if (_modifications.ApplyChanges(_properties))
			{
				this.Modified?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	public bool IsModified => _modifications != null;

	public bool IsHidden
	{
		get
		{
			return _properties.GetPropertyValue(0, "appinfo", "steam_edit", "is_hidden") != 0;
		}
		set
		{
			if (value || _properties.HasProperty("appinfo", "steam_edit", "is_hidden"))
			{
				SetPropertyValue(PropertyType.Int32, value ? 1 : 0, "appinfo", "steam_edit", "is_hidden");
			}
		}
	}

	public bool IsCompleted
	{
		get
		{
			return _properties.GetPropertyValue(0, "appinfo", "steam_edit", "is_completed") != 0;
		}
		set
		{
			if (value || _properties.HasProperty("appinfo", "steam_edit", "is_completed"))
			{
				SetPropertyValue(PropertyType.Int32, value ? 1 : 0, "appinfo", "steam_edit", "is_completed");
			}
		}
	}

	public string Name
	{
		get
		{
			if (_cachedName == null)
			{
				_cachedName = _properties.GetPropertyValue<string>(null, new string[3] { "appinfo", "common", "name" });
			}
			return _cachedName;
		}
		set
		{
			SetPropertyValue(PropertyType.String, value, "appinfo", "common", "name");
			ClearCachedProps();
		}
	}

	public bool HasSortAs
	{
		get
		{
			if (!_cachedHasSortAs.HasValue)
			{
				_cachedHasSortAs = _properties.HasProperty("appinfo", "common", "sortas") && _properties.GetPropertyValue(Name, "appinfo", "common", "sortas").Length > 0;
			}
			return _cachedHasSortAs.Value;
		}
	}

	public string SortAs
	{
		get
		{
			if (_cachedSortAs == null)
			{
				_cachedSortAs = _properties.GetPropertyValue(Name, "appinfo", "common", "sortas");
				if (_cachedSortAs.Length == 0)
				{
					_cachedSortAs = Name;
				}
			}
			return _cachedSortAs;
		}
		set
		{
			SetPropertyValue(PropertyType.String, value, "appinfo", "common", "sortas");
			ClearCachedProps();
		}
	}

	public string InstallPath
	{
		get
		{
			string propertyValue = _properties.GetPropertyValue("", "appinfo", "config", "installdir");
			return Path.Combine(SteamData.SteamDir, "steamapps", "common", propertyValue);
		}
	}

	public AppInfoEntryType Type
	{
		get
		{
			string propertyValue = _properties.GetPropertyValue<string>(null, new string[3] { "appinfo", "common", "type" });
			if (propertyValue == null)
			{
				return AppInfoEntryType.Unknown;
			}
			string text = propertyValue.ToLowerInvariant();
			switch (text)
			{
			case "game":
				return AppInfoEntryType.Game;
			case "config":
				return AppInfoEntryType.Config;
			case "tool":
				return AppInfoEntryType.Tool;
			case "demo":
				return AppInfoEntryType.Demo;
			case "dlc":
				return AppInfoEntryType.DLC;
			case "application":
				return AppInfoEntryType.Application;
			case "video":
				return AppInfoEntryType.Video;
			case "guide":
				return AppInfoEntryType.Guide;
			case "music":
				return AppInfoEntryType.Music;
			case "beta":
				return AppInfoEntryType.Beta;
			default:
				Log.WriteLine("Unrecognized entry type: {0}", text);
				return AppInfoEntryType.Unknown;
			}
		}
		set
		{
			string value2;
			switch (value)
			{
			case AppInfoEntryType.Game:
				value2 = "game";
				break;
			case AppInfoEntryType.Config:
				value2 = "config";
				break;
			case AppInfoEntryType.Tool:
				value2 = "tool";
				break;
			case AppInfoEntryType.Demo:
				value2 = "demo";
				break;
			case AppInfoEntryType.DLC:
				value2 = "dlc";
				break;
			case AppInfoEntryType.Application:
				value2 = "application";
				break;
			case AppInfoEntryType.Video:
				value2 = "video";
				break;
			case AppInfoEntryType.Guide:
				value2 = "guide";
				break;
			case AppInfoEntryType.Music:
				value2 = "music";
				break;
			case AppInfoEntryType.Beta:
				value2 = "beta";
				break;
			default:
				Log.WriteLine("Unrecognized entry type: {0}", value);
				return;
			}
			SetPropertyValue(PropertyType.String, value2, "appinfo", "common", "type");
		}
	}

	public LaunchTable Launches
	{
		get
		{
			return new LaunchTable(GetLaunchPropertyTable());
		}
		set
		{
			SetPropertyValue(PropertyType.Table, value.PropertyTable, "appinfo", "config", "launch");
		}
	}

	public event EventHandler Modified;

	private void ClearCachedProps()
	{
		_cachedName = null;
		_cachedSortAs = null;
		_cachedHasSortAs = null;
	}

	public byte[] GetOriginalDataCopy()
	{
		return (byte[])_originalData.Clone();
	}

	public T GetPropertyValue<T>(T DefaultValue, params string[] PropertyPath)
	{
		return _properties.GetPropertyValue(DefaultValue, PropertyPath);
	}

	public void SetPropertyValue(PropertyType Type, object Value, params string[] PropertyPath)
	{
		PropertyTable propertyTable = _properties;
		foreach (string item in PropertyPath.Take(PropertyPath.Length - 1))
		{
			PropertyTable propertyTable2;
			if (propertyTable.HasProperty(item))
			{
				propertyTable2 = propertyTable.GetPropertyValue<PropertyTable>(item);
			}
			else
			{
				propertyTable2 = new PropertyTable();
				propertyTable.SetPropertyValue(item, PropertyType.Table, propertyTable2);
			}
			propertyTable = propertyTable2 ?? throw new InvalidOperationException("Couldn't get " + PropertyPath.Aggregate((string A, string B) => A + " \\ " + B) + " as a PropertyTable.");
		}
		propertyTable.SetPropertyValue(PropertyPath.Last(), Type, Value);
		if (ModificationsCreate().AddChange(Type, Value, PropertyPath))
		{
			this.Modified?.Invoke(this, EventArgs.Empty);
		}
		ClearCachedProps();
	}

	public void RevertModifiedProperty(params string[] PropertyPath)
	{
		if (_modifications == null || !_modifications.Changes.HasProperty(PropertyPath))
		{
			return;
		}
		_modifications.Changes.RemoveProperty(PropertyPath);
		if (_modifications.OriginalData != null)
		{
			using BinaryReader reader = new BinaryReader(new MemoryStream(_modifications.OriginalData));
			Property rawProperty = PropertyTableIO.ReadPropertyTable_Mods(reader).GetRawProperty(PropertyPath);
			_properties.SetRawProperty(rawProperty, PropertyPath);
		}
		if (_modifications.Changes.Count == 0)
		{
			Revert();
			return;
		}
		ClearCachedProps();
		this.Modified?.Invoke(this, EventArgs.Empty);
	}

	private ModifiedEntry ModificationsCreate()
	{
		if (_modifications == null)
		{
			_modifications = new ModifiedEntry(this);
		}
		return _modifications;
	}

	private PropertyTable GetLaunchPropertyTable()
	{
		PropertyTable propertyTable = _properties.GetPropertyValue<PropertyTable>(null, new string[3] { "appinfo", "config", "launch" });
		if (propertyTable == null)
		{
			propertyTable = new PropertyTable();
		}
		return propertyTable;
	}

	public override string ToString()
	{
		return Name;
	}

	public bool Revert()
	{
		if (_modifications != null)
		{
			if (_modifications.OriginalData == null)
			{
				throw new NotImplementedException();
			}
			using (BinaryReader reader = new BinaryReader(new MemoryStream(_modifications.OriginalData)))
			{
				_properties = PropertyTableIO.ReadPropertyTable_Mods(reader);
			}
			if (_properties != null)
			{
				ClearCachedProps();
				_modifications = null;
				this.Modified?.Invoke(this, EventArgs.Empty);
				return true;
			}
		}
		return false;
	}

	public static AppInfoEntry_0x07564429 FromReader(BinaryReader reader, IList<string> stringTable)
	{
		int num = reader.ReadInt32();
		if (num == 0)
		{
			return null;
		}
		AppInfoEntry_0x07564429 appInfoEntry_0x = new AppInfoEntry_0x07564429
		{
			ID = num
		};
		try
		{
			int count = reader.ReadInt32();
			using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(reader.ReadBytes(count))))
			{
				appInfoEntry_0x._stuffBeforeHash = binaryReader.ReadBytes(16);
				binaryReader.ReadBytes(20);
				appInfoEntry_0x._changeNumber = binaryReader.ReadUInt32();
				binaryReader.ReadBytes(20);
				appInfoEntry_0x._properties = PropertyTableIO.ReadPropertyTable_0x07564429(binaryReader, stringTable);
				string propertyValue = appInfoEntry_0x._properties.GetPropertyValue<string>(null, new string[3] { "appinfo", "steam_edit", "base_name" });
				if (propertyValue != null)
				{
					appInfoEntry_0x._properties.SetPropertyValue(PropertyType.String, propertyValue, "appinfo", "common", "name");
				}
				if (!appInfoEntry_0x._properties.HasProperty("appinfo", "common", "name_localized"))
				{
					PropertyTable propertyValue2 = appInfoEntry_0x._properties.GetPropertyValue<PropertyTable>(null, new string[3] { "appinfo", "steam_edit", "base_name_localized" });
					if (propertyValue2 != null)
					{
						appInfoEntry_0x._properties.SetPropertyValue(PropertyType.Table, propertyValue2, "appinfo", "common", "name_localized");
					}
				}
				string propertyValue3 = appInfoEntry_0x._properties.GetPropertyValue<string>(null, new string[3] { "appinfo", "steam_edit", "base_type" });
				if (propertyValue3 != null)
				{
					appInfoEntry_0x._properties.SetPropertyValue(PropertyType.String, propertyValue3, "appinfo", "common", "type");
				}
			}
			appInfoEntry_0x._originalData = PropertyTableIO.WritePropertyTable_Mods(appInfoEntry_0x._properties);
			appInfoEntry_0x.ClearCachedProps();
			return appInfoEntry_0x;
		}
		catch (Exception e)
		{
			throw new AppInfoLoadException($"Failed to load entry with appId {appInfoEntry_0x.ID:X8}", e);
		}
	}

	public void Write(BinaryWriter writer, ref List<string> stringTable, ref Dictionary<string, uint> stringLookup)
	{
		ClearCachedProps();
		PropertyTable propertyTable = new PropertyTable(_properties);
		PropertyTable propertyValue = propertyTable.GetPropertyValue<PropertyTable>(null, new string[2] { "appinfo", "common" });
		if (propertyValue != null)
		{
			string text = Name;
			if (text != null)
			{
				PropertyTable propertyValue2 = propertyValue.GetPropertyValue<PropertyTable>(null, new string[1] { "name_localized" });
				if (propertyValue2 != null)
				{
					propertyTable.SetPropertyValue(PropertyType.Table, propertyValue2, "appinfo", "steam_edit", "base_name_localized");
					if ((_modifications != null && _modifications.Changes.HasProperty("appinfo", "common", "name")) || Settings.Default.ReplaceLocalizedNames)
					{
						propertyValue.RemoveProperty("name_localized");
					}
				}
				propertyTable.SetPropertyValue(PropertyType.String, text, "appinfo", "steam_edit", "base_name");
				if (SteamData.UseCompleted && IsCompleted)
				{
					text += SteamData.CompletedSuffix;
				}
				propertyValue.SetPropertyValue("name", PropertyType.String, text);
			}
			else
			{
				Log.WriteLine("AppEntry {0:X8} has null name!", ID);
			}
			string text2 = propertyValue.GetPropertyValue("type", "game");
			if (text2 != null)
			{
				propertyTable.SetPropertyValue(PropertyType.String, text2, "appinfo", "steam_edit", "base_type");
				if (SteamData.UseHidden && IsHidden)
				{
					text2 = "hidden_" + text2;
				}
				propertyValue.SetPropertyValue("type", PropertyType.String, text2);
			}
			else
			{
				Log.WriteLine("AppEntry {0:X8} has null type!", ID);
			}
		}
		string s = propertyTable.ToString();
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		SHA1 sHA = SHA1.Create();
		byte[] buffer = sHA.ComputeHash(bytes);
		writer.Write(ID);
		byte[] buffer2 = PropertyTableIO.WritePropertyTable_0x07564429(propertyTable, stringTable, stringLookup);
		byte[] buffer3 = sHA.ComputeHash(buffer2);
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		binaryWriter.Write(_stuffBeforeHash);
		binaryWriter.Write(buffer);
		binaryWriter.Write(_changeNumber);
		binaryWriter.Write(buffer3);
		binaryWriter.Write(buffer2);
		MemoryStream memoryStream = (MemoryStream)binaryWriter.BaseStream;
		writer.Write((int)memoryStream.Length);
		writer.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
	}
}
