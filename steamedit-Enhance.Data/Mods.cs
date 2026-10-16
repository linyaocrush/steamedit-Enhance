#nullable enable
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Common;
using steamedit_Enhance.Data.Native;

namespace steamedit_Enhance.Data;

/// <summary>
/// 修改记录存储（mods.dat）。文件编解码完全由 Rust 内核完成
/// （<see cref="SteameditNative.ModsToFlat"/> / <see cref="SteameditNative.ModsFromFlat"/>），
/// 此处只负责：把「文件字节 ↔ 扁平容器」与 C# 对象（<see cref="ModifiedEntry"/>）之间互转，
/// 以及备份/落盘等 I/O 编排。
/// </summary>
/// <remarks>
/// 读写版本：v7（<c>CurrentVersion</c>）。v4/v5/v6 仍可读取，读取时 Rust 会把其中的
/// VDF 形式的 Changes 转成扁平树，保存时自动升级为 v7 —— 即「无缝迁移」。
/// </remarks>
internal class Mods
{
	/// <summary>当前写出的 mods.dat 版本（v7：全长度前缀 + Changes 直接存扁平树，读写零转码）。</summary>
	private const uint CurrentVersion = 7;

	/// <summary>可读取的最老版本。</summary>
	private const uint MinSupportedVersion = 4;

	private readonly IAppInfo _appInfo;

	private Mods(IAppInfo appInfo)
	{
		_appInfo = appInfo;
	}

	public static Mods Load(string path, IAppInfo appInfo, IFileSystem? fileSystem = null)
	{
		Mods mods = new Mods(appInfo);
		fileSystem ??= RealFileSystem.Instance;
		if (!fileSystem.FileExists(path))
			return mods;

		byte[] fileBytes = ReadAllBytes(fileSystem, path);
		byte[]? container = SteameditNative.ModsToFlat(fileBytes);
		if (container == null)
			throw new InvalidDataException(SteameditNative.LastError);

		uint version = ReadVersion(container);
		if (version < MinSupportedVersion || version > CurrentVersion)
		{
			Log.WriteLine("Mods file has unsupported version {0}", version);
			return mods;
		}

		List<ModifiedEntry> list = ParseContainer(container);
		var entryById = new Dictionary<int, IAppInfoEntry>();
		foreach (var entry in appInfo.Entries)
			entryById[entry.ID] = entry;
		foreach (ModifiedEntry modEntry in list)
		{
			if (entryById.TryGetValue(modEntry.ID, out var appInfoEntry))
				appInfoEntry.Modification = modEntry;
		}
		return mods;
	}

	public void Save(string path, IFileSystem fileSystem)
	{
		string tempPath = path + ".tmp";
		string backupPath = path + ".bak";
		bool hasBackup = false;
		try
		{
			var modifiedEntries = _appInfo.Entries.Where(e => e.IsModified).ToList();
			byte[] container = BuildContainer(modifiedEntries);
			byte[]? fileBytes = SteameditNative.ModsFromFlat(container);
			if (fileBytes == null)
				throw new InvalidDataException(SteameditNative.LastError);

			using (var tempStream = fileSystem.CreateFile(tempPath))
			{
				tempStream.Write(fileBytes, 0, fileBytes.Length);
			}

			if (fileSystem.FileExists(path))
			{
				fileSystem.FileCopy(path, backupPath, overwrite: true);
				hasBackup = true;
			}
			fileSystem.FileMove(tempPath, path, overwrite: true);
		}
		catch (Exception ex)
		{
			Log.WriteLine("Mods.Save failed: {0}", ex.ToString());
			// 清理临时文件
			try { if (fileSystem.FileExists(tempPath)) fileSystem.FileDelete(tempPath); }
			catch { /* best effort */ }
			// 从备份恢复
			if (hasBackup)
			{
				try
				{
					fileSystem.FileCopy(backupPath, path, overwrite: true);
					Log.WriteLine("  Mods restored from backup");
				}
				catch (Exception restoreEx)
				{
					Log.WriteLine("  Mods backup restore also failed: {0}", restoreEx.ToString());
				}
			}
			throw;
		}
	}

	private static byte[] ReadAllBytes(IFileSystem fileSystem, string path)
	{
		using Stream stream = fileSystem.OpenRead(path);
		using var ms = new MemoryStream();
		stream.CopyTo(ms);
		return ms.ToArray();
	}

	/// <summary>扁平容器布局：<c>[u32 version][u32 count]</c>，随后每个条目
	/// <c>[i32 id][u32 changesLen][Changes flat][u32 originalDataVersion][u32 changeNumber][u32 dataLen][data]</c>。
	/// 与 v7 文件同构（仅字段顺序不同），因此读写都是纯拷贝。</summary>
	private static uint ReadVersion(byte[] container)
		=> BinaryPrimitives.ReadUInt32LittleEndian(container);

	private static List<ModifiedEntry> ParseContainer(byte[] container)
	{
		int pos = 0;
		_ = ReadU32(container, ref pos); // version — 已在 Load 中校验
		uint count = ReadU32(container, ref pos);
		var list = new List<ModifiedEntry>((int)count);
		for (uint i = 0; i < count; i++)
		{
			int id = BinaryPrimitives.ReadInt32LittleEndian(container.AsSpan(pos));
			pos += 4;

			// 扁平树自带长度前缀，可直接按长度解析（无需扫描找结尾）。
			int changesLen = (int)ReadU32(container, ref pos);
			PropertyTable changes = FlatTableCodec.FromFlat(container.AsSpan(pos, changesLen));
			pos += changesLen;

			uint dataVersion = ReadU32(container, ref pos);
			uint changeNumber = ReadU32(container, ref pos);
			uint dataLen = ReadU32(container, ref pos);
			byte[]? data = dataLen > 0 ? container.AsSpan(pos, (int)dataLen).ToArray() : null;
			pos += (int)dataLen;

			var entry = new ModifiedEntry(id)
			{
				Changes = changes,
				OriginalDataVersion = dataVersion,
				OriginalDataChangeNumber = changeNumber,
			};
			if (data != null)
				entry.OriginalData = data;
			list.Add(entry);
		}
		return list;
	}

	private static byte[] BuildContainer(List<IAppInfoEntry> entries)
	{
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
		w.Write(CurrentVersion);
		w.Write((uint)entries.Count);
		foreach (IAppInfoEntry item in entries)
		{
			var mod = item.Modification!;
			w.Write(mod.ID);
			byte[] changes = FlatTableCodec.ToFlat(mod.Changes);
			w.Write((uint)changes.Length);
			w.Write(changes);
			w.Write(mod.OriginalDataVersion);
			w.Write(mod.OriginalDataChangeNumber);
			byte[]? data = mod.OriginalData;
			w.Write((uint)(data?.Length ?? 0));
			if (data != null)
				w.Write(data);
		}
		w.Flush();
		return ms.ToArray();
	}

	private static uint ReadU32(byte[] buffer, ref int pos)
	{
		uint value = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(pos));
		pos += 4;
		return value;
	}
}
