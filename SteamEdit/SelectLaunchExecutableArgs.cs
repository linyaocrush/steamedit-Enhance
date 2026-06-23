#nullable enable
using System;

namespace SteamEdit;

internal class SelectLaunchExecutableArgs : EventArgs
{
	public string Executable = null!;

	public bool MakeWorkingDirMatch;
}
