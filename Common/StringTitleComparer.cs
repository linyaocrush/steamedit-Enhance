#nullable enable
using System;
using System.Collections.Generic;

namespace Common;

internal class StringTitleComparer : IComparer<string>
{
	private static readonly StringTitleComparer StaticInstance = new StringTitleComparer();

	public int Compare(string? x, string? y)
	{
		return StringComparer.CurrentCulture.Compare(Normalize(x), Normalize(y));
	}

	public static int CompareStrings(string? x, string? y)
	{
		return StaticInstance.Compare(x, y);
	}

	public static string Normalize(string? s)
	{
		if (s == null) return "";
		s = s.Trim();
		if (s.StartsWith("the ", StringComparison.InvariantCultureIgnoreCase))
		{
			s = s.Substring(4).Trim();
		}
		return s;
	}
}
