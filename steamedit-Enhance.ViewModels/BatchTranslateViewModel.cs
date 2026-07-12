#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Common;
using steamedit_Enhance.Data;

namespace steamedit_Enhance.ViewModels;

internal sealed class BatchTranslateViewModel : ViewModelBase
{
    private int _progress;
    private int _total;
    private string _currentName = "";
    private int _successCount;
    private int _skipCount;
    private int _failCount;
    private bool _isRunning;
    private bool _isPaused;

    private CancellationTokenSource? _cts;
    private volatile bool _isPausedFlag;

    public int Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public int Total { get => _total; private set => SetProperty(ref _total, value); }
    public string CurrentName { get => _currentName; private set => SetProperty(ref _currentName, value); }
    public int SuccessCount { get => _successCount; private set => SetProperty(ref _successCount, value); }
    public int SkipCount { get => _skipCount; private set => SetProperty(ref _skipCount, value); }
    public int FailCount { get => _failCount; private set => SetProperty(ref _failCount, value); }
    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }
    public bool IsPaused { get => _isPaused; set { if (SetProperty(ref _isPaused, value)) OnPausedChanged(); } }

    public event Action? StateChanged;
    public event Action<IReadOnlyList<(int Id, string Name, string Error)>>? FailuresDetected;

    private readonly List<(int Id, string Name, string Error)> _failures = new();

    public async Task RunAsync(List<IAppInfoEntry> entries, string targetLanguage)
    {
        _cts = new CancellationTokenSource();
        var uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
        IsRunning = true;
        Total = entries.Count;

        string customSystemPrompt = Settings.Default.AiCustomSystemPrompt;
        string customUserMessage = Settings.Default.AiCustomUserMessage;
        int concurrency = Math.Max(Settings.Default.AiBatchTranslateConcurrency, 1);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = _cts.Token
        };

        try
        {
            int index = 0;
            await Parallel.ForEachAsync(entries, parallelOptions, async (entry, ct) =>
            {
                while (_isPausedFlag && !ct.IsCancellationRequested)
                    await Task.Delay(100, ct);
                ct.ThrowIfCancellationRequested();
                int current = Interlocked.Increment(ref index);
                if (current % 5 == 0)
                    uiContext.Post(_ => Progress = current, null);
                await TranslateOneAsync(entry, targetLanguage, customSystemPrompt, customUserMessage, ct);
                // 每 50 条刷新一次，减少批量翻译中数据丢失风险
                if (current % 50 == 0)
                    TranslationGlossary.Flush();
            });

            TranslationGlossary.Flush();
        }
        catch (OperationCanceledException)
        {
            // normal exit on cancel
        }
        catch (Exception ex)
        {
            Log.WriteLine("BatchTranslate error: {0}", ex);
        }
        finally
        {
            IsRunning = false;
            if (_failures.Count > 0)
                FailuresDetected?.Invoke(_failures.AsReadOnly());
            StateChanged?.Invoke();
        }
    }

    public void Pause()
    {
        IsPaused = !IsPaused;
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    public void Dispose()
    {
        try { _cts?.Dispose(); } catch (ObjectDisposedException) { }
    }

    private void OnPausedChanged()
    {
        _isPausedFlag = _isPaused;
    }

    private async Task TranslateOneAsync(
        IAppInfoEntry entry, string targetLanguage,
        string customSystemPrompt, string customUserMessage,
        CancellationToken ct)
    {
        string currentName = entry.Name;
        CurrentName = currentName ?? "";
        StateChanged?.Invoke();

        string originalName = entry.Modification?.GetOriginalName() ?? currentName;
        string? existingRef = (originalName != currentName) ? currentName : null;

        string? existingTranslation = TranslationGlossary.FindTranslatedByOriginalName(originalName);
        if (!string.IsNullOrWhiteSpace(existingTranslation))
        {
            ApplyTranslation(entry, currentName, existingTranslation);
            Interlocked.Increment(ref _successCount);
            SuccessCount = _successCount;
            StateChanged?.Invoke();
            return;
        }

        string? context = AiTranslator.BuildContext(
            entry.GetPropertyValue<string>(null!, AppInfoPaths.Developers),
            entry.GetPropertyValue<string>(null!, AppInfoPaths.Publishers),
            entry.GetPropertyValue<string>(null!, AppInfoPaths.Type));

        try
        {
            Log.WriteLine("Batch translate: \"{0}\" (orig: \"{1}\"{2})",
                currentName, originalName,
                existingRef != null ? ", ref: \"" + existingRef + "\"" : "");
            string translated = await AiTranslator.TranslateWithContextAsync(
                originalName, targetLanguage, context,
                customSystemPrompt, customUserMessage, cancellationToken: ct, existingTranslation: existingRef);

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
            lock (_failures) _failures.Add((entry.ID, currentName, ex.Message));
            Interlocked.Increment(ref _failCount);
        }

        SuccessCount = Volatile.Read(ref _successCount);
        SkipCount = Volatile.Read(ref _skipCount);
        FailCount = Volatile.Read(ref _failCount);
        StateChanged?.Invoke();
    }

    private void ApplyTranslation(IAppInfoEntry entry, string previousName, string translated)
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
}
