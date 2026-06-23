#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common;

namespace SteamEdit.Data.Impl;

internal abstract class AppInfoBase<TEntry> : IAppInfo where TEntry : AppInfoEntryBase
{
	protected uint _unknownValueAtStart;
	protected List<TEntry> _entries = new List<TEntry>();
	private List<IAppInfoEntry>? _cachedEntries;
	private Dictionary<uint, IAppInfoEntry>? _entryLookup;

	protected abstract uint MagicNumber { get; }

	public IEnumerable<IAppInfoEntry> Entries => _cachedEntries ??= _entries.Cast<IAppInfoEntry>().ToList();

	public event EventHandler? Modified;

	public IAppInfoEntry? FindEntry(uint AppID)
	{
		if (_entryLookup == null)
			_entryLookup = _entries.ToDictionary(e => (uint)e.ID, e => (IAppInfoEntry)e);
		return _entryLookup.TryGetValue(AppID, out var entry) ? entry : null;
	}

	public void Save(string path)
	{
		string tempPath = path + ".tmp";
		try
		{
			using (FileStream tempStream = File.Create(tempPath))
			using (BinaryWriter binaryWriter = new BinaryWriter(tempStream))
			{
				binaryWriter.Write(MagicNumber);
				binaryWriter.Write(_unknownValueAtStart);
				SaveEntries(binaryWriter);
			}
			File.Replace(tempPath, path, null);
		}
		catch
		{
			try { if (File.Exists(tempPath)) File.Delete(tempPath); }
			catch (Exception cleanupEx) { Log.WriteLine("AppInfoBase.Save cleanup failed: {0}", cleanupEx.Message); }
			throw;
		}
	}

	public bool RevertEntry(IAppInfoEntry entry)
	{
		if (entry != null)
		{
			return ((TEntry)entry).Revert();
		}
		return false;
	}

	public void RemoveEntry(IAppInfoEntry entry)
	{
		if (entry != null)
		{
			TEntry item = (TEntry)entry;
			_entries.Remove(item);
			_entryLookup?.Remove((uint)item.ID);
			_cachedEntries = null;
		}
	}

	protected void OnEntryModified(object? sender, EventArgs e)
	{
		this.Modified?.Invoke(this, new EventArgs());
	}

	protected abstract void SaveEntries(BinaryWriter writer);

	/// <summary>
	/// 通用的条目加载辅助。读取 unknownValueAtStart 后，循环调用 readEntry 直到返回 null。
	/// </summary>
	protected void LoadEntriesCore(BinaryReader reader, Func<BinaryReader, TEntry?> readEntry)
	{
		_unknownValueAtStart = reader.ReadUInt32();
		ReadEntriesLoop(reader, readEntry);
	}

	/// <summary>
	/// 条目循环读取。当 unknownValueAtStart 已在外部读取时使用此方法。
	/// </summary>
	protected void ReadEntriesLoop(BinaryReader reader, Func<BinaryReader, TEntry?> readEntry)
	{
		TEntry? entry;
		while ((entry = readEntry(reader)) != null)
		{
			_entries.Add(entry);
			entry.Modified += OnEntryModified;
		}
	}
}
