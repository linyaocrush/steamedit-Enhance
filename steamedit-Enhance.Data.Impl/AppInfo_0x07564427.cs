#nullable enable
using System.IO;

namespace steamedit_Enhance.Data.Impl;

internal class AppInfo_0x07564427 : AppInfoBase<AppInfoEntry_0x07564427>
{
	public const uint MagicNumberValue = 123094055u;

	protected override uint MagicNumber => MagicNumberValue;

	public static AppInfo_0x07564427 Load(BinaryReader reader)
	{
		var appInfo = new AppInfo_0x07564427();
		appInfo.LoadEntriesCore(reader, AppInfoEntryBase.ReadFromReader<AppInfoEntry_0x07564427>);
		return appInfo;
	}

	protected override void SaveEntries(BinaryWriter writer)
	{
		foreach (var entry in Entries)
			((AppInfoEntryBase)entry).Write(writer);
		writer.Write(0);
	}
}
