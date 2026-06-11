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

	private CheckBox checkEnableAutoUpdate;

	private TextBox textPassThroughArgs;

	private TextBox textAppInfoPath;

	private CheckBox checkReplaceLocalizedNames;

	public SettingsDlg()
	{
		InitializeComponent();
	}

	private void Settings_Load(object sender, EventArgs e)
	{
		checkEnableCompleted.Checked = Settings.Default.EnableCompleted;
		checkEnabledHidden.Checked = Settings.Default.EnableHidden;
		checkReplaceLocalizedNames.Checked = Settings.Default.ReplaceLocalizedNames;
		checkEnableAutoUpdate.Checked = Settings.Default.EnableAutoUpdate;
		textPassThroughArgs.Text = Settings.Default.PassThroughArguments;
		textAppInfoPath.Text = Settings.Default.AppInfoPath;
	}

	private void SettingsDlg_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (base.DialogResult == DialogResult.OK)
		{
			Settings.Default.EnableCompleted = checkEnableCompleted.Checked;
			Settings.Default.EnableHidden = checkEnabledHidden.Checked;
			Settings.Default.ReplaceLocalizedNames = checkReplaceLocalizedNames.Checked;
			Settings.Default.EnableAutoUpdate = checkEnableAutoUpdate.Checked;
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
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SteamEdit.SettingsDlg));
		this.checkReplaceLocalizedNames = new System.Windows.Forms.CheckBox();
		this.checkEnabledHidden = new System.Windows.Forms.CheckBox();
		this.checkEnableCompleted = new System.Windows.Forms.CheckBox();
		this.textAppInfoPath = new System.Windows.Forms.TextBox();
		this.textPassThroughArgs = new System.Windows.Forms.TextBox();
		this.checkEnableAutoUpdate = new System.Windows.Forms.CheckBox();
		this.toolTip = new System.Windows.Forms.ToolTip(this.components);
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		System.Windows.Forms.Label label = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label2 = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label3 = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label4 = new System.Windows.Forms.Label();
		System.Windows.Forms.TableLayoutPanel tableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
		System.Windows.Forms.Label label5 = new System.Windows.Forms.Label();
		System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
		System.Windows.Forms.Button button2 = new System.Windows.Forms.Button();
		System.Windows.Forms.Label label6 = new System.Windows.Forms.Label();
		System.Windows.Forms.Button button3 = new System.Windows.Forms.Button();
		tableLayoutPanel.SuspendLayout();
		panel.SuspendLayout();
		base.SuspendLayout();
		button.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		button.DialogResult = System.Windows.Forms.DialogResult.OK;
		button.Location = new System.Drawing.Point(12, 200);
		button.Name = "btnOK";
		button.Size = new System.Drawing.Size(88, 30);
		button.TabIndex = 3;
		button.Text = "OK";
		button.UseVisualStyleBackColor = true;
		button.Click += new System.EventHandler(btnOK_Click);
		label.AutoSize = true;
		label.Location = new System.Drawing.Point(6, 6);
		label.Margin = new System.Windows.Forms.Padding(6);
		label.Name = "label1";
		label.Size = new System.Drawing.Size(103, 13);
		label.TabIndex = 0;
		label.Text = "Enable \"Completed\"";
		label2.AutoSize = true;
		label2.Location = new System.Drawing.Point(6, 32);
		label2.Margin = new System.Windows.Forms.Padding(6);
		label2.Name = "label2";
		label2.Size = new System.Drawing.Size(87, 13);
		label2.TabIndex = 1;
		label2.Text = "Enable \"Hidden\"";
		label3.AutoSize = true;
		label3.Location = new System.Drawing.Point(6, 84);
		label3.Margin = new System.Windows.Forms.Padding(6);
		label3.Name = "label3";
		label3.Size = new System.Drawing.Size(67, 13);
		label3.TabIndex = 3;
		label3.Text = "Auto-Update";
		label4.AutoSize = true;
		label4.Location = new System.Drawing.Point(6, 110);
		label4.Margin = new System.Windows.Forms.Padding(6);
		label4.Name = "label4";
		label4.Size = new System.Drawing.Size(126, 13);
		label4.TabIndex = 4;
		label4.Text = "Pass-Through Arguments";
		tableLayoutPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 41.91617f));
		tableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 58.08383f));
		tableLayoutPanel.Controls.Add(label5, 0, 2);
		tableLayoutPanel.Controls.Add(this.checkReplaceLocalizedNames, 1, 2);
		tableLayoutPanel.Controls.Add(this.checkEnabledHidden, 1, 1);
		tableLayoutPanel.Controls.Add(this.checkEnableCompleted, 1, 0);
		tableLayoutPanel.Controls.Add(label, 0, 0);
		tableLayoutPanel.Controls.Add(label2, 0, 1);
		tableLayoutPanel.Controls.Add(panel, 1, 5);
		tableLayoutPanel.Controls.Add(label6, 0, 5);
		tableLayoutPanel.Controls.Add(this.textPassThroughArgs, 1, 4);
		tableLayoutPanel.Controls.Add(this.checkEnableAutoUpdate, 1, 3);
		tableLayoutPanel.Controls.Add(label4, 0, 4);
		tableLayoutPanel.Controls.Add(label3, 0, 3);
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
		tableLayoutPanel.Size = new System.Drawing.Size(395, 188);
		tableLayoutPanel.TabIndex = 7;
		label5.AutoSize = true;
		label5.Location = new System.Drawing.Point(6, 58);
		label5.Margin = new System.Windows.Forms.Padding(6);
		label5.Name = "label6";
		label5.Size = new System.Drawing.Size(145, 13);
		label5.TabIndex = 9;
		label5.Text = "Replace All Localized Names";
		this.checkReplaceLocalizedNames.AutoSize = true;
		this.checkReplaceLocalizedNames.Location = new System.Drawing.Point(171, 58);
		this.checkReplaceLocalizedNames.Margin = new System.Windows.Forms.Padding(6);
		this.checkReplaceLocalizedNames.Name = "checkReplaceLocalizedNames";
		this.checkReplaceLocalizedNames.Size = new System.Drawing.Size(15, 14);
		this.checkReplaceLocalizedNames.TabIndex = 8;
		this.toolTip.SetToolTip(this.checkReplaceLocalizedNames, "Check for SteamEdit updates at startup.");
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
		panel.Location = new System.Drawing.Point(165, 131);
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
		this.toolTip.SetToolTip(this.textAppInfoPath, resources.GetString("textAppInfoPath.ToolTip"));
		label6.AutoSize = true;
		label6.Location = new System.Drawing.Point(6, 137);
		label6.Margin = new System.Windows.Forms.Padding(6);
		label6.Name = "label5";
		label6.Size = new System.Drawing.Size(147, 13);
		label6.TabIndex = 6;
		label6.Text = "AppInfo Path (requires restart)";
		this.textPassThroughArgs.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.textPassThroughArgs.Location = new System.Drawing.Point(168, 107);
		this.textPassThroughArgs.Name = "textPassThroughArgs";
		this.textPassThroughArgs.Size = new System.Drawing.Size(224, 20);
		this.textPassThroughArgs.TabIndex = 5;
		this.toolTip.SetToolTip(this.textPassThroughArgs, resources.GetString("textPassThroughArgs.ToolTip"));
		this.checkEnableAutoUpdate.AutoSize = true;
		this.checkEnableAutoUpdate.Location = new System.Drawing.Point(171, 84);
		this.checkEnableAutoUpdate.Margin = new System.Windows.Forms.Padding(6);
		this.checkEnableAutoUpdate.Name = "checkEnableAutoUpdate";
		this.checkEnableAutoUpdate.Size = new System.Drawing.Size(15, 14);
		this.checkEnableAutoUpdate.TabIndex = 2;
		this.toolTip.SetToolTip(this.checkEnableAutoUpdate, "Check for SteamEdit updates at startup.");
		this.checkEnableAutoUpdate.UseVisualStyleBackColor = true;
		button3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		button3.DialogResult = System.Windows.Forms.DialogResult.OK;
		button3.Location = new System.Drawing.Point(106, 200);
		button3.Name = "btnCancel";
		button3.Size = new System.Drawing.Size(88, 30);
		button3.TabIndex = 8;
		button3.Text = "Cancel";
		button3.UseVisualStyleBackColor = true;
		button3.Click += new System.EventHandler(btnCancel_Click);
		base.AcceptButton = button;
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.CancelButton = button3;
		base.ClientSize = new System.Drawing.Size(413, 234);
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
