#nullable enable
namespace SteamEdit.Data;

internal class LaunchItem
{
	public string Label { get; set; } = "";

	public string Executable { get; set; } = "";

	public string Arguments { get; set; } = "";

	public string WorkingDir { get; set; } = "";
}
