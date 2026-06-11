using System;

namespace SteamEdit.Data;

[Flags]
internal enum AppInfoEntryType
{
	Unknown = 1,
	Game = 2,
	Config = 4,
	Tool = 8,
	Demo = 0x10,
	DLC = 0x20,
	Application = 0x40,
	Video = 0x80,
	Guide = 0x100,
	Music = 0x200,
	Beta = 0x400
}
