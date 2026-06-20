#nullable enable
using System.IO;

namespace SteamEdit.Data.Impl;

internal class AppInfo_0x07564428 : AppInfoBase<AppInfoEntry_0x07564428>
{
	public const uint MagicNumberValue = 123094056u;

	protected override uint MagicNumber => MagicNumberValue;

	public static AppInfo_0x07564428 Load(BinaryReader reader)
	{
		AppInfo_0x07564428 appInfo = new AppInfo_0x07564428();
		appInfo._unknownValueAtStart = reader.ReadUInt32();
		AppInfoEntry_0x07564428? entry;
		while ((entry = AppInfoEntry_0x07564428.FromReader(reader)) != null)
		{
			appInfo._entries.Add(entry);
			entry.Modified += appInfo.OnEntryModified;
		}
		return appInfo;
	}

	protected override void SaveEntries(BinaryWriter writer)
	{
		foreach (AppInfoEntry_0x07564428 entry in Entries)
		{
			entry.Write(writer);
		}
		writer.Write(0);
	}
}
