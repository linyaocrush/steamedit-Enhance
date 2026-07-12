#nullable enable
using System.IO;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SteamEdit.Data;
using SteamEdit.Data.Impl;

namespace SteamEditTests;

/// <summary>
/// P0 高风险测试：数据完整性相关的核心逻辑。
/// </summary>
[TestClass]
public class DataIntegrityTests
{
	/// <summary>
	/// 测试用 Entry，可配置 completed/hidden 状态和 DisplaySettings。
	/// </summary>
	private class TestEntry : AppInfoEntryBase
	{
		public override uint AppInfoVersion => 0x07564428u;

		public TestEntry(IEntryDisplaySettings ds, int id = 12345, bool completed = false, bool hidden = false)
		{
			DisplaySettings = ds;
			ID = id;
			_stuffBeforeHash = new byte[16]; // 初始化为全零
			_changeNumber = 0;
			_properties = new PropertyTable();
			var appinfo = new PropertyTable();
			var common = new PropertyTable();
			common.AddPropertyValue("name", PropertyType.String, "Test Game");
			common.AddPropertyValue("type", PropertyType.String, "game");
			appinfo.AddPropertyValue("common", PropertyType.Table, common);
			_properties.AddPropertyValue("appinfo", PropertyType.Table, appinfo);
			_originalData = PropertyTableIO.WritePropertyTable_0x00000000(_properties);
			RestoreBaseProperties();
			IsCompleted = completed;
			IsHidden = hidden;
		}

		public void Write(BinaryWriter writer)
		{
			WriteEntryCore(writer, 2, (subWriter, table) =>
			{
				byte[] tableData = PropertyTableIO.WritePropertyTable_0x00000000(table);
				subWriter.Write(tableData);
			});
		}

		/// <summary>
		/// 暴露 PrepareWriteTable 供测试。
		/// </summary>
		public PropertyTable PrepareWriteTableForTest(out PropertyTable common)
		{
			return PrepareWriteTable(out common);
		}

		/// <summary>
		/// 将当前属性序列化为字节数组（模拟保存后的原始数据）。
		/// </summary>
		public byte[] SerializeCurrentProperties()
		{
			return PropertyTableIO.WritePropertyTable_0x00000000(_properties);
		}
	}

	/// <summary>
	/// 测试用 DisplaySettings：UseCompleted 和 UseHidden 都启用。
	/// </summary>
	private class TestDisplaySettings_AllEnabled : IEntryDisplaySettings
	{
		public string SteamDir => @"C:\Program Files (x86)\Steam";
		public bool UseCompleted => true;
		public string CompletedSuffix => " ✓";
		public bool UseHidden => true;
	}

	/// <summary>
	/// 测试用 DisplaySettings：UseCompleted 和 UseHidden 都禁用。
	/// </summary>
	private class TestDisplaySettings_AllDisabled : IEntryDisplaySettings
	{
		public string SteamDir => @"C:\Program Files (x86)\Steam";
		public bool UseCompleted => false;
		public string CompletedSuffix => " ✓";
		public bool UseHidden => false;
	}

	// ── P0-1: WriteEntryCore 往返测试 ──

	[TestMethod]
	public void WriteEntryCore_Roundtrip_PreservesNameAndType()
	{
		var ds = new TestDisplaySettings_AllDisabled();
		var entry = new TestEntry(ds);
		entry.Name = "Roundtrip Game";
		entry.Type = AppInfoEntryType.Tool;

		using var ms = new MemoryStream();
		using (var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true))
		{
			entry.Write(writer);
		}

		ms.Position = 0;
		using var reader = new BinaryReader(ms);
		var loaded = AppInfoEntryBase.ReadFromReader<AppInfoEntry_0x07564428>(reader);

		Assert.IsNotNull(loaded);
		Assert.AreEqual(entry.ID, loaded!.ID);
		Assert.AreEqual("Roundtrip Game", loaded.Name);
		Assert.AreEqual(AppInfoEntryType.Tool, loaded.Type);
	}

	[TestMethod]
	public void WriteEntryCore_Roundtrip_PreservesChangeNumber()
	{
		var ds = new TestDisplaySettings_AllDisabled();
		var entry = new TestEntry(ds);

		using var ms = new MemoryStream();
		using (var writer = new BinaryWriter(ms, System.Text.Encoding.Default, leaveOpen: true))
		{
			entry.Write(writer);
		}

		ms.Position = 0;
		using var reader = new BinaryReader(ms);
		var loaded = AppInfoEntryBase.ReadFromReader<AppInfoEntry_0x07564428>(reader);

		Assert.IsNotNull(loaded);
		Assert.AreEqual(entry.ChangeNumber, loaded!.ChangeNumber);
	}

	// ── P0-2: PrepareWriteTable completed/hidden 前缀逻辑 ──

	[TestMethod]
	public void PrepareWriteTable_Completed_AppendsCheckmark()
	{
		var ds = new TestDisplaySettings_AllEnabled();
		var entry = new TestEntry(ds, completed: true);
		entry.Name = "My Game";

		entry.PrepareWriteTableForTest(out var common);
		string writtenName = common.GetPropertyValue("name", "");

		Assert.AreEqual("My Game ✓", writtenName);
	}

	[TestMethod]
	public void PrepareWriteTable_NotCompleted_NoCheckmark()
	{
		var ds = new TestDisplaySettings_AllEnabled();
		var entry = new TestEntry(ds, completed: false);
		entry.Name = "My Game";

		entry.PrepareWriteTableForTest(out var common);
		string writtenName = common.GetPropertyValue("name", "");

		Assert.AreEqual("My Game", writtenName);
	}

	[TestMethod]
	public void PrepareWriteTable_Hidden_PrependsHiddenPrefix()
	{
		var ds = new TestDisplaySettings_AllEnabled();
		var entry = new TestEntry(ds, hidden: true);
		entry.Name = "Secret Game";
		entry.Type = AppInfoEntryType.Game;

		entry.PrepareWriteTableForTest(out var common);
		string writtenType = common.GetPropertyValue("type", "");

		Assert.AreEqual("hidden_game", writtenType);
	}

	[TestMethod]
	public void PrepareWriteTable_NotHidden_NoPrefix()
	{
		var ds = new TestDisplaySettings_AllEnabled();
		var entry = new TestEntry(ds, hidden: false);
		entry.Name = "Normal Game";
		entry.Type = AppInfoEntryType.Game;

		entry.PrepareWriteTableForTest(out var common);
		string writtenType = common.GetPropertyValue("type", "");

		Assert.AreEqual("game", writtenType);
	}

	[TestMethod]
	public void PrepareWriteTable_CompletedAndHidden_BothApplied()
	{
		var ds = new TestDisplaySettings_AllEnabled();
		var entry = new TestEntry(ds, completed: true, hidden: true);
		entry.Name = "Hidden Completed Game";
		entry.Type = AppInfoEntryType.DLC;

		entry.PrepareWriteTableForTest(out var common);
		string writtenName = common.GetPropertyValue("name", "");
		string writtenType = common.GetPropertyValue("type", "");

		Assert.AreEqual("Hidden Completed Game ✓", writtenName);
		Assert.AreEqual("hidden_dlc", writtenType);
	}

	[TestMethod]
	public void PrepareWriteTable_DisabledSettings_NoModification()
	{
		var ds = new TestDisplaySettings_AllDisabled();
		var entry = new TestEntry(ds, completed: true, hidden: true);
		entry.Name = "Game";
		entry.Type = AppInfoEntryType.Game;

		entry.PrepareWriteTableForTest(out var common);
		string writtenName = common.GetPropertyValue("name", "");
		string writtenType = common.GetPropertyValue("type", "");

		// Settings 禁用时不应修改
		Assert.AreEqual("Game", writtenName);
		Assert.AreEqual("game", writtenType);
	}

	// ── P0-5: Revert 测试 ──

	[TestMethod]
	public void Revert_WithModification_RestoresOriginalName()
	{
		var ds = new TestDisplaySettings_AllDisabled();
		var entry = new TestEntry(ds);
		entry.Name = "Original Name";
		entry.Type = AppInfoEntryType.Game;

		// 保存当前状态作为原始数据
		byte[] originalData = entry.SerializeCurrentProperties();

		// 创建修改（完整路径：appinfo -> common -> name）
		var changes = new PropertyTable();
		var appinfo = new PropertyTable();
		var common = new PropertyTable();
		common.AddPropertyValue("name", PropertyType.String, "Modified Name");
		appinfo.AddPropertyValue("common", PropertyType.Table, common);
		changes.AddPropertyValue("appinfo", PropertyType.Table, appinfo);

		entry.Modification = new ModifiedEntry(entry.ID)
		{
			Changes = changes,
			OriginalData = originalData,
			OriginalDataVersion = 0x07564428,
			OriginalDataChangeNumber = 0
		};

		// 验证修改已应用
		Assert.AreEqual("Modified Name", entry.Name);

		// Revert
		bool result = entry.Revert();

		Assert.IsTrue(result);
		Assert.AreEqual("Original Name", entry.Name);
		Assert.IsNull(entry.Modification);
	}

	[TestMethod]
	public void Revert_Modification_RestoresOriginalType()
	{
		var ds = new TestDisplaySettings_AllDisabled();
		var entry = new TestEntry(ds);
		entry.Name = "Game";
		entry.Type = AppInfoEntryType.Game;

		// 保存当前状态作为原始数据
		byte[] originalData = entry.SerializeCurrentProperties();

		// 创建修改（完整路径：appinfo -> common -> type）
		var changes = new PropertyTable();
		var appinfo = new PropertyTable();
		var common = new PropertyTable();
		common.AddPropertyValue("type", PropertyType.String, "tool");
		appinfo.AddPropertyValue("common", PropertyType.Table, common);
		changes.AddPropertyValue("appinfo", PropertyType.Table, appinfo);

		entry.Modification = new ModifiedEntry(entry.ID)
		{
			Changes = changes,
			OriginalData = originalData,
			OriginalDataVersion = 0x07564428,
			OriginalDataChangeNumber = 0
		};

		// 验证修改已应用
		Assert.AreEqual(AppInfoEntryType.Tool, entry.Type);

		// Revert
		bool result = entry.Revert();

		Assert.IsTrue(result);
		Assert.AreEqual(AppInfoEntryType.Game, entry.Type);
	}
}
