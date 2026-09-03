#nullable enable
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace steamedit_Enhance.Data;

/// <summary>
/// 从 Steam 官方 CDN 下载竖版封面（library_600x900.jpg），下载成功写入目标文件。
/// 只写本地文件，是只读网络操作；失败/无效内容返回 false，不会留下半成品文件。
/// </summary>
internal sealed class CoverDownloader
{
	private static readonly string[] s_hosts =
	{
		"https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/library_600x900.jpg",
		"https://cdn.akamai.steamstatic.com/steam/apps/{0}/library_600x900.jpg"
	};

	private static readonly SocketsHttpHandler _sharedHandler = new()
	{
		PooledConnectionLifetime = TimeSpan.FromMinutes(5)
	};

	private readonly HttpClient _httpClient;

	public CoverDownloader(HttpClient? httpClient = null)
	{
		_httpClient = httpClient ?? CreateDefaultClient();
	}

	private static HttpClient CreateDefaultClient()
	{
		var client = new HttpClient(_sharedHandler) { Timeout = TimeSpan.FromSeconds(10) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd("steamedit-Enhance");
		return client;
	}

	/// <summary>下载竖封面到 destFile。成功返回 true；失败（404/网络/无效图片）返回 false。</summary>
	internal async Task<bool> DownloadPortraitAsync(int appId, string destFile, CancellationToken cancellationToken = default)
	{
		if (appId <= 0)
			return false;

		byte[]? bytes = null;
		foreach (string host in s_hosts)
		{
			cancellationToken.ThrowIfCancellationRequested();
			try
			{
				string url = string.Format(host, appId);
				bytes = await _httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
				if (IsValidImage(bytes))
					break;
				bytes = null; // 不是图片内容，尝试下一个源
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
			{
				if (cancellationToken.IsCancellationRequested)
					throw;
				Log.WriteLine("CoverDownloader: host {0} failed for app {1}: {2}", host, appId, ex.Message);
				bytes = null;
			}
		}

		if (bytes == null)
			return false;

		return WriteAtomically(destFile, bytes);
	}

	private static bool WriteAtomically(string destFile, byte[] bytes)
	{
		string dir = Path.GetDirectoryName(destFile) ?? "";
		string tmp = destFile + ".tmp";
		try
		{
			Directory.CreateDirectory(dir);
			File.WriteAllBytes(tmp, bytes);
			File.Move(tmp, destFile, overwrite: true);
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Log.WriteLine("CoverDownloader: failed to write {0}: {1}", destFile, ex.Message);
			try { if (File.Exists(tmp)) File.Delete(tmp); } catch (Exception) { }
			return false;
		}
	}

	internal static bool IsValidImage(byte[] data)
	{
		if (data == null || data.Length < 8)
			return false;
		// JPEG: FF D8 FF
		if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
			return true;
		// PNG: 89 50 4E 47 0D 0A 1A 0A
		if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
			data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
			return true;
		return false;
	}
}
