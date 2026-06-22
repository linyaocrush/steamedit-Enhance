#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamEdit.Data;

namespace SteamEditTests;

[TestClass]
public class SteamDataTests
{
	/// <summary>
	/// 记录文件操作的 mock 文件系统。
	/// </summary>
	private class MockFileSystem : IFileSystem
	{
		public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
		public List<(string Op, string Arg1, string? Arg2)> Operations { get; } = new();
		public bool CopyShouldThrow { get; set; }

		public bool FileExists(string path)
		{
			Operations.Add(("Exists", path, null));
			return Files.ContainsKey(path);
		}

		public void FileCopy(string source, string dest, bool overwrite)
		{
			Operations.Add(("Copy", source, dest));
			if (CopyShouldThrow) throw new IOException("Copy failed");
			if (!Files.TryGetValue(source, out var data))
				throw new FileNotFoundException("Source not found", source);
			Files[dest] = (byte[])data.Clone();
		}

		public Stream OpenRead(string path)
		{
			Operations.Add(("OpenRead", path, null));
			if (!Files.TryGetValue(path, out var data))
				throw new FileNotFoundException("File not found", path);
			return new MemoryStream(data);
		}

		public Stream CreateFile(string path)
		{
			Operations.Add(("Create", path, null));
			var ms = new MemoryStream();
			return new TrackedMemoryStream(ms, data => Files[path] = data);
		}

		public void FileMove(string source, string dest, bool overwrite)
		{
			Operations.Add(("Move", source, dest));
			if (!Files.TryGetValue(source, out var data))
				throw new FileNotFoundException("Source not found", source);
			Files[dest] = data;
			Files.Remove(source);
		}

		public void FileDelete(string path)
		{
			Operations.Add(("Delete", path, null));
			Files.Remove(path);
		}

		public void CreateDirectory(string path)
		{
			Operations.Add(("CreateDir", path, null));
		}
	}

	/// <summary>
	/// 关闭时将内容回调的 MemoryStream，用于捕获 CreateFile 的写入数据。
	/// </summary>
	private class TrackedMemoryStream : MemoryStream
	{
		private readonly Action<byte[]> _onClose;
		public TrackedMemoryStream(MemoryStream inner, Action<byte[]> onClose) : base() { _onClose = onClose; }
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_onClose(GetBuffer().AsSpan(0, (int)Position).ToArray());
			}
			base.Dispose(disposing);
		}
	}

	/// <summary>
	/// 模拟 IAppInfo，可控制 Save 行为。
	/// </summary>
	private class FakeAppInfo : IAppInfo
	{
		public IEnumerable<IAppInfoEntry> Entries { get; set; } = Array.Empty<IAppInfoEntry>();
		public event EventHandler? Modified;
		public IAppInfoEntry? FindEntry(uint AppID) => null;
		public bool RevertEntry(IAppInfoEntry entry) => false;
		public void RemoveEntry(IAppInfoEntry entry) { }

		public bool SaveShouldThrow { get; set; }
		public string? LastSavePath { get; private set; }
		public int SaveCallCount { get; private set; }

		public void Save(string path)
		{
			LastSavePath = path;
			SaveCallCount++;
			if (SaveShouldThrow)
				throw new IOException("Simulated save failure");
		}

		public void RaiseModified() => Modified?.Invoke(this, EventArgs.Empty);
	}

	private MockFileSystem _mockFs = null!;
	private FakeAppInfo _fakeAppInfo = null!;
	private IFileSystem _originalFs = null!;
	private IAppInfo _originalAppInfo = null!;
	private string _originalAppInfoPath = null!;
	private string _originalModInfoPath = null!;
	private bool _originalNeedsSave;
	private bool _originalNeedsApply;

	[TestInitialize]
	public void SetUp()
	{
		_mockFs = new MockFileSystem();
		_fakeAppInfo = new FakeAppInfo();

		// 保存原始状态
		_originalFs = GetField<IFileSystem>("FileSystem")!;
		_originalAppInfo = SteamData.AppInfoData;
		_originalAppInfoPath = SteamData.AppInfoPath;
		_originalModInfoPath = SteamData.ModInfoPath;
		_originalNeedsSave = SteamData.NeedsSave;
		_originalNeedsApply = SteamData.NeedsApply;
	}

	[TestCleanup]
	public void TearDown()
	{
		// 恢复原始状态
		SetStaticProperty("FileSystem", _originalFs);
		SetStaticProperty("AppInfoData", _originalAppInfo);
		SetStaticProperty("AppInfoPath", _originalAppInfoPath);
		SetStaticProperty("ModInfoPath", _originalModInfoPath);
		SetField("_needsSave", _originalNeedsSave);
		SetField("_needsApply", _originalNeedsApply);
	}

	// ── Apply() 测试 ──

	[TestMethod]
	public void Apply_Success_SetsNeedsApplyFalse()
	{
		ArrangeApply(appInfoPath: @"C:\steam\appcache\appinfo.vdf");

		SteamData.Apply();

		Assert.IsFalse(SteamData.NeedsApply);
		Assert.AreEqual(1, _fakeAppInfo.SaveCallCount);
	}

	[TestMethod]
	public void Apply_Success_CreatesBackup()
	{
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		_mockFs.Files[appInfoPath] = new byte[] { 1, 2, 3 };
		ArrangeApply(appInfoPath);

		SteamData.Apply();

		string backupPath = appInfoPath + ".bak";
		Assert.IsTrue(_mockFs.Files.ContainsKey(backupPath));
		CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, _mockFs.Files[backupPath]);
	}

	[TestMethod]
	public void Apply_SaveThrows_RestoresFromBackup()
	{
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		byte[] originalData = { 1, 2, 3 };
		_mockFs.Files[appInfoPath] = (byte[])originalData.Clone();
		_fakeAppInfo.SaveShouldThrow = true;
		ArrangeApply(appInfoPath);

		try { SteamData.Apply(); }
		catch (IOException) { /* expected */ }

		// 备份应已恢复
		CollectionAssert.AreEqual(originalData, _mockFs.Files[appInfoPath]);
	}

	[TestMethod]
	public void Apply_SaveThrows_PreservesNeedsApply()
	{
		ArrangeApply(appInfoPath: @"C:\steam\appcache\appinfo.vdf");
		_fakeAppInfo.SaveShouldThrow = true;

		try { SteamData.Apply(); }
		catch (IOException) { /* expected */ }

		// 失败时 NeedsApply 应保持 true（未被清除）
		Assert.IsTrue(SteamData.NeedsApply);
	}

	[TestMethod]
	public void Apply_BackupRestoreFails_StillThrowsOriginal()
	{
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		_mockFs.Files[appInfoPath] = new byte[] { 1, 2, 3 };
		_fakeAppInfo.SaveShouldThrow = true;
		ArrangeApply(appInfoPath);

		// 第二次 File.Copy（恢复）时也失败
		int copyCount = 0;
		var originalFs = _mockFs;
		var wrappedFs = new CopyFailOnSecondCall(originalFs, () => ++copyCount >= 2);
		SetStaticProperty("FileSystem", wrappedFs);

		bool caught = false;
		try { SteamData.Apply(); }
		catch (IOException ex) when (ex.Message == "Simulated save failure")
		{
			caught = true;
		}
		Assert.IsTrue(caught, "Should re-throw the original save exception");
	}

	/// <summary>
	/// 第 N 次 Copy 调用时抛出异常的包装文件系统。
	/// </summary>
	private class CopyFailOnSecondCall : IFileSystem
	{
		private readonly IFileSystem _inner;
		private readonly Func<bool> _shouldFail;
		public CopyFailOnSecondCall(IFileSystem inner, Func<bool> shouldFail) { _inner = inner; _shouldFail = shouldFail; }
		public bool FileExists(string path) => _inner.FileExists(path);
		public void FileCopy(string source, string dest, bool overwrite)
		{
			if (_shouldFail()) throw new IOException("Copy failed during restore");
			_inner.FileCopy(source, dest, overwrite);
		}
		public Stream OpenRead(string path) => _inner.OpenRead(path);
		public Stream CreateFile(string path) => _inner.CreateFile(path);
		public void FileMove(string source, string dest, bool overwrite) => _inner.FileMove(source, dest, overwrite);
		public void FileDelete(string path) => _inner.FileDelete(path);
		public void CreateDirectory(string path) => _inner.CreateDirectory(path);
	}

	// ── Save() 测试 ──

	[TestMethod]
	public void Apply_CallsAppInfoSaveWithCorrectPath()
	{
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		ArrangeApply(appInfoPath);

		SteamData.Apply();

		Assert.AreEqual(appInfoPath, _fakeAppInfo.LastSavePath);
	}

	[TestMethod]
	public void Apply_NoExistingFile_SkipsBackup()
	{
		// appinfo.vdf 不存在时，不应创建备份
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		ArrangeApply(appInfoPath);
		// 不添加文件到 mock

		SteamData.Apply();

		string backupPath = appInfoPath + ".bak";
		Assert.IsFalse(_mockFs.Files.ContainsKey(backupPath));
	}

	// ── 辅助方法 ──

	private void ArrangeApply(string appInfoPath)
	{
		SetStaticProperty("FileSystem", _mockFs);
		SetStaticProperty("AppInfoData", _fakeAppInfo);
		SetStaticProperty("AppInfoPath", appInfoPath);
		SetField("_needsApply", true);
	}

	private static T? GetField<T>(string name)
	{
		var field = typeof(SteamData).GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
		return (T?)field?.GetValue(null);
	}

	private static void SetField(string name, object? value)
	{
		var field = typeof(SteamData).GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
		field?.SetValue(null, value);
	}

	private static void SetStaticProperty(string name, object? value)
	{
		var prop = typeof(SteamData).GetProperty(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		prop?.SetValue(null, value);
	}
}
