using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SteamEdit.Data.Impl;

internal class AppInfo_0x07564429 : IAppInfo
{
	public const uint MagicNumber = 123094057u;

	private uint _unknownValueAtStart;

	private List<AppInfoEntry_0x07564429> _entries = new List<AppInfoEntry_0x07564429>();

	public IEnumerable<IAppInfoEntry> Entries => _entries.Cast<IAppInfoEntry>();

	public event EventHandler Modified;

	public IAppInfoEntry FindEntry(uint AppID)
	{
		foreach (AppInfoEntry_0x07564429 entry in _entries)
		{
			if (entry.ID == AppID)
			{
				return entry;
			}
		}
		return null;
	}

	public static IAppInfo Load(BinaryReader reader)
	{
		AppInfo_0x07564429 appInfo_0x = new AppInfo_0x07564429();
		appInfo_0x._unknownValueAtStart = reader.ReadUInt32();
		long position = reader.ReadInt64();
		long position2 = reader.BaseStream.Position;
		reader.BaseStream.Position = position;
		uint num = reader.ReadUInt32();
		string[] array = new string[num];
		for (uint num2 = 0u; num2 < num; num2++)
		{
			array[num2] = PropertyTableIO.ReadZString(reader);
		}
		reader.BaseStream.Position = position2;
		AppInfoEntry_0x07564429 appInfoEntry_0x;
		while ((appInfoEntry_0x = AppInfoEntry_0x07564429.FromReader(reader, array)) != null)
		{
			appInfo_0x._entries.Add(appInfoEntry_0x);
			appInfoEntry_0x.Modified += appInfo_0x.OnEntryModified;
		}
		return appInfo_0x;
	}

	public void Save(string path)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		binaryWriter.Write(123094057u);
		binaryWriter.Write(_unknownValueAtStart);
		List<string> stringTable = new List<string>();
		Dictionary<string, uint> stringLookup = new Dictionary<string, uint>();
		using (BinaryWriter binaryWriter2 = new BinaryWriter(new MemoryStream()))
		{
			foreach (AppInfoEntry_0x07564429 entry in Entries)
			{
				entry.Write(binaryWriter2, ref stringTable, ref stringLookup);
			}
			binaryWriter2.Write(0);
			long value = binaryWriter.BaseStream.Position + 8 + binaryWriter2.BaseStream.Length;
			binaryWriter.Write(value);
			binaryWriter.Write(((MemoryStream)binaryWriter2.BaseStream).ToArray());
			binaryWriter.Write((uint)stringTable.Count);
			foreach (string item in stringTable)
			{
				PropertyTableIO.WriteZString(binaryWriter, item);
			}
		}
		using FileStream destination = File.Open(path, FileMode.Create, FileAccess.Write);
		binaryWriter.BaseStream.Position = 0L;
		binaryWriter.BaseStream.CopyTo(destination);
	}

	public void RevertEntry(IAppInfoEntry entry)
	{
		((AppInfoEntry_0x07564429)entry).Revert();
	}

	public void RemoveEntry(IAppInfoEntry entry)
	{
		if (entry != null)
		{
			AppInfoEntry_0x07564429 item = (AppInfoEntry_0x07564429)entry;
			_entries.Remove(item);
		}
	}

	private void OnEntryModified(object sender, EventArgs e)
	{
		this.Modified?.Invoke(this, new EventArgs());
	}
}
