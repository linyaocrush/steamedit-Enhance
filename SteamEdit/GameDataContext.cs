#nullable enable
using System.Collections.Generic;
using SteamEdit.Data;

namespace SteamEdit;

internal class GameDataContext
{
	internal List<IAppInfoEntry> _entries = new();
	internal List<IAppInfoEntry> _baseEntries = new();

	public IReadOnlyList<IAppInfoEntry> Entries => _entries;
	public IReadOnlyList<IAppInfoEntry> BaseEntries => _baseEntries;
	public int TotalEntryCount { get; internal set; }
	public string CurrentSearchTerm { get; internal set; } = "";
	public Dictionary<int, string> DisplayNames { get; } = new();
}
