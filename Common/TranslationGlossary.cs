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
	public string Original { get; set; }

	[JsonPropertyName("translated")]
	public string Translated { get; set; }
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
	private static GlossaryData _data;
	private static string _filePath;
	private static readonly object _lock = new();

	public static void Initialize(string dataDirectory)
	{
		_filePath = Path.Combine(dataDirectory, "glossary.json");
		Load();
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
		}
	}

	public static void SyncFromMods(IEnumerable<IAppInfoEntryAdapter> entries)
	{
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
			Save();
		}
		Log.WriteLine("Glossary: Updated {0} -> \"{1}\"", key, translatedName);
	}

	public static string ToPromptString(int maxEntries = 100)
	{
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

	private static void Save()
	{
		try
		{
			var options = new JsonSerializerOptions { WriteIndented = true };
			string json = JsonSerializer.Serialize(_data, options);
			File.WriteAllText(_filePath, json, Encoding.UTF8);
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
