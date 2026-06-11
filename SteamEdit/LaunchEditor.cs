using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Common;
using SteamEdit.Data;

namespace SteamEdit;

internal class LaunchEditor : Form
{
	private string _baseDir;

	private string _gameName;

	private IContainer components;

	private FlowLayoutPanel flowLayout;

	private TextBox lblInstall;

	private Label lblInstallPath;

	public IEnumerable<LaunchItem> LaunchItems => from item in flowLayout.Controls.OfType<LaunchEditorItem>()
		select new LaunchItem
		{
			Label = item.Label,
			Executable = item.Executable,
			Arguments = item.Arguments,
			WorkingDir = item.WorkingDir
		};

	public LaunchEditor(IEnumerable<LaunchItem> items, string baseDir, string gameName)
	{
		InitializeComponent();
		I18n.LanguageChanged += OnLanguageChanged;
		ApplyLanguage();
		DoubleBuffered = true;
		_baseDir = baseDir;
		_gameName = gameName;
		lblInstall.Text = _baseDir;
		lblInstall.Select(0, 0);
		foreach (LaunchItem item in items)
		{
			AddItem(item);
		}
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
		ApplyLanguage();
	}

	private void ApplyLanguage()
	{
		Text = Strings.LaunchEditor_Title;
		lblInstallPath.Text = Strings.LaunchEditor_LabelInstallPath;
		var btnAdd = Controls.Find("btnAdd", true).FirstOrDefault() as Button;
		if (btnAdd != null) btnAdd.Text = Strings.LaunchEditor_BtnAddNew;
		var btnOK = Controls.Find("btnOK", true).FirstOrDefault() as Button;
		if (btnOK != null) btnOK.Text = Strings.LaunchEditor_BtnOK;
		var btnCancel = Controls.Find("btnCancel", true).FirstOrDefault() as Button;
		if (btnCancel != null) btnCancel.Text = Strings.LaunchEditor_BtnCancel;
	}

	private void AddItem(LaunchItem item)
	{
		LaunchEditorItem launchEditorItem = new LaunchEditorItem(item, _gameName, flowLayout.Controls.Count == 0);
		flowLayout.Controls.Add(launchEditorItem);
		launchEditorItem.OnDelete += uiItem_OnDelete;
		launchEditorItem.OnMoveDown += uiItem_OnMoveDown;
		launchEditorItem.OnMoveUp += uiItem_OnMoveUp;
		launchEditorItem.OnSelectWorkingDir += uiItem_OnSelectWorkingDir;
		launchEditorItem.OnSelectExecutable += uiItem_OnSelectExecutable;
		launchEditorItem.OnCopyLaunchInfo += uiItem_OnCopyLaunchInfo;
	}

	private void uiItem_OnCopyLaunchInfo(object sender, CopyLaunchInfoArgs e)
	{
		using SelectAppLaunch selectAppLaunch = new SelectAppLaunch();
		if (selectAppLaunch.ShowDialog(this) == DialogResult.OK)
		{
			LaunchItem launchInfo = selectAppLaunch.LaunchInfo;
			e.LaunchInfo = new LaunchItem
			{
				Label = launchInfo.Label,
				Arguments = launchInfo.Arguments,
				Executable = AppHelper.MakePathRelative(launchInfo.Executable, _baseDir),
				WorkingDir = AppHelper.MakePathRelative(launchInfo.WorkingDir, _baseDir)
			};
		}
	}

	private void uiItem_OnSelectExecutable(object sender, SelectLaunchExecutableArgs e)
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		string path = Path.Combine(_baseDir, e.Executable);
		openFileDialog.CheckFileExists = true;
		openFileDialog.CheckPathExists = true;
		openFileDialog.Multiselect = false;
		openFileDialog.Title = "Select Executable";
		openFileDialog.FileName = Path.GetFileName(path);
		openFileDialog.InitialDirectory = Path.GetDirectoryName(path);
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			e.Executable = AppHelper.MakePathRelative(openFileDialog.FileName, _baseDir);
			DialogResult dialogResult = MessageBox.Show(this, Strings.Msg_MakeItMatch, Strings.Msg_MakeItMatchTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
			e.MakeWorkingDirMatch = dialogResult == DialogResult.Yes;
		}
	}

	private void uiItem_OnSelectWorkingDir(object sender, SelectLaunchDirArgs e)
	{
		using FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
		string fullPath = Path.GetFullPath(Path.Combine(_baseDir, e.WorkingDir));
		folderBrowserDialog.ShowNewFolderButton = false;
		folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;
		folderBrowserDialog.SelectedPath = fullPath;
		if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
		{
			e.WorkingDir = AppHelper.MakePathRelative(folderBrowserDialog.SelectedPath, _baseDir);
		}
	}

	private void uiItem_OnMoveUp(object sender, EventArgs e)
	{
		LaunchEditorItem child = (LaunchEditorItem)sender;
		int num = flowLayout.Controls.GetChildIndex(child) - 1;
		if (num >= 0)
		{
			flowLayout.Controls.SetChildIndex(child, num);
		}
	}

	private void uiItem_OnMoveDown(object sender, EventArgs e)
	{
		LaunchEditorItem child = (LaunchEditorItem)sender;
		int num = flowLayout.Controls.GetChildIndex(child) + 1;
		if (num < flowLayout.Controls.Count)
		{
			flowLayout.Controls.SetChildIndex(child, num);
		}
	}

	private void uiItem_OnDelete(object sender, EventArgs e)
	{
		LaunchEditorItem launchEditorItem = (LaunchEditorItem)sender;
		launchEditorItem.OnDelete -= uiItem_OnDelete;
		launchEditorItem.OnMoveDown -= uiItem_OnMoveDown;
		launchEditorItem.OnMoveUp -= uiItem_OnMoveUp;
		launchEditorItem.OnSelectWorkingDir -= uiItem_OnSelectWorkingDir;
		launchEditorItem.OnSelectExecutable -= uiItem_OnSelectExecutable;
		launchEditorItem.OnCopyLaunchInfo -= uiItem_OnCopyLaunchInfo;
		flowLayout.Controls.Remove(launchEditorItem);
	}

	private void btnAdd_Click(object sender, EventArgs e)
	{
		AddItem(new LaunchItem());
	}

	private void flowLayout_Resize(object sender, EventArgs e)
	{
		foreach (LaunchEditorItem item in flowLayout.Controls.OfType<LaunchEditorItem>())
		{
			item.Width = item.Parent.ClientSize.Width - item.Left * 2;
		}
	}

	private void btnOK_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.OK;
		Close();
	}

	private void btnCancel_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.Cancel;
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
		this.flowLayout = new System.Windows.Forms.FlowLayoutPanel();
		this.lblInstall = new System.Windows.Forms.TextBox();
		this.lblInstallPath = new System.Windows.Forms.Label();
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		System.Windows.Forms.Button button2 = new System.Windows.Forms.Button();
		System.Windows.Forms.Button button3 = new System.Windows.Forms.Button();
		base.SuspendLayout();
		this.lblInstallPath.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		this.lblInstallPath.AutoSize = true;
		this.lblInstallPath.Location = new System.Drawing.Point(89, 336);
		this.lblInstallPath.Name = "label1";
		this.lblInstallPath.Size = new System.Drawing.Size(62, 13);
		this.lblInstallPath.TabIndex = 5;
		this.lblInstallPath.Text = "Install Path:";
		this.flowLayout.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.flowLayout.AutoScroll = true;
		this.flowLayout.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.flowLayout.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
		this.flowLayout.Location = new System.Drawing.Point(8, 8);
		this.flowLayout.Margin = new System.Windows.Forms.Padding(0);
		this.flowLayout.Name = "flowLayout";
		this.flowLayout.Size = new System.Drawing.Size(714, 318);
		this.flowLayout.TabIndex = 2;
		this.flowLayout.WrapContents = false;
		this.flowLayout.Resize += new System.EventHandler(flowLayout_Resize);
		button.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
		button.Location = new System.Drawing.Point(8, 332);
		button.Name = "btnAdd";
		button.Size = new System.Drawing.Size(75, 22);
		button.TabIndex = 2;
		button.Text = "Add New";
		button.UseVisualStyleBackColor = true;
		button.Click += new System.EventHandler(btnAdd_Click);
		button2.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		button2.DialogResult = System.Windows.Forms.DialogResult.OK;
		button2.Location = new System.Drawing.Point(648, 332);
		button2.Name = "btnOK";
		button2.Size = new System.Drawing.Size(75, 22);
		button2.TabIndex = 0;
		button2.Text = "OK";
		button2.UseVisualStyleBackColor = true;
		button2.Click += new System.EventHandler(btnOK_Click);
		button3.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
		button3.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		button3.Location = new System.Drawing.Point(566, 332);
		button3.Name = "btnCancel";
		button3.Size = new System.Drawing.Size(75, 22);
		button3.TabIndex = 1;
		button3.Text = "Cancel";
		button3.UseVisualStyleBackColor = true;
		button3.Click += new System.EventHandler(btnCancel_Click);
		this.lblInstall.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lblInstall.BackColor = System.Drawing.SystemColors.Control;
		this.lblInstall.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.lblInstall.Location = new System.Drawing.Point(156, 336);
		this.lblInstall.Margin = new System.Windows.Forms.Padding(2);
		this.lblInstall.Name = "lblInstall";
		this.lblInstall.ReadOnly = true;
		this.lblInstall.Size = new System.Drawing.Size(406, 13);
		this.lblInstall.TabIndex = 3;
		this.lblInstall.Text = "[Install Dir]";
		base.AcceptButton = button2;
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.CancelButton = button3;
		base.ClientSize = new System.Drawing.Size(734, 365);
		base.ControlBox = false;
		base.Controls.Add(this.lblInstall);
		base.Controls.Add(this.lblInstallPath);
		base.Controls.Add(button3);
		base.Controls.Add(button2);
		base.Controls.Add(button);
		base.Controls.Add(this.flowLayout);
		this.MinimumSize = new System.Drawing.Size(339, 199);
		base.Name = "LaunchEditor";
		this.Text = "LaunchEditor";
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
