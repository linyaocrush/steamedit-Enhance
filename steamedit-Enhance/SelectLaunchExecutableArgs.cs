#nullable enable
using System;

namespace steamedit_Enhance;

internal class SelectLaunchExecutableArgs : EventArgs
{
	public string Executable = null!;

	public bool MakeWorkingDirMatch;
}
