using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SteamEdit;

public class VRBatch : Form
{
	public enum ValueTag
	{
		Default,
		Remove
	}

	private IContainer components;

	private ComboBox listValue;

	private Label label1;

	private Button btnCancel;

	private Button btnOkay;

	public ValueTag Value => (ValueTag)listValue.SelectedIndex;

	private static readonly string[] VRRawValues = { "default", "removed" };

	public VRBatch()
	{
		InitializeComponent();
		I18n.LanguageChanged += OnLanguageChanged;
		ApplyLanguage();
		foreach (var val in VRRawValues) listValue.Items.Add(val);
		listValue.SelectedIndex = 0;
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
		ApplyLanguage();
	}

	private void ApplyLanguage()
	{
		Text = Strings.VRBatch_Title;
		label1.Text = Strings.VRBatch_LabelSetAll;
		btnOkay.Text = Strings.VRBatch_BtnOkay;
		btnCancel.Text = Strings.VRBatch_BtnCancel;
		listValue.Format -= VrCombo_Format;
		listValue.Format += VrCombo_Format;
		listValue.Refresh();
	}

	private void VrCombo_Format(object sender, ListControlConvertEventArgs e)
	{
		string raw = e.ListItem as string;
		if (raw == "default") e.Value = Strings.MainWindow_VR_Default;
		else if (raw == "removed") e.Value = Strings.MainWindow_VR_Removed;
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
		this.label1 = new System.Windows.Forms.Label();
		this.btnCancel = new System.Windows.Forms.Button();
		this.btnOkay = new System.Windows.Forms.Button();
		this.listValue = new System.Windows.Forms.ComboBox();
		base.SuspendLayout();
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(12, 15);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(36, 13);
		this.label1.TabIndex = 0;
		this.label1.Text = "Set all";
		this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		this.btnCancel.Location = new System.Drawing.Point(109, 39);
		this.btnCancel.Name = "btnCancel";
		this.btnCancel.Size = new System.Drawing.Size(75, 23);
		this.btnCancel.TabIndex = 4;
		this.btnCancel.Text = "Cancel";
		this.btnCancel.UseVisualStyleBackColor = true;
		this.btnOkay.DialogResult = System.Windows.Forms.DialogResult.OK;
		this.btnOkay.Location = new System.Drawing.Point(22, 39);
		this.btnOkay.Name = "btnOkay";
		this.btnOkay.Size = new System.Drawing.Size(75, 23);
		this.btnOkay.TabIndex = 5;
		this.btnOkay.Text = "Okay";
		this.btnOkay.UseVisualStyleBackColor = true;
		this.listValue.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.listValue.FormattingEnabled = true;
		this.listValue.Location = new System.Drawing.Point(63, 12);
		this.listValue.Name = "listValue";
		this.listValue.Size = new System.Drawing.Size(121, 21);
		this.listValue.TabIndex = 1;
		base.AcceptButton = this.btnOkay;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.CancelButton = this.btnCancel;
		base.ClientSize = new System.Drawing.Size(202, 71);
		base.ControlBox = false;
		base.Controls.Add(this.btnOkay);
		base.Controls.Add(this.btnCancel);
		base.Controls.Add(this.listValue);
		base.Controls.Add(this.label1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "VRBatch";
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
