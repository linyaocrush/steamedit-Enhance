#nullable enable

namespace Common;

internal interface ISettings
{
    bool EnableCompleted { get; set; }
    bool EnableHidden { get; set; }
    bool ReplaceLocalizedNames { get; set; }
    string PassThroughArguments { get; set; }
    AppInfoEntryType Filters { get; set; }
    ShowEntriesMode FiltersShowEntries { get; set; }
    string AppInfoPath { get; set; }
    string ModsLocation { get; set; }
    string ShownVersion { get; set; }
    int WindowLocationX { get; set; }
    int WindowLocationY { get; set; }
    int WindowSizeX { get; set; }
    int WindowSizeY { get; set; }
    int MainWindowSplitter { get; set; }
    bool AutoStart { get; set; }
    bool AutoStartAutoFix { get; set; }
    bool AutoStartForceStart { get; set; }
    string Language { get; set; }
    string AiApiUrl { get; set; }
    string AiApiKey { get; set; }
    string AiModel { get; set; }
    string SearchEngine { get; set; }
    string AiTargetLanguage { get; set; }
    string AiCustomSystemPrompt { get; set; }
    string AiCustomUserMessage { get; set; }
    string AiTranslationRules { get; set; }
    int AiTimeoutSeconds { get; set; }
    int AiBatchTranslateConcurrency { get; set; }
    Log.LogLevel LogLevel { get; set; }
    bool EnableDebugLogging { get; set; }
    string SortField { get; set; }
    bool SortDescending { get; set; }
}
