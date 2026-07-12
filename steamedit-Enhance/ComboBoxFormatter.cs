#nullable enable
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Properties;

namespace steamedit_Enhance;

internal static class ComboBoxFormatter
{
	public static void FormatControllerSupport(object? sender, ListControlConvertEventArgs e)
	{
		if (e.ListItem is string raw)
			e.Value = raw switch
			{
				"none" => Strings.MainWindow_Controller_None,
				"partial" => Strings.MainWindow_Controller_Partial,
				"full" => Strings.MainWindow_Controller_Full,
				_ => raw,
			};
	}

	public static void FormatVR(object? sender, ListControlConvertEventArgs e)
	{
		if (e.ListItem is string raw)
			e.Value = raw switch
			{
				"default" => Strings.MainWindow_VR_Default,
				"removed" => Strings.MainWindow_VR_Removed,
				_ => raw,
			};
	}

	public static void FormatEntryType(object? sender, ListControlConvertEventArgs e)
	{
		if (e.ListItem is AppInfoEntryType entryType)
			e.Value = I18n.GetEnumDisplayName(entryType);
	}
}
