#nullable enable
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common;

namespace steamedit_Enhance.Data;

/// <summary>
/// 从 Steam 官方商店接口获取某游戏某语言的官方名称。
/// 只读网络操作：GET store.steampowered.com/api/appdetails。失败返回 null，绝不抛给调用方。
/// </summary>
internal sealed class OfficialNameFetcher
{
	private static readonly SocketsHttpHandler _sharedHandler = new()
	{
		PooledConnectionLifetime = TimeSpan.FromMinutes(5),
		AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
	};

	private readonly HttpClient _httpClient;

	public OfficialNameFetcher(HttpClient? httpClient = null)
	{
		_httpClient = httpClient ?? CreateDefaultClient();
	}

	private static HttpClient CreateDefaultClient()
	{
		var client = new HttpClient(_sharedHandler) { Timeout = TimeSpan.FromSeconds(20) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd("steamedit-Enhance");
		return client;
	}

	/// <summary>取官方名称；无商店页面/网络失败/被限流均返回 null。</summary>
	internal async Task<string?> FetchNameAsync(int appId, string locale, CancellationToken cancellationToken = default)
	{
		if (appId <= 0) return null;
		string url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l={locale}";
		try
		{
			using var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
			if (!resp.IsSuccessStatusCode)
			{
				if ((int)resp.StatusCode >= 500 || resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
					Log.WriteLine("OfficialNameFetcher: HTTP {0} for app {1}", (int)resp.StatusCode, appId);
				return null;
			}
			string json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
			return ParseName(json, appId);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return null;
		}
		catch (Exception ex)
		{
			Log.WriteLine("OfficialNameFetcher: failed for app {0}: {1}", appId, ex.Message);
			return null;
		}
	}

	internal static string? ParseName(string json, int appId)
	{
		try
		{
			using var doc = JsonDocument.Parse(json);
			string key = appId.ToString(CultureInfo.InvariantCulture);
			if (doc.RootElement.ValueKind != JsonValueKind.Object)
				return null;
			if (!doc.RootElement.TryGetProperty(key, out var item) || item.ValueKind != JsonValueKind.Object)
				return null;
			if (!item.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True)
				return null;
			if (!item.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
				return null;
			if (!data.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
				return null;
			string? result = name.GetString();
			return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
		}
		catch (JsonException ex)
		{
			Log.WriteLine("OfficialNameFetcher: JSON parse failed for app {0}: {1}", appId, ex.Message);
			return null;
		}
	}
}
