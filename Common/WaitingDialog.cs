using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Common;

internal class WaitingDialog : Form
{
	public delegate bool WaitWhileDelegate();

	private bool _isWaiting;

	private IContainer components;

	private Label message;

	private ProgressBar progressBar1;

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

	public WaitingDialog()
		: this("")
	{
	}

	public WaitingDialog(string message)
	{
		InitializeComponent();
		Message = message;
	}

	public void ShowAndWait(IWin32Window parent, WaitWhileDelegate waitWhile)
	{
		Show(parent);
		_isWaiting = true;
		while (waitWhile())
		{
			Application.DoEvents();
			Thread.Sleep(10);
		}
		_isWaiting = false;
		Close();
	}

	public void ShowAndWait(WaitWhileDelegate waitWhile)
	{
		ShowAndWait(null, waitWhile);
	}

	private void WaitForUpdateThread_FormClosing(object sender, FormClosingEventArgs e)
	{
		if (_isWaiting)
		{
			e.Cancel = true;
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
		this.message = new System.Windows.Forms.Label();
		this.progressBar1 = new System.Windows.Forms.ProgressBar();
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
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.ClientSize = new System.Drawing.Size(383, 78);
		base.ControlBox = false;
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
