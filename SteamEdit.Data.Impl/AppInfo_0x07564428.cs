using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SteamEdit.Data.Impl;

internal class AppInfo_0x07564428 : IAppInfo
{
	public const uint MagicNumber = 123094056u;

	private uint _unknownValueAtStart;

	private List<AppInfoEntry_0x07564428> _entries = new List<AppInfoEntry_0x07564428>();
	private List<IAppInfoEntry> _cachedEntries;
	private Dictionary<uint, IAppInfoEntry> _entryLookup;

	public IEnumerable<IAppInfoEntry> Entries => _cachedEntries ??= _entries.Cast<IAppInfoEntry>().ToList();

	public event EventHandler Modified;

	public IAppInfoEntry FindEntry(uint AppID)
	{
		if (_entryLookup == null)
			_entryLookup = _entries.ToDictionary(e => (uint)e.ID, e => (IAppInfoEntry)e);
		return _entryLookup.TryGetValue(AppID, out var entry) ? entry : null;
	}

	public static IAppInfo Load(BinaryReader reader)
	{
		AppInfo_0x07564428 appInfo_0x = new AppInfo_0x07564428();
		appInfo_0x._unknownValueAtStart = reader.ReadUInt32();
		AppInfoEntry_0x07564428 appInfoEntry_0x;
		while ((appInfoEntry_0x = AppInfoEntry_0x07564428.FromReader(reader)) != null)
		{
			appInfo_0x._entries.Add(appInfoEntry_0x);
			appInfoEntry_0x.Modified += appInfo_0x.OnEntryModified;
		}
		return appInfo_0x;
	}

	public void Save(string path)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		binaryWriter.Write(123094056u);
		binaryWriter.Write(_unknownValueAtStart);
		foreach (AppInfoEntry_0x07564428 entry in Entries)
		{
			entry.Write(binaryWriter);
		}
		binaryWriter.Write(0);
		using FileStream destination = File.Open(path, FileMode.Create, FileAccess.Write);
		binaryWriter.BaseStream.Position = 0L;
		binaryWriter.BaseStream.CopyTo(destination);
	}

	public void RevertEntry(IAppInfoEntry entry)
	{
		((AppInfoEntry_0x07564428)entry).Revert();
	}

	public void RemoveEntry(IAppInfoEntry entry)
	{
		if (entry != null)
		{
			AppInfoEntry_0x07564428 item = (AppInfoEntry_0x07564428)entry;
			_entries.Remove(item);
		}
	}

	private void OnEntryModified(object sender, EventArgs e)
	{
		this.Modified?.Invoke(this, new EventArgs());
	}
}
