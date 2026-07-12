#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Common;

namespace steamedit_Enhance;

internal class BatchRenameDialog : LocalizableForm
{
    private IContainer components = null!;
    private CheckBox chkPrefix = null!;
    private TextBox txtPrefix = null!;
    private CheckBox chkSuffix = null!;
    private TextBox txtSuffix = null!;
    private Button btnApply = null!;
    private Button btnCancel = null!;
    private readonly int _entryCount;

    public string Prefix => chkPrefix.Checked ? txtPrefix.Text : "";
    public string Suffix => chkSuffix.Checked ? txtSuffix.Text : "";

    public BatchRenameDialog(int entryCount)
    {
        _entryCount = entryCount;
        InitializeComponent();
        ThemeManager.StyleDialog(this);
    }

    protected override void ApplyLanguage()
    {
        Text = Strings.BatchRename_Title;
        chkPrefix.Text = Strings.BatchRename_Prefix;
        chkSuffix.Text = Strings.BatchRename_Suffix;
        btnApply.Text = Strings.BatchRename_BtnApply;
        btnCancel.Text = Strings.BatchRename_BtnCancel;
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.chkPrefix = new CheckBox();
        this.txtPrefix = new TextBox();
        this.chkSuffix = new CheckBox();
        this.txtSuffix = new TextBox();
        this.btnApply = new Button();
        this.btnCancel = new Button();

        SuspendLayout();

        // chkPrefix
        this.chkPrefix.AutoSize = true;
        this.chkPrefix.Location = new Point(12, 15);
        this.chkPrefix.TabIndex = 0;
        this.chkPrefix.Text = "Prefix:";

        // txtPrefix
        this.txtPrefix.Location = new Point(80, 12);
        this.txtPrefix.Size = new Size(200, 23);
        this.txtPrefix.TabIndex = 1;

        // chkSuffix
        this.chkSuffix.AutoSize = true;
        this.chkSuffix.Location = new Point(12, 45);
        this.chkSuffix.TabIndex = 2;
        this.chkSuffix.Text = "Suffix:";

        // txtSuffix
        this.txtSuffix.Location = new Point(80, 42);
        this.txtSuffix.Size = new Size(200, 23);
        this.txtSuffix.TabIndex = 3;

        // btnApply
        this.btnApply.DialogResult = DialogResult.OK;
        this.btnApply.Location = new Point(124, 80);
        this.btnApply.Size = new Size(75, 28);
        this.btnApply.TabIndex = 4;
        this.btnApply.Text = "Apply";
        this.btnApply.UseVisualStyleBackColor = true;

        // btnCancel
        this.btnCancel.DialogResult = DialogResult.Cancel;
        this.btnCancel.Location = new Point(205, 80);
        this.btnCancel.Size = new Size(75, 28);
        this.btnCancel.TabIndex = 5;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.UseVisualStyleBackColor = true;

        // Form
        AcceptButton = this.btnApply;
        AutoScaleMode = AutoScaleMode.None;
        CancelButton = this.btnCancel;
        ClientSize = new Size(294, 120);
        ControlBox = true;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Batch Rename";
        Controls.Add(this.btnCancel);
        Controls.Add(this.btnApply);
        Controls.Add(this.txtSuffix);
        Controls.Add(this.chkSuffix);
        Controls.Add(this.txtPrefix);
        Controls.Add(this.chkPrefix);

        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }
}
