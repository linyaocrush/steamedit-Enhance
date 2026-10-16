#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

/// <summary>
/// <see cref="CoverDownloader"/> 现在只是 Rust 内核 (<c>ste_net_cover_download</c>) 的薄封装：
/// HTTP、多源兜底、图片头校验与原子写入都在原生侧，并由 Rust 单元/集成测试（内含本地
/// HTTP 模拟服务器）覆盖。这里只验证无需联网的入口守卫。
/// </summary>
[TestClass]
public class CoverDownloaderTests
{
    private static string _dir = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "steamedit-downloader-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }
        catch { /* ignore */ }
    }

    [TestMethod]
    public async Task Download_NonPositiveAppId_ReturnsFalse()
    {
        var downloader = new CoverDownloader();
        Assert.IsFalse(await downloader.DownloadPortraitAsync(0, Path.Combine(_dir, "x.jpg")));
        Assert.IsFalse(await downloader.DownloadPortraitAsync(-1, Path.Combine(_dir, "x.jpg")));
    }

    [TestMethod]
    public async Task Download_EmptyDest_ReturnsFalse()
    {
        var downloader = new CoverDownloader();
        Assert.IsFalse(await downloader.DownloadPortraitAsync(440, ""));
    }

    [TestMethod]
    public async Task Download_Cancelled_Throws()
    {
        var downloader = new CoverDownloader();
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => downloader.DownloadPortraitAsync(440, Path.Combine(_dir, "x.jpg"), cts.Token));
    }
}
