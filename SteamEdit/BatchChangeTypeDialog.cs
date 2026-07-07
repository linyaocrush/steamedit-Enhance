#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Common;

namespace SteamEdit;

internal class BatchChangeTypeDialog : LocalizableForm
{
    private IContainer components = null!;
    private Label lblType = null!;
    private ComboBox cmbType = null!;
    private Button btnApply = null!;
    private Button btnCancel = null!;

    public AppInfoEntryType SelectedType => (AppInfoEntryType)cmbType.SelectedItem!;

    public BatchChangeTypeDialog()
    {
        InitializeComponent();
        cmbType.Format += (s, e) =>
        {
            if (e.ListItem is AppInfoEntryType entryType)
                e.Value = I18n.GetEnumDisplayName(entryType);
        };
        ThemeManager.StyleDialog(this);
    }

    protected override void ApplyLanguage()
    {
        Text = Strings.BatchChangeType_Title;
        lblType.Text = Strings.BatchChangeType_Label;
        btnApply.Text = Strings.BatchChangeType_BtnApply;
        btnCancel.Text = Strings.BatchChangeType_BtnCancel;
        // 刷新类型下拉框以使用当前语言的枚举显示名
        cmbType.Refresh();
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.lblType = new Label();
        this.cmbType = new ComboBox();
        this.btnApply = new Button();
        this.btnCancel = new Button();

        SuspendLayout();

        // lblType
        this.lblType.AutoSize = true;
        this.lblType.Location = new Point(12, 17);
        this.lblType.TabIndex = 0;
        this.lblType.Text = "New Type:";

        // cmbType
        this.cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cmbType.Location = new Point(80, 14);
        this.cmbType.Size = new Size(160, 24);
        this.cmbType.TabIndex = 1;
        this.cmbType.Items.AddRange(new object[]
        {
            AppInfoEntryType.Game,
            AppInfoEntryType.Tool,
            AppInfoEntryType.Demo,
            AppInfoEntryType.DLC,
            AppInfoEntryType.Application,
            AppInfoEntryType.Video,
            AppInfoEntryType.Guide,
            AppInfoEntryType.Config,
            AppInfoEntryType.Music,
            AppInfoEntryType.Beta,
            AppInfoEntryType.Unknown,
        });
        this.cmbType.SelectedIndex = 0;

        // btnApply
        this.btnApply.DialogResult = DialogResult.OK;
        this.btnApply.Location = new Point(84, 55);
        this.btnApply.Size = new Size(75, 28);
        this.btnApply.TabIndex = 2;
        this.btnApply.Text = "Apply";
        this.btnApply.UseVisualStyleBackColor = true;

        // btnCancel
        this.btnCancel.DialogResult = DialogResult.Cancel;
        this.btnCancel.Location = new Point(165, 55);
        this.btnCancel.Size = new Size(75, 28);
        this.btnCancel.TabIndex = 3;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.UseVisualStyleBackColor = true;

        // Form
        AcceptButton = this.btnApply;
        AutoScaleMode = AutoScaleMode.None;
        CancelButton = this.btnCancel;
        ClientSize = new Size(254, 95);
        ControlBox = true;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Batch Change Type";
        Controls.Add(this.cmbType);
        Controls.Add(this.lblType);
        Controls.Add(this.btnCancel);
        Controls.Add(this.btnApply);

        ResumeLayout(false);
        PerformLayout();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }
}
