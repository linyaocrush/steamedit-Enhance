#nullable enable
using System.IO;

namespace SteamEdit.Data.Impl;

internal class AppInfoEntry_0x07564427 : AppInfoEntryBase
{
	public override uint AppInfoVersion => 123094055u;

	public static AppInfoEntry_0x07564427? FromReader(BinaryReader reader)
	{
		return ReadEntryCore(reader,
			() => new AppInfoEntry_0x07564427(),
			subReader => PropertyTableIO.ReadPropertyTable_0x00000000(subReader),
			hashBlockCount: 1);
	}

	public void Write(BinaryWriter writer)
	{
		WriteEntryCore(writer, 1, (subWriter, table) =>
		{
			byte[] tableData = PropertyTableIO.WritePropertyTable_0x00000000(table);
			subWriter.Write(tableData);
		});
	}
}
