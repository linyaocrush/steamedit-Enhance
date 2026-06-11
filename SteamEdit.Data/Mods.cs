using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common;

namespace SteamEdit.Data;

internal class Mods
{
	private delegate void ReaderFunction(BinaryReader reader, List<ModifiedEntry> modEntries);

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
			ReaderFunction readerFunction = mods.GetReaderFunction(version);
			if (readerFunction != null)
			{
				List<ModifiedEntry> list = new List<ModifiedEntry>();
				readerFunction(binaryReader, list);
				foreach (ModifiedEntry modEntry in list)
				{
					IAppInfoEntry appInfoEntry = mods._appInfo.Entries.FirstOrDefault((IAppInfoEntry e) => e.ID == modEntry.ID);
					if (appInfoEntry != null)
					{
						appInfoEntry.Modification = modEntry;
					}
				}
			}
		}
		return mods;
	}

	private ReaderFunction GetReaderFunction(uint version)
	{
		switch (version)
		{
		case 4u:
			return ReadModsV4;
		case 5u:
			return ReadModsV5;
		case 6u:
			return ReadModsV6;
		default:
			Log.WriteLine("Mods file has unsupported version {0}", version);
			return null;
		}
	}

	private void ReadModsV4(BinaryReader reader, List<ModifiedEntry> modEntries)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			ModifiedEntry modifiedEntry = new ModifiedEntry(reader.ReadInt32());
			modifiedEntry.Changes = PropertyTableIO.ReadPropertyTable_Mods(reader);
			modifiedEntry.OriginalDataVersion = 0u;
			int num2 = reader.ReadInt32();
			reader.BaseStream.Seek(36L, SeekOrigin.Current);
			modifiedEntry.OriginalDataChangeNumber = reader.ReadUInt32();
			reader.BaseStream.Seek(20L, SeekOrigin.Current);
			modifiedEntry.OriginalData = reader.ReadBytes(num2 - 60);
			modEntries.Add(modifiedEntry);
		}
	}

	private void ReadModsV5(BinaryReader reader, List<ModifiedEntry> modEntries)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			ModifiedEntry modifiedEntry = new ModifiedEntry(reader.ReadInt32());
			modifiedEntry.Changes = PropertyTableIO.ReadPropertyTable_Mods(reader);
			modifiedEntry.OriginalDataVersion = reader.ReadUInt32();
			int num2 = reader.ReadInt32();
			reader.BaseStream.Seek(36L, SeekOrigin.Current);
			modifiedEntry.OriginalDataChangeNumber = reader.ReadUInt32();
			reader.BaseStream.Seek(20L, SeekOrigin.Current);
			modifiedEntry.OriginalData = reader.ReadBytes(num2 - 60);
			if (modifiedEntry.OriginalDataVersion >= 123094057)
			{
				modifiedEntry.OriginalData = null;
				modifiedEntry.OriginalDataVersion = 0u;
				modifiedEntry.OriginalDataChangeNumber = 0u;
			}
			modEntries.Add(modifiedEntry);
		}
	}

	private void ReadModsV6(BinaryReader reader, List<ModifiedEntry> modEntries)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			ModifiedEntry modifiedEntry = new ModifiedEntry(reader.ReadInt32());
			modifiedEntry.Changes = PropertyTableIO.ReadPropertyTable_Mods(reader);
			modifiedEntry.OriginalDataVersion = reader.ReadUInt32();
			modifiedEntry.OriginalDataChangeNumber = reader.ReadUInt32();
			int num2 = reader.ReadInt32();
			if (num2 > 0)
			{
				modifiedEntry.OriginalData = reader.ReadBytes(num2);
			}
			else
			{
				modifiedEntry.OriginalData = null;
			}
			modEntries.Add(modifiedEntry);
		}
	}

	public void Save(string path)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		binaryWriter.Write(6u);
		int value = _appInfo.Entries.Count((IAppInfoEntry e) => e.IsModified);
		binaryWriter.Write(value);
		foreach (IAppInfoEntry item in _appInfo.Entries.Where((IAppInfoEntry e) => e.IsModified))
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
