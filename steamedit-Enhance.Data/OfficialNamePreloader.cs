#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace steamedit_Enhance.Data;

internal readonly record struct OfficialNameProgress(
	int Processed, int Total, int Already, int Fetched, int Failed, int Skipped, string CurrentName);

internal readonly record struct OfficialNamePreloadResult(
	int Total, int Already, int Fetched, int Failed, int Skipped);

/// <summary>
/// 批量“补齐”官方本地化名称（纯显示缓存，绝不写 appinfo）：
/// 对每个条目——用户已改名或非空默认名缺失时跳过；appinfo 自带该语言官方名即视为已有；
/// 否则从 Steam store API 获取并写入本地显示缓存。两阶段：先串行分类，再按并发度分批下载。
/// </summary>
internal sealed class OfficialNamePreloader
{
	private readonly IOfficialNameFetcher _fetcher;
	private readonly LocalizedNameCache _cache;
	private readonly int _concurrency;

	public OfficialNamePreloader(LocalizedNameCache cache, int concurrency = 6, IOfficialNameFetcher? fetcher = null)
	{
		_cache = cache;
		_fetcher = fetcher ?? new OfficialNameFetcher();
		_concurrency = Math.Max(1, concurrency);
	}

	public async Task<OfficialNamePreloadResult> RunAsync(
		IReadOnlyList<IAppInfoEntry> entries,
		string locale,
		IProgress<OfficialNameProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		if (entries == null) throw new ArgumentNullException(nameof(entries));
		if (string.IsNullOrEmpty(locale) || locale == SteamLanguages.English)
			return new OfficialNamePreloadResult(0, 0, 0, 0, 0);

		int already = 0;
		int skipped = 0;
		var toFetch = new List<IAppInfoEntry>(entries.Count);

		// 阶段一：串行分类
		foreach (var entry in entries)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (entry.ID <= 0 || string.IsNullOrWhiteSpace(entry.Name)) { skipped++; continue; }
			if (LocalizedNameResolver.IsUserRenamed(entry)) { skipped++; continue; }
			if (LocalizedNameResolver.TryGetLocalizedFromAppInfo(entry, locale) != null) { already++; continue; }
			if (_cache.Contains(entry.ID, locale)) { already++; continue; }
			toFetch.Add(entry);
		}

		int total = toFetch.Count;
		int fetched = 0;
		int failed = 0;
		int processed = 0;

		// 阶段二：分批并发获取
		for (int start = 0; start < toFetch.Count; start += _concurrency)
		{
			cancellationToken.ThrowIfCancellationRequested();
			int end = Math.Min(start + _concurrency, toFetch.Count);
			var batch = new List<Task>(end - start);
			for (int i = start; i < end; i++)
			{
				IAppInfoEntry entry = toFetch[i];
				batch.Add(Task.Run(async () =>
				{
					bool ok = false;
					try
					{
						string? name = await _fetcher.FetchNameAsync(entry.ID, locale, cancellationToken).ConfigureAwait(false);
						if (name != null && _cache.Set(entry.ID, locale, name))
							ok = true;
					}
					catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
					{
						ok = false;
					}
					catch (Exception ex)
					{
						Log.WriteLine("OfficialNamePreloader: fetch failed for {0} ({1}): {2}", entry.ID, entry.Name, ex.Message);
						ok = false;
					}
					if (ok) Interlocked.Increment(ref fetched);
					else Interlocked.Increment(ref failed);
					int p = Interlocked.Increment(ref processed);
					progress?.Report(new OfficialNameProgress(p, total, Volatile.Read(ref already),
						Volatile.Read(ref fetched), Volatile.Read(ref failed), Volatile.Read(ref skipped), entry.Name));
				}, cancellationToken));
			}
			await Task.WhenAll(batch).ConfigureAwait(false);
		}

		// 无论正常结束还是取消，落盘已获取的缓存
		try { _cache.Save(); }
		catch (Exception ex) { Log.WriteLine("OfficialNamePreloader: cache save error: {0}", ex.Message); }

		return new OfficialNamePreloadResult(total, already, fetched, failed, skipped);
	}
}
