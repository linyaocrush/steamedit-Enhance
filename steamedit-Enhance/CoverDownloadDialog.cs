#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.Properties;

namespace steamedit_Enhance;

/// <summary>批量识别/下载缺失竖版封面的进度对话框。</summary>
internal class CoverDownloadDialog : LocalizableForm
{
    private IContainer components = null!;
    private Label lblProgress = null!;
    private ProgressBar progressBar = null!;
    private Label lblCurrent = null!;
    private Label lblStats = null!;
    private Button btnCancel = null!;

    private readonly string _steamDir;
    private readonly IReadOnlyList<CoverTarget> _targets;
    private readonly CancellationTokenSource _cts = new();
    private CoverPreloadResult? _result;
    private CoverProgress? _lastProgress;
    private bool _running = true;

    public CoverDownloadDialog(string steamDir, IReadOnlyList<CoverTarget> targets)
    {
        _steamDir = steamDir;
        _targets = targets;
        InitializeComponent();
        ThemeManager.StyleDialog(this);
        Load += CoverDownloadDialog_Load;
    }

    public CoverPreloadResult? Result => _result;

    private async void CoverDownloadDialog_Load(object? sender, EventArgs e)
    {
        var progress = new Progress<CoverProgress>(OnProgress);
        try
        {
            var preloader = new CoverPreloader(_steamDir, "");
            _result = await Task.Run(() => preloader.RunAsync(_targets, progress, _cts.Token)).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.WriteLine("CoverDownload error: {0}", ex);
            if (!IsDisposed)
                MessageBox.Show(this, ex.Message, Strings.CoverDlg_Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed)
            {
                _running = false;
                progressBar.Value = progressBar.Maximum;
                if (_result is CoverPreloadResult r)
                    OnProgress(new CoverProgress(r.Total, r.Total, r.Already, r.Downloaded, r.Failed, ""));
                btnCancel.Text = Strings.CoverDlg_Close;
                btnCancel.Enabled = true;
            }
        }
    }

    private void OnProgress(CoverProgress p)
    {
        if (IsDisposed)
            return;
        _lastProgress = p;
        lblProgress.Text = string.Format(Strings.CoverDlg_Progress, p.Processed, p.Total);
        progressBar.Maximum = Math.Max(p.Total, 1);
        progressBar.Value = Math.Min(p.Processed, p.Total);
        lblCurrent.Text = p.Total > 0
            ? string.Format(Strings.CoverDlg_Current, p.CurrentName)
            : "";
        lblStats.Text = string.Format(Strings.CoverDlg_Stats, p.Already, p.Downloaded, p.Failed);
    }

    private void btnCancel_Click(object? sender, EventArgs e)
    {
        if (_running)
        {
            btnCancel.Enabled = false;
            _cts.Cancel();
        }
        else
        {
            DialogResult = DialogResult.OK;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_running)
            _cts.Cancel();
        base.OnFormClosing(e);
    }

    protected override void ApplyLanguage()
    {
        Text = Strings.CoverDlg_Title;
        btnCancel.Text = _running ? Strings.CoverDlg_Cancel : Strings.CoverDlg_Close;
        if (_lastProgress is CoverProgress p)
            OnProgress(p);
    }

    protected override void Dispose(bool disposing)
    {
        _cts.Dispose();
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
        this.btnCancel = new System.Windows.Forms.Button();
        base.SuspendLayout();
        // lblProgress
        this.lblProgress.AutoSize = true;
        this.lblProgress.Location = new System.Drawing.Point(12, 15);
        this.lblProgress.Name = "lblProgress";
        this.lblProgress.TabIndex = 0;
        this.lblProgress.Text = "Progress";
        // progressBar
        this.progressBar.Location = new System.Drawing.Point(12, 35);
        this.progressBar.Name = "progressBar";
        this.progressBar.Size = new System.Drawing.Size(460, 23);
        this.progressBar.TabIndex = 1;
        // lblCurrent
        this.lblCurrent.AutoEllipsis = true;
        this.lblCurrent.Location = new System.Drawing.Point(12, 65);
        this.lblCurrent.Name = "lblCurrent";
        this.lblCurrent.Size = new System.Drawing.Size(460, 17);
        this.lblCurrent.TabIndex = 2;
        this.lblCurrent.Text = "";
        // lblStats
        this.lblStats.AutoSize = true;
        this.lblStats.Location = new System.Drawing.Point(12, 90);
        this.lblStats.Name = "lblStats";
        this.lblStats.Size = new System.Drawing.Size(36, 17);
        this.lblStats.TabIndex = 3;
        this.lblStats.Text = "Stats";
        // btnCancel
        this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
        this.btnCancel.Location = new System.Drawing.Point(382, 120);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(90, 30);
        this.btnCancel.TabIndex = 4;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.UseVisualStyleBackColor = true;
        this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
        // CoverDownloadDialog
        base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        base.ClientSize = new System.Drawing.Size(484, 161);
        base.Controls.Add(this.btnCancel);
        base.Controls.Add(this.lblStats);
        base.Controls.Add(this.lblCurrent);
        base.Controls.Add(this.progressBar);
        base.Controls.Add(this.lblProgress);
        base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        base.MaximizeBox = false;
        base.MinimizeBox = false;
        base.Name = "CoverDownloadDialog";
        base.ShowInTaskbar = false;
        base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Download Covers";
        base.ResumeLayout(false);
        base.PerformLayout();
    }
}
