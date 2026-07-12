#nullable enable
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Common;

namespace steamedit_Enhance;

internal sealed class ShortcutHandler
{
	private readonly Dictionary<(Keys Key, bool Control, bool Shift), Action> _shortcutMap = new();

	public void Register((Keys Key, bool Control, bool Shift) combo, Action action)
	{
		_shortcutMap[combo] = action;
	}

	public bool TryHandle(Keys keyCode, bool control, bool shift)
	{
		if (_shortcutMap.TryGetValue((keyCode, control, shift), out var action))
		{
			action();
			return true;
		}
		return false;
	}
}
