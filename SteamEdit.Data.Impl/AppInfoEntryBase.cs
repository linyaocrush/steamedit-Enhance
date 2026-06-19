using System;
using System.IO;
using System.Linq;
using Common;

namespace SteamEdit.Data.Impl;

internal abstract class AppInfoEntryBase : IAppInfoEntry
{
	private string _cachedName;
	private bool? _cachedHasSortAs;
	private string _cachedSortAs;

	protected byte[] _stuffBeforeHash;
	protected uint _changeNumber;
	protected byte[] _originalData;
	protected PropertyTable _properties;
	protected ModifiedEntry _modifications;

	public abstract uint AppInfoVersion { get; }

	public int ID { get; protected set; }

	public uint ChangeNumber => _changeNumber;

	public ModifiedEntry Modification
	{
		get => _modifications;
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
				RaiseModified();
			}
		}
	}

	public bool IsModified => _modifications != null;

	public bool IsHidden
	{
		get => _properties.GetPropertyValue(0, AppInfoPaths.IsHidden) != 0;
		set
		{
			if (value || _properties.HasProperty(AppInfoPaths.IsHidden))
			{
				SetPropertyValue(PropertyType.Int32, value ? 1 : 0, AppInfoPaths.IsHidden);
			}
		}
	}

	public bool IsCompleted
	{
		get => _properties.GetPropertyValue(0, AppInfoPaths.IsCompleted) != 0;
		set
		{
			if (value || _properties.HasProperty(AppInfoPaths.IsCompleted))
			{
				SetPropertyValue(PropertyType.Int32, value ? 1 : 0, AppInfoPaths.IsCompleted);
			}
		}
	}

	public string Name
	{
		get
		{
			if (_cachedName == null)
			{
				_cachedName = _properties.GetPropertyValue<string>(null, AppInfoPaths.Name);
			}
			return _cachedName;
		}
		set
		{
			SetPropertyValue(PropertyType.String, value, AppInfoPaths.Name);
			ClearCachedProps();
		}
	}

	public bool HasSortAs
	{
		get
		{
			if (!_cachedHasSortAs.HasValue)
			{
				_cachedHasSortAs = _properties.HasProperty(AppInfoPaths.SortAs) && _properties.GetPropertyValue<string>(Name, AppInfoPaths.SortAs).Length > 0;
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
				_cachedSortAs = _properties.GetPropertyValue<string>(Name, AppInfoPaths.SortAs);
				if (_cachedSortAs.Length == 0)
				{
					_cachedSortAs = Name;
				}
			}
			return _cachedSortAs;
		}
		set
		{
			SetPropertyValue(PropertyType.String, value, AppInfoPaths.SortAs);
			ClearCachedProps();
		}
	}

	public string InstallPath
	{
		get
		{
			string propertyValue = _properties.GetPropertyValue<string>("", AppInfoPaths.InstallDir);
			return Path.Combine(SteamData.SteamDir, "steamapps", "common", propertyValue);
		}
	}

	public AppInfoEntryType Type
	{
		get
		{
			string propertyValue = _properties.GetPropertyValue<string>(null, AppInfoPaths.Type);
			if (propertyValue == null) return AppInfoEntryType.Unknown;
			return propertyValue.ToLowerInvariant() switch
			{
				"game" => AppInfoEntryType.Game,
				"config" => AppInfoEntryType.Config,
				"tool" => AppInfoEntryType.Tool,
				"demo" => AppInfoEntryType.Demo,
				"dlc" => AppInfoEntryType.DLC,
				"application" => AppInfoEntryType.Application,
				"video" => AppInfoEntryType.Video,
				"guide" => AppInfoEntryType.Guide,
				"music" => AppInfoEntryType.Music,
				"beta" => AppInfoEntryType.Beta,
				_ => LogAndReturnUnknown(propertyValue),
			};
		}
		set
		{
			string str = value switch
			{
				AppInfoEntryType.Game => "game",
				AppInfoEntryType.Config => "config",
				AppInfoEntryType.Tool => "tool",
				AppInfoEntryType.Demo => "demo",
				AppInfoEntryType.DLC => "dlc",
				AppInfoEntryType.Application => "application",
				AppInfoEntryType.Video => "video",
				AppInfoEntryType.Guide => "guide",
				AppInfoEntryType.Music => "music",
				AppInfoEntryType.Beta => "beta",
				_ => LogAndReturnNull(value),
			};
			if (str != null)
			{
				SetPropertyValue(PropertyType.String, str, AppInfoPaths.Type);
			}
		}
	}

	private static AppInfoEntryType LogAndReturnUnknown(string text)
	{
		Log.WriteLine("Unrecognized entry type: {0}", text);
		return AppInfoEntryType.Unknown;
	}

	private static string LogAndReturnNull(AppInfoEntryType value)
	{
		Log.WriteLine("Unrecognized entry type: {0}", value);
		return null;
	}

	public LaunchTable Launches
	{
		get => new LaunchTable(GetLaunchPropertyTable());
		set => SetPropertyValue(PropertyType.Table, value.PropertyTable, AppInfoPaths.Launch);
	}

	public event EventHandler Modified;

	protected void RaiseModified() => this.Modified?.Invoke(this, EventArgs.Empty);

	protected void ClearCachedProps()
	{
		_cachedName = null;
		_cachedSortAs = null;
		_cachedHasSortAs = null;
	}

	public byte[] GetOriginalDataCopy() => (byte[])_originalData.Clone();

	public T GetPropertyValue<T>(T DefaultValue, params string[] PropertyPath)
	{
		return _properties.GetPropertyValue(DefaultValue, PropertyPath);
	}

	public void SetPropertyValue(PropertyType Type, object Value, params string[] PropertyPath)
	{
		_properties.SetPropertyValue(Type, Value, PropertyPath);
		if (ModificationsCreate().AddChange(Type, Value, PropertyPath))
		{
			RaiseModified();
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

	public PropertyType? GetOriginalPropertyType(params string[] PropertyPath)
	{
		if (_modifications?.OriginalData == null || PropertyPath.Length == 0) return null;
		using var reader = new BinaryReader(new MemoryStream(_modifications.OriginalData));
		var table = PropertyTableIO.ReadPropertyTable_Mods(reader);
		// 遍历路径到达父表
		foreach (var key in PropertyPath.Take(PropertyPath.Length - 1))
		{
			var prop = table[key];
			if (prop == null) return null;
			table = prop.GetValue<PropertyTable>();
			if (table == null) return null;
		}
		var target = table[PropertyPath.Last()];
		return target?.PropertyType;
	}

	public object GetOriginalPropertyValue(params string[] PropertyPath)
	{
		if (_modifications?.OriginalData == null || PropertyPath.Length == 0) return null;
		using var reader = new BinaryReader(new MemoryStream(_modifications.OriginalData));
		var table = PropertyTableIO.ReadPropertyTable_Mods(reader);
		// 遍历路径到达父表
		foreach (var key in PropertyPath.Take(PropertyPath.Length - 1))
		{
			var prop = table[key];
			if (prop == null) return null;
			table = prop.GetValue<PropertyTable>();
			if (table == null) return null;
		}
		return table.GetPropertyAsObject(PropertyPath.Last());
	}

	protected ModifiedEntry ModificationsCreate()
	{
		if (_modifications == null)
		{
			_modifications = new ModifiedEntry(this);
		}
		return _modifications;
	}

	private PropertyTable GetLaunchPropertyTable()
	{
		PropertyTable propertyTable = _properties.GetPropertyValue<PropertyTable>(null, AppInfoPaths.Launch);
		return propertyTable ?? new PropertyTable();
	}

	public override string ToString() => Name;

	public virtual bool Revert()
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
				RaiseModified();
				return true;
			}
		}
		return false;
	}

	protected void RestoreBaseProperties()
	{
		string baseName = _properties.GetPropertyValue<string>(null, AppInfoPaths.BaseName);
		if (baseName != null)
		{
			_properties.SetPropertyValue(PropertyType.String, baseName, AppInfoPaths.Name);
		}
		string baseType = _properties.GetPropertyValue<string>(null, AppInfoPaths.BaseType);
		if (baseType != null)
		{
			_properties.SetPropertyValue(PropertyType.String, baseType, AppInfoPaths.Type);
		}
	}

	protected PropertyTable PrepareWriteTable(out PropertyTable common)
	{
		ClearCachedProps();
		PropertyTable table = new PropertyTable(_properties);
		common = table.GetPropertyValue<PropertyTable>(null, "appinfo", "common");
		if (common != null)
		{
			string text = Name;
			if (text != null)
			{
				PropertyTable nameLocalized = common.GetPropertyValue<PropertyTable>(null, "name_localized");
				if (nameLocalized != null)
				{
					table.SetPropertyValue(PropertyType.Table, nameLocalized, AppInfoPaths.BaseNameLocalized);
					HandleNameLocalizedRemoval(common, nameLocalized);
				}
				table.SetPropertyValue(PropertyType.String, text, AppInfoPaths.BaseName);
				if (SteamData.UseCompleted && IsCompleted)
				{
					text += SteamData.CompletedSuffix;
				}
				common.SetPropertyValue("name", PropertyType.String, text);
			}
			else
			{
				Log.WriteLine("AppEntry {0:X8} has null name!", ID);
			}
			string text2 = common.GetPropertyValue("type", "game");
			if (text2 != null)
			{
				table.SetPropertyValue(PropertyType.String, text2, AppInfoPaths.BaseType);
				if (SteamData.UseHidden && IsHidden)
				{
					text2 = "hidden_" + text2;
				}
				common.SetPropertyValue("type", PropertyType.String, text2);
			}
			else
			{
				Log.WriteLine("AppEntry {0:X8} has null type!", ID);
			}
		}
		return table;
	}

	protected virtual void HandleNameLocalizedRemoval(PropertyTable common, PropertyTable nameLocalized)
	{
		common.RemoveProperty("name_localized");
	}
}
