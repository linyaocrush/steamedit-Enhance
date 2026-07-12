#nullable enable
using System.IO;

namespace steamedit_Enhance.Data;

/// <summary>
/// 文件系统操作抽象，用于解耦 SteamData 对静态 File 类的直接依赖，
/// 使 Save/Apply 的备份恢复逻辑可测试。
/// </summary>
internal interface IFileSystem
{
	bool FileExists(string path);
	void FileCopy(string source, string dest, bool overwrite);
	Stream OpenRead(string path);
	Stream CreateFile(string path);
	void FileMove(string source, string dest, bool overwrite);
	void FileDelete(string path);
	void CreateDirectory(string path);
}

/// <summary>
/// 委托给 System.IO.File 的默认实现。
/// </summary>
internal sealed class RealFileSystem : IFileSystem
{
	public static readonly RealFileSystem Instance = new();

	public bool FileExists(string path) => File.Exists(path);
	public void FileCopy(string source, string dest, bool overwrite) => File.Copy(source, dest, overwrite);
	public Stream OpenRead(string path) => File.OpenRead(path);
	public Stream CreateFile(string path) => File.Create(path);
	public void FileMove(string source, string dest, bool overwrite) => File.Move(source, dest, overwrite);
	public void FileDelete(string path) => File.Delete(path);
	public void CreateDirectory(string path) => Directory.CreateDirectory(path);
}
