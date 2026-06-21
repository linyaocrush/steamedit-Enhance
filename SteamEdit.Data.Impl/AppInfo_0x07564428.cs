#nullable enable
using System.IO;

namespace SteamEdit.Data.Impl;

internal class AppInfo_0x07564428 : AppInfoBase<AppInfoEntry_0x07564428>
{
	public const uint MagicNumberValue = 123094056u;

	protected override uint MagicNumber => MagicNumberValue;

	public static AppInfo_0x07564428 Load(BinaryReader reader)
	{
		var appInfo = new AppInfo_0x07564428();
		appInfo.LoadEntriesCore(reader, AppInfoEntry_0x07564428.FromReader);
		return appInfo;
	}

	protected override void SaveEntries(BinaryWriter writer)
	{
		foreach (var entry in Entries)
			((AppInfoEntry_0x07564428)entry).Write(writer);
		writer.Write(0);
	}
}
