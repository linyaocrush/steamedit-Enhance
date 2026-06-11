using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SteamEdit.Properties;

namespace SteamEdit;

public class About : Form
{
	private IContainer components;

	private Label lblVersion;

	private LinkLabel linkWebsite;

	public About()
	{
		InitializeComponent();
		I18n.LanguageChanged += OnLanguageChanged;
		ApplyLanguage();
		linkWebsite.Text = Resources.WebsiteUrl;
		lblVersion.Text = "v" + Application.ProductVersion;
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
		ApplyLanguage();
	}

	private void ApplyLanguage()
	{
		Text = Strings.About_Title;
		var panel = Controls.Find("panel1", true).FirstOrDefault() as Panel;
		if (panel != null)
		{
			var lbl = panel.Controls.Find("label1", true).FirstOrDefault();
			if (lbl != null) lbl.Text = Strings.About_AppName;
			var lbl2 = panel.Controls.Find("label2", true).FirstOrDefault();
			if (lbl2 != null) lbl2.Text = Strings.About_LabelAuthor;
			var lbl3 = panel.Controls.Find("label3", true).FirstOrDefault();
			if (lbl3 != null) lbl3.Text = Strings.About_LabelWebsite;
			var btnClose = panel.Controls.Find("btnClose", true).FirstOrDefault() as Button;
			if (btnClose != null) btnClose.Text = Strings.About_BtnClose;
			var linkRN = panel.Controls.Find("linkReleaseNotes", true).FirstOrDefault() as LinkLabel;
			if (linkRN != null) linkRN.Text = Strings.About_ReleaseNotes;
		}
	}

	private void linkWebsite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		Process.Start(Resources.WebsiteUrl);
	}

	private void linkEmail_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		Process.Start("mailto:tim@tg-software.com?subject=[SteamEdit]");
	}

	private void linkReleaseNotes_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
	{
		ReleaseNotesView releaseNotesView = new ReleaseNotesView();
		if (releaseNotesView.FillNotes(""))
		{
			releaseNotesView.ShowDialog(this);
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
		this.lblVersion = new System.Windows.Forms.Label();
		this.linkWebsite = new System.Windows.Forms.LinkLabel();
		System.Windows.Forms.PictureBox pictureBox = new System.Windows.Forms.PictureBox();
		System.Windows.Forms.Label label = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label2 = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label3 = new System.Windows.Forms.Label();
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
		System.Windows.Forms.LinkLabel linkLabel = new System.Windows.Forms.LinkLabel();
		System.Windows.Forms.LinkLabel linkLabel2 = new System.Windows.Forms.LinkLabel();
		((System.ComponentModel.ISupportInitialize)pictureBox).BeginInit();
		panel.SuspendLayout();
		base.SuspendLayout();
		pictureBox.Image = SteamEdit.Properties.Resources.logo1;
		pictureBox.InitialImage = null;
		pictureBox.Location = new System.Drawing.Point(5, 5);
		pictureBox.Margin = new System.Windows.Forms.Padding(2);
		pictureBox.Name = "pictureBox1";
		pictureBox.Size = new System.Drawing.Size(64, 64);
		pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
		pictureBox.TabIndex = 14;
		pictureBox.TabStop = false;
		label.AutoSize = true;
		label.Location = new System.Drawing.Point(44, 102);
		label.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
		label.Name = "label3";
		label.Size = new System.Drawing.Size(49, 13);
		label.TabIndex = 12;
		label.Text = "Website:";
		label2.AutoSize = true;
		label2.Location = new System.Drawing.Point(52, 85);
		label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
		label2.Name = "label2";
		label2.Size = new System.Drawing.Size(41, 13);
		label2.TabIndex = 11;
		label2.Text = "Author:";
		label3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		label3.Location = new System.Drawing.Point(72, 5);
		label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
		label3.Name = "label1";
		label3.Size = new System.Drawing.Size(271, 64);
		label3.TabIndex = 9;
		label3.Text = "SteamEdit";
		label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
		button.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		button.Location = new System.Drawing.Point(131, 125);
		button.Margin = new System.Windows.Forms.Padding(2);
		button.Name = "btnClose";
		button.Size = new System.Drawing.Size(101, 23);
		button.TabIndex = 8;
		button.Text = "Close";
		button.UseVisualStyleBackColor = true;
		panel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
		panel.Controls.Add(linkLabel);
		panel.Controls.Add(this.lblVersion);
		panel.Controls.Add(pictureBox);
		panel.Controls.Add(linkLabel2);
		panel.Controls.Add(label);
		panel.Controls.Add(label2);
		panel.Controls.Add(this.linkWebsite);
		panel.Controls.Add(label3);
		panel.Controls.Add(button);
		panel.Dock = System.Windows.Forms.DockStyle.Fill;
		panel.Location = new System.Drawing.Point(0, 0);
		panel.Margin = new System.Windows.Forms.Padding(2);
		panel.Name = "panel1";
		panel.Size = new System.Drawing.Size(352, 157);
		panel.TabIndex = 8;
		linkLabel.AutoSize = true;
		linkLabel.Location = new System.Drawing.Point(265, 56);
		linkLabel.Name = "linkReleaseNotes";
		linkLabel.Size = new System.Drawing.Size(77, 13);
		linkLabel.TabIndex = 16;
		linkLabel.TabStop = true;
		linkLabel.Text = "Release Notes";
		linkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkReleaseNotes_LinkClicked);
		this.lblVersion.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.lblVersion.Location = new System.Drawing.Point(234, 5);
		this.lblVersion.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
		this.lblVersion.Name = "lblVersion";
		this.lblVersion.Size = new System.Drawing.Size(113, 64);
		this.lblVersion.TabIndex = 15;
		this.lblVersion.Text = "[ver]";
		this.lblVersion.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		linkLabel2.AutoSize = true;
		linkLabel2.Location = new System.Drawing.Point(128, 85);
		linkLabel2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
		linkLabel2.Name = "linkEmail";
		linkLabel2.Size = new System.Drawing.Size(56, 13);
		linkLabel2.TabIndex = 13;
		linkLabel2.TabStop = true;
		linkLabel2.Text = "Tim Green";
		linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkEmail_LinkClicked);
		this.linkWebsite.AutoSize = true;
		this.linkWebsite.Location = new System.Drawing.Point(128, 102);
		this.linkWebsite.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
		this.linkWebsite.Name = "linkWebsite";
		this.linkWebsite.Size = new System.Drawing.Size(24, 13);
		this.linkWebsite.TabIndex = 10;
		this.linkWebsite.TabStop = true;
		this.linkWebsite.Text = "[url]";
		this.linkWebsite.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(linkWebsite_LinkClicked);
		base.AcceptButton = button;
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.CancelButton = button;
		base.ClientSize = new System.Drawing.Size(352, 157);
		base.ControlBox = false;
		base.Controls.Add(panel);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.Margin = new System.Windows.Forms.Padding(2);
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "About";
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		((System.ComponentModel.ISupportInitialize)pictureBox).EndInit();
		panel.ResumeLayout(false);
		panel.PerformLayout();
		base.ResumeLayout(false);
	}
}
