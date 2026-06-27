#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SteamEdit.Data;

namespace SteamEdit;

internal class BatchReplaceDialog : LocalizableForm
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private readonly IReadOnlyList<IAppInfoEntry> _entries;
    private IContainer components = null!;
    private TextBox textFind = null!;
    private TextBox textReplace = null!;
    private Button btnCaseSensitive = null!;
    private Button btnRegex = null!;
    private Button btnReplaceAll = null!;
    private Button btnCancel = null!;
    private Label lblMatchCount = null!;
    private Label lblFind = null!;
    private Label lblReplace = null!;
    private ListView listPreview = null!;

    private ToolTip toolTips = null!;
    private bool _caseSensitive;
    private bool _regexMode;
    private readonly Timer _debounceTimer;

    private RegexOptions RegexOptionsValue => _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
    private StringComparison ComparisonValue => _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static readonly Color HighlightColor = Color.FromArgb(80, 70, 130, 230);
    private static readonly Color HighlightColorSelected = Color.FromArgb(120, 70, 130, 230);

    public BatchReplaceDialog(IReadOnlyList<IAppInfoEntry> entries)
    {
        _entries = entries;
        InitializeComponent();
        ThemeManager.ApplyToForm(this);
        ThemeManager.StyleTextBox(textFind);
        ThemeManager.StyleTextBox(textReplace);
        ThemeManager.StyleButton(btnReplaceAll);
        ThemeManager.StyleButton(btnCancel);
        foreach (Control c in Controls)
        {
            if (c is Label l)
            {
                l.BackColor = Color.Transparent;
                l.ForeColor = ThemeManager.TextColor;
            }
        }
        _debounceTimer = new Timer { Interval = 200 };
        _debounceTimer.Tick += (_, _) => { _debounceTimer.Stop(); UpdatePreview(); };
        UpdatePreview();
    }

    protected override void ApplyLanguage()
    {
        Text = Strings.BatchReplace_Title;
        lblFind.Text = Strings.BatchReplace_Find;
        lblReplace.Text = Strings.BatchReplace_Replace;
        btnReplaceAll.Text = Strings.BatchReplace_BtnReplaceAll;
        btnCancel.Text = Strings.BatchReplace_BtnCancel;
        toolTips.SetToolTip(btnCaseSensitive, Strings.BatchReplace_CaseSensitive);
        toolTips.SetToolTip(btnRegex, Strings.BatchReplace_Regex);
        if (listPreview.Columns.Count >= 3)
        {
            listPreview.Columns[1].Text = Strings.BatchReplace_PreviewOriginal;
            listPreview.Columns[2].Text = Strings.BatchReplace_PreviewResult;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _debounceTimer.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.textFind = new TextBox();
        this.textReplace = new TextBox();
        this.btnCaseSensitive = new Button();
        this.btnRegex = new Button();
        this.btnReplaceAll = new Button();
        this.btnCancel = new Button();
        this.lblMatchCount = new Label();
        this.lblFind = new Label();
        this.lblReplace = new Label();
        this.listPreview = new ListView();
        this.components = new System.ComponentModel.Container();
        toolTips = new ToolTip(components);
        SuspendLayout();

        // lblFind
        lblFind.AutoSize = true;
        lblFind.Location = new Point(12, 15);
        lblFind.Name = "lblFind";
        lblFind.Text = "Find:";

        // textFind
        textFind.Location = new Point(70, 12);
        textFind.Name = "textFind";
        textFind.Size = new Size(250, 20);
        textFind.TabIndex = 0;
        textFind.TextChanged += OnTextFind_TextChanged;

        // btnCaseSensitive
        btnCaseSensitive.FlatStyle = FlatStyle.Flat;
        btnCaseSensitive.Location = new Point(326, 11);
        btnCaseSensitive.Name = "btnCaseSensitive";
        btnCaseSensitive.Size = new Size(30, 22);
        btnCaseSensitive.TabIndex = 1;
        btnCaseSensitive.Text = "Aa";
        btnCaseSensitive.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        btnCaseSensitive.UseVisualStyleBackColor = true;
        btnCaseSensitive.Click += OnBtnCaseSensitive_Click;

        // btnRegex
        btnRegex.FlatStyle = FlatStyle.Flat;
        btnRegex.Location = new Point(358, 11);
        btnRegex.Name = "btnRegex";
        btnRegex.Size = new Size(30, 22);
        btnRegex.TabIndex = 2;
        btnRegex.Text = ".*";
        btnRegex.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        btnRegex.UseVisualStyleBackColor = true;
        btnRegex.Click += OnBtnRegex_Click;

        // lblReplace
        lblReplace.AutoSize = true;
        lblReplace.Location = new Point(12, 41);
        lblReplace.Name = "lblReplace";
        lblReplace.Text = "Replace:";

        // textReplace
        textReplace.Location = new Point(70, 38);
        textReplace.Name = "textReplace";
        textReplace.Size = new Size(318, 20);
        textReplace.TabIndex = 3;
        textReplace.TextChanged += (s, e) => RestartDebounce();

        // lblMatchCount
        lblMatchCount.AutoSize = true;
        lblMatchCount.Location = new Point(12, 68);
        lblMatchCount.Name = "lblMatchCount";
        lblMatchCount.ForeColor = SystemColors.GrayText;
        lblMatchCount.Text = "Matched: 0 games";

        // listPreview
        listPreview.Location = new Point(12, 88);
        listPreview.Name = "listPreview";
        listPreview.Size = new Size(376, 140);
        listPreview.TabIndex = 6;
        listPreview.View = View.Details;
        listPreview.FullRowSelect = true;
        listPreview.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        listPreview.OwnerDraw = true;
        listPreview.GridLines = true;
        listPreview.Columns.Add("#", 30);
        listPreview.Columns.Add("Original", 155);
        listPreview.Columns.Add("Result", 155);
        listPreview.DrawColumnHeader += OnListPreview_DrawColumnHeader;
        listPreview.DrawItem += OnListPreview_DrawItem;
        listPreview.DrawSubItem += OnListPreview_DrawSubItem;

        // btnReplaceAll
        btnReplaceAll.DialogResult = DialogResult.OK;
        btnReplaceAll.Location = new Point(232, 236);
        btnReplaceAll.Name = "btnReplaceAll";
        btnReplaceAll.Size = new Size(75, 26);
        btnReplaceAll.TabIndex = 4;
        btnReplaceAll.Text = "Replace All";
        btnReplaceAll.UseVisualStyleBackColor = true;

        // btnCancel
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Location = new Point(313, 236);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(75, 26);
        btnCancel.TabIndex = 5;
        btnCancel.Text = "Cancel";
        btnCancel.UseVisualStyleBackColor = true;

        // BatchReplaceDialog
        AcceptButton = btnReplaceAll;
        CancelButton = btnCancel;
        AutoScaleDimensions = new SizeF(6F, 13F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(400, 270);
        ControlBox = false;
        Controls.Add(listPreview);
        Controls.Add(btnCancel);
        Controls.Add(btnReplaceAll);
        Controls.Add(lblMatchCount);
        Controls.Add(textReplace);
        Controls.Add(lblReplace);
        Controls.Add(btnRegex);
        Controls.Add(btnCaseSensitive);
        Controls.Add(textFind);
        Controls.Add(lblFind);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "BatchReplaceDialog";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ResumeLayout(false);
        PerformLayout();
    }

    private void OnTextFind_TextChanged(object? sender, EventArgs e) => RestartDebounce();

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void OnBtnCaseSensitive_Click(object? sender, EventArgs e)
    {
        _caseSensitive = !_caseSensitive;
        btnCaseSensitive.BackColor = _caseSensitive ? ThemeManager.AccentLight : SystemColors.Control;
        btnCaseSensitive.ForeColor = _caseSensitive ? ThemeManager.AccentColor : SystemColors.ControlText;
        UpdatePreview();
    }

    private void OnBtnRegex_Click(object? sender, EventArgs e)
    {
        _regexMode = !_regexMode;
        btnRegex.BackColor = _regexMode ? ThemeManager.AccentLight : SystemColors.Control;
        btnRegex.ForeColor = _regexMode ? ThemeManager.AccentColor : SystemColors.ControlText;
        UpdatePreview();
    }

    private void OnListPreview_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.DrawDefault = true;
    }

    private void OnListPreview_DrawItem(object? sender, DrawListViewItemEventArgs e)
    {
        e.DrawDefault = false;
        using var brush = new SolidBrush(e.Item.Selected ? ThemeManager.AccentLight : Color.White);
        e.Graphics.FillRectangle(brush, e.Bounds);
    }

    private void OnListPreview_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.ColumnIndex == 0)
        {
            e.DrawDefault = true;
            return;
        }

        var ranges = e.Item!.Tag as List<(int Index, int Length)>;
        if (ranges == null || ranges.Count == 0)
        {
            e.DrawDefault = true;
            return;
        }

        string text = e.SubItem!.Text;
        bool selected = e.Item.Selected;
        var textColor = selected ? Color.White : ThemeManager.TextColor;
        var bgColor = selected ? HighlightColorSelected : HighlightColor;

        int x = e.Bounds.X + 2;
        int y = e.Bounds.Y;
        int lastIdx = 0;

        foreach (var (idx, len) in ranges)
        {
            if (idx > lastIdx)
            {
                string before = text.Substring(lastIdx, idx - lastIdx);
                TextRenderer.DrawText(e.Graphics, before, Font, new Point(x, y), textColor);
                x += TextRenderer.MeasureText(e.Graphics, before, Font).Width;
            }

            string match = text.Substring(idx, len);
            var matchSize = TextRenderer.MeasureText(e.Graphics, match, Font);
            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(brush, x, y, matchSize.Width, matchSize.Height);
            }
            TextRenderer.DrawText(e.Graphics, match, Font, new Point(x, y), textColor);
            x += matchSize.Width;
            lastIdx = idx + len;
        }

        if (lastIdx < text.Length)
        {
            string after = text.Substring(lastIdx);
            TextRenderer.DrawText(e.Graphics, after, Font, new Point(x, y), textColor);
        }
    }

    private void UpdatePreview()
    {
        string find = textFind.Text;
        string replace = textReplace.Text ?? "";
        if (string.IsNullOrEmpty(find))
        {
            lblMatchCount.Text = string.Format(Strings.BatchReplace_MatchCount, 0);
            btnReplaceAll.Enabled = false;
            listPreview.Items.Clear();
            return;
        }

        int count = 0;
        listPreview.BeginUpdate();
        listPreview.Items.Clear();
        try
        {
            foreach (var entry in _entries)
            {
                if (!IsMatch(entry.Name, find)) continue;

                count++;
                string result = ComputeResult(entry.Name, find, replace);
                var ranges = GetMatchRanges(entry.Name, find);

                var item = new ListViewItem(count.ToString());
                item.SubItems.Add(entry.Name);
                item.SubItems.Add(result);
                item.Tag = ranges;
                listPreview.Items.Add(item);
            }
            lblMatchCount.Text = string.Format(Strings.BatchReplace_MatchCount, count);
            btnReplaceAll.Enabled = count > 0;
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            lblMatchCount.Text = string.Format(Strings.BatchReplace_InvalidRegex, ex.Message);
            btnReplaceAll.Enabled = false;
        }
        finally
        {
            listPreview.EndUpdate();
        }
    }

    private string ComputeResult(string original, string find, string replace)
    {
        if (_regexMode)
        {
            var options = RegexOptionsValue;
            return Regex.Replace(original, find, replace, options, RegexTimeout);
        }
        else
        {
            var comparison = ComparisonValue;
            return ReplaceWithStringComparison(original, find, replace, comparison);
        }
    }

    private List<(int Index, int Length)> GetMatchRanges(string text, string pattern)
    {
        var ranges = new List<(int Index, int Length)>();
        if (_regexMode)
        {
            var options = RegexOptionsValue;
            foreach (Match m in Regex.Matches(text, pattern, options, RegexTimeout))
            {
                ranges.Add((m.Index, m.Length));
            }
        }
        else
        {
            var comparison = ComparisonValue;
            int pos = 0;
            while (pos < text.Length)
            {
                int idx = text.IndexOf(pattern, pos, comparison);
                if (idx < 0) break;
                ranges.Add((idx, pattern.Length));
                pos = idx + pattern.Length;
            }
        }
        return ranges;
    }

    private bool IsMatch(string text, string pattern)
    {
        if (_regexMode)
        {
            var options = RegexOptionsValue;
            return Regex.IsMatch(text, pattern, options, RegexTimeout);
        }
        else
        {
            var comparison = ComparisonValue;
            return text.Contains(pattern, comparison);
        }
    }

    public int ReplaceAll()
    {
        string find = textFind.Text;
        string replace = textReplace.Text ?? "";
        if (string.IsNullOrEmpty(find)) return 0;

        int totalReplacements = 0;
        foreach (var entry in _entries)
        {
            string original = entry.Name;
            string result;

            if (_regexMode)
            {
                var options = RegexOptionsValue;
                try
                {
                    result = Regex.Replace(original, find, replace, options, RegexTimeout);
                }
                catch (RegexMatchTimeoutException)
                {
                    continue; // skip entries that cause regex timeout
                }
            }
            else
            {
                var comparison = ComparisonValue;
                if (!original.Contains(find, comparison)) continue;
                result = ReplaceWithStringComparison(original, find, replace, comparison);
            }

            if (result != original)
            {
                entry.Name = result;
                totalReplacements++;
            }
        }
        return totalReplacements;
    }

    private static string ReplaceWithStringComparison(string source, string oldValue, string newValue, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(oldValue)) return source;
        var sb = new System.Text.StringBuilder(source.Length);
        int pos = 0;
        while (pos < source.Length)
        {
            int idx = source.IndexOf(oldValue, pos, comparison);
            if (idx < 0)
            {
                sb.Append(source, pos, source.Length - pos);
                break;
            }
            sb.Append(source, pos, idx - pos);
            sb.Append(newValue);
            pos = idx + oldValue.Length;
        }
        return sb.ToString();
    }
}
