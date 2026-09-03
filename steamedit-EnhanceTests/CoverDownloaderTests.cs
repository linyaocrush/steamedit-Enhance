#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

[TestClass]
public class CoverDownloaderTests
{
    private static string _dir = null!;

    private static readonly byte[] s_jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    private static readonly byte[] s_png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
    private static readonly byte[] s_text = Encoding.UTF8.GetBytes("<!DOCTYPE html><html>404</html>");

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

    private static HttpResponseMessage BytesResponse(byte[] body)
        => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body)
        };

    [TestMethod]
    public async Task Download_SuccessWritesFile()
    {
        var mock = new MockHttpHandler(_ => BytesResponse(s_jpeg));
        using var client = new HttpClient(mock);
        var downloader = new CoverDownloader(client);

        string dest = Path.Combine(_dir, "440.jpg");
        bool ok = await downloader.DownloadPortraitAsync(440, dest);

        Assert.IsTrue(ok);
        Assert.IsTrue(File.Exists(dest));
        CollectionAssert.AreEqual(s_jpeg, File.ReadAllBytes(dest));
    }

    [TestMethod]
    public async Task Download_FirstHostInvalid_FallsBackToSecond()
    {
        var mock = new MockHttpHandler(request =>
            request.RequestUri!.Host.Contains("akamai") ? BytesResponse(s_jpeg) : BytesResponse(s_text));
        using var client = new HttpClient(mock);
        var downloader = new CoverDownloader(client);

        string dest = Path.Combine(_dir, "440.jpg");
        bool ok = await downloader.DownloadPortraitAsync(440, dest);

        Assert.IsTrue(ok);
        Assert.IsTrue(File.Exists(dest));
    }

    [TestMethod]
    public async Task Download_AllHostsNotFound_ReturnsFalseNoFile()
    {
        var mock = new MockHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(mock);
        var downloader = new CoverDownloader(client);

        string dest = Path.Combine(_dir, "999999.jpg");
        bool ok = await downloader.DownloadPortraitAsync(999999, dest);

        Assert.IsFalse(ok);
        Assert.IsFalse(File.Exists(dest));
    }

    [TestMethod]
    public async Task Download_InvalidContent_ReturnsFalseNoFile()
    {
        var mock = new MockHttpHandler(_ => BytesResponse(s_text));
        using var client = new HttpClient(mock);
        var downloader = new CoverDownloader(client);

        string dest = Path.Combine(_dir, "440.jpg");
        bool ok = await downloader.DownloadPortraitAsync(440, dest);

        Assert.IsFalse(ok);
        Assert.IsFalse(File.Exists(dest));
    }

    [TestMethod]
    public async Task Download_NetworkFailure_ReturnsFalse()
    {
        var mock = new MockHttpHandler(_ => throw new HttpRequestException("offline"));
        using var client = new HttpClient(mock);
        var downloader = new CoverDownloader(client);

        bool ok = await downloader.DownloadPortraitAsync(440, Path.Combine(_dir, "440.jpg"));
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public async Task Download_NonPositiveAppId_ReturnsFalse()
    {
        var mock = new MockHttpHandler(_ => BytesResponse(s_jpeg));
        using var client = new HttpClient(mock);
        var downloader = new CoverDownloader(client);

        Assert.IsFalse(await downloader.DownloadPortraitAsync(0, Path.Combine(_dir, "x.jpg")));
        Assert.IsFalse(await downloader.DownloadPortraitAsync(-1, Path.Combine(_dir, "x.jpg")));
    }

    [TestMethod]
    public void IsValidImage_DetectsFormats()
    {
        Assert.IsTrue(CoverDownloader.IsValidImage(s_jpeg));
        Assert.IsTrue(CoverDownloader.IsValidImage(s_png));
        Assert.IsFalse(CoverDownloader.IsValidImage(s_text));
        Assert.IsFalse(CoverDownloader.IsValidImage(Array.Empty<byte>()));
        Assert.IsFalse(CoverDownloader.IsValidImage(new byte[] { 0xFF, 0xD8 }));
    }
}
