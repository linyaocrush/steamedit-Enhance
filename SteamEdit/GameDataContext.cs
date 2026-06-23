#nullable enable
using System.Collections.Generic;
using SteamEdit.Data;

namespace SteamEdit;

internal class GameDataContext
{
	public List<IAppInfoEntry> Entries { get; internal set; } = new();
	public List<IAppInfoEntry> BaseEntries { get; internal set; } = new();
	public int TotalEntryCount { get; internal set; }
	public string CurrentSearchTerm { get; internal set; } = "";
	public Dictionary<int, string> DisplayNames { get; } = new();
}
