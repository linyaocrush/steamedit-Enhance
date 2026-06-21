#nullable enable
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

internal class BatchTranslateDialog : LocalizableForm
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

    private void InvokeOnUI(Action action)
    {
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    public BatchTranslateDialog(List<IAppInfoEntry> entries, string targetLanguage)
    {
        _entries = entries;
        _targetLanguage = targetLanguage;
        InitializeComponent();
    }

    protected override void ApplyLanguage()
    {
        Text = Strings.BatchTranslate_Title;
        btnPause.Text = _isPaused ? Strings.BatchTranslate_Resume : Strings.BatchTranslate_Pause;
        btnCancel.Text = Strings.BatchTranslate_Cancel;
        UpdateStatsDisplay();
    }

    private void UpdateStatsDisplay()
    {
        // 快照计数器，避免并行模式下读取不一致
        int current = Volatile.Read(ref _currentIndex);
        int success = Volatile.Read(ref _successCount);
        int skip = Volatile.Read(ref _skipCount);
        int fail = Volatile.Read(ref _failCount);
        int total = _entries.Count;
        lblProgress.Text = string.Format(Strings.BatchTranslate_Progress, current, total);
        progressBar.Maximum = total;
        progressBar.Value = Math.Min(current, total);
        lblStats.Text = string.Format(Strings.BatchTranslate_Stats, success, skip, fail);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            await RunBatchAsync();
        }
        catch (OperationCanceledException)
        {
            // 用户取消，正常退出
        }
        catch (Exception ex)
        {
            Log.WriteLine("BatchTranslate error: {0}", ex);
            InvokeOnUI(() => MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error));
        }
    }

    private async Task RunBatchAsync()
    {
        _cts = new CancellationTokenSource();
        string customSystemPrompt = Settings.Default.AiCustomSystemPrompt;
        string customUserMessage = Settings.Default.AiCustomUserMessage;
        int concurrency = Math.Max(Settings.Default.AiBatchTranslateConcurrency, 1);

        if (concurrency <= 1)
        {
            // 串行模式
            for (_currentIndex = 0; _currentIndex < _entries.Count; _currentIndex++)
            {
                if (_cts.Token.IsCancellationRequested) break;
                _pauseEvent.Wait(_cts.Token);
                if (_cts.Token.IsCancellationRequested) break;

                await TranslateOneAsync(_entries[_currentIndex], customSystemPrompt, customUserMessage, _cts.Token);
            }
        }
        else
        {
            // 并行模式
            var semaphore = new SemaphoreSlim(concurrency);
            var tasks = new List<Task>();
            int index = 0;

            foreach (var entry in _entries)
            {
                if (_cts.Token.IsCancellationRequested) break;

                _pauseEvent.Wait(_cts.Token);
                if (_cts.Token.IsCancellationRequested) break;

                await semaphore.WaitAsync(_cts.Token);
                _currentIndex = Interlocked.Increment(ref index);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await TranslateOneAsync(entry, customSystemPrompt, customUserMessage, _cts.Token);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, _cts.Token));
            }

            await Task.WhenAll(tasks);
        }

        TranslationGlossary.Flush();

        InvokeOnUI(() => { UpdateStatsDisplay(); btnPause.Enabled = false; btnCancel.Text = Strings.BatchTranslate_Close; });
    }

    private async Task TranslateOneAsync(IAppInfoEntry entry, string customSystemPrompt, string customUserMessage, CancellationToken ct)
    {
        string currentName = entry.Name;

        InvokeOnUI(() => lblCurrent.Text = string.Format(Strings.BatchTranslate_Current, currentName));

        // Check glossary fast path
        string? existingTranslation = TranslationGlossary.FindTranslatedByOriginalName(currentName);
        if (!string.IsNullOrWhiteSpace(existingTranslation))
        {
            ApplyTranslation(entry, currentName, existingTranslation);
            Interlocked.Increment(ref _successCount);
            InvokeOnUI(UpdateStatsDisplay);
            return;
        }

        // Build context from VDF metadata
        string? context = AiTranslator.BuildContext(
            entry.GetPropertyValue<string>(null!, AppInfoPaths.Developers),
            entry.GetPropertyValue<string>(null!, AppInfoPaths.Publishers),
            entry.GetPropertyValue<string>(null!, AppInfoPaths.Type));

        try
        {
            string translated = await AiTranslator.TranslateWithContextAsync(
                currentName, _targetLanguage, context,
                customSystemPrompt, customUserMessage, ct);

            if (!string.IsNullOrWhiteSpace(translated))
            {
                ApplyTranslation(entry, currentName, translated);
                Interlocked.Increment(ref _successCount);
            }
            else
            {
                Interlocked.Increment(ref _skipCount);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.WriteLine("Batch translate failed for \"{0}\": {1}", currentName, ex.Message);
            Interlocked.Increment(ref _failCount);
        }

        InvokeOnUI(UpdateStatsDisplay);
    }

    private void ApplyTranslation(IAppInfoEntry entry, string previousName, string translated)
    {
        void DoApply()
        {
            entry.Name = translated;
            try
            {
                string originalName = entry.Modification?.GetOriginalName() ?? previousName;
                TranslationGlossary.UpdateEntry(entry.ID, originalName, translated);
            }
            catch (Exception ex)
            {
                Log.WriteLine("Batch glossary update failed for {0:X8}: {1}", entry.ID, ex.Message);
            }
        }
        InvokeOnUI(DoApply);
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
        try { _cts?.Dispose(); } catch (ObjectDisposedException) { }
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
