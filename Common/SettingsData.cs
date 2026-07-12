#nullable enable
using System.Text.Json.Serialization;

namespace Common;

/// <summary>
/// 仅用于 JSON 序列化/反序列化的 POCO，结构化为嵌套对象。
/// Settings 的扁平属性访问器保持不动以保证向后兼容。
/// </summary>
internal sealed class SettingsData
{
    [JsonPropertyName("Display")]
    public DisplaySettingsData Display { get; set; } = new();

    [JsonPropertyName("Window")]
    public WindowSettingsData Window { get; set; } = new();

    [JsonPropertyName("Ai")]
    public AiSettingsData Ai { get; set; } = new();
}

internal sealed class DisplaySettingsData
{
    public bool EnableCompleted { get; set; } = true;
    public bool EnableHidden { get; set; } = true;
    public bool ReplaceLocalizedNames { get; set; }
    public string PassThroughArguments { get; set; } = "";
    public string Filters { get; set; } = "Game, Application, Video";
    public string FiltersShowEntries { get; set; } = "All";
    public string AppInfoPath { get; set; } = "";
    public string ModsLocation { get; set; } = "";
    public string ShownVersion { get; set; } = "";
    public bool AutoStart { get; set; }
    public bool AutoStartAutoFix { get; set; }
    public bool AutoStartForceStart { get; set; }
    public string Language { get; set; } = "";
    public string LogLevel { get; set; } = "Info";
    public bool EnableDebugLogging { get; set; }
    public string SortField { get; set; } = "Name";
    public bool SortDescending { get; set; }
    public string SearchEngine { get; set; } = "Google";
}

internal sealed class WindowSettingsData
{
    public int LocationX { get; set; } = int.MaxValue;
    public int LocationY { get; set; } = int.MaxValue;
    public int SizeX { get; set; } = int.MaxValue;
    public int SizeY { get; set; } = int.MaxValue;
    public int Splitter { get; set; } = int.MaxValue;
}

internal sealed class AiSettingsData
{
    public string ApiUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-4o-mini";
    public string TargetLanguage { get; set; } = "zh-CN";
    public string CustomSystemPrompt { get; set; } = "";
    public string CustomUserMessage { get; set; } = "";
    public string TranslationRules { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 120;
    public int BatchTranslateConcurrency { get; set; } = 3;
}
