using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SteamEdit;

public class ControllerBatch : Form
{
	private IContainer components;

	private ComboBox listFrom;

	private ComboBox listTo;

	public string From => (string)listFrom.SelectedItem;

	public string To => (string)listTo.SelectedItem;

	public ControllerBatch()
	{
		InitializeComponent();
		I18n.LanguageChanged += OnLanguageChanged;
		ApplyLanguage();
		listFrom.Items.Add("none");
		listFrom.Items.Add("partial");
		listFrom.Items.Add("full");
		listFrom.SelectedItem = "none";
		listTo.Items.Add("none");
		listTo.Items.Add("partial");
		listTo.Items.Add("full");
		listTo.SelectedItem = "none";
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
		ApplyLanguage();
	}

	private void ApplyLanguage()
	{
		Text = Strings.ControllerBatch_Title;
		var label1 = Controls.Find("label1", true).FirstOrDefault();
		if (label1 != null) label1.Text = Strings.ControllerBatch_LabelChange;
		var label2 = Controls.Find("label2", true).FirstOrDefault();
		if (label2 != null) label2.Text = Strings.ControllerBatch_LabelTo;
		var btnOkay = Controls.Find("btnOkay", true).FirstOrDefault() as Button;
		if (btnOkay != null) btnOkay.Text = Strings.ControllerBatch_BtnOkay;
		var btnCancel = Controls.Find("btnCancel", true).FirstOrDefault() as Button;
		if (btnCancel != null) btnCancel.Text = Strings.ControllerBatch_BtnCancel;
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
		this.listFrom = new System.Windows.Forms.ComboBox();
		this.listTo = new System.Windows.Forms.ComboBox();
		System.Windows.Forms.Label label = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label2 = new System.Windows.Forms.Label();
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		System.Windows.Forms.Button button2 = new System.Windows.Forms.Button();
		base.SuspendLayout();
		label.AutoSize = true;
		label.Location = new System.Drawing.Point(12, 15);
		label.Name = "label1";
		label.Size = new System.Drawing.Size(44, 13);
		label.TabIndex = 0;
		label.Text = "Change";
		this.listFrom.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.listFrom.FormattingEnabled = true;
		this.listFrom.Location = new System.Drawing.Point(63, 12);
		this.listFrom.Name = "listFrom";
		this.listFrom.Size = new System.Drawing.Size(121, 21);
		this.listFrom.TabIndex = 1;
		this.listTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.listTo.FormattingEnabled = true;
		this.listTo.Location = new System.Drawing.Point(63, 39);
		this.listTo.Name = "listTo";
		this.listTo.Size = new System.Drawing.Size(121, 21);
		this.listTo.TabIndex = 3;
		label2.AutoSize = true;
		label2.Location = new System.Drawing.Point(40, 42);
		label2.Name = "label2";
		label2.Size = new System.Drawing.Size(16, 13);
		label2.TabIndex = 2;
		label2.Text = "to";
		button.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		button.Location = new System.Drawing.Point(109, 66);
		button.Name = "btnCancel";
		button.Size = new System.Drawing.Size(75, 23);
		button.TabIndex = 4;
		button.Text = "Cancel";
		button.UseVisualStyleBackColor = true;
		button2.DialogResult = System.Windows.Forms.DialogResult.OK;
		button2.Location = new System.Drawing.Point(22, 66);
		button2.Name = "btnOkay";
		button2.Size = new System.Drawing.Size(75, 23);
		button2.TabIndex = 5;
		button2.Text = "Okay";
		button2.UseVisualStyleBackColor = true;
		base.AcceptButton = button2;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.CancelButton = button;
		base.ClientSize = new System.Drawing.Size(202, 105);
		base.ControlBox = false;
		base.Controls.Add(button2);
		base.Controls.Add(button);
		base.Controls.Add(this.listTo);
		base.Controls.Add(label2);
		base.Controls.Add(this.listFrom);
		base.Controls.Add(label);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "ControllerBatch";
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
