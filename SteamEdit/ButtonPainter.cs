#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SteamEdit;

internal static class ButtonPainter
{
    public static void PaintGreen(object? sender, PaintEventArgs e)
    {
        var btn = (Button)sender!;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int radius = 6;
        var rect = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
        using var path = ThemeManager.GetRoundedRectangle(rect, radius);
        using var bgBrush = new SolidBrush(btn.Parent!.BackColor);
        g.FillRectangle(bgBrush, btn.ClientRectangle);

        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, btn.Width, btn.Height),
            Color.FromArgb(109, 184, 43),
            Color.FromArgb(76, 107, 34),
            LinearGradientMode.Vertical);
        g.FillPath(brush, path);

        var textRect = btn.ClientRectangle;
        textRect.Y -= 1;
        TextRenderer.DrawText(g, btn.Text, btn.Font, textRect, btn.ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
