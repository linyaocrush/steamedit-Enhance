#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Common;

namespace SteamEdit;

public class MessageBoxWithDetails : LocalizableForm
{
	private const string DetailsFormat = "Details {0}";

	private IContainer components = null!;

	private Button btnClose = null!;

	private Button btnCopy = null!;

	private Button btnExit = null!;

	private Label lblMessage = null!;

	private TextBox tbDetails = null!;

	private bool _exitRequested;

	public MessageBoxWithDetails(string message, string title, string? details = null, bool showExitButton = false)
	{
		InitializeComponent();
		lblMessage.Text = message;
		Text = title;
		if (details != null)
		{
			tbDetails.Text = details;
		}
		else
		{
			base.Height -= tbDetails.Height + 10;
		}
		btnExit.Visible = showExitButton;
		Log.WriteLine("-----------------");
		Log.WriteLine(title);
		Log.WriteLine(message);
		if (details != null)
		{
			Log.WriteLine("----");
			Log.WriteLine(details);
		}
		Log.WriteLine("-----------------");
	}

	public static bool ShowWithExit(string message, string title, string details)
	{
		var dialog = new MessageBoxWithDetails(message, title, details, showExitButton: true);
		dialog.ShowDialog();
		return dialog._exitRequested;
	}

	protected override void ApplyLanguage()
	{
		btnClose.Text = Strings.MessageBoxWithDetails_BtnClose;
		btnCopy.Text = Strings.MessageBoxWithDetails_BtnCopyClipboard;
		if (btnExit.Visible) btnExit.Text = Strings.CrashDialog_ExitButton;
	}

	public static void Show(string message, string title, string? details = null)
	{
		new MessageBoxWithDetails(message, title, details).ShowDialog();
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		int messageBoxHeight = lblMessage.Height;
		SetMessageBoxHeight(messageBoxHeight);
	}

	private void SetMessageBoxHeight(int heightChange)
	{
		base.Height += heightChange;
		if (base.Height < 150)
		{
			base.Height = 150;
		}
	}

	private void btnClose_Click(object? sender, EventArgs e)
	{
		Close();
	}

	private void btnCopy_Click(object? sender, EventArgs e)
	{
		Clipboard.SetText(tbDetails.Text);
	}

	private void btnExit_Click(object? sender, EventArgs e)
	{
		_exitRequested = true;
		Close();
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
		this.btnClose = new System.Windows.Forms.Button();
		this.btnCopy = new System.Windows.Forms.Button();
		this.btnExit = new System.Windows.Forms.Button();
		this.lblMessage = new System.Windows.Forms.Label();
		this.tbDetails = new System.Windows.Forms.TextBox();
		base.SuspendLayout();
		this.btnClose.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.btnClose.Location = new System.Drawing.Point(267, 73);
		this.btnClose.Name = "btnClose";
		this.btnClose.Size = new System.Drawing.Size(75, 23);
		this.btnClose.TabIndex = 0;
		this.btnClose.Text = "Close";
		this.btnClose.UseVisualStyleBackColor = true;
		this.btnClose.Click += new System.EventHandler(btnClose_Click);
		this.btnCopy.Location = new System.Drawing.Point(12, 73);
		this.btnCopy.Name = "btnCopy";
		this.btnCopy.Size = new System.Drawing.Size(128, 23);
		this.btnCopy.TabIndex = 4;
		this.btnCopy.Text = "Copy To Clipboard";
		this.btnCopy.UseVisualStyleBackColor = true;
		this.btnCopy.Click += new System.EventHandler(btnCopy_Click);
		this.btnExit.Location = new System.Drawing.Point(146, 73);
		this.btnExit.Name = "btnExit";
		this.btnExit.Size = new System.Drawing.Size(115, 23);
		this.btnExit.TabIndex = 7;
		this.btnExit.Text = "Exit";
		this.btnExit.UseVisualStyleBackColor = true;
		this.btnExit.Visible = false;
		this.btnExit.Click += new System.EventHandler(btnExit_Click);
		this.lblMessage.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblMessage.Location = new System.Drawing.Point(12, 9);
		this.lblMessage.Name = "lblMessage";
		this.lblMessage.Size = new System.Drawing.Size(330, 61);
		this.lblMessage.TabIndex = 5;
		this.lblMessage.Text = "label1";
		this.tbDetails.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.tbDetails.Location = new System.Drawing.Point(12, 104);
		this.tbDetails.Multiline = true;
		this.tbDetails.Name = "tbDetails";
		this.tbDetails.ReadOnly = true;
		this.tbDetails.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
		this.tbDetails.Size = new System.Drawing.Size(328, 100);
		this.tbDetails.TabIndex = 6;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(354, 216);
		base.Controls.Add(this.tbDetails);
		base.Controls.Add(this.lblMessage);
		base.Controls.Add(this.btnCopy);
		base.Controls.Add(this.btnExit);
		base.Controls.Add(this.btnClose);
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "MessageBoxWithDetails";
		base.ShowIcon = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		base.TopMost = true;
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
