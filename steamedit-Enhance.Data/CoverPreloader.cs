#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace steamedit_Enhance.Data;

/// <summary>判断某条目类型是否值得为其识别/下载竖版封面。</summary>
internal static class CoverEligibility
{
	public static bool IsGameLike(AppInfoEntryType type)
		=> type is AppInfoEntryType.Game or AppInfoEntryType.Demo
			or AppInfoEntryType.DLC or AppInfoEntryType.Application;
}

internal readonly record struct CoverTarget(int AppId, string Name, AppInfoEntryType Type);

internal readonly record struct CoverProgress(int Processed, int Total, int Already, int Downloaded, int Failed, string CurrentName);

internal readonly record struct CoverPreloadResult(int Total, int Already, int Downloaded, int Failed);

/// <summary>
/// 批量“识别/补齐”封面：对每个符合条件的条目，本地能找到竖版封面(或在线缓存已有)即视为已有；
/// 否则从 Steam CDN 下载竖封面并写入 exe 同目录 cache 文件夹。两阶段：先串行分类(本地查询)，
/// 再按并发度分批下载。全程可取消，通过 IProgress 上报进度。
/// </summary>
internal sealed class CoverPreloader
{
	private readonly SteamCoverResolver _resolver;
	private readonly ICoverDownloader _downloader;
	private readonly string _cacheDir;
	private readonly int _concurrency;

	public CoverPreloader(string steamDir, string cacheDir, int concurrency = 6, ICoverDownloader? downloader = null)
	{
		_resolver = new SteamCoverResolver(steamDir ?? "");
		_cacheDir = string.IsNullOrWhiteSpace(cacheDir)
			? Path.Combine(AppHelper.GetAppDirectory(), "cache")
			: cacheDir;
		_downloader = downloader ?? new CoverDownloader();
		_concurrency = Math.Max(1, concurrency);
	}

	private static string CacheFileFor(string cacheDir, int appId) => Path.Combine(cacheDir, appId + ".jpg");

	public async Task<CoverPreloadResult> RunAsync(
		IReadOnlyList<CoverTarget> targets,
		IProgress<CoverProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		var eligible = new List<CoverTarget>(targets.Count);
		foreach (CoverTarget t in targets)
		{
			if (t.AppId > 0 && CoverEligibility.IsGameLike(t.Type))
				eligible.Add(t);
		}

		int total = eligible.Count;
		int already = 0;
		int downloaded = 0;
		int failed = 0;
		int processed = 0;

		// 阶段一：串行识别（resolver 内部缓存非线程安全，仅在单线程访问）
		var toDownload = new List<CoverTarget>();
		foreach (CoverTarget t in eligible)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string? portrait = _resolver.GetPortraitPath(t.AppId);
			if (portrait != null || File.Exists(CacheFileFor(_cacheDir, t.AppId)))
			{
				already++;
				progress?.Report(new CoverProgress(++processed, total, already, downloaded, failed, t.Name));
			}
			else
			{
				toDownload.Add(t);
			}
		}

		// 阶段二：分批并发下载
		for (int start = 0; start < toDownload.Count; start += _concurrency)
		{
			cancellationToken.ThrowIfCancellationRequested();
			int end = Math.Min(start + _concurrency, toDownload.Count);
			var batch = new List<Task>(end - start);
			for (int i = start; i < end; i++)
			{
				CoverTarget t = toDownload[i];
				batch.Add(Task.Run(async () =>
				{
					string file = CacheFileFor(_cacheDir, t.AppId);
					try
					{
						bool ok = await _downloader.DownloadPortraitAsync(t.AppId, file, cancellationToken).ConfigureAwait(false);
						if (ok) Interlocked.Increment(ref downloaded);
						else Interlocked.Increment(ref failed);
					}
					catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
					{
						Interlocked.Increment(ref failed);
					}
					catch (Exception ex)
					{
						Log.WriteLine("CoverPreloader: download failed for {0} ({1}): {2}", t.AppId, t.Name, ex.Message);
						Interlocked.Increment(ref failed);
					}
					finally
					{
						int p = Interlocked.Increment(ref processed);
						progress?.Report(new CoverProgress(p, total, Volatile.Read(ref already),
							Volatile.Read(ref downloaded), Volatile.Read(ref failed), t.Name));
					}
				}, cancellationToken));
			}
			await Task.WhenAll(batch).ConfigureAwait(false);
		}

		return new CoverPreloadResult(total, already, downloaded, failed);
	}
}
