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
		var appInfo = new AppInfo_0x07564429();
		appInfo._unknownValueAtStart = reader.ReadUInt32();

		// 读取字符串表
		long stringTableOffset = reader.ReadInt64();
		long entriesStart = reader.BaseStream.Position;
		reader.BaseStream.Position = stringTableOffset;
		uint count = reader.ReadUInt32();
		if (count > 50_000)
			throw new InvalidDataException(
				$"String table size {count} exceeds maximum 50000. File may be corrupted.");
		string[] stringTable = new string[count];
		for (uint i = 0; i < count; i++)
			stringTable[i] = PropertyTableIO.ReadZString(reader);
		reader.BaseStream.Position = entriesStart;

		// 加载条目（使用字符串表上下文，_unknownValueAtStart 已在上方读取）
		appInfo.ReadEntriesLoop(reader, br => AppInfoEntry_0x07564429.FromReader(br, stringTable));
		return appInfo;
	}

	protected override void SaveEntries(BinaryWriter writer)
	{
		var stringTable = new List<string>();
		var stringLookup = new Dictionary<string, uint>();
		using var entryWriter = new BinaryWriter(new MemoryStream());
		foreach (var entry in Entries)
			((AppInfoEntry_0x07564429)entry).Write(entryWriter, stringTable, stringLookup);
		entryWriter.Write(0);
		// 写入字符串表偏移量 → 条目数据 → 字符串表
		long stringTableOffsetPos = writer.BaseStream.Position + 8 + entryWriter.BaseStream.Length;
		writer.Write(stringTableOffsetPos);
		writer.Write(((MemoryStream)entryWriter.BaseStream).ToArray());
		writer.Write((uint)stringTable.Count);
		foreach (string s in stringTable)
			PropertyTableIO.WriteZString(writer, s);
	}
}
