#nullable enable
using System.IO;

namespace SteamEdit.Data.Impl;

internal class AppInfoEntry_0x07564428 : AppInfoEntryBase
{
	public override uint AppInfoVersion => 123094056u;

	public static AppInfoEntry_0x07564428? FromReader(BinaryReader reader)
	{
		return ReadEntryCore(reader,
			() => new AppInfoEntry_0x07564428(),
			subReader => PropertyTableIO.ReadPropertyTable_0x00000000(subReader),
			hashBlockCount: 2);
	}

	public void Write(BinaryWriter writer)
	{
		WriteEntryCore(writer, 2, (subWriter, table) =>
		{
			byte[] tableData = PropertyTableIO.WritePropertyTable_0x00000000(table);
			subWriter.Write(tableData);
		});
	}
}
