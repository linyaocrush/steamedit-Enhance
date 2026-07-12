#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SteamEdit;

internal static class ThemeManager
{
    // ── Color Palette ──
    public static Color BackColor { get; } = Color.FromArgb(242, 244, 248);
    public static Color PanelBackColor { get; } = Color.White;
    public static Color AccentColor { get; } = Color.FromArgb(0, 103, 192);
    public static Color AccentLight { get; } = Color.FromArgb(222, 235, 255);
    public static Color AccentLightAlt { get; } = Color.FromArgb(238, 245, 253);
    public static Color TextColor { get; } = Color.FromArgb(30, 30, 30);
    public static Color SecondaryText { get; } = Color.FromArgb(120, 120, 120);
    public static Color BorderColor { get; } = Color.FromArgb(214, 218, 224);

    // ListBox
    public static Color ListBackColor { get; } = Color.White;
    public static Color ListAlternateBackColor { get; } = Color.FromArgb(248, 249, 250);
    public static Color ListSelectedBackColor { get; } = Color.FromArgb(205, 228, 252);
    public static Color ListSelectedUnfocusedBackColor { get; } = Color.FromArgb(230, 235, 242);

    // Buttons
    public static Color PrimaryButtonBack { get; } = Color.FromArgb(0, 103, 192);
    public static Color PrimaryButtonHover { get; } = Color.FromArgb(0, 90, 170);
    public static Color PrimaryButtonFore { get; } = Color.White;
    public static Color DefaultButtonBack { get; } = Color.FromArgb(230, 233, 237);
    public static Color DefaultButtonHover { get; } = Color.FromArgb(215, 218, 224);
    public static Color DefaultButtonFore { get; } = Color.FromArgb(30, 30, 30);

    // Status bar
    public static Color StatusBarBack { get; } = Color.FromArgb(240, 242, 245);

    // ── Shared Helpers ──

    public static GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        AddRoundedRect(path, rect, radius);
        return path;
    }

    public static void UpdateRoundedRectangle(GraphicsPath path, Rectangle rect, int radius)
    {
        path.Reset();
        AddRoundedRect(path, rect, radius);
    }

    private static void AddRoundedRect(GraphicsPath path, Rectangle rect, int radius)
    {
        path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
        path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
        path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
        path.CloseFigure();
    }

    // ── Form / Dialog Styling ──

    public static void ApplyToForm(Form form)
    {
        form.BackColor = BackColor;
    }

    public static void StyleDialog(Form form)
    {
        form.BackColor = BackColor;
        StyleDialogControls(form.Controls);
    }

    private static void StyleDialogControls(Control.ControlCollection controls)
    {
        foreach (Control c in controls)
        {
            switch (c)
            {
                case Button btn:
                    StyleButton(btn);
                    break;
                case LinkLabel link:
                    link.BackColor = Color.Transparent;
                    link.ForeColor = AccentColor;
                    break;
                case Label lbl:
                    lbl.BackColor = Color.Transparent;
                    lbl.ForeColor = TextColor;
                    break;
                case CheckBox chk:
                    StyleCheckBox(chk);
                    break;
                case TextBoxBase tb:
                    StyleTextBox(tb);
                    break;
                case ComboBox cb:
                    StyleComboBox(cb);
                    break;
                case NumericUpDown nud:
                    StyleNumericUpDown(nud);
                    break;
                case Panel p:
                    if (p.BorderStyle == BorderStyle.Fixed3D)
                        p.BorderStyle = BorderStyle.None;
                    StyleDialogControls(p.Controls);
                    break;
                case GroupBox or FlowLayoutPanel or TabControl or TabPage:
                    StyleDialogControls(c.Controls);
                    break;
            }
        }
    }

    // ── Button Styling ──

    public static void StyleButton(Button btn, bool isPrimary = false)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
        btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
        btn.UseVisualStyleBackColor = false;
        btn.BackColor = Color.Transparent;
        btn.ForeColor = isPrimary ? PrimaryButtonFore : DefaultButtonFore;

        AttachRoundedCorners(btn, isPrimary);
    }

    private static void AttachRoundedCorners(Button btn, bool isPrimary)
    {
        bool hovered = false;
        Color normalBack = isPrimary ? PrimaryButtonBack : DefaultButtonBack;
        Color hoverBack = isPrimary ? PrimaryButtonHover : DefaultButtonHover;

        btn.MouseEnter += (_, _) => { hovered = true; btn.Invalidate(); };
        btn.MouseLeave += (_, _) => { hovered = false; btn.Invalidate(); };

        btn.Paint += (sender, e) =>
        {
            var b = (Button)sender!;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int radius = Math.Max(4, (int)Math.Round(6.0 * b.DeviceDpi / 96.0));

            // Clear background with form color (handles Transparent parent containers)
            Color clearColor = b.FindForm()?.BackColor ?? SystemColors.Control;
            using (var bgBrush = new SolidBrush(clearColor))
                g.FillRectangle(bgBrush, b.ClientRectangle);

            // Draw rounded fill
            var rect = new Rectangle(0, 0, b.Width - 1, b.Height - 1);
            using var path = GetRoundedRectangle(rect, radius);

            Color back = hovered ? hoverBack : normalBack;
            using (var brush = new SolidBrush(back))
                g.FillPath(brush, path);

            // Draw image or text
            if (b.Image != null)
            {
                int imgSize = Math.Min(b.Width, b.Height) - 8;
                if (imgSize > 0)
                {
                    var imgRect = new Rectangle(
                        (b.Width - imgSize) / 2,
                        (b.Height - imgSize) / 2,
                        imgSize, imgSize);
                    g.DrawImage(b.Image, imgRect);
                }
            }
            else if (!string.IsNullOrEmpty(b.Text))
            {
                var textRect = b.ClientRectangle;
                textRect.Y -= 1;
                TextRenderer.DrawText(g, b.Text, b.Font, textRect, b.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };
    }

    // ── Other Control Styling ──

    public static void StyleTextBox(TextBoxBase textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Color.White;
    }

    public static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = Color.White;
    }

    public static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.BackColor = Color.Transparent;
    }

    public static void StyleNumericUpDown(NumericUpDown nud)
    {
        nud.BackColor = Color.White;
    }

    public static void StyleToolStrip(StatusStrip statusStrip)
    {
        statusStrip.BackColor = StatusBarBack;
        statusStrip.GripStyle = ToolStripGripStyle.Hidden;
    }

    // ── Context Menu Styling ──

    public static void StyleContextMenuStrip(ContextMenuStrip menu)
    {
        menu.Renderer = s_themeRenderer;
        menu.ForeColor = TextColor;
    }

    private static readonly ToolStripProfessionalRenderer s_themeRenderer = new ThemeMenuRenderer();

    private sealed class ThemeMenuRenderer : ToolStripProfessionalRenderer
    {
        public ThemeMenuRenderer() : base(new ThemeMenuColorTable()) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawRectangle(pen,
                e.AffectedBounds.X, e.AffectedBounds.Y,
                e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
            base.OnRenderToolStripBorder(e);
        }
    }

    private sealed class ThemeMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => PanelBackColor;
        public override Color ImageMarginGradientBegin => PanelBackColor;
        public override Color ImageMarginGradientMiddle => PanelBackColor;
        public override Color ImageMarginGradientEnd => PanelBackColor;
        public override Color MenuItemSelected => AccentLight;
        public override Color MenuItemSelectedGradientBegin => AccentLight;
        public override Color MenuItemSelectedGradientEnd => AccentLight;
        public override Color MenuItemPressedGradientBegin => AccentLightAlt;
        public override Color MenuItemPressedGradientEnd => AccentLightAlt;
        public override Color MenuItemBorder => AccentColor;
        public override Color CheckBackground => AccentLight;
        public override Color CheckPressedBackground => AccentLightAlt;
        public override Color CheckSelectedBackground => AccentLight;
        public override Color SeparatorDark => BorderColor;
        public override Color SeparatorLight => PanelBackColor;
    }

    // ── UserControl Styling ──

    public static void StyleUserControl(UserControl uc)
    {
        StyleDialogControls(uc.Controls);
    }
}
