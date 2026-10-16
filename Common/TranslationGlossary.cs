#nullable enable
using System;
using System.Collections.Generic;
using steamedit_Enhance.Data.Native;

namespace Common;

internal interface IAppInfoEntryAdapter
{
	int ID { get; }
	string OriginalName { get; }
	string TranslatedName { get; }
}

/// <summary>
/// 翻译术语库（AI 训练对）。JSON 结构、大小写不敏感索引、原子落盘与 .bak 回退
/// 全部由 Rust 内核 (<c>ste_glossary_*</c>) 负责，本类只是句柄封装。
/// </summary>
internal static class TranslationGlossary
{
	private static readonly object _lock = new();
	private static IntPtr _handle;
	private static bool _initialized;

	public static void Initialize(string dataDirectory)
	{
		lock (_lock)
		{
			if (_initialized) return;
			_handle = SteameditNative.GlossaryNew();
			SteameditNative.GlossaryInitialize(_handle, dataDirectory);
			_initialized = true;
		}
	}

	private static void ThrowIfNotInitialized()
	{
		if (!_initialized)
			throw new InvalidOperationException("TranslationGlossary.Initialize() must be called before using this class.");
	}

	public static void SyncFromMods(IEnumerable<IAppInfoEntryAdapter> entries)
	{
		ThrowIfNotInitialized();
		lock (_lock)
		{
			bool changed = false;
			foreach (var entry in entries)
			{
				if (SteameditNative.GlossaryHasApp(_handle, entry.ID)) continue;

				if (!string.IsNullOrWhiteSpace(entry.OriginalName) &&
					!string.IsNullOrWhiteSpace(entry.TranslatedName) &&
					entry.OriginalName != entry.TranslatedName)
				{
					if (SteameditNative.GlossaryUpdate(_handle, entry.ID, entry.OriginalName, entry.TranslatedName))
						changed = true;
				}
			}

			if (changed)
				Log.WriteLine("Glossary: Synced, now has {0} entries", SteameditNative.GlossaryCount(_handle));
		}
	}

	public static void UpdateEntry(int appId, string originalName, string translatedName)
	{
		ThrowIfNotInitialized();
		if (string.IsNullOrWhiteSpace(originalName) || string.IsNullOrWhiteSpace(translatedName))
			return;
		if (originalName == translatedName) return;

		lock (_lock)
		{
			SteameditNative.GlossaryUpdate(_handle, appId, originalName, translatedName);
		}
	}

	/// <summary>将待保存的更改写入磁盘。批量操作结束后调用。</summary>
	public static void Flush()
	{
		ThrowIfNotInitialized();
		lock (_lock) { SteameditNative.GlossaryFlush(_handle); }
	}

	public static string? ToPromptString(int maxEntries = 100)
	{
		ThrowIfNotInitialized();
		lock (_lock) { return SteameditNative.GlossaryPrompt(_handle, maxEntries); }
	}

	public static string? FindTranslatedByOriginalName(string originalName)
	{
		ThrowIfNotInitialized();
		if (string.IsNullOrWhiteSpace(originalName)) return null;
		lock (_lock) { return SteameditNative.GlossaryFind(_handle, originalName); }
	}
}
