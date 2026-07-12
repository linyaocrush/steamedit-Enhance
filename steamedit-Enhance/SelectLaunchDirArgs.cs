#nullable enable
using System;

namespace steamedit_Enhance;

internal class SelectLaunchDirArgs : EventArgs
{
	public string WorkingDir = null!;
}
