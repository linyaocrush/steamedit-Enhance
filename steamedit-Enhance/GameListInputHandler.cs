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
	private readonly ListView _appSelection;
	private readonly ToolTip _toolTips;
	private readonly Func<int, string> _getToolTip;

	private readonly Timer _appSearchTermTimer = new();
	private readonly StringBuilder _appSearchTerm = new();

	public GameListInputHandler(
		GameListDrawer drawer,
		ListView appSelection,
		ToolTip toolTips,
		Func<int, string> getToolTip)
	{
		_drawer = drawer;
		_appSelection = appSelection;
		_toolTips = toolTips;
		_getToolTip = getToolTip;

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
		int start = _appSelection.SelectedIndices.Count > 0 ? _appSelection.SelectedIndices[0] : 0;
		if (text.Length == 1) start++;
		int count = _appSelection.VirtualListSize;
		for (int i = 0; i < count; i++)
		{
			int idx = (start + i) % count;
			if (_getToolTip(idx).StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
			{
				_appSelection.SelectedIndices.Clear();
				_appSelection.SelectedIndices.Add(idx);
				break;
			}
		}
	}

	private void OnMouseMove(object? sender, MouseEventArgs e)
	{
		int idx = _appSelection.HitTest(e.Location).Item?.Index ?? -1;
		if (idx >= 0 && idx < _appSelection.VirtualListSize)
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
		int idx = _appSelection.HitTest(e.Location).Item?.Index ?? -1;
		if (idx >= 0 && idx < _appSelection.VirtualListSize)
		{
			if (_appSelection.SelectedIndices.Contains(idx))
				return;
			_appSelection.SelectedIndices.Clear();
			_appSelection.SelectedIndices.Add(idx);
		}
	}

	public void Dispose()
	{
		_appSearchTermTimer?.Dispose();
	}
}
