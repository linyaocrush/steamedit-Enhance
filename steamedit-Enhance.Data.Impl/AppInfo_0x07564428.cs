#nullable enable
using System.IO;

namespace steamedit_Enhance.Data.Impl;

internal class AppInfo_0x07564428 : AppInfoBase<AppInfoEntry_0x07564428>
{
	public const uint MagicNumberValue = 123094056u;

	protected override uint MagicNumber => MagicNumberValue;

	public static AppInfo_0x07564428 Load(BinaryReader reader)
	{
		var appInfo = new AppInfo_0x07564428();
		appInfo.LoadEntriesCore(reader, AppInfoEntryBase.ReadFromReader<AppInfoEntry_0x07564428>);
		return appInfo;
	}


}
