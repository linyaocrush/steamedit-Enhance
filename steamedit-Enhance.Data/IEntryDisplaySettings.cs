#nullable enable
namespace steamedit_Enhance.Data;

/// <summary>
/// Decouples entry display logic from the static SteamData class.
/// </summary>
internal interface IEntryDisplaySettings
{
	string SteamDir { get; }
	bool UseCompleted { get; }
	string CompletedSuffix { get; }
	bool UseHidden { get; }
}
