#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Common;
using SteamEdit.Data;

namespace SteamEdit.Data.Impl;

internal abstract class AppInfoEntryBase : IAppInfoEntry
{
	/// <summary>
	/// 注入的显示设置，解耦对静态 SteamData 的直接依赖。
	/// 初始化时由 Program.InitializeSteamData 设置。
	/// </summary>
	internal static IEntryDisplaySettings DisplaySettings { get; set; } = null!;

	private string? _cachedName;
	private bool _nameCached;
	private bool? _cachedHasSortAs;
	private string? _cachedSortAs;
	private bool _sortAsCached;
	private AppInfoEntryType? _cachedType;
	private PropertyTable? _cachedOriginalData;

	protected byte[] _stuffBeforeHash = null!;
	protected uint _changeNumber;
	protected byte[] _originalData = null!;
	protected PropertyTable _properties = null!;
	protected ModifiedEntry? _modifications;

	public abstract uint AppInfoVersion { get; }

	public int ID { get; protected set; }

	public uint ChangeNumber => _changeNumber;

	public ModifiedEntry? Modification
	{
		get => _modifications;
		set
		{
			if (value?.OriginalData != null)
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
			_cachedOriginalData = null;
			if (_modifications?.ApplyChanges(_properties) == true)
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
			if (!_nameCached)
			{
				_cachedName = _properties.GetPropertyValue<string>(null!, AppInfoPaths.Name);
				_nameCached = true;
			}
			return _cachedName!;
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
				var sortAs = _properties.HasProperty(AppInfoPaths.SortAs) ? _properties.GetPropertyValue<string>(Name, AppInfoPaths.SortAs) : null;
				_cachedHasSortAs = sortAs != null && sortAs.Length > 0;
			}
			return _cachedHasSortAs.Value;
		}
	}

	public string SortAs
	{
		get
		{
			if (!_sortAsCached)
			{
				_cachedSortAs = _properties.GetPropertyValue<string>(Name, AppInfoPaths.SortAs);
				if (_cachedSortAs == null || _cachedSortAs.Length == 0)
				{
					_cachedSortAs = Name;
				}
				_sortAsCached = true;
			}
			return _cachedSortAs!;
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
			return Path.Combine(DisplaySettings.SteamDir, "steamapps", "common", propertyValue);
		}
	}

	private static readonly Dictionary<string, AppInfoEntryType> StringToType = new(StringComparer.OrdinalIgnoreCase)
	{
		["game"] = AppInfoEntryType.Game,
		["config"] = AppInfoEntryType.Config,
		["tool"] = AppInfoEntryType.Tool,
		["demo"] = AppInfoEntryType.Demo,
		["dlc"] = AppInfoEntryType.DLC,
		["application"] = AppInfoEntryType.Application,
		["video"] = AppInfoEntryType.Video,
		["guide"] = AppInfoEntryType.Guide,
		["music"] = AppInfoEntryType.Music,
		["beta"] = AppInfoEntryType.Beta,
	};

	private static readonly Dictionary<AppInfoEntryType, string> TypeToString = StringToType
		.ToDictionary(kv => kv.Value, kv => kv.Key);

	public AppInfoEntryType Type
	{
		get
		{
			if (_cachedType.HasValue) return _cachedType.Value;
			string? propertyValue = _properties.GetPropertyValue<string>(null!, AppInfoPaths.Type);
			if (propertyValue == null) return AppInfoEntryType.Unknown;
			if (!StringToType.TryGetValue(propertyValue, out var result))
			{
				Log.WriteLine("Unrecognized entry type: {0}", propertyValue);
				result = AppInfoEntryType.Unknown;
			}
			_cachedType = result;
			return result;
		}
		set
		{
			if (!TypeToString.TryGetValue(value, out var str))
			{
				Log.WriteLine("Unrecognized entry type: {0}", value);
				return;
			}
			SetPropertyValue(PropertyType.String, str, AppInfoPaths.Type);
		}
	}

	public LaunchTable Launches
	{
		get => new LaunchTable(GetLaunchPropertyTable());
		set => SetPropertyValue(PropertyType.Table, value.PropertyTable, AppInfoPaths.Launch);
	}

	public event EventHandler? Modified;

	protected void RaiseModified() => this.Modified?.Invoke(this, EventArgs.Empty);

	protected void ClearCachedProps()
	{
		_cachedName = null;
		_nameCached = false;
		_cachedSortAs = null;
		_sortAsCached = false;
		_cachedHasSortAs = null;
		_cachedType = null;
		_cachedOriginalData = null;
	}

	public byte[] GetOriginalDataCopy()
	{
		if (_originalData == null)
			throw new InvalidOperationException($"GetOriginalDataCopy called on entry {ID:X8} before original data was initialized.");
		return (byte[])_originalData.Clone();
	}

	public T GetPropertyValue<T>(T DefaultValue, params string[] PropertyPath)
	{
		return _properties.GetPropertyValue(DefaultValue, PropertyPath);
	}

	public void SetPropertyValue(PropertyType Type, object? Value, params string[] PropertyPath)
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
			Property? rawProperty = DeserializeOriginalData().GetRawProperty(PropertyPath);
			_properties.SetRawProperty(rawProperty!, PropertyPath);
		}
		if (_modifications.Changes.Count == 0)
		{
			Revert();
			return;
		}
		ClearCachedProps();
		this.Modified?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>
	/// 从原始数据中遍历路径，返回包含目标属性的父表。
	/// </summary>
	private PropertyTable? TraverseToParent(string[] propertyPath)
	{
		if (_modifications?.OriginalData == null || propertyPath.Length == 0) return null;
		var table = DeserializeOriginalData();
		for (int i = 0; i < propertyPath.Length - 1; i++)
		{
			var prop = table[propertyPath[i]];
			if (prop == null) return null;
			table = prop.GetValue<PropertyTable>();
			if (table == null) return null;
		}
		return table;
	}

	public PropertyType? GetOriginalPropertyType(params string[] PropertyPath)
	{
		var table = TraverseToParent(PropertyPath);
		var target = table?[PropertyPath[PropertyPath.Length - 1]];
		return target?.PropertyType;
	}

	public object? GetOriginalPropertyValue(params string[] PropertyPath)
	{
		var table = TraverseToParent(PropertyPath);
		return table?.GetPropertyAsObject(PropertyPath[PropertyPath.Length - 1]);
	}

	private PropertyTable DeserializeOriginalData()
	{
		return _cachedOriginalData ??= PropertyTableIO.ReadPropertyTable_0x00000000(new BinaryReader(new MemoryStream(_modifications!.OriginalData!)));
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
		PropertyTable? propertyTable = _properties.GetPropertyValue<PropertyTable>(null!, AppInfoPaths.Launch);
		return propertyTable ?? new PropertyTable();
	}

	public override string ToString() => Name;

	public virtual bool Revert()
	{
		if (_modifications != null)
		{
			if (_modifications.OriginalData == null)
			{
				return false;
			}
			_properties = DeserializeOriginalData();
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
		string? baseName = _properties.GetPropertyValue<string>(null!, AppInfoPaths.BaseName);
		if (baseName != null)
		{
			_properties.SetPropertyValue(PropertyType.String, baseName, AppInfoPaths.Name);
		}
		string? baseType = _properties.GetPropertyValue<string>(null!, AppInfoPaths.BaseType);
		if (baseType != null)
		{
			_properties.SetPropertyValue(PropertyType.String, baseType, AppInfoPaths.Type);
		}
	}

	protected PropertyTable PrepareWriteTable(out PropertyTable common)
	{
		ClearCachedProps();
		PropertyTable table = new PropertyTable(_properties);
		common = table.GetPropertyValue<PropertyTable>(null!, "appinfo", "common")!;
		if (common != null)
		{
			string text = Name;
			if (text != null)
			{
				PropertyTable? nameLocalized = common.GetPropertyValue<PropertyTable>(null!, "name_localized");
				if (nameLocalized != null)
				{
					table.SetPropertyValue(PropertyType.Table, nameLocalized, AppInfoPaths.BaseNameLocalized);
					HandleNameLocalizedRemoval(common, nameLocalized);
				}
				table.SetPropertyValue(PropertyType.String, text, AppInfoPaths.BaseName);
				if (DisplaySettings.UseCompleted && IsCompleted)
				{
					text += DisplaySettings.CompletedSuffix;
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
				if (DisplaySettings.UseHidden && IsHidden)
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

	/// <summary>
	/// 通用的条目读取模板。各版本子类传入工厂方法、属性表读取委托和哈希块数量。
	/// </summary>
	protected static TEntry? ReadEntryCore<TEntry>(
		BinaryReader reader,
		Func<TEntry> createEntry,
		Func<BinaryReader, PropertyTable> readPropertyTable,
		int hashBlockCount,
		Action<TEntry>? postProcess = null) where TEntry : AppInfoEntryBase
	{
		int num = reader.ReadInt32();
		if (num == 0) return null;

		var entry = createEntry();
		entry.ID = num;
		try
		{
			int count = reader.ReadInt32();
			if (count < 0)
				throw new InvalidDataException($"Entry data size {count} is negative.");
			using (BinaryReader subReader = new BinaryReader(new MemoryStream(reader.ReadBytes(count))))
			{
				entry._stuffBeforeHash = subReader.ReadBytes(16);
				subReader.ReadBytes(20); // hash 1
				entry._changeNumber = subReader.ReadUInt32();
				for (int i = 1; i < hashBlockCount; i++)
					subReader.ReadBytes(20); // additional hash blocks
				entry._properties = readPropertyTable(subReader);
				entry.RestoreBaseProperties();
			}
			postProcess?.Invoke(entry);
			entry._originalData = PropertyTableIO.WritePropertyTable_0x00000000(entry._properties);
			entry.ClearCachedProps();
			return entry;
		}
		catch (Exception e)
		{
			throw new AppInfoLoadException($"Failed to load entry with appId {entry.ID:X8}", e);
		}
	}

	/// <summary>
	/// 通用的条目写入模板。各版本子类传入属性表写入委托和哈希块数量。
	/// </summary>
	protected void WriteEntryCore(BinaryWriter writer, int hashBlockCount, Action<BinaryWriter, PropertyTable> writePropertyTable)
	{
		PropertyTable table = PrepareWriteTable(out _);
		string s = table.ToString();
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		using var sha = SHA1.Create();
		byte[] hash = sha.ComputeHash(bytes);
		writer.Write(ID);
		using BinaryWriter subWriter = new BinaryWriter(new MemoryStream());
		subWriter.Write(_stuffBeforeHash);
		subWriter.Write(hash);
		subWriter.Write(_changeNumber);
		writePropertyTable(subWriter, table);
		if (hashBlockCount > 1)
		{
			// 回填 tableHash：需要先写入 tableData，计算哈希，再回填
			MemoryStream subMs = (MemoryStream)subWriter.BaseStream;
			long afterChangeNumber = 16 + 20 + 4; // stuffBeforeHash + hash + changeNumber
			subMs.Position = afterChangeNumber;
			byte[] tableData = new byte[subMs.Length - afterChangeNumber];
			subMs.Read(tableData, 0, tableData.Length);
			byte[] tableHash = sha.ComputeHash(tableData);
			// 重写：stuffBeforeHash + hash + changeNumber + tableHash + tableData
			subMs.SetLength(0);
			subWriter.Write(_stuffBeforeHash);
			subWriter.Write(hash);
			subWriter.Write(_changeNumber);
			subWriter.Write(tableHash);
			subWriter.Write(tableData);
		}
		MemoryStream outMs = (MemoryStream)subWriter.BaseStream;
		writer.Write((int)outMs.Length);
		writer.Write(outMs.GetBuffer(), 0, (int)outMs.Length);
	}
}
