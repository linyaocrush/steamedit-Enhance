using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common;

namespace SteamEdit.Data;

internal class Mods
{
	private const uint CurrentVersion = 6u;

	private readonly IAppInfo _appInfo;

	private Mods(IAppInfo appInfo)
	{
		_appInfo = appInfo;
	}

	public static Mods Load(string path, IAppInfo appInfo)
	{
		Mods mods = new Mods(appInfo);
		if (File.Exists(path))
		{
			using BinaryReader binaryReader = new BinaryReader(File.OpenRead(path));
			uint version = binaryReader.ReadUInt32();
			if (version >= 4u && version <= 6u)
			{
				List<ModifiedEntry> list = new List<ModifiedEntry>();
				mods.ReadEntries(binaryReader, list, (int)version);
				var entryById = new Dictionary<int, IAppInfoEntry>();
				foreach (var entry in mods._appInfo.Entries) entryById[entry.ID] = entry;
				foreach (ModifiedEntry modEntry in list)
				{
					if (entryById.TryGetValue(modEntry.ID, out var appInfoEntry))
					{
						appInfoEntry.Modification = modEntry;
					}
				}
			}
			else
			{
				Log.WriteLine("Mods file has unsupported version {0}", version);
			}
		}
		return mods;
	}

	private void ReadEntries(BinaryReader reader, List<ModifiedEntry> modEntries, int version)
	{
		int count = reader.ReadInt32();
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		if (count < 0 || count > 100_000)
			throw new InvalidDataException($"Mods entry count {count} is out of valid range.");
		if (count > remaining / 8)
			throw new InvalidDataException($"Mods entry count {count} exceeds available stream data.");
		for (int i = 0; i < count; i++)
		{
			ModifiedEntry entry = new ModifiedEntry(reader.ReadInt32());
			entry.Changes = PropertyTableIO.ReadPropertyTable_Mods(reader);

			// V4: OriginalDataVersion 不在文件中，硬编码 0
			entry.OriginalDataVersion = (version >= 5) ? reader.ReadUInt32() : 0u;

			if (version <= 5)
			{
				// V4/V5: 原始格式，跳过 36+20=60 字节的未知字段
				int dataSize = reader.ReadInt32();
				reader.BaseStream.Seek(36L, SeekOrigin.Current);
				entry.OriginalDataChangeNumber = reader.ReadUInt32();
				reader.BaseStream.Seek(20L, SeekOrigin.Current);
				entry.OriginalData = reader.ReadBytes(dataSize - 60);
			}
			else
			{
				// V6: 紧凑格式，无跳过
				entry.OriginalDataChangeNumber = reader.ReadUInt32();
				int dataSize = reader.ReadInt32();
				entry.OriginalData = (dataSize > 0) ? reader.ReadBytes(dataSize) : null;
			}

			// V5: 0x07564429 格式的 entry 不保存原始数据
			if (version == 5 && entry.OriginalDataVersion >= 123094057)
			{
				entry.OriginalData = null;
				entry.OriginalDataVersion = 0u;
				entry.OriginalDataChangeNumber = 0u;
			}

			modEntries.Add(entry);
		}
	}

	public void Save(string path)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		binaryWriter.Write(6u);
		var modifiedEntries = _appInfo.Entries.Where(e => e.IsModified).ToList();
		binaryWriter.Write(modifiedEntries.Count);
		foreach (IAppInfoEntry item in modifiedEntries)
		{
			binaryWriter.Write(item.Modification.ID);
			PropertyTableIO.WritePropertyTable_Mods(binaryWriter, item.Modification.Changes);
			binaryWriter.Write(item.Modification.OriginalDataVersion);
			binaryWriter.Write(item.Modification.OriginalDataChangeNumber);
			binaryWriter.Write((item.Modification.OriginalData != null) ? item.Modification.OriginalData.Length : 0);
			if (item.Modification.OriginalData != null)
			{
				binaryWriter.Write(item.Modification.OriginalData);
			}
		}
		using FileStream destination = File.Open(path, FileMode.Create, FileAccess.Write);
		binaryWriter.BaseStream.Position = 0L;
		binaryWriter.BaseStream.CopyTo(destination);
	}
}
