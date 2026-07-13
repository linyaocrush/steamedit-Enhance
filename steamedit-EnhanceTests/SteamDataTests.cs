#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class SteamDataTests
{
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

		public void Save(string path, IFileSystem? fileSystem = null)
		{
			LastSavePath = path;
			SaveCallCount++;
			if (SaveShouldThrow)
				throw new IOException("Simulated save failure");
		}

		public void RaiseModified() => Modified?.Invoke(this, EventArgs.Empty);
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

	// ── 辅助方法 ──

	/// <summary>
	/// 创建一个配置好的 SteamData 实例用于测试。
	/// </summary>
	private static SteamData CreateSteamData(IFileSystem fs, FakeAppInfo appInfo, string appInfoPath, bool needsApply = true)
	{
		var svc = new SteamData();
		// 通过反射设置私有字段/属性（因为 Initialize 需要真实的 appinfo.vdf 文件）
		SetInstanceField(svc, "_initProps", new SteamDataInitProps { AppInfoPath = appInfoPath, DataDirectory = Path.GetDirectoryName(appInfoPath) ?? "" });
		SetInstanceProperty(svc, "AppInfoData", appInfo);
		SetInstanceProperty(svc, "AppInfoPath", appInfoPath);
		SetInstanceProperty(svc, "ModInfoPath", Path.Combine(Path.GetDirectoryName(appInfoPath) ?? "", "mods.dat"));
		SetInstanceProperty(svc, "SteamDir", Path.GetFullPath(Path.Combine(Path.GetDirectoryName(appInfoPath) ?? "", "..\\")));
		SetInstanceField(svc, "_needsApply", needsApply);
		SetInstanceField(svc, "_needsSave", false);
		svc.FileSystem = fs;
		return svc;
	}

	private static void SetInstanceField(object obj, string name, object? value)
	{
		var field = obj.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		field?.SetValue(obj, value);
	}

	private static void SetInstanceProperty(object obj, string name, object? value)
	{
		var prop = obj.GetType().GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
		prop?.SetValue(obj, value);
	}

	// ── Apply() 测试 ──

	[TestMethod]
	public void Apply_Success_SetsNeedsApplyFalse()
	{
		var fs = new MockFileSystem();
		var appInfo = new FakeAppInfo();
		var svc = CreateSteamData(fs, appInfo, @"C:\steam\appcache\appinfo.vdf");

		svc.Apply();

		Assert.IsFalse(svc.NeedsApply);
		Assert.AreEqual(1, appInfo.SaveCallCount);
	}

	[TestMethod]
	public void Apply_Success_CreatesBackup()
	{
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		var fs = new MockFileSystem();
		fs.Files[appInfoPath] = new byte[] { 1, 2, 3 };
		var appInfo = new FakeAppInfo();
		var svc = CreateSteamData(fs, appInfo, appInfoPath);

		svc.Apply();

		string backupPath = appInfoPath + ".bak";
		Assert.IsTrue(fs.Files.ContainsKey(backupPath));
		CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, fs.Files[backupPath]);
	}

	[TestMethod]
	public void Apply_SaveThrows_RestoresFromBackup()
	{
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		byte[] originalData = { 1, 2, 3 };
		var fs = new MockFileSystem();
		fs.Files[appInfoPath] = (byte[])originalData.Clone();
		var appInfo = new FakeAppInfo { SaveShouldThrow = true };
		var svc = CreateSteamData(fs, appInfo, appInfoPath);

		try { svc.Apply(); }
		catch (IOException) { /* expected */ }

		// 备份应已恢复
		CollectionAssert.AreEqual(originalData, fs.Files[appInfoPath]);
	}

	[TestMethod]
	public void Apply_SaveThrows_PreservesNeedsApply()
	{
		var fs = new MockFileSystem();
		var appInfo = new FakeAppInfo { SaveShouldThrow = true };
		var svc = CreateSteamData(fs, appInfo, @"C:\steam\appcache\appinfo.vdf");

		try { svc.Apply(); }
		catch (IOException) { /* expected */ }

		// 失败时 NeedsApply 应保持 true（未被清除）
		Assert.IsTrue(svc.NeedsApply);
	}

	[TestMethod]
	public void Apply_SaveThrows_SetsNeedsApplyTrue_EvenIfWasFalse()
	{
		// 边界情况：NeedsApply 原本为 false 时调用 Apply() 失败
		// 修复 R1：catch 块中显式设置 NeedsApply = true
		var fs = new MockFileSystem();
		var appInfo = new FakeAppInfo { SaveShouldThrow = true };
		var svc = CreateSteamData(fs, appInfo, @"C:\steam\appcache\appinfo.vdf", needsApply: false);

		try { svc.Apply(); }
		catch (IOException) { /* expected */ }

		Assert.IsTrue(svc.NeedsApply, "Apply 失败后 NeedsApply 必须为 true");
	}

	[TestMethod]
	public void Apply_BackupRestoreFails_StillThrowsOriginal()
	{
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		var fs = new MockFileSystem();
		fs.Files[appInfoPath] = new byte[] { 1, 2, 3 };
		var appInfo = new FakeAppInfo { SaveShouldThrow = true };

		// 第二次 File.Copy（恢复）时也失败
		int copyCount = 0;
		var wrappedFs = new CopyFailOnSecondCall(fs, () => ++copyCount >= 2);
		var svc = CreateSteamData(wrappedFs, appInfo, appInfoPath);

		bool caught = false;
		try { svc.Apply(); }
		catch (IOException ex) when (ex.Message == "Simulated save failure")
		{
			caught = true;
		}
		Assert.IsTrue(caught, "Should re-throw the original save exception");
	}

	[TestMethod]
	public void Apply_CallsAppInfoSaveWithCorrectPath()
	{
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		var fs = new MockFileSystem();
		var appInfo = new FakeAppInfo();
		var svc = CreateSteamData(fs, appInfo, appInfoPath);

		svc.Apply();

		Assert.AreEqual(appInfoPath, appInfo.LastSavePath);
	}

	[TestMethod]
	public void Apply_NoExistingFile_SkipsBackup()
	{
		// appinfo.vdf 不存在时，不应创建备份
		var appInfoPath = @"C:\steam\appcache\appinfo.vdf";
		var fs = new MockFileSystem();
		var appInfo = new FakeAppInfo();
		var svc = CreateSteamData(fs, appInfo, appInfoPath);

		svc.Apply();

		string backupPath = appInfoPath + ".bak";
		Assert.IsFalse(fs.Files.ContainsKey(backupPath));
	}

	// ── 接口隔离测试 ──

	[TestMethod]
	public void SteamData_ImplementsBothReadAndFullInterfaces()
	{
		var svc = new SteamData();
		Assert.IsTrue(svc is ISteamDataReadService);
		Assert.IsTrue(svc is ISteamDataService);
	}

	[TestMethod]
	public void ReadService_DoesNotExposeWriteOperations()
	{
		// 验证 ISteamDataReadService 接口不包含 Save/Apply 等写入方法
		var readInterface = typeof(ISteamDataReadService);
		var writeMethods = new[] { "Save", "Apply", "Initialize", "Shutdown" };
		foreach (var method in writeMethods)
			Assert.IsFalse(readInterface.GetMethod(method) != null,
				$"ISteamDataReadService should not expose '{method}'");
	}

	[TestMethod]
	public void ReadService_UseCompletedAndUseHidden_AreReadOnly()
	{
		// 验证 ISteamDataReadService 的 UseCompleted/UseHidden 只有 getter
		var completedProp = typeof(ISteamDataReadService).GetProperty("UseCompleted");
		Assert.IsNotNull(completedProp);
		Assert.IsNotNull(completedProp!.GetMethod);
		Assert.IsNull(completedProp.SetMethod);

		var hiddenProp = typeof(ISteamDataReadService).GetProperty("UseHidden");
		Assert.IsNotNull(hiddenProp);
		Assert.IsNotNull(hiddenProp!.GetMethod);
		Assert.IsNull(hiddenProp.SetMethod);
	}
}
