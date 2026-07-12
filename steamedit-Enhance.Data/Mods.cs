#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common;

namespace steamedit_Enhance.Data;

internal class Mods
{
	private const uint CurrentVersion = 6u;
	private const int MaxEntryCount = 100_000;
	private const int V4UnknownFieldSize = 36;
	private const int V4ChangeNumberOffset = 20;
	private const int V4MinDataSize = 60;

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
		if (count < 0 || count > MaxEntryCount)
			throw new InvalidDataException($"Mods entry count {count} is out of valid range.");
		if (count > remaining / 8)
			throw new InvalidDataException($"Mods entry count {count} exceeds available stream data.");
		for (int i = 0; i < count; i++)
		{
			ModifiedEntry entry = new ModifiedEntry(reader.ReadInt32());
			entry.Changes = PropertyTableIO.ReadPropertyTable_0x00000000(reader);

			// V4: OriginalDataVersion 不在文件中，硬编码 0
			entry.OriginalDataVersion = (version >= 5) ? reader.ReadUInt32() : 0u;

			if (version <= 5)
			{
				// V4/V5: 原始格式，跳过 V4UnknownFieldSize+V4ChangeNumberOffset=60 字节的未知字段
				int dataSize = reader.ReadInt32();
				long dataRemaining = reader.BaseStream.Length - reader.BaseStream.Position;
				if (dataSize < V4MinDataSize || dataSize > dataRemaining)
					throw new InvalidDataException($"Mods entry dataSize {dataSize} is out of valid range (remaining={dataRemaining}).");
				reader.BaseStream.Seek(V4UnknownFieldSize, SeekOrigin.Current);
				entry.OriginalDataChangeNumber = reader.ReadUInt32();
				reader.BaseStream.Seek(V4ChangeNumberOffset, SeekOrigin.Current);
				entry.OriginalData = reader.ReadBytes(dataSize - V4MinDataSize);
			}
			else
			{
				// V6: 紧凑格式，无跳过
				entry.OriginalDataChangeNumber = reader.ReadUInt32();
				int dataSize = reader.ReadInt32();
				if (dataSize < 0)
					throw new InvalidDataException($"Mods entry dataSize {dataSize} is negative.");
				if (dataSize > 0)
				{
					long dataRemaining = reader.BaseStream.Length - reader.BaseStream.Position;
					if (dataSize > dataRemaining)
						throw new InvalidDataException($"Mods entry dataSize {dataSize} exceeds available stream data (remaining={dataRemaining}).");
				}
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

	public void Save(string path, IFileSystem fileSystem)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		binaryWriter.Write(6u);
		var modifiedEntries = _appInfo.Entries.Where(e => e.IsModified).ToList();
		binaryWriter.Write(modifiedEntries.Count);
		foreach (IAppInfoEntry item in modifiedEntries)
		{
			var mod = item.Modification!;
			binaryWriter.Write(mod.ID);
			PropertyTableIO.WritePropertyTable_0x00000000(binaryWriter, mod.Changes);
			binaryWriter.Write(mod.OriginalDataVersion);
			binaryWriter.Write(mod.OriginalDataChangeNumber);
			binaryWriter.Write((mod.OriginalData != null) ? mod.OriginalData.Length : 0);
			if (mod.OriginalData != null)
			{
				binaryWriter.Write(mod.OriginalData);
			}
		}
		string tempPath = path + ".tmp";
		string backupPath = path + ".bak";
		bool hasBackup = false;
		try
		{
			if (fileSystem.FileExists(path))
			{
				fileSystem.FileCopy(path, backupPath, overwrite: true);
				hasBackup = true;
			}
			using (var destination = fileSystem.CreateFile(tempPath))
			{
				binaryWriter.BaseStream.Position = 0L;
				binaryWriter.BaseStream.CopyTo(destination);
			}
			fileSystem.FileMove(tempPath, path, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.WriteLine("Mods.Save failed: {0}", ex.ToString());
			// 清理临时文件
			try { if (fileSystem.FileExists(tempPath)) fileSystem.FileDelete(tempPath); }
			catch { /* best effort */ }
			// 从备份恢复
			if (hasBackup)
			{
				try
				{
					fileSystem.FileCopy(backupPath, path, overwrite: true);
					Log.WriteLine("  Mods restored from backup");
				}
				catch (Exception restoreEx)
				{
					Log.WriteLine("  Mods backup restore also failed: {0}", restoreEx.ToString());
				}
			}
			throw;
		}
	}
}
