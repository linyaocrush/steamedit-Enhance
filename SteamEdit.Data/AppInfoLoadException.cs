#nullable enable
using System;

namespace SteamEdit.Data;

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
