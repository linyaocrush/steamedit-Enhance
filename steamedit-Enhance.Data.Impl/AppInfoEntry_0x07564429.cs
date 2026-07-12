#nullable enable
using System.Collections.Generic;
using System.IO;
using Common;

namespace steamedit_Enhance.Data.Impl;

internal class AppInfoEntry_0x07564429 : AppInfoEntryBase
{
	public override uint AppInfoVersion => 123094057u;

	protected override int HashBlockCount => 2;

	private IList<string>? _stringTable;

	protected override PropertyTable ReadPropertyTableFrom(BinaryReader reader)
		=> PropertyTableIO.ReadPropertyTable_0x07564429(reader, _stringTable!);

	protected override void OnReadCompleted()
	{
		if (!_properties.HasProperty(AppInfoPaths.NameLocalized))
		{
			PropertyTable? nameLocalized = _properties.GetPropertyValue<PropertyTable>(null!, AppInfoPaths.BaseNameLocalized);
			if (nameLocalized != null)
			{
				_properties.SetPropertyValue(PropertyType.Table, nameLocalized, AppInfoPaths.NameLocalized);
			}
		}
	}

	protected override void HandleNameLocalizedRemoval(PropertyTable common, PropertyTable nameLocalized)
	{
		if ((_modifications != null && _modifications.Changes.HasProperty(AppInfoPaths.Name)) || Settings.Default.ReplaceLocalizedNames)
		{
			common.RemoveProperty("name_localized");
		}
	}

	public static AppInfoEntry_0x07564429? FromReader(BinaryReader reader, IList<string> stringTable)
		=> ReadFromReader<AppInfoEntry_0x07564429>(reader, entry => entry._stringTable = stringTable);

	public void Write(BinaryWriter writer, List<string> stringTable, Dictionary<string, uint> stringLookup)
	{
		WriteEntryCore(writer, 2, (subWriter, table) =>
		{
			byte[] tableData = PropertyTableIO.WritePropertyTable_0x07564429(table, stringTable, stringLookup);
			subWriter.Write(tableData);
		});
	}
}
