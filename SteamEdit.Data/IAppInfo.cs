#nullable enable
using System;
using System.Collections.Generic;

namespace SteamEdit.Data;

internal interface IAppInfo
{
	IEnumerable<IAppInfoEntry> Entries { get; }

	event EventHandler? Modified;

	IAppInfoEntry? FindEntry(uint AppID);

	void Save(string path);

	bool RevertEntry(IAppInfoEntry entry);

	void RemoveEntry(IAppInfoEntry entry);
}
