#nullable enable
using System;

namespace SteamEdit.Data;

[Serializable]
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
