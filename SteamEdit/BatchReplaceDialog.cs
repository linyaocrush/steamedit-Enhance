using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SteamEdit.Data;

namespace SteamEdit;

internal class BatchReplaceDialog : Form
{
    private readonly IReadOnlyList<IAppInfoEntry> _entries;
    private IContainer components;
    private TextBox textFind;
    private TextBox textReplace;
    private Button btnCaseSensitive;
    private Button btnRegex;
    private Button btnReplaceAll;
    private Button btnCancel;
    private Label lblMatchCount;
    private Label lblFind;
    private Label lblReplace;
    private ListView listPreview;

    private ToolTip toolTips;
    private bool _caseSensitive;
    private bool _regexMode;

    public BatchReplaceDialog(IReadOnlyList<IAppInfoEntry> entries)
    {
        _entries = entries;
        InitializeComponent();
        I18n.LanguageChanged += OnLanguageChanged;
        ApplyLanguage();
        UpdateMatchCount();
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = Strings.BatchReplace_Title;
        lblFind.Text = Strings.BatchReplace_Find;
        lblReplace.Text = Strings.BatchReplace_Replace;
        btnReplaceAll.Text = Strings.BatchReplace_BtnReplaceAll;
        btnCancel.Text = Strings.BatchReplace_BtnCancel;
        toolTips.SetToolTip(btnCaseSensitive, Strings.BatchReplace_CaseSensitive);
        toolTips.SetToolTip(btnRegex, Strings.BatchReplace_Regex);
    }

    protected override void Dispose(bool disposing)
    {
        I18n.LanguageChanged -= OnLanguageChanged;
        if (disposing && components != null)
        {
            components.Dispose();
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

    private void OnTextFind_TextChanged(object sender, EventArgs e)
    {
        UpdateMatchCount();
    }

    private void OnBtnCaseSensitive_Click(object sender, EventArgs e)
    {
        _caseSensitive = !_caseSensitive;
        btnCaseSensitive.BackColor = _caseSensitive ? SystemColors.Highlight : SystemColors.Control;
        btnCaseSensitive.ForeColor = _caseSensitive ? SystemColors.HighlightText : SystemColors.ControlText;
        UpdateMatchCount();
    }

    private void OnBtnRegex_Click(object sender, EventArgs e)
    {
        _regexMode = !_regexMode;
        btnRegex.BackColor = _regexMode ? SystemColors.Highlight : SystemColors.Control;
        btnRegex.ForeColor = _regexMode ? SystemColors.HighlightText : SystemColors.ControlText;
        UpdateMatchCount();
    }

    private void OnListPreview_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.DrawDefault = true;
    }

    private void OnListPreview_DrawItem(object sender, DrawListViewItemEventArgs e)
    {
        e.DrawDefault = false;
        e.Graphics.FillRectangle(e.Item.Selected ? SystemBrushes.Highlight : SystemBrushes.Window, e.Bounds);
    }

    private void OnListPreview_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
    {
        e.DrawDefault = true;
    }

    private void UpdateMatchCount()
    {
        string find = textFind.Text;
        if (string.IsNullOrEmpty(find))
        {
            lblMatchCount.Text = string.Format(Strings.BatchReplace_MatchCount, 0);
            btnReplaceAll.Enabled = false;
            return;
        }

        int count = 0;
        try
        {
            foreach (var entry in _entries)
            {
                if (IsMatch(entry.Name, find))
                    count++;
            }
            lblMatchCount.Text = string.Format(Strings.BatchReplace_MatchCount, count);
            btnReplaceAll.Enabled = count > 0;
        }
        catch (ArgumentException ex)
        {
            lblMatchCount.Text = string.Format(Strings.BatchReplace_InvalidRegex, ex.Message);
            btnReplaceAll.Enabled = false;
        }
    }

    private bool IsMatch(string text, string pattern)
    {
        if (_regexMode)
        {
            var options = _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            return Regex.IsMatch(text, pattern, options);
        }
        else
        {
            var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
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
                var options = _caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                result = Regex.Replace(original, find, replace, options);
            }
            else
            {
                var comparison = _caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
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
