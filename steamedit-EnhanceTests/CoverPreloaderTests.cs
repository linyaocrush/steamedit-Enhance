#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class CoverPreloaderTests
{
    private static string _root = null!;

    private static readonly byte[] s_jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "steamedit-preloader-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch { /* ignore */ }
    }

    private static string SteamDir => Path.Combine(_root, "steam");
    private static string CacheDir => Path.Combine(_root, "cache");

    private static void WriteFile(string relativePath, byte[] content)
    {
        string full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
    }

    private static CoverTarget Target(int id, string name, AppInfoEntryType type) => new(id, name, type);

    [TestMethod]
    public async Task Run_ClassifiesAndDownloads()
    {
        // app 10: 本地已有竖版封面
        WriteFile(@"steam\appcache\librarycache\10\library_600x900.jpg", s_jpeg);
        // app 20: 无本地封面但 cache 目录已有下载文件
        WriteFile(@"cache\20.jpg", s_jpeg);
        // app 30 / 40 / 50: 需要下载

        var targets = new List<CoverTarget>
        {
            Target(10, "Local Game", AppInfoEntryType.Game),
            Target(20, "Cached Game", AppInfoEntryType.Demo),
            Target(30, "Needs Download", AppInfoEntryType.DLC),
            Target(40, "Will 404", AppInfoEntryType.Game),
            Target(50, "Application", AppInfoEntryType.Application),
            // 非候选类型应被排除
            Target(60, "Tool", AppInfoEntryType.Tool),
            Target(0, "Bad Id", AppInfoEntryType.Game),
        };

        var mock = new MockHttpHandler(request =>
        {
            string url = request.RequestUri!.ToString();
            if (url.Contains("/40/"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(s_jpeg) };
        });
        using var client = new HttpClient(mock);
        var preloader = new CoverPreloader(SteamDir, CacheDir, concurrency: 2, httpClient: client);

        var result = await preloader.RunAsync(targets);

        Assert.AreEqual(5, result.Total);          // 10,20,30,40,50
        Assert.AreEqual(2, result.Already);        // 10(local), 20(cached)
        Assert.AreEqual(2, result.Downloaded);     // 30,50 成功
        Assert.AreEqual(1, result.Failed);         // 40 返回 404
        Assert.IsTrue(File.Exists(Path.Combine(CacheDir, "30.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(CacheDir, "50.jpg")));
        Assert.IsFalse(File.Exists(Path.Combine(CacheDir, "40.jpg")));
        Assert.IsFalse(File.Exists(Path.Combine(CacheDir, "60.jpg")));
    }

    [TestMethod]
    public async Task Run_EmptyTargets_ReturnsZero()
    {
        var preloader = new CoverPreloader(SteamDir, CacheDir);
        var result = await preloader.RunAsync(new List<CoverTarget>());
        Assert.AreEqual(0, result.Total);
        Assert.AreEqual(0, result.Already);
        Assert.AreEqual(0, result.Downloaded);
        Assert.AreEqual(0, result.Failed);
    }

    [TestMethod]
    public async Task Run_AllOffline_FailedCounts()
    {
        var targets = new List<CoverTarget> { Target(1, "A", AppInfoEntryType.Game) };
        var mock = new MockHttpHandler(_ => throw new HttpRequestException("offline"));
        using var client = new HttpClient(mock);
        var preloader = new CoverPreloader(SteamDir, CacheDir, httpClient: client);

        var result = await preloader.RunAsync(targets);
        Assert.AreEqual(1, result.Failed);
        Assert.AreEqual(0, result.Downloaded);
    }

    [TestMethod]
    public async Task Run_AlreadyHasLocalPortrait_NoDownloadAttempted()
    {
        WriteFile(@"steam\appcache\librarycache\77\library_600x900.jpg", s_jpeg);
        int calls = 0;
        var mock = new MockHttpHandler(_ => { calls++; return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(s_jpeg) }; });
        using var client = new HttpClient(mock);
        var preloader = new CoverPreloader(SteamDir, CacheDir, httpClient: client);

        var result = await preloader.RunAsync(new List<CoverTarget> { Target(77, "Owned", AppInfoEntryType.Game) });
        Assert.AreEqual(1, result.Already);
        Assert.AreEqual(0, calls);
    }
}
