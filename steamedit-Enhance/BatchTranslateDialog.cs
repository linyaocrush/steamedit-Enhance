#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.ViewModels;

namespace steamedit_Enhance;

internal class BatchTranslateDialog : LocalizableForm
{
    private IContainer components = null!;
    private Label lblProgress = null!;
    private ProgressBar progressBar = null!;
    private Label lblCurrent = null!;
    private Label lblStats = null!;
    private Button btnPause = null!;
    private Button btnCancel = null!;

    private readonly BatchTranslateViewModel _viewModel = new();

    public BatchTranslateDialog(List<IAppInfoEntry> entries, string targetLanguage)
    {
        InitializeComponent();
        ThemeManager.StyleDialog(this);

        _viewModel.StateChanged += OnViewModelStateChanged;
        _viewModel.FailuresDetected += OnFailuresDetected;

        // Start translation after load
        Load += async (_, _) =>
        {
            try
            {
                await _viewModel.RunAsync(entries, targetLanguage);
            }
            catch (Exception ex)
            {
                Log.WriteLine("BatchTranslate error: {0}", ex);
                if (!IsDisposed)
                    InvokeOnUI(() => { if (!IsDisposed) MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); });
            }
            finally
            {
                OnViewModelStateChanged();
            }
        };
    }

    public int SuccessCount => _viewModel.SuccessCount;
    public int SkipCount => _viewModel.SkipCount;
    public int FailCount => _viewModel.FailCount;

    private void InvokeOnUI(Action action)
    {
        if (IsDisposed) return;
        try
        {
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }
        catch (ObjectDisposedException) { }
    }

    protected override void ApplyLanguage()
    {
        Text = Strings.BatchTranslate_Title;
        btnPause.Text = _viewModel.IsPaused ? Strings.BatchTranslate_Resume : Strings.BatchTranslate_Pause;
        btnCancel.Text = _viewModel.IsRunning ? Strings.BatchTranslate_Cancel : Strings.BatchTranslate_Close;
        UpdateDisplay();
    }

    private void OnViewModelStateChanged()
    {
        if (IsDisposed) return;
        InvokeOnUI(UpdateDisplay);
    }

    private void OnFailuresDetected(IReadOnlyList<(int Id, string Name, string Error)> failures)
    {
        if (IsDisposed) return;
        InvokeOnUI(() =>
        {
            if (IsDisposed) return;
            var detail = new System.Text.StringBuilder();
            detail.AppendLine($"Batch translate completed with {failures.Count} failure(s):");
            detail.AppendLine();
            int maxShow = Math.Min(failures.Count, 10);
            for (int i = 0; i < maxShow; i++)
            {
                var f = failures[i];
                detail.AppendLine($"  [{f.Id:X8}] {f.Name}: {f.Error}");
            }
            if (failures.Count > 10)
                detail.AppendLine($"... and {failures.Count - 10} more failure(s)");
            MessageBox.Show(this, detail.ToString(), Strings.Msg_CriticalError, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        });
    }

    private void UpdateDisplay()
    {
        lblProgress.Text = string.Format(Strings.BatchTranslate_Progress, _viewModel.Progress, _viewModel.Total);
        progressBar.Maximum = Math.Max(_viewModel.Total, 1);
        progressBar.Value = Math.Min(_viewModel.Progress, _viewModel.Total);
        lblCurrent.Text = _viewModel.IsRunning
            ? string.Format(Strings.BatchTranslate_Current, _viewModel.CurrentName)
            : "";
        lblStats.Text = string.Format(Strings.BatchTranslate_Stats, _viewModel.SuccessCount, _viewModel.SkipCount, _viewModel.FailCount);
        btnPause.Enabled = _viewModel.IsRunning;
        btnPause.Text = _viewModel.IsPaused ? Strings.BatchTranslate_Resume : Strings.BatchTranslate_Pause;
        btnCancel.Text = _viewModel.IsRunning ? Strings.BatchTranslate_Cancel : Strings.BatchTranslate_Close;
    }

    private void btnPause_Click(object? sender, EventArgs e) => _viewModel.Pause();

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        _viewModel.Cancel();
        if (!_viewModel.IsRunning)
            DialogResult = DialogResult.Cancel;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _viewModel.Cancel();
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        _viewModel.StateChanged -= OnViewModelStateChanged;
        _viewModel.FailuresDetected -= OnFailuresDetected;
        _viewModel.Dispose();
        if (disposing && components != null)
            components.Dispose();
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
        // 
        // lblProgress
        // 
        this.lblProgress.AutoSize = true;
        this.lblProgress.Location = new System.Drawing.Point(12, 15);
        this.lblProgress.Name = "lblProgress";
        this.lblProgress.Size = new System.Drawing.Size(62, 17);
        this.lblProgress.TabIndex = 0;
        this.lblProgress.Text = "Progress";
        // 
        // progressBar
        // 
        this.progressBar.Anchor |= System.Windows.Forms.AnchorStyles.Right;
        this.progressBar.Location = new System.Drawing.Point(12, 35);
        this.progressBar.Name = "progressBar";
        this.progressBar.Size = new System.Drawing.Size(460, 23);
        this.progressBar.TabIndex = 1;
        // 
        // lblCurrent
        // 
        this.lblCurrent.AutoEllipsis = true;
        this.lblCurrent.Location = new System.Drawing.Point(12, 65);
        this.lblCurrent.Name = "lblCurrent";
        this.lblCurrent.Size = new System.Drawing.Size(460, 17);
        this.lblCurrent.TabIndex = 2;
        this.lblCurrent.Text = "";
        // 
        // lblStats
        // 
        this.lblStats.AutoSize = true;
        this.lblStats.Location = new System.Drawing.Point(12, 90);
        this.lblStats.Name = "lblStats";
        this.lblStats.Size = new System.Drawing.Size(36, 17);
        this.lblStats.TabIndex = 3;
        this.lblStats.Text = "Stats";
        // 
        // btnPause
        // 
        this.btnPause.Anchor = (System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left);
        this.btnPause.Location = new System.Drawing.Point(12, 120);
        this.btnPause.Name = "btnPause";
        this.btnPause.Size = new System.Drawing.Size(90, 30);
        this.btnPause.TabIndex = 4;
        this.btnPause.Text = "Pause";
        this.btnPause.UseVisualStyleBackColor = true;
        this.btnPause.Click += new System.EventHandler(this.btnPause_Click);
        // 
        // btnCancel
        // 
        this.btnCancel.Anchor = (System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right);
        this.btnCancel.Location = new System.Drawing.Point(382, 120);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(90, 30);
        this.btnCancel.TabIndex = 5;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
        // 
        // BatchTranslateDialog
        // 
        base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        base.ClientSize = new System.Drawing.Size(484, 161);
        base.Controls.Add(this.btnPause);
        base.Controls.Add(this.btnCancel);
        base.Controls.Add(this.lblStats);
        base.Controls.Add(this.lblCurrent);
        base.Controls.Add(this.progressBar);
        base.Controls.Add(this.lblProgress);
        base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        base.MaximizeBox = false;
        base.MinimizeBox = false;
        base.Name = "BatchTranslateDialog";
        base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Batch Translate";
        base.ResumeLayout(false);
        base.PerformLayout();
    }
}
