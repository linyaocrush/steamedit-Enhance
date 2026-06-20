#nullable enable
using System.IO;

namespace SteamEdit.Data;

internal class ModifiedEntry
{
	public readonly int ID;

	private byte[]? _originalData;

	public byte[]? OriginalData
	{
		get
		{
			return _originalData;
		}
		set
		{
			if (value != null)
			{
				_originalData = (byte[])value.Clone();
			}
			else
			{
				_originalData = null;
			}
		}
	}

	public uint OriginalDataVersion { get; set; }

	public uint OriginalDataChangeNumber { get; set; }

	public PropertyTable Changes { get; set; } = null!;

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

	public string? GetOriginalName()
	{
		if (OriginalData == null) return null;
		using var reader = new BinaryReader(new MemoryStream(OriginalData));
		var props = PropertyTableIO.ReadPropertyTable_Mods(reader);
		return props.GetPropertyValue<string>(null!, AppInfoPaths.Name);
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
		if (depth > 32)
			throw new InvalidDataException($"Property table nesting depth {depth} exceeds maximum 32.");
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
