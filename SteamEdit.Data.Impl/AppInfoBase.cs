#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		binaryWriter.Write(MagicNumber);
		binaryWriter.Write(_unknownValueAtStart);
		SaveEntries(binaryWriter);
		using FileStream destination = File.Open(path, FileMode.Create, FileAccess.Write);
		binaryWriter.BaseStream.Position = 0L;
		binaryWriter.BaseStream.CopyTo(destination);
	}

	public void RevertEntry(IAppInfoEntry entry)
	{
		if (entry != null)
		{
			((TEntry)entry).Revert();
		}
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
}
