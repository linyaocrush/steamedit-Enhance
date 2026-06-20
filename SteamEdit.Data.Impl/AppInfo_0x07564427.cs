#nullable enable
using System.IO;

namespace SteamEdit.Data.Impl;

internal class AppInfo_0x07564427 : AppInfoBase<AppInfoEntry_0x07564427>
{
	public const uint MagicNumberValue = 123094055u;

	protected override uint MagicNumber => MagicNumberValue;

	public static AppInfo_0x07564427 Load(BinaryReader reader)
	{
		AppInfo_0x07564427 appInfo = new AppInfo_0x07564427();
		appInfo._unknownValueAtStart = reader.ReadUInt32();
		AppInfoEntry_0x07564427? entry;
		while ((entry = AppInfoEntry_0x07564427.FromReader(reader)) != null)
		{
			appInfo._entries.Add(entry);
			entry.Modified += appInfo.OnEntryModified;
		}
		return appInfo;
	}

	protected override void SaveEntries(BinaryWriter writer)
	{
		foreach (AppInfoEntry_0x07564427 entry in Entries)
		{
			entry.Write(writer);
		}
		writer.Write(0);
	}
}
