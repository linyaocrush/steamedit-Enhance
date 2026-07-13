#nullable enable
using System.IO;

namespace steamedit_Enhance.Data;

internal class ModifiedEntry
{
	public readonly int ID;

	private byte[]? _originalData;
	private PropertyTable? _cachedOriginalTable;

	public byte[]? OriginalData
	{
		get => _originalData?.Clone() as byte[];
		set
		{
			_originalData = value?.Clone() as byte[];
			_cachedOriginalTable = null;
		}
	}

	public uint OriginalDataVersion { get; set; }

	public uint OriginalDataChangeNumber { get; set; }

	public PropertyTable Changes { get; internal set; } = null!;

	public ModifiedEntry(IAppInfoEntry entry)
	{
		ID = entry.ID;
		OriginalDataVersion = entry.AppInfoVersion;
		OriginalDataChangeNumber = entry.ChangeNumber;
		OriginalData = entry.GetOriginalDataCopy();
		Changes = new PropertyTable();
	}

	public ModifiedEntry(int id)
	{
		ID = id;
	}

	public bool AddChange(PropertyType type, object? value, string[] propertyPath)
	{
		return Changes.SetPropertyValue(type, value, propertyPath);
	}

	public PropertyTable GetOriginalTable()
	{
		_cachedOriginalTable ??= DeserializeOriginalTable();
		return _cachedOriginalTable;
	}

	public string? GetOriginalName()
	{
		if (_originalData == null) return null;
		return GetOriginalTable().GetPropertyValue<string>(null!, AppInfoPaths.Name);
	}

	private PropertyTable DeserializeOriginalTable()
	{
		using var reader = new BinaryReader(new MemoryStream(_originalData!));
		return PropertyTableIO.ReadPropertyTable_0x00000000(reader);
	}

	public bool ApplyChanges(PropertyTable entryTable)
	{
		if (Changes != null)
		{
			return ApplyChanges(Changes, entryTable);
		}
		return false;
	}

	private bool ApplyChanges(PropertyTable changesTable, PropertyTable entryTable, int depth = 0)
	{
		if (depth > PropertyTable.MaxNestingDepth)
			throw new InvalidDataException($"Property table nesting depth {depth} exceeds maximum {PropertyTable.MaxNestingDepth}.");
		bool result = false;
		foreach (string propertyName in changesTable.PropertyNames)
		{
			object? propertyAsObject = changesTable.GetPropertyAsObject(propertyName);
			PropertyType propertyType = changesTable.GetPropertyType(propertyName);
			if (propertyType == PropertyType.Table)
			{
				if (propertyAsObject is not PropertyTable changesTable2) continue;
				PropertyTable? propertyTable = entryTable.GetPropertyValue<PropertyTable>(propertyName);
				if (propertyTable == null)
				{
					propertyTable = new PropertyTable();
					entryTable.SetPropertyValue(propertyName, PropertyType.Table, propertyTable);
				}
				if (ApplyChanges(changesTable2, propertyTable, depth + 1))
				{
					result = true;
				}
			}
			else if (entryTable.SetPropertyValue(propertyName, propertyType, propertyAsObject))
			{
				result = true;
			}
		}
		return result;
	}
}
