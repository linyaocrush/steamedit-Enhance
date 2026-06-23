#nullable enable
using System;
using SteamEdit.Data;

namespace SteamEdit;

internal class CopyLaunchInfoArgs : EventArgs
{
	public LaunchItem LaunchInfo = null!;
}
