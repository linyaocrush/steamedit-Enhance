#nullable enable
using System;
using steamedit_Enhance.Data;

namespace steamedit_Enhance;

internal class CopyLaunchInfoArgs : EventArgs
{
	public LaunchItem LaunchInfo = null!;
}
