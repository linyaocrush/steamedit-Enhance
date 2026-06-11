using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Common;
using SteamEdit.Properties;

namespace SteamEdit;

internal class SettingsDlg : Form
{
    private IContainer components;

    private CheckBox checkEnableCompleted;

    private ToolTip toolTip;

    private CheckBox checkEnabledHidden;

    private TextBox textPassThroughArgs;

    private TextBox textAppInfoPath;

    private CheckBox checkReplaceLocalizedNames;

    private ComboBox comboLanguage;

    private Label lblLanguage;

    private Label label1;

    private Label label2;

    private Label label4;

    private Label label5;

    private Label label6;

    public SettingsDlg()
    {
        InitializeComponent();
    }

    private void Settings_Load(object sender, EventArgs e)
    {
        checkEnableCompleted.Checked = Settings.Default.EnableCompleted;
        checkEnabledHidden.Checked = Settings.Default.EnableHidden;
        checkReplaceLocalizedNames.Checked = Settings.Default.ReplaceLocalizedNames;
        textPassThroughArgs.Text = Settings.Default.PassThroughArguments;
        textAppInfoPath.Text = Settings.Default.AppInfoPath;

        // Set language combo selection
        string savedLang = Settings.Default.Language;
        if (string.IsNullOrEmpty(savedLang))
        {
            savedLang = System.Globalization.CultureInfo.CurrentUICulture.Name;
        }
        if (savedLang.StartsWith("zh"))
        {
            comboLanguage.SelectedIndex = 1;
        }
        else
        {
            comboLanguage.SelectedIndex = 0;
        }

        ApplyLanguage();
    }

    private void SettingsDlg_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (base.DialogResult == DialogResult.OK)
        {
            Settings.Default.EnableCompleted = checkEnableCompleted.Checked;
            Settings.Default.EnableHidden = checkEnabledHidden.Checked;
            Settings.Default.ReplaceLocalizedNames = checkReplaceLocalizedNames.Checked;
            Settings.Default.PassThroughArguments = textPassThroughArgs.Text;
            Settings.Default.AppInfoPath = textAppInfoPath.Text;
            Settings.Default.Save();
        }
    }

    private void btnOK_Click(object sender, EventArgs e)
    {
        base.DialogResult = DialogResult.OK;
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        base.DialogResult = DialogResult.Cancel;
    }

    private void btnSelectAppInfo_Click(object sender, EventArgs e)
    {
        using OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.CheckFileExists = true;
        openFileDialog.Filter = "appinfo.vdf|appinfo.vdf";
        openFileDialog.Multiselect = false;
        openFileDialog.InitialDirectory = Path.GetDirectoryName(textAppInfoPath.Text);
        openFileDialog.FileName = Path.GetFileName(textAppInfoPath.Text);
        if (openFileDialog.ShowDialog(this) == DialogResult.OK)
        {
            textAppInfoPath.Text = openFileDialog.FileName;
        }
    }

    private void comboLanguage_SelectedIndexChanged(object sender, EventArgs e)
    {
        string cultureCode = comboLanguage.SelectedIndex == 1 ? "zh-CN" : "en";
        I18n.SetLanguage(cultureCode);
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(ApplyLanguage));
            return;
        }
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = Strings.Settings_Title;
        label1.Text = Strings.Settings_EnableCompleted;
        label2.Text = Strings.Settings_EnableHidden;
        lblLanguage.Text = Strings.Settings_Language;
        label4.Text = Strings.Settings_PassThroughArgs;
        label5.Text = Strings.Settings_ReplaceLocalizedNames;
        label6.Text = Strings.Settings_AppInfoPath;
        checkReplaceLocalizedNames.Text = "";
        toolTip.SetToolTip(checkReplaceLocalizedNames, Strings.Tooltip_ReplaceLocalizedNames);
        checkEnabledHidden.Text = "";
        toolTip.SetToolTip(checkEnabledHidden, Strings.Tooltip_EnableHidden);
        checkEnableCompleted.Text = "";
        toolTip.SetToolTip(checkEnableCompleted, Strings.Tooltip_EnableCompleted);
        toolTip.SetToolTip(textAppInfoPath, Strings.Tooltip_AppInfoPath);
        toolTip.SetToolTip(textPassThroughArgs, Strings.Tooltip_PassThroughArgs);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        I18n.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        I18n.LanguageChanged -= OnLanguageChanged;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.checkReplaceLocalizedNames = new System.Windows.Forms.CheckBox();
        this.checkEnabledHidden = new System.Windows.Forms.CheckBox();
        this.checkEnableCompleted = new System.Windows.Forms.CheckBox();
        this.textAppInfoPath = new System.Windows.Forms.TextBox();
        this.textPassThroughArgs = new System.Windows.Forms.TextBox();
        this.comboLanguage = new System.Windows.Forms.ComboBox();
        this.lblLanguage = new System.Windows.Forms.Label();
        this.toolTip = new System.Windows.Forms.ToolTip(this.components);
        System.Windows.Forms.Button button = new System.Windows.Forms.Button();
        this.label1 = new System.Windows.Forms.Label();
        this.label2 = new System.Windows.Forms.Label();
        this.label4 = new System.Windows.Forms.Label();
        System.Windows.Forms.TableLayoutPanel tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
        this.label5 = new System.Windows.Forms.Label();
        System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
        System.Windows.Forms.Button button2 = new System.Windows.Forms.Button();
        this.label6 = new System.Windows.Forms.Label();
        System.Windows.Forms.Button button3 = new System.Windows.Forms.Button();
        tableLayoutPanel.SuspendLayout();
        panel.SuspendLayout();
        base.SuspendLayout();
        button.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        button.DialogResult = System.Windows.Forms.DialogResult.OK;
        button.Location = new System.Drawing.Point(12, 226);
        button.Name = "btnOK";
        button.Size = new System.Drawing.Size(88, 30);
        button.TabIndex = 3;
        button.Text = "OK";
        button.UseVisualStyleBackColor = true;
        button.Click += new System.EventHandler(btnOK_Click);
        this.label1.AutoSize = true;
        this.label1.Location = new System.Drawing.Point(6, 6);
        this.label1.Margin = new System.Windows.Forms.Padding(6);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(103, 13);
        this.label1.TabIndex = 0;
        this.label1.Text = "Enable \"Completed\"";
        this.label2.AutoSize = true;
        this.label2.Location = new System.Drawing.Point(6, 32);
        this.label2.Margin = new System.Windows.Forms.Padding(6);
        this.label2.Name = "label2";
        this.label2.Size = new System.Drawing.Size(87, 13);
        this.label2.TabIndex = 1;
        this.label2.Text = "Enable \"Hidden\"";
        this.lblLanguage.AutoSize = true;
        this.lblLanguage.Location = new System.Drawing.Point(6, 58);
        this.lblLanguage.Margin = new System.Windows.Forms.Padding(6);
        this.lblLanguage.Name = "lblLanguage";
        this.lblLanguage.Size = new System.Drawing.Size(55, 13);
        this.lblLanguage.TabIndex = 10;
        this.lblLanguage.Text = "Language:";
        this.comboLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this.comboLanguage.FormattingEnabled = true;
        this.comboLanguage.Items.AddRange(new object[] { "English", "简体中文" });
        this.comboLanguage.Location = new System.Drawing.Point(168, 55);
        this.comboLanguage.Name = "comboLanguage";
        this.comboLanguage.Size = new System.Drawing.Size(121, 21);
        this.comboLanguage.TabIndex = 11;
        this.comboLanguage.SelectedIndexChanged += new System.EventHandler(comboLanguage_SelectedIndexChanged);
        this.label4.AutoSize = true;
        this.label4.Location = new System.Drawing.Point(6, 110);
        this.label4.Margin = new System.Windows.Forms.Padding(6);
        this.label4.Name = "label4";
        this.label4.Size = new System.Drawing.Size(126, 13);
        this.label4.TabIndex = 4;
        this.label4.Text = "Pass-Through Arguments";
        tableLayoutPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        tableLayoutPanel.ColumnCount = 2;
        tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 41.91617f));
        tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58.08383f));
        tableLayoutPanel.Controls.Add(this.label5, 0, 2);
        tableLayoutPanel.Controls.Add(this.checkReplaceLocalizedNames, 1, 2);
        tableLayoutPanel.Controls.Add(this.checkEnabledHidden, 1, 1);
        tableLayoutPanel.Controls.Add(this.checkEnableCompleted, 1, 0);
        tableLayoutPanel.Controls.Add(this.label1, 0, 0);
        tableLayoutPanel.Controls.Add(this.label2, 0, 1);
        tableLayoutPanel.Controls.Add(panel, 1, 5);
        tableLayoutPanel.Controls.Add(this.label6, 0, 5);
        tableLayoutPanel.Controls.Add(this.textPassThroughArgs, 1, 4);
        tableLayoutPanel.Controls.Add(this.lblLanguage, 0, 3);
        tableLayoutPanel.Controls.Add(this.comboLanguage, 1, 3);
        tableLayoutPanel.Controls.Add(this.label4, 0, 4);
        tableLayoutPanel.Location = new System.Drawing.Point(9, 9);
        tableLayoutPanel.Margin = new System.Windows.Forms.Padding(0);
        tableLayoutPanel.Name = "tableLayoutPanel1";
        tableLayoutPanel.RowCount = 6;
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle());
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27f));
        tableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 13f));
        tableLayoutPanel.Size = new System.Drawing.Size(395, 214);
        tableLayoutPanel.TabIndex = 7;
        this.label5.AutoSize = true;
        this.label5.Location = new System.Drawing.Point(6, 84);
        this.label5.Margin = new System.Windows.Forms.Padding(6);
        this.label5.Name = "label5";
        this.label5.Size = new System.Drawing.Size(145, 13);
        this.label5.TabIndex = 9;
        this.label5.Text = "Replace All Localized Names";
        this.checkReplaceLocalizedNames.AutoSize = true;
        this.checkReplaceLocalizedNames.Location = new System.Drawing.Point(171, 84);
        this.checkReplaceLocalizedNames.Margin = new System.Windows.Forms.Padding(6);
        this.checkReplaceLocalizedNames.Name = "checkReplaceLocalizedNames";
        this.checkReplaceLocalizedNames.Size = new System.Drawing.Size(15, 14);
        this.checkReplaceLocalizedNames.TabIndex = 8;
        this.toolTip.SetToolTip(this.checkReplaceLocalizedNames, "Replace all localized game names with the original name.");
        this.checkReplaceLocalizedNames.UseVisualStyleBackColor = true;
        this.checkEnabledHidden.AutoSize = true;
        this.checkEnabledHidden.Location = new System.Drawing.Point(171, 32);
        this.checkEnabledHidden.Margin = new System.Windows.Forms.Padding(6);
        this.checkEnabledHidden.Name = "checkEnabledHidden";
        this.checkEnabledHidden.Size = new System.Drawing.Size(15, 14);
        this.checkEnabledHidden.TabIndex = 1;
        this.toolTip.SetToolTip(this.checkEnabledHidden, "Hide games in Steam's list.");
        this.checkEnabledHidden.UseVisualStyleBackColor = true;
        this.checkEnableCompleted.AutoSize = true;
        this.checkEnableCompleted.Location = new System.Drawing.Point(171, 6);
        this.checkEnableCompleted.Margin = new System.Windows.Forms.Padding(6);
        this.checkEnableCompleted.Name = "checkEnableCompleted";
        this.checkEnableCompleted.Size = new System.Drawing.Size(15, 14);
        this.checkEnableCompleted.TabIndex = 0;
        this.toolTip.SetToolTip(this.checkEnableCompleted, "Append symbol on completed games");
        this.checkEnableCompleted.UseVisualStyleBackColor = true;
        panel.Controls.Add(button2);
        panel.Controls.Add(this.textAppInfoPath);
        panel.Dock = System.Windows.Forms.DockStyle.Fill;
        panel.Location = new System.Drawing.Point(165, 157);
        panel.Margin = new System.Windows.Forms.Padding(0);
        panel.Name = "panel1";
        panel.Size = new System.Drawing.Size(230, 57);
        panel.TabIndex = 7;
        button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        button2.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
        button2.Image = SteamEdit.Properties.Resources.openfolderHS;
        button2.Location = new System.Drawing.Point(201, 0);
        button2.Margin = new System.Windows.Forms.Padding(0);
        button2.Name = "btnSelectAppInfo";
        button2.Size = new System.Drawing.Size(26, 25);
        button2.TabIndex = 12;
        button2.UseVisualStyleBackColor = true;
        button2.Click += new System.EventHandler(btnSelectAppInfo_Click);
        this.textAppInfoPath.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textAppInfoPath.Location = new System.Drawing.Point(3, 3);
        this.textAppInfoPath.Name = "textAppInfoPath";
        this.textAppInfoPath.Size = new System.Drawing.Size(195, 20);
        this.textAppInfoPath.TabIndex = 7;
        this.toolTip.SetToolTip(this.textAppInfoPath, "Path to Steam's appinfo.vdf file");
        this.label6.AutoSize = true;
        this.label6.Location = new System.Drawing.Point(6, 163);
        this.label6.Margin = new System.Windows.Forms.Padding(6);
        this.label6.Name = "label6";
        this.label6.Size = new System.Drawing.Size(147, 13);
        this.label6.TabIndex = 6;
        this.label6.Text = "AppInfo Path (requires restart)";
        this.textPassThroughArgs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.textPassThroughArgs.Location = new System.Drawing.Point(168, 107);
        this.textPassThroughArgs.Name = "textPassThroughArgs";
        this.textPassThroughArgs.Size = new System.Drawing.Size(224, 20);
        this.textPassThroughArgs.TabIndex = 5;
        this.toolTip.SetToolTip(this.textPassThroughArgs, "Arguments to pass along when launching Steam...");
        button3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
        button3.DialogResult = System.Windows.Forms.DialogResult.OK;
        button3.Location = new System.Drawing.Point(106, 226);
        button3.Name = "btnCancel";
        button3.Size = new System.Drawing.Size(88, 30);
        button3.TabIndex = 8;
        button3.Text = "Cancel";
        button3.UseVisualStyleBackColor = true;
        button3.Click += new System.EventHandler(btnCancel_Click);
        base.AcceptButton = button;
        base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        base.CancelButton = button3;
        base.ClientSize = new System.Drawing.Size(413, 260);
        base.ControlBox = false;
        base.Controls.Add(button3);
        base.Controls.Add(tableLayoutPanel);
        base.Controls.Add(button);
        base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        base.Margin = new System.Windows.Forms.Padding(6);
        base.MaximizeBox = false;
        base.MinimizeBox = false;
        base.Name = "SettingsDlg";
        base.ShowIcon = false;
        base.ShowInTaskbar = false;
        base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Settings";
        base.TopMost = true;
        base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(SettingsDlg_FormClosing);
        base.Load += new System.EventHandler(Settings_Load);
        tableLayoutPanel.ResumeLayout(false);
        tableLayoutPanel.PerformLayout();
        panel.ResumeLayout(false);
        panel.PerformLayout();
        base.ResumeLayout(false);
    }
}
