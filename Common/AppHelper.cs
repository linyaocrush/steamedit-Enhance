using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Common;

internal static class AppHelper
{
	private static string _cachedAppDirectory;
	public static bool HasArgument(string tag, string[] commandLineArgs)
	{
		for (int i = 1; i < commandLineArgs.Length; i++)
		{
			string text = commandLineArgs[i];
			if (text == "--")
			{
				break;
			}
			if (text[0] == '/')
			{
				text = text.Substring(1);
			}
			else if (text[0] == '-' && text[1] == '-')
			{
				text = text.Substring(2);
			}
			else
			{
				if (text[0] != '-')
				{
					continue;
				}
				text = text.Substring(1);
			}
			if (text.Equals(tag, StringComparison.InvariantCultureIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	public static string GetPassthroughArguments(string[] commandLineArgs)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(Settings.Default.PassThroughArguments);
		bool flag = false;
		for (int i = 1; i < commandLineArgs.Length; i++)
		{
			string text = commandLineArgs[i];
			if (flag)
			{
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append(" ");
				}
				stringBuilder.Append(text);
			}
			else if (text == "--")
			{
				flag = true;
			}
		}
		return stringBuilder.ToString();
	}

	public static string GetAppDirectory()
	{
		return _cachedAppDirectory ??= Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
	}

	public static string MakePathRelative(string src, string relToFolder)
	{
		Uri uri = new Uri(relToFolder + Path.DirectorySeparatorChar);
		Uri uri2 = new Uri(src);
		return Uri.UnescapeDataString(uri.MakeRelativeUri(uri2).ToString());
	}
}
