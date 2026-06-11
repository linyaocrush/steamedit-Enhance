using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Common;

namespace SteamEdit.Data.Impl;

internal class AppInfoEntry_0x07564428 : AppInfoEntryBase
{
	public override uint AppInfoVersion => 123094056u;

	public static AppInfoEntry_0x07564428 FromReader(BinaryReader reader)
	{
		int num = reader.ReadInt32();
		if (num == 0) return null;

		var entry = new AppInfoEntry_0x07564428 { ID = num };
		try
		{
			int count = reader.ReadInt32();
			using (BinaryReader subReader = new BinaryReader(new MemoryStream(reader.ReadBytes(count))))
			{
				entry._stuffBeforeHash = subReader.ReadBytes(16);
				subReader.ReadBytes(20);
				entry._changeNumber = subReader.ReadUInt32();
				subReader.ReadBytes(20);
				entry._properties = PropertyTableIO.ReadPropertyTable_0x00000000(subReader);
				entry.RestoreBaseProperties();
			}
			entry._originalData = PropertyTableIO.WritePropertyTable_Mods(entry._properties);
			entry.ClearCachedProps();
			return entry;
		}
		catch (Exception e)
		{
			throw new AppInfoLoadException($"Failed to load entry with appId {entry.ID:X8}", e);
		}
	}

	public void Write(BinaryWriter writer)
	{
		PropertyTable table = PrepareWriteTable(out _);
		string s = table.ToString();
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		using var sha = SHA1.Create();
		byte[] hash = sha.ComputeHash(bytes);
		writer.Write(ID);
		byte[] tableData = PropertyTableIO.WritePropertyTable_0x00000000(table);
		byte[] tableHash = sha.ComputeHash(tableData);
		using BinaryWriter subWriter = new BinaryWriter(new MemoryStream());
		subWriter.Write(_stuffBeforeHash);
		subWriter.Write(hash);
		subWriter.Write(_changeNumber);
		subWriter.Write(tableHash);
		subWriter.Write(tableData);
		MemoryStream ms = (MemoryStream)subWriter.BaseStream;
		writer.Write((int)ms.Length);
		writer.Write(ms.GetBuffer(), 0, (int)ms.Length);
	}
}
