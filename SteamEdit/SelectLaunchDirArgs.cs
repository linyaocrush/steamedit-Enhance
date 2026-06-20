#nullable enable
using System;

namespace SteamEdit;

internal class SelectLaunchDirArgs : EventArgs
{
	public string WorkingDir;
}
