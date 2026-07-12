#nullable enable
using System;

namespace steamedit_Enhance.Data;

internal class AppInfoLoadException : Exception
{
	public AppInfoLoadException()
	{
	}

	public AppInfoLoadException(string message)
		: base(message)
	{
	}

	public AppInfoLoadException(string message, Exception e)
		: base(message, e)
	{
	}
}
