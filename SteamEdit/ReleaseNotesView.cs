using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Common;

namespace SteamEdit;

public class ReleaseNotesView : Form
{
	private IContainer components;

	private TextBox textView;

	public string ShownVersion { get; private set; }

	public ReleaseNotesView()
	{
		InitializeComponent();
		ShownVersion = "";
	}

	public bool FillNotes(string sinceVersion)
	{
		bool result = false;
		ShownVersion = "";
		try
		{
			string[] array = File.ReadAllLines(Path.Combine(AppHelper.GetAppDirectory(), "ReleaseNotes.txt"));
			if (array == null || array.Length == 0)
			{
				Close();
				return false;
			}
			StringBuilder stringBuilder = new StringBuilder();
			string text = null;
			for (int i = 0; i < array.Length; i++)
			{
				string text2 = array[i];
				if (text2.StartsWith("v"))
				{
					text = text2.Substring(1, text2.IndexOf(' ') - 1);
					if (ShownVersion.Length == 0)
					{
						ShownVersion = text;
					}
					if (text == sinceVersion)
					{
						break;
					}
				}
				if (text != null)
				{
					result = true;
					stringBuilder.AppendLine(array[i] + "\n");
				}
			}
			textView.Text = stringBuilder.ToString();
		}
		catch (Exception ex)
		{
			Log.WriteLine("Exception while reading release notes:\n{}", ex.ToString());
			Close();
		}
		return result;
	}

	private void ReleaseNotesView_Load(object sender, EventArgs e)
	{
		textView.SelectionStart = textView.Text.Length;
		textView.DeselectAll();
	}

	private void ReleaseNotesView_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape)
		{
			Close();
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
		this.textView = new System.Windows.Forms.TextBox();
		base.SuspendLayout();
		this.textView.Dock = System.Windows.Forms.DockStyle.Fill;
		this.textView.Font = new System.Drawing.Font("Arial", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.textView.Location = new System.Drawing.Point(0, 0);
		this.textView.Multiline = true;
		this.textView.Name = "textView";
		this.textView.ReadOnly = true;
		this.textView.ScrollBars = System.Windows.Forms.ScrollBars.Both;
		this.textView.Size = new System.Drawing.Size(630, 323);
		this.textView.TabIndex = 0;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 13f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(630, 323);
		base.Controls.Add(this.textView);
		base.KeyPreview = true;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "ReleaseNotesView";
		base.ShowIcon = false;
		base.ShowInTaskbar = false;
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "Release Notes";
		base.Load += new System.EventHandler(ReleaseNotesView_Load);
		base.KeyDown += new System.Windows.Forms.KeyEventHandler(ReleaseNotesView_KeyDown);
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
