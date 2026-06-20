#nullable enable
using System.Collections.Generic;
using System.IO;

namespace SteamEdit.Data.Impl;

internal class AppInfo_0x07564429 : AppInfoBase<AppInfoEntry_0x07564429>
{
	public const uint MagicNumberValue = 123094057u;

	protected override uint MagicNumber => MagicNumberValue;

	public static AppInfo_0x07564429 Load(BinaryReader reader)
	{
		AppInfo_0x07564429 appInfo = new AppInfo_0x07564429();
		appInfo._unknownValueAtStart = reader.ReadUInt32();
		long position = reader.ReadInt64();
		long position2 = reader.BaseStream.Position;
		reader.BaseStream.Position = position;
		uint num = reader.ReadUInt32();
		if (num > 50_000)
			throw new InvalidDataException(
				$"String table size {num} exceeds maximum 50000. File may be corrupted.");
		string[] array = new string[num];
		for (uint num2 = 0u; num2 < num; num2++)
		{
			array[num2] = PropertyTableIO.ReadZString(reader);
		}
		reader.BaseStream.Position = position2;
		AppInfoEntry_0x07564429? entry;
		while ((entry = AppInfoEntry_0x07564429.FromReader(reader, array)) != null)
		{
			appInfo._entries.Add(entry);
			entry.Modified += appInfo.OnEntryModified;
		}
		return appInfo;
	}

	protected override void SaveEntries(BinaryWriter writer)
	{
		List<string> stringTable = new List<string>();
		Dictionary<string, uint> stringLookup = new Dictionary<string, uint>();
		using (BinaryWriter binaryWriter2 = new BinaryWriter(new MemoryStream()))
		{
			foreach (AppInfoEntry_0x07564429 entry in Entries)
			{
				entry.Write(binaryWriter2, ref stringTable, ref stringLookup);
			}
			binaryWriter2.Write(0);
			long value = writer.BaseStream.Position + 8 + binaryWriter2.BaseStream.Length;
			writer.Write(value);
			writer.Write(((MemoryStream)binaryWriter2.BaseStream).ToArray());
			writer.Write((uint)stringTable.Count);
			foreach (string item in stringTable)
			{
				PropertyTableIO.WriteZString(writer, item);
			}
		}
	}
}
