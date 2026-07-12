#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.Properties;
using steamedit_Enhance.ViewModels;

namespace steamedit_Enhance;

internal sealed class GameListDrawer : IDisposable
{
	private const int IconSpacing = 6;

	private readonly GameListViewModel _viewModel;
	private readonly ListBox _appSelection;
	private readonly GameDataContext _ctx;
	private readonly Dictionary<AppInfoEntryType, Image> _filterImages;
	private readonly ImageAttributes _bgLogoAttributes;

	private readonly Font _defaultAppSelectionFont;
	private readonly Font _boldAppSelectionFont;
	private readonly Font _hiddenAppSelectionFont;

	private readonly SolidBrush _brushListBackColor;
	private readonly SolidBrush _brushListAlternateBackColor;
	private readonly SolidBrush _brushListSelectedBackColor;
	private readonly SolidBrush _brushListSelectedUnfocusedBackColor;
	private readonly SolidBrush _brushAccentLightAlt;
	private readonly Pen _penBorder;
	private readonly GraphicsPath _cachedSelectionPath = new();

	public GameListDrawer(
		GameListViewModel viewModel,
		ListBox appSelection,
		GameDataContext ctx,
		Dictionary<AppInfoEntryType, Image> filterImages)
	{
		_viewModel = viewModel;
		_appSelection = appSelection;
		_ctx = ctx;
		_filterImages = filterImages;

		// 构建灰度 ColorMatrix
		float[][] gray = new float[3][]
		{
			new float[3] { 0.3f, 0.3f, 0.3f },
			new float[3] { 0.59f, 0.59f, 0.59f },
			new float[3] { 0.11f, 0.11f, 0.11f }
		};
		float blend = 0.5f;
		ColorMatrix cm = new ColorMatrix();
		for (int i = 0; i < 3; i++)
			for (int j = 0; j < 3; j++)
				cm[i, j] = blend * gray[i][j] + (1f - blend) * (i == j ? 1f : 0f);
		cm[3, 3] = 0.08f;
		_bgLogoAttributes = new ImageAttributes();
		_bgLogoAttributes.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

		_defaultAppSelectionFont = _appSelection.Font;
		_boldAppSelectionFont = new Font(_appSelection.Font, FontStyle.Bold);
		_hiddenAppSelectionFont = new Font(_appSelection.Font, FontStyle.Strikeout);

		_brushListBackColor = new SolidBrush(ThemeManager.ListBackColor);
		_brushListAlternateBackColor = new SolidBrush(ThemeManager.ListAlternateBackColor);
		_brushListSelectedBackColor = new SolidBrush(ThemeManager.ListSelectedBackColor);
		_brushListSelectedUnfocusedBackColor = new SolidBrush(ThemeManager.ListSelectedUnfocusedBackColor);
		_brushAccentLightAlt = new SolidBrush(ThemeManager.AccentLightAlt);
		_penBorder = new Pen(ThemeManager.BorderColor);

		appSelection.DrawItem += OnDrawItem;
	}

	public ImageAttributes BgLogoAttributes => _bgLogoAttributes;
	public Dictionary<AppInfoEntryType, Image> FilterImages => _filterImages;

	public void InvalidateItem(int index)
	{
		if (index >= 0)
			_appSelection.Invalidate(_appSelection.GetItemRectangle(index));
	}

	public void InvalidateAll() => _appSelection.Invalidate();

	private int _lastHoverIndex = -1;

	public int LastHoverIndex
	{
		get => _lastHoverIndex;
		set
		{
			if (_lastHoverIndex >= 0)
				InvalidateItem(_lastHoverIndex);
			_lastHoverIndex = value;
			if (value >= 0)
				InvalidateItem(value);
		}
	}

	private void OnDrawItem(object? sender, DrawItemEventArgs e)
	{
		var entries = _ctx.Entries;
		if (e.Index < 0 || e.Index >= entries.Count) return;

		var entry = entries[e.Index];
		string text = _viewModel.GetDisplayName(entry);

		bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
		bool focused = _appSelection.Focused;
		bool hover = e.Index == _lastHoverIndex && !selected;

		Rectangle bounds = e.Bounds;
		const int padding = 4;
		const int iconGap = 4;

		// Background
		Brush backBrush;
		if (selected)
			backBrush = focused ? _brushListSelectedBackColor : _brushListSelectedUnfocusedBackColor;
		else if (hover)
			backBrush = _brushAccentLightAlt;
		else if (e.Index % 2 == 1)
			backBrush = _brushListAlternateBackColor;
		else
			backBrush = _brushListBackColor;

		if (selected)
		{
			var rowRect = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
			int radius = Math.Max(2, (int)Math.Round(4.0 * _appSelection.DeviceDpi / 96.0));
			ThemeManager.UpdateRoundedRectangle(_cachedSelectionPath, rowRect, radius);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			e.Graphics.FillPath(backBrush, _cachedSelectionPath);
			e.Graphics.SmoothingMode = SmoothingMode.Default;

			if (focused)
				ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds);
		}
		else
		{
			e.Graphics.FillRectangle(backBrush, bounds);
		}

		// Row separator
		e.Graphics.DrawLine(_penBorder, bounds.X, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);

		// Icons and Text
		Color foreColor = _appSelection.ForeColor;
		Font font = _defaultAppSelectionFont;
		int iconSize = bounds.Height - 8;
		int leftX = bounds.X + padding;

		// Left: filter type icon
		var currentFilters = _viewModel.FilterFlags;
		if ((currentFilters & (currentFilters - 1)) != 0)
		{
			if (_filterImages.TryGetValue(entry.Type, out var icon))
				e.Graphics.DrawImage(icon, leftX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
			leftX += iconSize + iconGap;
		}

		// Right: status icons
		int rightX = bounds.Right - padding - iconSize;
		bool hidden = entry.IsHidden;
		bool completed = entry.IsCompleted;
		bool modified = entry.IsModified;

		if (hidden)
		{
			e.Graphics.DrawImage(Resources.icon_hidden, rightX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
			rightX -= iconSize + iconGap;
			foreColor = Color.FromArgb(255 - (255 - foreColor.R) / 2, 255 - (255 - foreColor.G) / 2, 255 - (255 - foreColor.B) / 2);
			font = _hiddenAppSelectionFont;
		}
		if (completed)
		{
			e.Graphics.DrawImage(Resources.icon_completed, rightX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
			rightX -= iconSize + iconGap;
		}
		if (modified)
		{
			e.Graphics.DrawImage(Resources.icon_modified, rightX, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);
			font = _boldAppSelectionFont;
		}

		// Text
		int textWidth = rightX - leftX;
		if (textWidth > 0)
		{
			var textBounds = new Rectangle(leftX, bounds.Y, textWidth, bounds.Height);
			Size textSize = TextRenderer.MeasureText(text, font);
			textBounds.Inflate(0, -((textBounds.Height - textSize.Height) / 2));
			TextRenderer.DrawText(e.Graphics, text, font, textBounds, foreColor, TextFormatFlags.EndEllipsis);
		}
	}

	public void Dispose()
	{
		_boldAppSelectionFont?.Dispose();
		_hiddenAppSelectionFont?.Dispose();
		_bgLogoAttributes?.Dispose();
		_brushListBackColor?.Dispose();
		_brushListAlternateBackColor?.Dispose();
		_brushListSelectedBackColor?.Dispose();
		_brushListSelectedUnfocusedBackColor?.Dispose();
		_brushAccentLightAlt?.Dispose();
		_penBorder?.Dispose();
		_cachedSelectionPath?.Dispose();
		foreach (var img in _filterImages.Values)
			img.Dispose();
	}
}
