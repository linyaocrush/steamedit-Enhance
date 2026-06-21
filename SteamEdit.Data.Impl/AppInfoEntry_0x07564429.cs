#nullable enable
using System.Collections.Generic;
using System.IO;
using Common;

namespace SteamEdit.Data.Impl;

internal class AppInfoEntry_0x07564429 : AppInfoEntryBase
{
	public override uint AppInfoVersion => 123094057u;

	protected override void HandleNameLocalizedRemoval(PropertyTable common, PropertyTable nameLocalized)
	{
		if ((_modifications != null && _modifications.Changes.HasProperty(AppInfoPaths.Name)) || Settings.Default.ReplaceLocalizedNames)
		{
			common.RemoveProperty("name_localized");
		}
	}

	public static AppInfoEntry_0x07564429? FromReader(BinaryReader reader, IList<string> stringTable)
	{
		return ReadEntryCore(reader,
			() => new AppInfoEntry_0x07564429(),
			subReader => PropertyTableIO.ReadPropertyTable_0x07564429(subReader, stringTable),
			hashBlockCount: 2,
			postProcess: entry =>
			{
				if (!entry._properties.HasProperty(AppInfoPaths.NameLocalized))
				{
					PropertyTable? nameLocalized = entry._properties.GetPropertyValue<PropertyTable>(null!, AppInfoPaths.BaseNameLocalized);
					if (nameLocalized != null)
					{
						entry._properties.SetPropertyValue(PropertyType.Table, nameLocalized, AppInfoPaths.NameLocalized);
					}
				}
			});
	}

	public void Write(BinaryWriter writer, List<string> stringTable, Dictionary<string, uint> stringLookup)
	{
		WriteEntryCore(writer, 2, (subWriter, table) =>
		{
			byte[] tableData = PropertyTableIO.WritePropertyTable_0x07564429(table, stringTable, stringLookup);
			subWriter.Write(tableData);
		});
	}
}
