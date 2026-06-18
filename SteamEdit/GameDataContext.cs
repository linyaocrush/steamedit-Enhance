using System.Collections.Generic;
using SteamEdit.Data;

namespace SteamEdit;

internal class GameDataContext
{
	public List<IAppInfoEntry> Entries { get; set; } = new();
	public List<IAppInfoEntry> BaseEntries { get; set; } = new();
	public int TotalEntryCount { get; set; }
	public string CurrentSearchTerm { get; set; } = "";
	public AppInfoEntryType CurrentFilters { get; set; }
	public Dictionary<int, string> DisplayNames { get; } = new();
}
