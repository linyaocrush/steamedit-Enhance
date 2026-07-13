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
		ReadOnlySpan<char> span = s.AsSpan().Trim();
		if (span.StartsWith("the", StringComparison.OrdinalIgnoreCase) && span.Length > 3 && span[3] == ' ')
			span = span.Slice(4).TrimStart();
		else if (span.StartsWith("a", StringComparison.OrdinalIgnoreCase) && span.Length > 1 && span[1] == ' ')
			span = span.Slice(2).TrimStart();
		else if (span.StartsWith("an", StringComparison.OrdinalIgnoreCase) && span.Length > 2 && span[2] == ' ')
			span = span.Slice(3).TrimStart();
		return span.Length == s.Length ? s : new string(span);
	}
}
