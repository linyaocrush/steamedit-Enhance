namespace SteamEdit.Data;

internal class ModifiedEntry
{
	public readonly int ID;

	private byte[] _originalData;

	public byte[] OriginalData
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

	public PropertyTable Changes { get; set; }

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

	public bool AddChange(PropertyType type, object value, string[] propertyPath)
	{
		return Changes.SetPropertyValue(type, value, propertyPath);
	}

	public bool ApplyChanges(PropertyTable entryTable)
	{
		if (Changes != null)
		{
			return ApplyChanges(Changes, entryTable);
		}
		return false;
	}

	private bool ApplyChanges(PropertyTable changesTable, PropertyTable entryTable)
	{
		bool result = false;
		foreach (string propertyName in changesTable.PropertyNames)
		{
			object propertyAsObject = changesTable.GetPropertyAsObject(propertyName);
			PropertyType propertyType = changesTable.GetPropertyType(propertyName);
			if (propertyType == PropertyType.Table)
			{
				PropertyTable changesTable2 = (PropertyTable)propertyAsObject;
				PropertyTable propertyTable = entryTable.GetPropertyValue<PropertyTable>(propertyName);
				if (propertyTable == null)
				{
					propertyTable = new PropertyTable();
					entryTable.SetPropertyValue(propertyName, PropertyType.Table, propertyTable);
				}
				if (ApplyChanges(changesTable2, propertyTable))
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
