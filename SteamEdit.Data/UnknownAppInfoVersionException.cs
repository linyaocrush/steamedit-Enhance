using System;

namespace SteamEdit.Data;

[Serializable]
internal class UnknownAppInfoVersionException : Exception
{
	public string AppInfoPath { get; private set; }

	public uint AppInfoVersion { get; private set; }

	public UnknownAppInfoVersionException(string appInfoPath, uint version)
		: base("Unknown version, when trying to load appinfo.vdf")
	{
		AppInfoPath = appInfoPath;
		AppInfoVersion = version;
	}
}
