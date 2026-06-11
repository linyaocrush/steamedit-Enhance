using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SteamEdit.Data;
using SteamEdit.Properties;

namespace SteamEdit;

internal class LaunchEditorItem : UserControl
{
	private string _gameName;

	private bool _isFirst;

	private Control _oldParent;

	private IContainer components;

	private TextBox txtExec;

	private TextBox txtLabel;

	private TextBox txtArguments;

	private TextBox txtWorkingDir;

	public string Label => txtLabel.Text;

	public string Executable => txtExec.Text;

	public string Arguments => txtArguments.Text;

	public string WorkingDir => txtWorkingDir.Text;

	public event EventHandler OnMoveUp;

	public event EventHandler OnMoveDown;

	public event EventHandler OnDelete;

	public event EventHandler<SelectLaunchExecutableArgs> OnSelectExecutable;

	public event EventHandler<SelectLaunchDirArgs> OnSelectWorkingDir;

	public event EventHandler<CopyLaunchInfoArgs> OnCopyLaunchInfo;

	public LaunchEditorItem(LaunchItem info, string gameName, bool isFirst)
	{
		_gameName = gameName;
		_isFirst = isFirst;
		InitializeComponent();
		Set(info);
	}

	private void Set(LaunchItem info)
	{
		if (_isFirst)
		{
			txtLabel.Text = "Play " + _gameName;
			txtLabel.Enabled = false;
		}
		else
		{
			txtLabel.Text = info.Label;
			txtLabel.Enabled = true;
		}
		txtExec.Text = info.Executable;
		txtArguments.Text = info.Arguments;
		txtWorkingDir.Text = info.WorkingDir;
	}

	private void btnMoveUp_Click(object sender, EventArgs e)
	{
		this.OnMoveUp?.Invoke(this, new EventArgs());
	}

	private void btnDown_Click(object sender, EventArgs e)
	{
		this.OnMoveDown?.Invoke(this, new EventArgs());
	}

	private void btnDelete_Click(object sender, EventArgs e)
	{
		this.OnDelete?.Invoke(this, new EventArgs());
	}

	private void LaunchEditorItem_ParentChanged(object sender, EventArgs e)
	{
		if (_oldParent != null)
		{
			_oldParent.Resize -= Parent_Resize;
		}
		if (base.Parent != null)
		{
			base.Parent.Resize += Parent_Resize;
			FixSize();
		}
		_oldParent = base.Parent;
	}

	private void Parent_Resize(object sender, EventArgs e)
	{
		FixSize();
	}

	private void FixSize()
	{
		base.Width = base.Parent.ClientSize.Width - base.Left * 2;
	}

	private void panel1_Paint(object sender, PaintEventArgs e)
	{
		e.Graphics.DrawRectangle(new Pen(SystemColors.ControlLight), e.ClipRectangle.Left, e.ClipRectangle.Top, e.ClipRectangle.Width - 1, e.ClipRectangle.Height - 1);
	}

	private void btnSelectExec_Click(object sender, EventArgs e)
	{
		if (this.OnSelectExecutable != null)
		{
			SelectLaunchExecutableArgs selectLaunchExecutableArgs = new SelectLaunchExecutableArgs
			{
				Executable = txtExec.Text
			};
			this.OnSelectExecutable(this, selectLaunchExecutableArgs);
			txtExec.Text = selectLaunchExecutableArgs.Executable;
			if (selectLaunchExecutableArgs.MakeWorkingDirMatch)
			{
				txtWorkingDir.Text = Path.GetDirectoryName(selectLaunchExecutableArgs.Executable);
			}
		}
	}

	private void btnSelectDir_Click(object sender, EventArgs e)
	{
		if (this.OnSelectWorkingDir != null)
		{
			SelectLaunchDirArgs selectLaunchDirArgs = new SelectLaunchDirArgs
			{
				WorkingDir = txtWorkingDir.Text
			};
			this.OnSelectWorkingDir(this, selectLaunchDirArgs);
			txtWorkingDir.Text = selectLaunchDirArgs.WorkingDir;
		}
	}

	private void btnCopy_Click(object sender, EventArgs e)
	{
		if (this.OnCopyLaunchInfo != null)
		{
			CopyLaunchInfoArgs copyLaunchInfoArgs = new CopyLaunchInfoArgs();
			this.OnCopyLaunchInfo(this, copyLaunchInfoArgs);
			if (copyLaunchInfoArgs.LaunchInfo != null)
			{
				Set(copyLaunchInfoArgs.LaunchInfo);
			}
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
		this.txtWorkingDir = new System.Windows.Forms.TextBox();
		this.txtArguments = new System.Windows.Forms.TextBox();
		this.txtExec = new System.Windows.Forms.TextBox();
		this.txtLabel = new System.Windows.Forms.TextBox();
		System.Windows.Forms.Label label = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label2 = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label3 = new System.Windows.Forms.Label();
		System.Windows.Forms.Label label4 = new System.Windows.Forms.Label();
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		System.Windows.Forms.Button button2 = new System.Windows.Forms.Button();
		System.Windows.Forms.Button button3 = new System.Windows.Forms.Button();
		System.Windows.Forms.Button button4 = new System.Windows.Forms.Button();
		System.Windows.Forms.Button button5 = new System.Windows.Forms.Button();
		System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel();
		System.Windows.Forms.Button button6 = new System.Windows.Forms.Button();
		panel.SuspendLayout();
		base.SuspendLayout();
		label.Location = new System.Drawing.Point(6, 36);
		label.Name = "label1";
		label.Size = new System.Drawing.Size(77, 19);
		label.TabIndex = 2;
		label.Text = "Executable:";
		label.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		label2.Location = new System.Drawing.Point(3, 8);
		label2.Name = "label2";
		label2.Size = new System.Drawing.Size(80, 19);
		label2.TabIndex = 3;
		label2.Text = "Label:";
		label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.txtWorkingDir.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.txtWorkingDir.Font = new System.Drawing.Font("Microsoft Sans Serif", 16f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, 0);
		this.txtWorkingDir.Location = new System.Drawing.Point(89, 92);
		this.txtWorkingDir.Name = "txtWorkingDir";
		this.txtWorkingDir.Size = new System.Drawing.Size(356, 26);
		this.txtWorkingDir.TabIndex = 9;
		label3.Location = new System.Drawing.Point(6, 94);
		label3.Name = "label4";
		label3.Size = new System.Drawing.Size(77, 22);
		label3.TabIndex = 8;
		label3.Text = "Working Dir:";
		label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.txtArguments.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.txtArguments.Font = new System.Drawing.Font("Microsoft Sans Serif", 16f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, 0);
		this.txtArguments.Location = new System.Drawing.Point(89, 62);
		this.txtArguments.Name = "txtArguments";
		this.txtArguments.Size = new System.Drawing.Size(384, 26);
		this.txtArguments.TabIndex = 7;
		label4.Location = new System.Drawing.Point(6, 64);
		label4.Name = "label3";
		label4.Size = new System.Drawing.Size(77, 22);
		label4.TabIndex = 6;
		label4.Text = "Arguments:";
		label4.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
		this.txtExec.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.txtExec.Font = new System.Drawing.Font("Microsoft Sans Serif", 16f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, 0);
		this.txtExec.Location = new System.Drawing.Point(89, 34);
		this.txtExec.Name = "txtExec";
		this.txtExec.Size = new System.Drawing.Size(356, 26);
		this.txtExec.TabIndex = 5;
		this.txtLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.txtLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 16f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel, 0);
		this.txtLabel.Location = new System.Drawing.Point(89, 5);
		this.txtLabel.Name = "txtLabel";
		this.txtLabel.Size = new System.Drawing.Size(384, 26);
		this.txtLabel.TabIndex = 4;
		button.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		button.Location = new System.Drawing.Point(491, 18);
		button.Name = "btnMoveUp";
		button.Size = new System.Drawing.Size(51, 22);
		button.TabIndex = 0;
		button.Text = "Up";
		button.UseVisualStyleBackColor = true;
		button.Click += new System.EventHandler(btnMoveUp_Click);
		button2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		button2.Location = new System.Drawing.Point(491, 95);
		button2.Name = "btnDown";
		button2.Size = new System.Drawing.Size(51, 22);
		button2.TabIndex = 1;
		button2.Text = "Down";
		button2.UseVisualStyleBackColor = true;
		button2.Click += new System.EventHandler(btnDown_Click);
		button3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		button3.Location = new System.Drawing.Point(491, 69);
		button3.Name = "btnDelete";
		button3.Size = new System.Drawing.Size(51, 22);
		button3.TabIndex = 2;
		button3.Text = "Delete";
		button3.UseVisualStyleBackColor = true;
		button3.Click += new System.EventHandler(btnDelete_Click);
		button4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		button4.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
		button4.Image = SteamEdit.Properties.Resources.openfolderHS;
		button4.Location = new System.Drawing.Point(448, 92);
		button4.Margin = new System.Windows.Forms.Padding(0);
		button4.Name = "btnSelectDir";
		button4.Size = new System.Drawing.Size(26, 25);
		button4.TabIndex = 11;
		button4.UseVisualStyleBackColor = true;
		button4.Click += new System.EventHandler(btnSelectDir_Click);
		button5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		button5.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
		button5.Image = SteamEdit.Properties.Resources.OpenFile;
		button5.Location = new System.Drawing.Point(448, 34);
		button5.Margin = new System.Windows.Forms.Padding(0);
		button5.Name = "btnSelectExec";
		button5.Size = new System.Drawing.Size(26, 25);
		button5.TabIndex = 10;
		button5.UseVisualStyleBackColor = true;
		button5.Click += new System.EventHandler(btnSelectExec_Click);
		panel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		panel.Controls.Add(button4);
		panel.Controls.Add(label2);
		panel.Controls.Add(button5);
		panel.Controls.Add(label);
		panel.Controls.Add(this.txtWorkingDir);
		panel.Controls.Add(this.txtLabel);
		panel.Controls.Add(label3);
		panel.Controls.Add(this.txtExec);
		panel.Controls.Add(this.txtArguments);
		panel.Controls.Add(label4);
		panel.ForeColor = System.Drawing.SystemColors.ControlText;
		panel.Location = new System.Drawing.Point(4, 4);
		panel.Margin = new System.Windows.Forms.Padding(4);
		panel.Name = "panel1";
		panel.Padding = new System.Windows.Forms.Padding(4);
		panel.Size = new System.Drawing.Size(480, 124);
		panel.TabIndex = 3;
		panel.Paint += new System.Windows.Forms.PaintEventHandler(panel1_Paint);
		button6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		button6.Location = new System.Drawing.Point(491, 44);
		button6.Name = "btnCopy";
		button6.Size = new System.Drawing.Size(51, 22);
		button6.TabIndex = 4;
		button6.Text = "Copy...";
		button6.UseVisualStyleBackColor = true;
		button6.Click += new System.EventHandler(btnCopy_Click);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.Controls.Add(button6);
		base.Controls.Add(panel);
		base.Controls.Add(button3);
		base.Controls.Add(button);
		base.Controls.Add(button2);
		base.Name = "LaunchEditorItem";
		base.Size = new System.Drawing.Size(545, 132);
		base.ParentChanged += new System.EventHandler(LaunchEditorItem_ParentChanged);
		panel.ResumeLayout(false);
		panel.PerformLayout();
		base.ResumeLayout(false);
	}
}
