#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace steamedit_Enhance;

internal class WaitingDialog : LocalizableForm
{
	public delegate bool WaitWhileDelegate();

	private bool _isWaiting;
	private bool _cancelled;
	private bool _pumpingMessages;

	private IContainer components = null!;

	private Label message = null!;

	private ProgressBar progressBar1 = null!;

	private Button btnCancel = null!;

	public string Message
	{
		get
		{
			return message.Text;
		}
		set
		{
			message.Text = value;
		}
	}

	public bool WasCancelled => _cancelled;

	private static readonly TimeSpan CancelTimeout = TimeSpan.FromSeconds(30);

	public WaitingDialog()
		: this(Strings.WaitingDialog_Waiting)
	{
	}

	public WaitingDialog(string message)
	{
		InitializeComponent();
			ThemeManager.StyleDialog(this);
		Message = message;
	}

	protected override void ApplyLanguage()
	{
		Text = Strings.WaitingDialog_Title;
		btnCancel.Text = Strings.WaitingDialog_Cancel;
	}

	public bool ShowAndWait(IWin32Window parent, WaitWhileDelegate waitWhile)
	{
		_cancelled = false;
		Show(parent);
		_isWaiting = true;
		DateTime deadline = DateTime.UtcNow + CancelTimeout;
		while (waitWhile())
		{
			// DoEvents is acceptable here: modal dialog with only a Cancel button,
			// main form is blocked, re-entrancy risk is negligible.
			if (!_pumpingMessages)
			{
				_pumpingMessages = true;
				try { Application.DoEvents(); }
				finally { _pumpingMessages = false; }
			}
			Thread.Sleep(10);
			if (_cancelled || DateTime.UtcNow >= deadline)
			{
				_cancelled = true;
				break;
			}
		}
		_isWaiting = false;
		Close();
		return !_cancelled;
	}

	public bool ShowAndWait(WaitWhileDelegate waitWhile)
	{
		return ShowAndWait(null!, waitWhile);
	}

	private void WaitForUpdateThread_FormClosing(object? sender, FormClosingEventArgs e)
	{
		if (_isWaiting && !_cancelled)
		{
			e.Cancel = true;
		}
	}

	private void OnCancelClick(object? sender, EventArgs e)
	{
		_cancelled = true;
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
		this.message = new System.Windows.Forms.Label();
		this.progressBar1 = new System.Windows.Forms.ProgressBar();
		this.btnCancel = new System.Windows.Forms.Button();
		base.SuspendLayout();
		this.message.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.message.Font = new System.Drawing.Font("Microsoft Sans Serif", 10f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.message.Location = new System.Drawing.Point(12, 8);
		this.message.Name = "message";
		this.message.Size = new System.Drawing.Size(365, 42);
		this.message.TabIndex = 0;
		this.message.Text = "[Waiting...]";
		this.message.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		this.progressBar1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.progressBar1.Location = new System.Drawing.Point(12, 54);
		this.progressBar1.Maximum = 1000;
		this.progressBar1.Name = "progressBar1";
		this.progressBar1.Size = new System.Drawing.Size(365, 22);
		this.progressBar1.Step = 1;
		this.progressBar1.Style = System.Windows.Forms.ProgressBarStyle.Marquee;
		this.progressBar1.TabIndex = 1;
		this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top;
		this.btnCancel.Location = new System.Drawing.Point(157, 82);
		this.btnCancel.Name = "btnCancel";
		this.btnCancel.Size = new System.Drawing.Size(75, 25);
		this.btnCancel.TabIndex = 2;
		this.btnCancel.Text = "Cancel";
		this.btnCancel.UseVisualStyleBackColor = true;
		this.btnCancel.Click += new System.EventHandler(OnCancelClick);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.ClientSize = new System.Drawing.Size(383, 112);
		base.ControlBox = false;
		base.Controls.Add(this.btnCancel);
		base.Controls.Add(this.progressBar1);
		base.Controls.Add(this.message);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "WaitingDialog";
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Please Wait...";
		base.TopMost = true;
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(WaitForUpdateThread_FormClosing);
		base.ResumeLayout(false);
	}
}
