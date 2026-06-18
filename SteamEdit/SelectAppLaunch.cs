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

internal class SelectAppLaunch : Form
{
	private IContainer components = null!;

	private ComboBox appSelection;

	private ComboBox launchSelection;

	private Button btnOK;

	private Label lblLaunchItem;

	public LaunchItem LaunchInfo { get; private set; }

	public SelectAppLaunch()
	{
		InitializeComponent();
		I18n.LanguageChanged += OnLanguageChanged;
		ApplyLanguage();
		List<IAppInfoEntry> list = new List<IAppInfoEntry>(SteamData.AppInfoData.Entries.Where((IAppInfoEntry ent) => ent.Type == AppInfoEntryType.Game));
		list.Sort((IAppInfoEntry a, IAppInfoEntry b) => StringTitleComparer.CompareStrings(a.ToString(), b.ToString()));
		appSelection.BeginUpdate();
		appSelection.Items.Clear();
		ComboBox.ObjectCollection items = appSelection.Items;
		object[] items2 = list.ToArray();
		items.AddRange(items2);
		appSelection.EndUpdate();
		Set(null, null);
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
		ApplyLanguage();
	}

	private void ApplyLanguage()
	{
		Text = Strings.SelectAppLaunch_Title;
		lblLaunchItem.Text = Strings.SelectAppLaunch_LabelLaunchItem;
		btnOK.Text = Strings.SelectAppLaunch_BtnOK;
		var btnCancel = Controls.Find("btnCancel", true).FirstOrDefault() as Button;
		if (btnCancel != null) btnCancel.Text = Strings.SelectAppLaunch_BtnCancel;
	}

	private void appSelection_SelectedIndexChanged(object sender, EventArgs e)
	{
		Setup();
	}

	private void Setup()
	{
		bool enabled = appSelection.SelectedItem != null;
		launchSelection.Enabled = enabled;
		btnOK.Enabled = false;
		launchSelection.Items.Clear();
		IAppInfoEntry appInfoEntry = (IAppInfoEntry)appSelection.SelectedItem;
		if (appInfoEntry != null)
		{
			ComboBox.ObjectCollection items = launchSelection.Items;
			object[] items2 = appInfoEntry.Launches.Items.Select((LaunchItem item) => $"{item.Label} : {item.Executable} {item.Arguments} {item.WorkingDir}").ToArray();
			items.AddRange(items2);
		}
		if (launchSelection.Items.Count > 0)
		{
			launchSelection.SelectedIndex = 0;
			Set(appInfoEntry, appInfoEntry.Launches.Items.First());
		}
	}

	private void launchSelection_SelectionChangeCommitted(object sender, EventArgs e)
	{
		IAppInfoEntry appInfoEntry = (IAppInfoEntry)appSelection.SelectedItem;
		int selectedIndex = launchSelection.SelectedIndex;
		Set(appInfoEntry, appInfoEntry.Launches.Items.ElementAtOrDefault(selectedIndex));
	}

	private void Set(IAppInfoEntry entry, LaunchItem item)
	{
		btnOK.Enabled = item != null;
		if (item == null)
		{
			LaunchInfo = null;
			return;
		}
		LaunchInfo = new LaunchItem
		{
			Label = item.Label,
			Executable = Path.GetFullPath(Path.Combine(entry.InstallPath, item.Executable)),
			Arguments = item.Arguments,
			WorkingDir = Path.GetFullPath(Path.Combine(entry.InstallPath, item.WorkingDir))
		};
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
		this.appSelection = new System.Windows.Forms.ComboBox();
		this.launchSelection = new System.Windows.Forms.ComboBox();
		this.btnOK = new System.Windows.Forms.Button();
		this.lblLaunchItem = new System.Windows.Forms.Label();
		System.Windows.Forms.Button button = new System.Windows.Forms.Button();
		base.SuspendLayout();
		this.lblLaunchItem.AutoSize = true;
		this.lblLaunchItem.Location = new System.Drawing.Point(12, 40);
		this.lblLaunchItem.Name = "label1";
		this.lblLaunchItem.Size = new System.Drawing.Size(69, 13);
		this.lblLaunchItem.TabIndex = 3;
		this.lblLaunchItem.Text = "Launch Item:";
		this.appSelection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.appSelection.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Append;
		this.appSelection.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
		this.appSelection.FormattingEnabled = true;
		this.appSelection.Location = new System.Drawing.Point(12, 12);
		this.appSelection.MaxDropDownItems = 16;
		this.appSelection.Name = "appSelection";
		this.appSelection.Size = new System.Drawing.Size(360, 21);
		this.appSelection.TabIndex = 1;
		this.appSelection.SelectedIndexChanged += new System.EventHandler(appSelection_SelectedIndexChanged);
		this.appSelection.SelectionChangeCommitted += new System.EventHandler(appSelection_SelectedIndexChanged);
		this.appSelection.TextUpdate += new System.EventHandler(appSelection_SelectedIndexChanged);
		this.launchSelection.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.launchSelection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.launchSelection.FormattingEnabled = true;
		this.launchSelection.Location = new System.Drawing.Point(87, 38);
		this.launchSelection.Name = "launchSelection";
		this.launchSelection.Size = new System.Drawing.Size(285, 21);
		this.launchSelection.TabIndex = 2;
		this.launchSelection.SelectionChangeCommitted += new System.EventHandler(launchSelection_SelectionChangeCommitted);
		this.btnOK.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
		this.btnOK.Location = new System.Drawing.Point(297, 64);
		this.btnOK.Name = "btnOK";
		this.btnOK.Size = new System.Drawing.Size(75, 22);
		this.btnOK.TabIndex = 4;
		this.btnOK.Text = "OK";
		this.btnOK.UseVisualStyleBackColor = true;
		button.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
		button.DialogResult = System.Windows.Forms.DialogResult.Cancel;
		button.Location = new System.Drawing.Point(216, 64);
		button.Name = "btnCancel";
		button.Size = new System.Drawing.Size(75, 22);
		button.TabIndex = 5;
		button.Text = "Cancel";
		button.UseVisualStyleBackColor = true;
		base.AcceptButton = this.btnOK;
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
		base.CancelButton = button;
		base.ClientSize = new System.Drawing.Size(384, 91);
		base.ControlBox = false;
		base.Controls.Add(button);
		base.Controls.Add(this.btnOK);
		base.Controls.Add(this.lblLaunchItem);
		base.Controls.Add(this.launchSelection);
		base.Controls.Add(this.appSelection);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
		base.MaximizeBox = false;
		this.MaximumSize = new System.Drawing.Size(8995, 130);
		base.MinimizeBox = false;
		this.MinimumSize = new System.Drawing.Size(258, 130);
		base.Name = "SelectAppLaunch";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "Select Launch Info";
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
