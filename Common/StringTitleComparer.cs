using System;
using System.Collections.Generic;

namespace Common;

internal class StringTitleComparer : IComparer<string>
{
	private static readonly StringTitleComparer StaticInstance = new StringTitleComparer();

	public int Compare(string x, string y)
	{
		x = x.Trim();
		y = y.Trim();
		if (x.IndexOf("the ", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			x = x.Substring(4).Trim();
		}
		if (y.IndexOf("the ", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			y = y.Substring(4).Trim();
		}
		return StringComparer.CurrentCulture.Compare(x, y);
	}

	public static int CompareStrings(string x, string y)
	{
		return StaticInstance.Compare(x, y);
	}
}
