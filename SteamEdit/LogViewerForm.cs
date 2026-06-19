using System;
using System.IO;
using System.Windows.Forms;
using Common;

namespace SteamEdit;

internal class LogViewerForm : Form
{
	private TextBox txtLog;

	public LogViewerForm()
	{
		InitializeComponent();
		I18n.LanguageChanged += OnLanguageChanged;
		ApplyLanguage();
		LoadLog();
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
		ApplyLanguage();
	}

	private void ApplyLanguage()
	{
		Text = Strings.LogViewer_Title;
	}

	protected override void Dispose(bool disposing)
	{
		I18n.LanguageChanged -= OnLanguageChanged;
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		txtLog = new TextBox();
		SuspendLayout();

		txtLog.Dock = DockStyle.Fill;
		txtLog.Multiline = true;
		txtLog.ReadOnly = true;
		txtLog.ScrollBars = ScrollBars.Vertical;
		txtLog.Font = new System.Drawing.Font("Consolas", 9F);
		txtLog.WordWrap = false;

		AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
		AutoScaleMode = AutoScaleMode.Font;
		ClientSize = new System.Drawing.Size(600, 400);
		Controls.Add(txtLog);
		Name = "LogViewerForm";
		StartPosition = FormStartPosition.CenterParent;
		ShowIcon = false;
		ShowInTaskbar = false;
		ResumeLayout(false);
		PerformLayout();
	}

	private void LoadLog()
	{
		try
		{
			string logPath = Path.Combine(AppHelper.GetAppDirectory(), "log.txt");
			if (!File.Exists(logPath))
			{
				txtLog.Text = "Log file not found: " + logPath;
				return;
			}
			string[] lines = File.ReadAllLines(logPath);
			int start = Math.Max(0, lines.Length - 200);
			txtLog.Lines = new ArraySegment<string>(lines, start, lines.Length - start).ToArray();
			txtLog.SelectionStart = txtLog.TextLength;
			txtLog.ScrollToCaret();
		}
		catch (Exception ex)
		{
			txtLog.Text = "Failed to read log: " + ex.Message;
		}
	}
}
