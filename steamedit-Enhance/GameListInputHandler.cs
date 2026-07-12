#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using steamedit_Enhance.Data;

namespace steamedit_Enhance;

internal sealed class GameListInputHandler : IDisposable
{
	private readonly GameListDrawer _drawer;
	private readonly ListBox _appSelection;
	private readonly ToolTip _toolTips;
	private readonly Func<int, string> _getToolTip;
	private readonly IReadOnlyList<IAppInfoEntry> _entries;

	private readonly Timer _appSearchTermTimer = new();
	private readonly StringBuilder _appSearchTerm = new();

	public GameListInputHandler(
		GameListDrawer drawer,
		ListBox appSelection,
		ToolTip toolTips,
		Func<int, string> getToolTip,
		IReadOnlyList<IAppInfoEntry> entries)
	{
		_drawer = drawer;
		_appSelection = appSelection;
		_toolTips = toolTips;
		_getToolTip = getToolTip;
		_entries = entries;

		_appSearchTermTimer.Interval = 500;
		_appSearchTermTimer.Tick += (_, _) => { _appSearchTerm.Clear(); _appSearchTermTimer.Stop(); };

		appSelection.KeyPress += OnKeyPress;
		appSelection.MouseMove += OnMouseMove;
		appSelection.MouseLeave += OnMouseLeave;
		appSelection.MouseDown += OnMouseDown;
	}

	private void OnKeyPress(object? sender, KeyPressEventArgs e)
	{
		e.Handled = true;
		_appSearchTerm.Append(e.KeyChar);
		_appSearchTermTimer.Stop();
		_appSearchTermTimer.Start();
		string text = _appSearchTerm.ToString();
		int start = _appSelection.SelectedIndex;
		if (text.Length == 1) start++;
		for (int i = 0; i < _entries.Count; i++)
		{
			int idx = (start + i) % _entries.Count;
			if (_getToolTip(idx).StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
			{
				_appSelection.SelectedIndex = idx;
				break;
			}
		}
	}

	private void OnMouseMove(object? sender, MouseEventArgs e)
	{
		Point p = _appSelection.PointToClient(Control.MousePosition);
		int idx = _appSelection.IndexFromPoint(p);
		if (idx >= 0 && idx < _entries.Count)
		{
			_drawer.LastHoverIndex = idx;
			_toolTips.Active = false;
			_toolTips.SetToolTip(_appSelection, _getToolTip(idx));
			_toolTips.Active = true;
		}
		else
		{
			_drawer.LastHoverIndex = -1;
			_toolTips.SetToolTip(_appSelection, null);
		}
	}

	private void OnMouseLeave(object? sender, EventArgs e)
	{
		_drawer.LastHoverIndex = -1;
		_toolTips.SetToolTip(_appSelection, null);
	}

	private void OnMouseDown(object? sender, MouseEventArgs e)
	{
		if (e.Button != MouseButtons.Right) return;
		int idx = _appSelection.IndexFromPoint(e.Location);
		if (idx >= 0 && idx < _entries.Count)
		{
			if (_appSelection.SelectedIndices.Contains(idx))
				return;
			_appSelection.SelectedIndex = idx;
		}
	}

	public void Dispose()
	{
		_appSearchTermTimer?.Dispose();
	}
}
