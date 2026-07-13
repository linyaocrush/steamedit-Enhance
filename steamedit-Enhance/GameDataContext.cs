#nullable enable
using System.Collections.Generic;
using steamedit_Enhance.Data;

namespace steamedit_Enhance;

internal class GameDataContext
{
	private List<IAppInfoEntry> _entries = new();
	private List<IAppInfoEntry> _baseEntries = new();

	public IReadOnlyList<IAppInfoEntry> Entries => _entries;
	public IReadOnlyList<IAppInfoEntry> BaseEntries => _baseEntries;
	public int TotalEntryCount { get; internal set; }
	public string CurrentSearchTerm { get; internal set; } = "";
	public Dictionary<int, string> DisplayNames { get; } = new();

	public int IndexOf(IAppInfoEntry entry) => _entries.IndexOf(entry);

	public void ReloadEntries(IReadOnlyList<IAppInfoEntry> visibleEntries, IReadOnlyList<IAppInfoEntry> allEntries)
	{
		_entries = new List<IAppInfoEntry>(visibleEntries);
		_baseEntries = new List<IAppInfoEntry>(allEntries);
	}
}
