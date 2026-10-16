#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Common;
using steamedit_Enhance.Data;

namespace steamedit_Enhance.Data.Impl;

internal abstract class AppInfoEntryBase : IAppInfoEntry
{
	internal IEntryDisplaySettings? DisplaySettings { get; set; }

	private string? _cachedName;
	private bool _nameCached;
	private bool? _cachedHasSortAs;
	private string? _cachedSortAs;
	private bool _sortAsCached;
	private string? _cachedNormalizedSortAs;
	private bool _normalizedSortAsCached;
	private AppInfoEntryType? _cachedType;
	private PropertyTable? _cachedOriginalData;

	protected byte[] _stuffBeforeHash = null!;
	protected uint _changeNumber;
	protected PropertyTable _properties = null!;
	protected ModifiedEntry? _modifications;

	/// <summary>
	/// 加载时预取的“摘要表”：只包含列表/筛选/排序需要的热路径子树
	/// （见 <see cref="AppInfoPaths.HotPaths"/>）。其余属性在首次访问完整表时才物化。
	/// </summary>
	private PropertyTable? _summary;

	/// <summary>加载时预取的摘要表（热路径子树），仅供子类/测试诊断使用。</summary>
	protected PropertyTable? Summary => _summary;

	/// <summary>
	/// 条目在文件中的原始字节（含 40 字节条头 + 属性表）。保留整段以避免为属性表再做一次拷贝；
	/// 用于惰性生成完整属性表与 <see cref="_originalData"/>。
	/// </summary>
	private byte[]? _rawEntryData;
	private int _tableOffset;
	private int _tableLength;

	/// <summary>在条目原始字节上打开仅覆盖属性表区域的只读流（零拷贝）。</summary>
	private MemoryStream OpenTableStream()
		=> new MemoryStream(_rawEntryData!, _tableOffset, _tableLength, writable: false);

	/// <summary>惰性缓存：归一化为 0x00000000 格式的原始属性表字节。</summary>
	protected byte[]? _originalData;

	/// <summary>
	/// 满足“热路径”访问的表。若完整表已物化则用完整表；否则用摘要表；
	/// 两者皆无（例如测试直接构造、或原生不可用时应由调用方先物化）→ 物化完整表。
	/// </summary>
	private PropertyTable HotTable
	{
		get
		{
			if (_properties != null) return _properties;
			if (_summary != null) return _summary;
			return EnsureProperties();
		}
	}

	/// <summary>完整属性表，首次访问时从原始字节物化（含 base_name/base_type 还原与子类后处理）。</summary>
	protected PropertyTable Properties => EnsureProperties();

	/// <summary>惰性物化完整属性表；已物化（或被测试直接赋值）则原样返回。</summary>
	private PropertyTable EnsureProperties()
	{
		if (_properties != null) return _properties;
		if (_rawEntryData == null)
			throw new InvalidOperationException($"Entry {ID:X8} has no property data to materialize.");
		_properties = ReadPropertyTableFrom(new BinaryReader(OpenTableStream()));
		RestoreBaseProperties(_properties);
		OnReadCompleted(_properties);
		ClearCachedProps();
		return _properties;
	}

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
					value.OriginalData = EnsureOriginalData();
				}
			}
			ClearCachedProps();
			_modifications = value;
			_cachedOriginalData = null;
			if (_modifications?.ApplyChanges(Properties) == true)
			{
				RaiseModified();
			}
		}
	}

	public bool IsModified => _modifications != null;

	public bool IsHidden
	{
		get => HotTable.GetPropertyValue(0, AppInfoPaths.IsHidden) != 0;
		set
		{
			if (value || Properties.HasProperty(AppInfoPaths.IsHidden))
			{
				SetPropertyValue(PropertyType.Int32, value ? 1 : 0, AppInfoPaths.IsHidden);
			}
		}
	}

	public bool IsCompleted
	{
		get => HotTable.GetPropertyValue(0, AppInfoPaths.IsCompleted) != 0;
		set
		{
			if (value || Properties.HasProperty(AppInfoPaths.IsCompleted))
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
				_cachedName = HotTable.GetPropertyValue<string>(null!, AppInfoPaths.Name);
				_nameCached = true;
			}
			return _cachedName!;
		}
		set
		{
			if (string.Equals(value, Name, StringComparison.Ordinal)) return;
			SetPropertyValue(PropertyType.String, value, AppInfoPaths.Name);
		}
	}

	public bool HasSortAs
	{
		get
		{
			if (!_cachedHasSortAs.HasValue)
			{
				var sortAs = HotTable.HasProperty(AppInfoPaths.SortAs) ? HotTable.GetPropertyValue<string>(Name, AppInfoPaths.SortAs) : null;
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
				_cachedSortAs = HotTable.GetPropertyValue<string>(Name, AppInfoPaths.SortAs);
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
		}
	}

	public string NormalizedSortAs
	{
		get
		{
			if (!_normalizedSortAsCached)
			{
				_cachedNormalizedSortAs = StringTitleComparer.Normalize(SortAs);
				_normalizedSortAsCached = true;
			}
			return _cachedNormalizedSortAs!;
		}
	}

	public string InstallPath
	{
		get
		{
			string propertyValue = HotTable.GetPropertyValue<string>("", AppInfoPaths.InstallDir);
			return Path.Combine(DisplaySettings!.SteamDir, "steamapps", "common", propertyValue);
		}
	}

	private static readonly FrozenDictionary<string, AppInfoEntryType> StringToType =
		new Dictionary<string, AppInfoEntryType>(StringComparer.OrdinalIgnoreCase)
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
		}.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

	private static readonly FrozenDictionary<AppInfoEntryType, string> TypeToString =
		StringToType.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

	public AppInfoEntryType Type
	{
		get
		{
			if (_cachedType.HasValue) return _cachedType.Value;
			string? propertyValue = HotTable.GetPropertyValue<string>(null!, AppInfoPaths.Type);
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
		_cachedNormalizedSortAs = null;
		_normalizedSortAsCached = false;
		_cachedType = null;
		_cachedOriginalData = null;
	}

	public byte[] GetOriginalDataCopy()
	{
		return (byte[])EnsureOriginalData().Clone();
	}

	/// <summary>
	/// 惰性生成并缓存“归一化为 0x00000000 格式的原始属性表字节”。
	/// 加载阶段不再对每个条目做深拷贝 + 全量序列化，仅保存原始字节；
	/// 只有真正需要（新建修改 / 还原）时才解析并序列化一次。
	/// </summary>
	internal byte[] EnsureOriginalData()
	{
		if (_originalData != null)
			return _originalData;
		if (_rawEntryData == null)
			throw new InvalidOperationException($"GetOriginalDataCopy called on entry {ID:X8} before original data was initialized.");
		PropertyTable parsed = ReadPropertyTableFrom(new BinaryReader(OpenTableStream()));
		_originalData = PropertyTableIO.WritePropertyTable_0x00000000(parsed);
		return _originalData;
	}

	public T GetPropertyValue<T>(T defaultValue, params string[] propertyPath)
	{
		// 热路径可直接由摘要表满足，无需物化完整属性表。
		if (_properties == null && _summary != null && AppInfoPaths.IsHotPath(propertyPath))
		{
			return _summary.GetPropertyValue(defaultValue, propertyPath);
		}
		return Properties.GetPropertyValue(defaultValue, propertyPath);
	}

	public void SetPropertyValue(PropertyType type, object? value, params string[] propertyPath)
	{
		Properties.SetPropertyValue(type, value, propertyPath);
		if (ModificationsCreate().AddChange(type, value, propertyPath))
		{
			RaiseModified();
		}
		ClearCachedProps();
	}

	public void RevertModifiedProperty(params string[] propertyPath)
	{
		if (_modifications == null || !_modifications.Changes.HasProperty(propertyPath))
		{
			return;
		}
		_modifications.Changes.RemoveProperty(propertyPath);
		if (_modifications.OriginalData != null)
		{
			Property? rawProperty = DeserializeOriginalData().GetRawProperty(propertyPath);
			Properties.SetRawProperty(rawProperty!, propertyPath);
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
		PropertyTable? propertyTable = Properties.GetPropertyValue<PropertyTable>(null!, AppInfoPaths.Launch);
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

	protected void RestoreBaseProperties() => RestoreBaseProperties(_properties ?? _summary!);

	private void RestoreBaseProperties(PropertyTable table)
	{
		string? baseName = table.GetPropertyValue<string>(null!, AppInfoPaths.BaseName);
		if (baseName != null)
		{
			table.SetPropertyValue(PropertyType.String, baseName, AppInfoPaths.Name);
		}
		string? baseType = table.GetPropertyValue<string>(null!, AppInfoPaths.BaseType);
		if (baseType != null)
		{
			table.SetPropertyValue(PropertyType.String, baseType, AppInfoPaths.Type);
		}
	}

	protected PropertyTable PrepareWriteTable(out PropertyTable common)
	{
		ClearCachedProps();
		PropertyTable table = new PropertyTable(Properties);
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
				// 自动写入官方本地化默认名（界面为中文且条目未被手动改名时）。base_name 仍保留原名。
				string writtenName = GetAutoOfficialWriteName() ?? text;
				table.SetPropertyValue(PropertyType.String, text, AppInfoPaths.BaseName);
				if (DisplaySettings!.UseCompleted && IsCompleted)
				{
					writtenName += DisplaySettings.CompletedSuffix;
				}
				common.SetPropertyValue("name", PropertyType.String, writtenName);
			}
			else
			{
				Log.WriteLine("AppEntry {0:X8} has null name!", ID);
			}
			string text2 = common.GetPropertyValue("type", "game");
			if (text2 != null)
			{
				table.SetPropertyValue(PropertyType.String, text2, AppInfoPaths.BaseType);
				if (DisplaySettings!.UseHidden && IsHidden)
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
	/// 保存 appinfo 时自动把“官方本地化默认名”写入 common/name（不产生修改记录，不计为已编辑）。
	/// 仅当：界面语言为中文、该条目的 name 未被用户手动修改、且存在不同于当前名的官方本地化名时返回该名。
	/// </summary>
	private string? GetAutoOfficialWriteName()
	{
		try
		{
			string? official = LocalizedNameResolver.TryGetLocalizedOfficialName(this);
			if (string.IsNullOrEmpty(official)) return null;
			string current = Name ?? "";
			return official == current ? null : official;
		}
		catch (Exception ex)
		{
			Log.WriteLine("AppEntry {0:X8}: auto official-name resolve failed: {1}", ID, ex.Message);
			return null;
		}
	}

	protected virtual int HashBlockCount => 1;

	protected virtual PropertyTable ReadPropertyTableFrom(BinaryReader reader)
		=> PropertyTableIO.ReadPropertyTable_0x00000000(reader);

	/// <summary>
	/// 加载时读取“摘要表”：默认走原生稀疏解码，只保留热路径子树；
	/// 原生不可用时退化为完整读取（仍正确，只是不再省内存/时间）。
	/// </summary>
	protected virtual PropertyTable ReadSummaryFrom(BinaryReader reader)
		=> PropertyTableIO.ReadPropertyTableSparse_0x00000000(reader);

	protected virtual void OnReadCompleted(PropertyTable table) { }

	protected virtual void WritePropertyTableTo(BinaryWriter writer, PropertyTable table)
	{
		byte[] data = PropertyTableIO.WritePropertyTable_0x00000000(table);
		writer.Write(data);
	}

	public void Write(BinaryWriter writer) => WriteEntryCore(writer, HashBlockCount, WritePropertyTableTo);

	internal static TEntry? ReadFromReader<TEntry>(BinaryReader reader)
		where TEntry : AppInfoEntryBase, new()
		=> ReadFromReader<TEntry>(reader, null);

	internal static TEntry? ReadFromReader<TEntry>(BinaryReader reader, Action<TEntry>? setup)
		where TEntry : AppInfoEntryBase, new()
	{
		int id = reader.ReadInt32();
		if (id == 0) return null;

		var entry = new TEntry();
		entry.ID = id;
		setup?.Invoke(entry);
		try
		{
			entry.InitializeFromReader(reader);
		}
		catch (Exception e)
		{
			throw new AppInfoLoadException($"Failed to load entry with appId {entry.ID:X8}", e);
		}
		return entry;
	}

	private const int StuffBeforeHashSize = 16;
	private const int Sha1HashSize = 20;
	private const int ChangeNumberSize = 4;

	private void InitializeFromReader(BinaryReader reader)
	{
		int count = reader.ReadInt32();
		if (count < 0)
			throw new InvalidDataException($"Entry data size {count} is negative.");
		byte[] entryData = reader.ReadBytes(count);
		using (BinaryReader subReader = new BinaryReader(new MemoryStream(entryData)))
		{
			_stuffBeforeHash = subReader.ReadBytes(StuffBeforeHashSize);
			subReader.ReadBytes(Sha1HashSize);
			_changeNumber = subReader.ReadUInt32();
			for (int i = 1; i < HashBlockCount; i++)
				subReader.ReadBytes(Sha1HashSize);

			// 保留整个条目的原始字节（含条头），属性表区域按偏移零拷贝访问。
			long tableStart = subReader.BaseStream.Position;
			_rawEntryData = entryData;
			_tableOffset = (int)tableStart;
			_tableLength = (int)(subReader.BaseStream.Length - tableStart);

			// 只解析热路径子树（摘要表），完整属性表延迟到首次访问时物化。
			_summary = ReadSummaryFrom(new BinaryReader(OpenTableStream()));
			RestoreBaseProperties(_summary);
			OnReadCompleted(_summary);
			_properties = null!;
		}
		ClearCachedProps();
	}

	/// <summary>
	/// 通用的条目写入模板。各版本子类传入属性表写入委托和哈希块数量。
	/// </summary>
	protected void WriteEntryCore(BinaryWriter writer, int hashBlockCount, Action<BinaryWriter, PropertyTable> writePropertyTable)
	{
		PropertyTable table = PrepareWriteTable(out _);
		byte[] hash;
		using (var sha = SHA1.Create())
		using (var cs = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
		using (var sw = new StreamWriter(cs, Encoding.UTF8))
		{
			table.WriteTo(sw);
			sw.Flush();
			cs.FlushFinalBlock();
			hash = sha.Hash!;
		}
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
			long afterChangeNumber = StuffBeforeHashSize + Sha1HashSize + ChangeNumberSize;
			subMs.Position = afterChangeNumber;
			byte[] tableData = new byte[subMs.Length - afterChangeNumber];
			subMs.Read(tableData, 0, tableData.Length);
			byte[] tableHash = SHA1.HashData(tableData);
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
