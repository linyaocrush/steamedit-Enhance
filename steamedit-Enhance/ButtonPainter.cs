#nullable enable
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace steamedit_Enhance;

internal static class ButtonPainter
{
    private sealed class ButtonState
    {
        public bool IsHovered;
        public bool IsPressed;
        public bool EventsAttached;
    }

    private static readonly ConditionalWeakTable<Button, ButtonState> _states = new();

    public static void PaintGreen(object? sender, PaintEventArgs e)
    {
        var btn = (Button)sender!;
        var state = _states.GetOrCreateValue(btn);

        if (!state.EventsAttached)
        {
            state.EventsAttached = true;
            btn.MouseEnter += (_, _) => { state.IsHovered = true; btn.Invalidate(); };
            btn.MouseLeave += (_, _) => { state.IsHovered = false; state.IsPressed = false; btn.Invalidate(); };
            btn.MouseDown += (_, _) => { state.IsPressed = true; btn.Invalidate(); };
            btn.MouseUp += (_, _) => { state.IsPressed = false; btn.Invalidate(); };
        }

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int radius = 6;
        var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
        using var path = ThemeManager.GetRoundedRectangle(rect, radius);
        using var bgBrush = new SolidBrush(btn.Parent!.BackColor);
        g.FillRectangle(bgBrush, btn.ClientRectangle);

        Color top, bottom;
        if (!btn.Enabled)
        {
            top = Color.FromArgb(140, 180, 120);
            bottom = Color.FromArgb(110, 140, 90);
        }
        else if (state.IsPressed)
        {
            top = Color.FromArgb(80, 140, 35);
            bottom = Color.FromArgb(55, 85, 25);
        }
        else if (state.IsHovered)
        {
            top = Color.FromArgb(125, 200, 50);
            bottom = Color.FromArgb(85, 120, 38);
        }
        else
        {
            top = Color.FromArgb(109, 184, 43);
            bottom = Color.FromArgb(76, 107, 34);
        }

        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, btn.Width, btn.Height),
            top, bottom, LinearGradientMode.Vertical);
        g.FillPath(brush, path);

        var textRect = btn.ClientRectangle;
        if (state.IsPressed)
            textRect.Offset(1, 1);
        else
            textRect.Y -= 1;
        TextRenderer.DrawText(g, btn.Text, btn.Font, textRect, btn.ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
