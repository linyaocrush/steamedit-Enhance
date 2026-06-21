#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common;

internal class GlossaryEntry
{
	[JsonPropertyName("original")]
	public string Original { get; set; } = "";

	[JsonPropertyName("translated")]
	public string Translated { get; set; } = "";
}

internal class GlossaryData
{
	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;

	[JsonPropertyName("entries")]
	public Dictionary<string, GlossaryEntry> Entries { get; set; } = new();
}

internal static class TranslationGlossary
{
	private static GlossaryData _data = new();
	private static Dictionary<string, string> _originalToTranslated = new(StringComparer.OrdinalIgnoreCase);
	private static string? _filePath;
	private static bool _initialized;
	private static bool _dirty;
	private static readonly object _lock = new();
	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		WriteIndented = true,
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
	};

	public static void Initialize(string dataDirectory)
	{
		_filePath = Path.Combine(dataDirectory, "glossary.json");
		Load();
		_initialized = true;
	}

	private static void ThrowIfNotInitialized()
	{
		if (!_initialized)
			throw new InvalidOperationException("TranslationGlossary.Initialize() must be called before using this class.");
	}

	public static void Load()
	{
		lock (_lock)
		{
			if (File.Exists(_filePath))
			{
				try
				{
					string json = File.ReadAllText(_filePath, Encoding.UTF8);
					_data = JsonSerializer.Deserialize<GlossaryData>(json) ?? new GlossaryData();
					Log.WriteLine("Glossary: Loaded {0} entries from {1}", _data.Entries.Count, _filePath);
				}
				catch (Exception ex)
				{
					Log.WriteLine("Glossary: Failed to load, creating new: {0}", ex.Message);
					_data = new GlossaryData();
				}
			}
			else
			{
				Log.WriteLine("Glossary: File not found, will create on first sync");
				_data = new GlossaryData();
			}
			RebuildIndex();
		}
	}

	private static void RebuildIndex()
	{
		_originalToTranslated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var kv in _data.Entries)
		{
			if (!string.IsNullOrWhiteSpace(kv.Value.Original))
				_originalToTranslated[kv.Value.Original] = kv.Value.Translated;
		}
	}

	public static void SyncFromMods(IEnumerable<IAppInfoEntryAdapter> entries)
	{
		ThrowIfNotInitialized();
		lock (_lock)
		{
			bool changed = false;
			foreach (var entry in entries)
			{
				string key = entry.ID.ToString("X8");
				if (_data.Entries.ContainsKey(key)) continue;

				if (!string.IsNullOrWhiteSpace(entry.OriginalName) &&
					!string.IsNullOrWhiteSpace(entry.TranslatedName) &&
					entry.OriginalName != entry.TranslatedName)
				{
					_data.Entries[key] = new GlossaryEntry
					{
						Original = entry.OriginalName,
						Translated = entry.TranslatedName
					};
					_originalToTranslated[entry.OriginalName] = entry.TranslatedName;
					changed = true;
				}
			}

			if (changed)
			{
				Log.WriteLine("Glossary: Synced, now has {0} entries", _data.Entries.Count);
				Save();
			}
		}
	}

	public static void UpdateEntry(int appId, string originalName, string translatedName)
	{
		ThrowIfNotInitialized();
		if (string.IsNullOrWhiteSpace(originalName) || string.IsNullOrWhiteSpace(translatedName))
			return;
		if (originalName == translatedName) return;

		string key = appId.ToString("X8");
		lock (_lock)
		{
			_data.Entries[key] = new GlossaryEntry
			{
				Original = originalName,
				Translated = translatedName
			};
			_originalToTranslated[originalName] = translatedName;
			_dirty = true;
		}
		Log.WriteLine("Glossary: Updated {0} -> \"{1}\"", key, translatedName);
	}

	/// <summary>
	/// 将待保存的更改写入磁盘。批量操作结束后调用。
	/// </summary>
	public static void Flush()
	{
		ThrowIfNotInitialized();
		lock (_lock)
		{
			if (_dirty)
			{
				Save();
				_dirty = false;
			}
		}
	}

	public static string? ToPromptString(int maxEntries = 100)
	{
		ThrowIfNotInitialized();
		lock (_lock)
		{
			if (_data.Entries.Count == 0) return null;

			var sb = new StringBuilder();
			int count = 0;
			foreach (var kv in _data.Entries)
			{
				sb.AppendLine($"{kv.Value.Original} -> {kv.Value.Translated}");
				if (++count >= maxEntries) break;
			}
			return sb.ToString();
		}
	}

	public static string? FindTranslatedByOriginalName(string originalName)
	{
		ThrowIfNotInitialized();
		if (string.IsNullOrWhiteSpace(originalName)) return null;
		lock (_lock)
		{
			return _originalToTranslated.TryGetValue(originalName, out var translated) ? translated : null;
		}
	}

	private static void Save()
	{
		try
		{
			string json = JsonSerializer.Serialize(_data, _jsonOptions);
			if (_filePath == null) return;
			string tempPath = _filePath + ".tmp";
			File.WriteAllText(tempPath, json, Encoding.UTF8);
			File.Move(tempPath, _filePath, overwrite: true);
			Log.WriteLine("Glossary: Saved {0} entries", _data.Entries.Count);
		}
		catch (Exception ex)
		{
			Log.WriteLine("Glossary: Failed to save: {0}", ex.Message);
		}
	}
}

internal interface IAppInfoEntryAdapter
{
	int ID { get; }
	string OriginalName { get; }
	string TranslatedName { get; }
}
