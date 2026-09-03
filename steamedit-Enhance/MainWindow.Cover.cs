#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;
using steamedit_Enhance.Data;
using steamedit_Enhance.Properties;

namespace steamedit_Enhance;

partial class MainWindow
{
	private static readonly string CoverCacheDir = Path.Combine(AppHelper.GetAppDirectory(), "cache");

	private SteamCoverResolver _coverResolver = null!;
	private readonly Dictionary<int, Image> _coverImageCache = new();
	private readonly Dictionary<int, string> _coverSources = new();
	private readonly List<int> _coverOrder = new();
	private const int CoverCacheLimit = 16;
	private const int CoverLeft = 14;
	private const int CoverTop = 195;
	private const int CoverBottomMargin = 10;
	private const int CoverMaxHeight = 128;
	private CancellationTokenSource? _coverLoadCts;
	private string? _coverPath;
	private int? _coverCurrentAppId;

	// 封面渲染管线：_coverSource 为缓存里的原始图，_coverDisplay 为按上屏尺寸预渲染的副本(本类持有并负责 Dispose)。
	// 放大用 Lanczos-3、缩小时高质量双三次，避免 PictureBox Zoom 的低质量插值。
	private Image? _coverSource;
	private Image? _coverDisplay;
	private Size _coverDisplaySize;

	private readonly HashSet<int> _coverDownloading = new();
	private readonly HashSet<int> _coverDownloadFailed = new();
	private int _coverConsecutiveFailures;
	private const int CoverDownloadFailLimit = 5;
	private bool _coverOnlineDisabled;
	private CoverDownloader? _coverDownloader;

	private static string CoverCacheFile(int appId) => Path.Combine(CoverCacheDir, appId + ".jpg");

	private static bool IsCoverDownloadCandidate(AppInfoEntryType type)
		=> type is AppInfoEntryType.Game or AppInfoEntryType.Demo
			or AppInfoEntryType.DLC or AppInfoEntryType.Application;

	/// <summary>
	/// 根据宽高比动态调整预览框：横图 → 宽矮，竖图 → 高窄，
	/// 在右侧面板空白区(按钮行下方)内尽量放大并水平居中。
	/// </summary>
	private void LayoutCoverPreview()
	{
		if (IsDisposed || panel1.ClientSize.Width <= 0 || panel1.ClientSize.Height <= 0)
			return;

		int availW = Math.Max(40, panel1.ClientSize.Width - CoverLeft * 2);
		int availH = Math.Max(40, panel1.ClientSize.Height - CoverTop - CoverBottomMargin);

		Size size;
		if (_coverSource == null)
		{
			size = new Size(availW, Math.Min(availH, CoverMaxHeight));
			EnsureDisplayReady(Size.Empty);
		}
		else
		{
			size = FitCoverSize(_coverSource.Width, _coverSource.Height, availW, availH);
			EnsureDisplayReady(size);
		}

		int x = CoverLeft + Math.Max(0, (availW - size.Width) / 2);
		picPreview.SetBounds(x, CoverTop, size.Width, size.Height);
	}

	/// <summary>
	/// 把当前原始图预渲染为指定显示尺寸并挂到 picPreview（无图 target 传 Size.Empty 清空）。
	/// 放大 → Lanczos-3；缩小/等大 → 高质量双三次/拷贝。仅在目标尺寸变化时重建，避免缩放拖拽时反复重渲。
	/// </summary>
	private void EnsureDisplayReady(Size target)
	{
		if (_coverSource == null)
		{
			if (_coverDisplay != null)
			{
				_coverDisplay.Dispose();
				_coverDisplay = null;
			}
			_coverDisplaySize = Size.Empty;
			if (picPreview.Image != null)
				picPreview.Image = null;
			return;
		}

		// 有图但尺寸未变 → 无需重建；仅确保上屏引用正确
		if (_coverDisplay != null && _coverDisplaySize == target)
		{
			if (!ReferenceEquals(picPreview.Image, _coverDisplay))
				picPreview.Image = _coverDisplay;
			return;
		}

		// 与上次渲染相差很小(拖拽缩放过程)时沿用旧渲染，避免逐帧重建
		if (_coverDisplay != null && _coverDisplaySize.Width > 0 &&
			Math.Abs(target.Width - _coverDisplaySize.Width) < 4 &&
			Math.Abs(target.Height - _coverDisplaySize.Height) < 4)
		{
			if (!ReferenceEquals(picPreview.Image, _coverDisplay))
				picPreview.Image = _coverDisplay;
			return;
		}

		Bitmap? rendered = null;
		try
		{
			rendered = ImageScaler.Resize(_coverSource, target.Width, target.Height);
		}
		catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or InvalidOperationException)
		{
			Log.WriteLine("Cover preview resize failed: {0}", ex.Message);
		}

		if (rendered != null)
		{
			Image? old = _coverDisplay;
			_coverDisplay = rendered;
			_coverDisplaySize = target;
			picPreview.Image = rendered;
			old?.Dispose();
		}
		else
		{
			// 渲染失败时退回原图（由缓存管理生命周期，本类不释放）
			if (!ReferenceEquals(picPreview.Image, _coverSource))
			{
				if (_coverDisplay != null)
				{
					_coverDisplay.Dispose();
					_coverDisplay = null;
				}
				_coverDisplaySize = Size.Empty;
				picPreview.Image = _coverSource;
			}
		}
	}

	/// <summary>
	/// 在 (availW, availH) 内按原宽高比放大封面，返回不超过可用空间的最大尺寸。
	/// 纯计算，便于单元测试。
	/// </summary>
	internal static Size FitCoverSize(int imgW, int imgH, int availW, int availH)
	{
		float aspect = (float)imgW / imgH;
		int w = availW;
		int h = (int)Math.Round(w / aspect);
		if (h > availH)
		{
			h = availH;
			w = Math.Max(24, (int)Math.Round(h * aspect));
		}
		return new Size(Math.Max(24, w), Math.Max(24, h));
	}

	/// <summary>
	/// 选中条目变化时刷新封面预览。id 为 null 表示没有选中条目。
	/// 策略：本地竖版 → 在线缓存竖版 → 本地横版(临时) → 在线下载竖版兜底。
	/// </summary>
	private void UpdateCoverForSelection(int? appId, AppInfoEntryType entryType = AppInfoEntryType.Unknown)
	{
		_coverCurrentAppId = appId;

		if (_coverLoadCts != null)
		{
			_coverLoadCts.Cancel();
			_coverLoadCts.Dispose();
			_coverLoadCts = null;
		}

		if (appId == null)
		{
			SetCover(null, null);
			return;
		}

		if (_coverImageCache.TryGetValue(appId.Value, out Image? cached))
		{
			string? desired = _coverResolver.GetPortraitPath(appId.Value) ?? CoverCacheFileIfExists(appId.Value);
			string? source = _coverSources.TryGetValue(appId.Value, out string? s) ? s : null;

			// 缓存的是较早的横版，现在竖版已可用(在线下载完成/后补) → 丢弃缓存重载竖版
			if (desired != null && source != null &&
				!string.Equals(desired, source, StringComparison.OrdinalIgnoreCase))
			{
				DropCoverFromCache(appId.Value);
			}
			else
			{
				TouchCache(appId.Value);
				SetCover(cached, desired ?? source);
				return;
			}
		}

		// 1) 本地竖版封面
		string? portrait = _coverResolver.GetPortraitPath(appId.Value);
		if (portrait != null)
		{
			SetCover(null, portrait);
			_ = LoadCoverAsync(appId.Value, portrait, Token());
			return;
		}

		// 2) 在线缓存（之前下载好的竖版）
		string? cachedOnline = CoverCacheFileIfExists(appId.Value);
		if (cachedOnline != null)
		{
			SetCover(null, cachedOnline);
			_ = LoadCoverAsync(appId.Value, cachedOnline, Token());
			return;
		}

		bool candidate = IsCoverDownloadCandidate(entryType);

		// 3) 本地横版临时显示，同时后台下载竖版
		string? wide = _coverResolver.GetWidePath(appId.Value);
		if (wide != null)
		{
			SetCover(null, wide);
			_ = LoadCoverAsync(appId.Value, wide, Token());
			if (candidate)
				StartCoverDownload(appId.Value);
			return;
		}

		// 4) 本地无图 → 占位 + 在线下载
		SetCover(null, null);
		if (candidate)
			StartCoverDownload(appId.Value);
	}

	private CancellationToken Token() => (_coverLoadCts ??= new CancellationTokenSource()).Token;

	private void StartCoverDownload(int appId)
	{
		if (_coverOnlineDisabled || _coverDownloading.Contains(appId) || _coverDownloadFailed.Contains(appId))
			return;
		if (File.Exists(CoverCacheFile(appId)))
			return;

		_coverDownloading.Add(appId);
		_ = DownloadCoverAsync(appId);
	}

	private async Task DownloadCoverAsync(int appId)
	{
		CancellationToken token = Token();
		try
		{
			_coverDownloader ??= new CoverDownloader();
			bool ok = await Task.Run(() => _coverDownloader.DownloadPortraitAsync(appId, CoverCacheFile(appId), token), token).ConfigureAwait(true);

			if (ok && !token.IsCancellationRequested && !IsDisposed)
			{
				_coverConsecutiveFailures = 0;
				_coverOnlineDisabled = false;
				// 下载完成且用户仍停留在此条目 → 丢弃内存中已有的(横版)缓存，
				// 重新求值，让刚下载好的竖版能立刻替换显示（否则会被缓存短路，需重启才生效）
				if (_coverCurrentAppId == appId)
				{
					DropCoverFromCache(appId);
					UpdateCoverForSelection(appId, AppInfoEntryType.Unknown);
				}
			}
			else if (!ok && !token.IsCancellationRequested && !IsDisposed)
			{
				_coverDownloadFailed.Add(appId);
				_coverConsecutiveFailures++;
				if (_coverConsecutiveFailures >= CoverDownloadFailLimit)
				{
					_coverOnlineDisabled = true;
					Log.WriteLine("CoverDownloader: {0} consecutive failures, disabled auto download for this session", _coverConsecutiveFailures);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			_coverDownloading.Remove(appId);
		}
	}

	private string? CoverCacheFileIfExists(int appId)
	{
		string file = CoverCacheFile(appId);
		try
		{
			return File.Exists(file) ? file : null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private async Task LoadCoverAsync(int appId, string path, CancellationToken token)
	{
		Image? image = null;
		try
		{
			image = await Task.Run(() => Image.FromFile(path), token).ConfigureAwait(true);
			if (token.IsCancellationRequested || IsDisposed)
				return;

			if (_coverImageCache.ContainsKey(appId))
			{
				// 并发加载中另一路已先完成并缓存
				TouchCache(appId);
				SetCover(_coverImageCache[appId], path);
				return;
			}

			CacheCover(appId, image, path);
			image = null; // 图片已交由缓存管理
			SetCover(_coverImageCache[appId], path);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			// 文件损坏 / 非图片 / 读取失败等，仅记录，不打断界面
			if (!token.IsCancellationRequested && !IsDisposed)
			{
				Log.WriteLine("Cover preview failed to load '{0}': {1}", path, ex.Message);
				SetCover(null, path);
			}
		}
		finally
		{
			image?.Dispose();
		}
	}

	private void CacheCover(int appId, Image image, string sourcePath)
	{
		if (_coverImageCache.ContainsKey(appId))
			return;
		_coverImageCache[appId] = image;
		_coverSources[appId] = sourcePath;
		_coverOrder.Add(appId);

		while (_coverOrder.Count > CoverCacheLimit)
		{
			int oldest = _coverOrder[0];
			_coverOrder.RemoveAt(0);
			if (_coverImageCache.Remove(oldest, out Image? evicted))
			{
				_coverSources.Remove(oldest);
				// 当前展示的原始图正被预览引用(渲染失败时的兜底路径)，暂不释放，等 SetCover 换源时回收
				if (!ReferenceEquals(_coverSource, evicted))
					evicted.Dispose();
			}
		}
	}

	private void TouchCache(int appId)
	{
		if (_coverOrder.Remove(appId))
			_coverOrder.Add(appId);
	}

	/// <summary>
	/// 从内存缓存移除某 App 的图片（使其可被重新求值）。
	/// 不在此处 Dispose：若该图正被显示，交给 SetCover 在替换时释放。
	/// </summary>
	private void DropCoverFromCache(int appId)
	{
		_coverOrder.Remove(appId);
		_coverSources.Remove(appId);
		_coverImageCache.Remove(appId);
	}

	private void SetCover(Image? image, string? path)
	{
		if (IsDisposed)
			return;
		_coverPath = path;
		toolTips.SetToolTip(picPreview, path ?? "");
		if (!ReferenceEquals(image, _coverSource))
		{
			Image? oldSource = _coverSource;
			_coverSource = image;
			_coverDisplaySize = Size.Empty; // 强制按当前框尺寸重建
			// 被替换的原始图若已不在缓存(DropCoverFromCache 移除)则由本处回收
			if (oldSource != null && oldSource != image && !_coverImageCache.ContainsValue(oldSource))
				oldSource.Dispose();
		}
		LayoutCoverPreview();
		picPreview.Invalidate();
	}

	private void picPreview_Paint(object? sender, PaintEventArgs e)
	{
		var pb = (PictureBox)sender!;
		Graphics g = e.Graphics;

		using (var pen = new Pen(ThemeManager.BorderColor))
			g.DrawRectangle(pen, 0, 0, pb.Width - 1, pb.Height - 1);

		if (pb.Image == null)
		{
			using var back = new SolidBrush(ThemeManager.ListAlternateBackColor);
			g.FillRectangle(back, 1, 1, pb.Width - 2, pb.Height - 2);
			Rectangle textRect = new(0, 0, pb.Width, pb.Height);
			TextRenderer.DrawText(g, Strings.MainWindow_NoCover, pb.Font, textRect,
				ThemeManager.SecondaryText,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
	}

	private void picPreview_DoubleClick(object? sender, EventArgs e)
	{
		if (string.IsNullOrEmpty(_coverPath) || !File.Exists(_coverPath))
			return;
		try
		{
			Process.Start(new ProcessStartInfo("explorer.exe")
			{
				Arguments = $"/select,\"{_coverPath}\"",
				UseShellExecute = true
			})?.Dispose();
		}
		catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
		{
			Log.WriteLine("Failed to open cover folder: {0}", ex.Message);
		}
	}
}
