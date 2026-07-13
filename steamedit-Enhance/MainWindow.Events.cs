#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.Properties;

namespace steamedit_Enhance;

partial class MainWindow
{
	private void RegisterShortcuts()
	{
		_shortcutHandler.Register((Keys.S, true, false), () => _mainViewModel.SaveCommand.Execute(null));
		_shortcutHandler.Register((Keys.Z, true, false), () => _mainViewModel.UndoCommand.Execute(null));
		_shortcutHandler.Register((Keys.C, true, false), () =>
		{
			if (appFilterText.Focused) return;
			var entry = _listManager.GetSelectedEntry();
			if (entry != null) try { Clipboard.SetText(entry.Name); } catch (System.Runtime.InteropServices.ExternalException) { }
		});
		_shortcutHandler.Register((Keys.F, true, false), () =>
		{
			if (appFilterText.Focused) return;
			appFilterText.Focus();
			appFilterText.SelectAll();
		});
		_shortcutHandler.Register((Keys.Enter, true, true), () => _mainViewModel.SaveApplyRestartCommand.Execute(null));
		_shortcutHandler.Register((Keys.Enter, true, false), () => _mainViewModel.SaveApplyCommand.Execute(null));
		_shortcutHandler.Register((Keys.T, true, false), () => _mainViewModel.TranslateCommand.Execute(null));
		_shortcutHandler.Register((Keys.B, true, false), () => _mainViewModel.BatchTranslateCommand.Execute(null));
		_shortcutHandler.Register((Keys.R, true, false), () => _mainViewModel.RevertCommand.Execute(null));
	}

	// --- Dialog handlers ---

	private void OnBtnEditLaunch_Click(object? sender, EventArgs e)
	{
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		using LaunchEditor launchEditor = new LaunchEditor(_steamData.DataStore, entry.Launches.Items, entry.InstallPath, entry.Name);
		if (launchEditor.ShowDialog(this) == DialogResult.OK)
			entry.Launches = new LaunchTable(launchEditor.LaunchItems);
	}

	private void OnBtnSettings_Click(object? sender, EventArgs e)
	{
		using var dlg = new SettingsDlg();
		ThemeManager.StyleDialog(dlg);
		dlg.ShowDialog(this);
		_steamData.UseCompleted = Settings.Default.EnableCompleted;
		_steamData.UseHidden = Settings.Default.EnableHidden;
	}

	private void OnBtnRevert_Click(object? sender, EventArgs e)
	{
		_editController.RevertEntry(_listManager.GetSelectedEntry());
	}

	private void OnBtnSearch_Click(object? sender, EventArgs e)
	{
		var entry = _listManager.GetSelectedEntry();
		if (entry == null) return;
		string gameName = entry.Name;
		if (string.IsNullOrWhiteSpace(gameName)) return;
		string query = Uri.EscapeDataString($"{gameName} {EnumMappings.GetSearchDisplayName(Settings.Default.AiTargetLanguage)}");
		string baseUrl = EnumMappings.GetSearchEngineUrl(Settings.Default.SearchEngine);
		string url = baseUrl + query;
		Log.WriteLine("Search: Opening {0}", url);
		try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose(); }
		catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { MessageBox.Show(this, ex.Message, Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error); }
	}

	private async void OnBtnTranslate_Click(object? sender, EventArgs e)
	{
		try
		{
			if (IsDisposed) return;
			var entry = _listManager.GetSelectedEntry();
			if (entry == null) return;
			string currentName = entry.Name;
			if (string.IsNullOrWhiteSpace(currentName)) return;

			btnTranslate.Enabled = false;
			btnTranslate.Text = Strings.Msg_Translating;

			string originalName = entry.Modification?.GetOriginalName() ?? currentName;
			string? existingRef = (originalName != currentName) ? currentName : null;
			string targetLang = Settings.Default.AiTargetLanguage;

			Log.WriteLine("AI Translate: Starting for \"{0}\" (original: \"{1}\"{2})",
				currentName, originalName,
				existingRef != null ? ", existing translation available: \"" + existingRef + "\"" : "");
			string? context = AiTranslator.BuildContext(
				entry.GetPropertyValue<string>(null!, AppInfoPaths.Developers),
				entry.GetPropertyValue<string>(null!, AppInfoPaths.Publishers),
				entry.GetPropertyValue<string>(null!, AppInfoPaths.Type));
			string translated = await AiTranslator.TranslateWithContextAsync(
				originalName, targetLang, context,
				Settings.Default.AiCustomSystemPrompt,
				Settings.Default.AiCustomUserMessage,
				existingTranslation: existingRef);
			Log.WriteLine("AI Translate: Got result \"{0}\"", translated);

			if (!string.IsNullOrWhiteSpace(translated) && IsHandleCreated && !IsDisposed)
			{
				var snapshotEntry = entry;
				BeginInvoke(() =>
				{
					try
					{
						if (snapshotEntry == null || _editController.ViewModel.CurrentEntry != snapshotEntry)
							return;
						string previousName = snapshotEntry.Name;
						_editController.ApplyTranslation(snapshotEntry, translated, previousName);
						Log.WriteLine("AI Translate: UI updated to \"{0}\"", translated);
					}
					catch (Exception ex)
					{
						Log.WriteLine("AI Translate: BeginInvoke error: {0}", ex);
					}
				});
			}
			else if (string.IsNullOrWhiteSpace(translated))
			{
				Log.WriteLine("AI Translate: Result was empty, not updating");
			}
		}
		catch (Exception ex) when (!IsDisposed)
		{
			Log.WriteLine("AI Translate: Exception: {0}", ex);
			if (IsHandleCreated)
				MessageBox.Show(this, string.Format(Strings.Msg_TranslateFailed, ex.Message), Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		catch (Exception)
		{
		}
		finally
		{
			if (!IsDisposed)
			{
				btnTranslate.Text = Strings.MainWindow_BtnTranslate;
				btnTranslate.Enabled = true;
			}
		}
	}

	// --- Batch operation handlers ---

	private void OnBatchRename(object? sender, EventArgs e)
	{
		var entries = _listManager.GetSelectedEntries();
		if (entries.Count == 0) return;
		using var dlg = new BatchRenameDialog(entries.Count);
		if (dlg.ShowDialog(this) != DialogResult.OK) return;
		string prefix = dlg.Prefix;
		string suffix = dlg.Suffix;
		if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix)) return;

		var toEdit = new List<IAppInfoEntry>(entries.Count);
		var newNames = new List<string>(entries.Count);
		foreach (var entry in entries)
		{
			string newName = prefix + entry.Name + suffix;
			if (newName == entry.Name) continue;
			toEdit.Add(entry);
			newNames.Add(newName);
		}
		if (toEdit.Count == 0) return;

		int idx = 0;
		_editController.ApplyBatchPropertyChange(toEdit,
			PropertyType.String,
			e => e.Name,
			e => e.Name = newNames[idx++],
			AppInfoPaths.Name);
	}

	private void OnBatchChangeType(object? sender, EventArgs e)
	{
		var entries = _listManager.GetSelectedEntries();
		if (entries.Count == 0) return;
		using var dlg = new BatchChangeTypeDialog();
		if (dlg.ShowDialog(this) != DialogResult.OK) return;
		var newType = dlg.SelectedType;

		var toEdit = entries.Where(e => e.Type != newType).ToList();
		if (toEdit.Count == 0) return;
		_editController.ApplyBatchPropertyChange(toEdit,
			PropertyType.String,
			e => e.GetPropertyValue<string>(null!, AppInfoPaths.Type),
			e => e.Type = newType,
			AppInfoPaths.Type);
	}

	private void OnBatchMarkAsHidden(object? sender, EventArgs e)
	{
		var entries = _listManager.GetSelectedEntries();
		var toEdit = entries.Where(e => !e.IsHidden).ToList();
		if (toEdit.Count == 0) return;
		_editController.ApplyBatchPropertyChange(toEdit,
			PropertyType.Int32,
			e => e.GetPropertyValue(0, AppInfoPaths.IsHidden),
			e => e.IsHidden = true,
			AppInfoPaths.IsHidden);
	}

	private void OnBatchMarkAsVisible(object? sender, EventArgs e)
	{
		var entries = _listManager.GetSelectedEntries();
		var toEdit = entries.Where(e => e.IsHidden).ToList();
		if (toEdit.Count == 0) return;
		_editController.ApplyBatchPropertyChange(toEdit,
			PropertyType.Int32,
			e => e.GetPropertyValue(0, AppInfoPaths.IsHidden),
			e => e.IsHidden = false,
			AppInfoPaths.IsHidden);
	}

	private void OnBatchMarkAsCompleted(object? sender, EventArgs e)
	{
		var entries = _listManager.GetSelectedEntries();
		var toEdit = entries.Where(e => !e.IsCompleted).ToList();
		if (toEdit.Count == 0) return;
		_editController.ApplyBatchPropertyChange(toEdit,
			PropertyType.Int32,
			e => e.GetPropertyValue(0, AppInfoPaths.IsCompleted),
			e => e.IsCompleted = true,
			AppInfoPaths.IsCompleted);
	}

	private void OnBatchMarkAsIncomplete(object? sender, EventArgs e)
	{
		var entries = _listManager.GetSelectedEntries();
		var toEdit = entries.Where(e => e.IsCompleted).ToList();
		if (toEdit.Count == 0) return;
		_editController.ApplyBatchPropertyChange(toEdit,
			PropertyType.Int32,
			e => e.GetPropertyValue(0, AppInfoPaths.IsCompleted),
			e => e.IsCompleted = false,
			AppInfoPaths.IsCompleted);
	}

	private void OnBatchReset(object? sender, EventArgs e)
	{
		var entries = _listManager.GetSelectedEntries();
		if (entries.Count == 0) return;
		var result = MessageBox.Show(this,
			string.Format(Strings.BatchReset_ConfirmMessage, entries.Count),
			Strings.BatchReset_ConfirmTitle,
			MessageBoxButtons.YesNo, MessageBoxIcon.Question);
		if (result != DialogResult.Yes) return;

		foreach (var entry in entries)
		{
			if (!entry.IsModified) continue;
			entry.Revert();
			_listManager.NotifyEntryReverted(entry);
		}
	}

	// --- Batch translate / replace ---

	private void OnBtnBatchTranslate_Click(object? sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(Settings.Default.AiApiUrl) ||
			string.IsNullOrWhiteSpace(Settings.Default.AiApiKey))
		{
			MessageBox.Show(this, Strings.Msg_AiNotConfigured, Strings.Msg_CriticalError,
				MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

		var entriesToTranslate = new List<IAppInfoEntry>();
		foreach (IAppInfoEntry entry in _ctx.Entries)
		{
			if (entry.IsModified && entry.Modification?.Changes != null &&
				entry.Modification.Changes.HasProperty(AppInfoPaths.Name))
				continue;
			if (string.IsNullOrWhiteSpace(entry.Name)) continue;
			entriesToTranslate.Add(entry);
		}

		if (entriesToTranslate.Count == 0)
		{
			MessageBox.Show(this, Strings.Msg_NoTranslatableGames, Strings.MainWindow_BatchTranslate,
				MessageBoxButtons.OK, MessageBoxIcon.Information);
			return;
		}

		string targetLang = Settings.Default.AiTargetLanguage;
		using var dlg = new BatchTranslateDialog(entriesToTranslate, targetLang);
		dlg.ShowDialog(this);

		_listManager.InvalidateAll();
		_editController.LoadEntry(_listManager.GetSelectedEntry());
	}

	private void OnBtnBatchReplace_Click(object? sender, EventArgs e)
	{
		if (_ctx.Entries.Count == 0) return;
		using var dialog = new BatchReplaceDialog(_ctx.Entries);
		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			int replaced = dialog.ReplaceAll();
			if (replaced > 0)
			{
				_listManager.ClearDisplayNameCache();
				_listManager.SortEntries();
				_editController.LoadEntry(_listManager.GetSelectedEntry());
			}
		}
	}

	// --- Save / Apply / Restart ---

	private void OnBtnSave_Click(object? sender, EventArgs e)
	{
		if (_mainViewModel.IsBusy) return;
		_mainViewModel.IsBusy = true;
		try { _steamData.Save(); }
		catch (Exception ex) { Log.WriteLine("Save failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.Message)); }
		finally { _mainViewModel.IsBusy = false; }
	}

	private void OnBtnSaveApply_Click(object? sender, EventArgs e)
	{
		if (_mainViewModel.IsBusy) return;
		_mainViewModel.IsBusy = true;
		try
		{
			_steamData.Save();
			_steamData.Apply();
		}
		catch (Exception ex) { Log.WriteLine("Save/Apply failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.Message)); }
		finally { _mainViewModel.IsBusy = false; }
	}

	private void OnBtnSaveApplyRestart_Click(object? sender, EventArgs e)
	{
		if (_mainViewModel.IsBusy) return;
		_mainViewModel.IsBusy = true;
		try
		{
			SteamProcessManager.KillSteam(_steamData.SteamDir, Strings.SteamData_WaitingForClose, (msg, waitWhile) =>
			{
				using var dlg = new WaitingDialog(msg);
				dlg.ShowAndWait(() => waitWhile());
			});
			_steamData.Save();
			_steamData.Apply();
			SteamProcessManager.EnsureSteamRunning(_steamData.SteamDir, focusIfRunning: true);
		}
		catch (Exception ex) { Log.WriteLine("Save/Apply/Restart failed: {0}", ex); MessageBox.Show(string.Format(Strings.Msg_SaveFailed, ex.Message)); }
		finally { _mainViewModel.IsBusy = false; }
	}

	// --- Help / Shortcuts ---

	private void OnHelp_Click(object? sender, EventArgs e)
	{
		using var dlg = new About();
		ThemeManager.StyleDialog(dlg);
		dlg.ShowDialog(this);
	}

	private void OnBtnShortcuts_Click(object? sender, EventArgs e)
	{
		ShowShortcutsDialog(this);
	}

	// --- Menu / Paint / Steam status ---

	private void OnFilterSelectorMenu_Click(object? sender, EventArgs e)
	{
		if (!filterSelectorMenu.Visible)
		{
			Point screenLocation = filterSelector.PointToScreen(new Point(filterSelector.Size.Width, 0));
			filterSelectorMenu.Show(screenLocation);
		}
	}

	private void OnSortSelectorMenu_Click(object? sender, EventArgs e)
	{
		if (!sortSelectorMenu.Visible)
		{
			string currentField = Settings.Default.SortField;
			bool descending = Settings.Default.SortDescending;
			foreach (var item in sortSelectorMenu.Items)
			{
				if (item is not ToolStripMenuItem menuItem) continue;
				if (menuItem.Tag is string field)
					menuItem.Checked = field == currentField;
				else if (menuItem.Tag is bool isDesc)
					menuItem.Checked = isDesc == descending;
			}
			Point screenLocation = sortSelector.PointToScreen(new Point(sortSelector.Size.Width, 0));
			sortSelectorMenu.Show(screenLocation);
		}
	}

	private void panel1_Paint(object? sender, PaintEventArgs e)
	{
		float dpiX = e.Graphics.DpiX;
		float num = dpiX / 72f;
		Image logo = Resources.logo1;
		Size size = new Size((int)(150f * num), (int)(150f * num));
		Point location = new Point(panel1.ClientSize - size);
		Rectangle destRect = new Rectangle(location, size);
		e.Graphics.DrawImage(Resources.logo1, destRect, 0, 0, logo.Width, logo.Height, GraphicsUnit.Pixel, _listManager.BgLogoAttributes);
	}

	private void lblSteamStatus_MouseEnter(object? sender, EventArgs e)
	{
		SetSteamStatusLinkStyle(true);
	}

	private void lblSteamStatus_MouseLeave(object? sender, EventArgs e)
	{
		SetSteamStatusLinkStyle(false);
	}

	private void lblSteamStatus_Click(object? sender, EventArgs e)
	{
		if (SteamProcessManager.IsSteamRunning(_steamData.SteamDir))
		{
			SteamProcessManager.EnsureSteamRunning(_steamData.SteamDir, true);
		}
		else
		{
			var result = MessageBox.Show(
				Strings.SteamLaunch_ConfirmMessage,
				Strings.SteamLaunch_ConfirmTitle,
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Question);

			if (result == DialogResult.Yes)
			{
				string? exePath = SteamProcessManager.GetSteamExePath(_steamData.SteamDir);
				if (exePath == null)
				{
					MessageBox.Show(
						Strings.SteamLaunch_PathError,
						Strings.SteamLaunch_ConfirmTitle,
						MessageBoxButtons.OK,
						MessageBoxIcon.Error);
					return;
				}

				SteamProcessManager.EnsureSteamRunning(_steamData.SteamDir, true);
			}
		}
	}
}
