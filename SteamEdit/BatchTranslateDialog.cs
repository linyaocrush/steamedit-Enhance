using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;
using SteamEdit.Data;

namespace SteamEdit;

internal class BatchTranslateDialog : Form
{
    private IContainer components;
    private Label lblProgress;
    private ProgressBar progressBar;
    private Label lblCurrent;
    private Label lblStats;
    private Button btnPause;
    private Button btnCancel;

    private readonly List<IAppInfoEntry> _entries;
    private readonly string _targetLanguage;
    private CancellationTokenSource _cts;
    private ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);
    private bool _isPaused;
    private int _successCount;
    private int _skipCount;
    private int _failCount;
    private int _currentIndex;

    public int SuccessCount => _successCount;
    public int SkipCount => _skipCount;
    public int FailCount => _failCount;

    public BatchTranslateDialog(List<IAppInfoEntry> entries, string targetLanguage)
    {
        _entries = entries;
        _targetLanguage = targetLanguage;
        InitializeComponent();
        I18n.LanguageChanged += OnLanguageChanged;
        ApplyLanguage();
    }

    private void OnLanguageChanged(object sender, EventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(ApplyLanguage)); return; }
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        Text = Strings.BatchTranslate_Title;
        btnPause.Text = _isPaused ? Strings.BatchTranslate_Resume : Strings.BatchTranslate_Pause;
        btnCancel.Text = Strings.BatchTranslate_Cancel;
        UpdateStatsDisplay();
    }

    private void UpdateStatsDisplay()
    {
        lblProgress.Text = string.Format(Strings.BatchTranslate_Progress, _currentIndex, _entries.Count);
        progressBar.Maximum = _entries.Count;
        progressBar.Value = Math.Min(_currentIndex, _entries.Count);
        lblStats.Text = string.Format(Strings.BatchTranslate_Stats, _successCount, _skipCount, _failCount);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await RunBatchAsync();
    }

    private async Task RunBatchAsync()
    {
        _cts = new CancellationTokenSource();
        string customSystemPrompt = Settings.Default.AiCustomSystemPrompt;
        string customUserMessage = Settings.Default.AiCustomUserMessage;

        for (_currentIndex = 0; _currentIndex < _entries.Count; _currentIndex++)
        {
            if (_cts.Token.IsCancellationRequested) break;

            _pauseEvent.Wait(_cts.Token);
            if (_cts.Token.IsCancellationRequested) break;

            IAppInfoEntry entry = _entries[_currentIndex];
            string currentName = entry.Name;

            if (InvokeRequired)
                BeginInvoke(() => lblCurrent.Text = string.Format(Strings.BatchTranslate_Current, currentName));
            else
                lblCurrent.Text = string.Format(Strings.BatchTranslate_Current, currentName);

            // Check glossary fast path
            string existingTranslation = TranslationGlossary.FindTranslatedByOriginalName(currentName);
            if (!string.IsNullOrWhiteSpace(existingTranslation))
            {
                ApplyTranslation(entry, currentName, existingTranslation);
                _successCount++;
                if (InvokeRequired) BeginInvoke(UpdateStatsDisplay);
                else UpdateStatsDisplay();
                continue;
            }

            // Build context from VDF metadata
            string context = AiTranslator.BuildContext(entry);

            try
            {
                string translated = await AiTranslator.TranslateWithContextAsync(
                    currentName, _targetLanguage, context,
                    customSystemPrompt, customUserMessage, _cts.Token);

                if (!string.IsNullOrWhiteSpace(translated))
                {
                    ApplyTranslation(entry, currentName, translated);
                    _successCount++;
                }
                else
                {
                    _skipCount++;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.WriteLine("Batch translate failed for \"{0}\": {1}", currentName, ex.Message);
                _failCount++;
            }

            if (InvokeRequired) BeginInvoke(UpdateStatsDisplay);
            else UpdateStatsDisplay();
        }

        if (InvokeRequired)
            BeginInvoke(() => { UpdateStatsDisplay(); btnPause.Enabled = false; btnCancel.Text = Strings.BatchTranslate_Close; });
        else
        {
            UpdateStatsDisplay();
            btnPause.Enabled = false;
            btnCancel.Text = Strings.BatchTranslate_Close;
        }
    }

    private void ApplyTranslation(IAppInfoEntry entry, string previousName, string translated)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ApplyTranslation(entry, previousName, translated));
            return;
        }
        entry.Name = translated;
        // Update glossary
        try
        {
            string originalName = previousName;
            if (entry.Modification?.OriginalData != null)
            {
                using var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(entry.Modification.OriginalData));
                var props = PropertyTableIO.ReadPropertyTable_Mods(reader);
                originalName = props.GetPropertyValue<string>(null, "appinfo", "common", "name") ?? previousName;
            }
            TranslationGlossary.UpdateEntry(entry.ID, originalName, translated);
        }
        catch (Exception ex)
        {
            Log.WriteLine("Batch glossary update failed for {0:X8}: {1}", entry.ID, ex.Message);
        }
    }

    private void btnPause_Click(object sender, EventArgs e)
    {
        if (_isPaused)
        {
            _isPaused = false;
            _pauseEvent.Set();
            btnPause.Text = Strings.BatchTranslate_Pause;
        }
        else
        {
            _isPaused = true;
            _pauseEvent.Reset();
            btnPause.Text = Strings.BatchTranslate_Resume;
        }
    }

    private void btnCancel_Click(object sender, EventArgs e)
    {
        if (_isPaused) _pauseEvent.Set();
        _cts?.Cancel();
        DialogResult = DialogResult.Cancel;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isPaused) _pauseEvent.Set();
        _cts?.Cancel();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        I18n.LanguageChanged -= OnLanguageChanged;
        _cts?.Dispose();
        _pauseEvent?.Dispose();
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.lblProgress = new System.Windows.Forms.Label();
        this.progressBar = new System.Windows.Forms.ProgressBar();
        this.lblCurrent = new System.Windows.Forms.Label();
        this.lblStats = new System.Windows.Forms.Label();
        this.btnPause = new System.Windows.Forms.Button();
        this.btnCancel = new System.Windows.Forms.Button();
        base.SuspendLayout();
        // lblProgress
        this.lblProgress.AutoSize = true;
        this.lblProgress.Location = new System.Drawing.Point(12, 15);
        this.lblProgress.Name = "lblProgress";
        this.lblProgress.Size = new System.Drawing.Size(100, 13);
        this.lblProgress.TabIndex = 0;
        this.lblProgress.Text = "Progress: 0/0";
        // progressBar
        this.progressBar.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.progressBar.Location = new System.Drawing.Point(12, 35);
        this.progressBar.Name = "progressBar";
        this.progressBar.Size = new System.Drawing.Size(360, 23);
        this.progressBar.TabIndex = 1;
        // lblCurrent
        this.lblCurrent.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
        this.lblCurrent.Location = new System.Drawing.Point(12, 65);
        this.lblCurrent.Name = "lblCurrent";
        this.lblCurrent.Size = new System.Drawing.Size(360, 20);
        this.lblCurrent.TabIndex = 2;
        this.lblCurrent.Text = "";
        // lblStats
        this.lblStats.AutoSize = true;
        this.lblStats.Location = new System.Drawing.Point(12, 95);
        this.lblStats.Name = "lblStats";
        this.lblStats.Size = new System.Drawing.Size(200, 13);
        this.lblStats.TabIndex = 3;
        this.lblStats.Text = "";
        // btnPause
        this.btnPause.Location = new System.Drawing.Point(12, 125);
        this.btnPause.Name = "btnPause";
        this.btnPause.Size = new System.Drawing.Size(80, 28);
        this.btnPause.TabIndex = 4;
        this.btnPause.Text = "Pause";
        this.btnPause.UseVisualStyleBackColor = true;
        this.btnPause.Click += new System.EventHandler(btnPause_Click);
        // btnCancel
        this.btnCancel.Location = new System.Drawing.Point(100, 125);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(80, 28);
        this.btnCancel.TabIndex = 5;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new System.EventHandler(btnCancel_Click);
        // Form
        base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
        base.ClientSize = new System.Drawing.Size(384, 165);
        base.ControlBox = false;
        base.Controls.Add(this.btnCancel);
        base.Controls.Add(this.btnPause);
        base.Controls.Add(this.lblStats);
        base.Controls.Add(this.lblCurrent);
        base.Controls.Add(this.progressBar);
        base.Controls.Add(this.lblProgress);
        base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        base.MaximizeBox = false;
        base.MinimizeBox = false;
        base.Name = "BatchTranslateDialog";
        base.ShowIcon = false;
        base.ShowInTaskbar = false;
        base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Batch Translate";
        base.ResumeLayout(false);
        base.PerformLayout();
    }
}
