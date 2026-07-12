#nullable enable
using System.IO;
using System.IO.MemoryMappedFiles;
using steamedit_Enhance.Data.Impl;

namespace steamedit_Enhance.Data;

internal static class AppInfoFactory
{
	public static IAppInfo Load(string path)
	{
		if (!File.Exists(path))
		{
			throw new FileNotFoundException("Cannot find Steam's appinfo.vdf file", path);
		}
		using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
		using var stream = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
		using BinaryReader binaryReader = new BinaryReader(stream);
		uint num = binaryReader.ReadUInt32();
		return num switch
		{
			AppInfo_0x07564427.MagicNumberValue => AppInfo_0x07564427.Load(binaryReader),
			AppInfo_0x07564428.MagicNumberValue => AppInfo_0x07564428.Load(binaryReader),
			AppInfo_0x07564429.MagicNumberValue => AppInfo_0x07564429.Load(binaryReader),
			_ => throw new UnknownAppInfoVersionException(path, num),
		};
	}
}
