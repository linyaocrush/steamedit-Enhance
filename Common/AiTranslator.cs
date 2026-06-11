using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Common;

internal static class AiTranslator
{
	private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

	public static async Task<List<string>> FetchModelsAsync(CancellationToken cancellationToken = default)
	{
		string apiUrl = Settings.Default.AiApiUrl.TrimEnd('/');
		string apiKey = Settings.Default.AiApiKey;

		// Strip /chat/completions to get base URL, then call /models
		string baseUrl = apiUrl;
		if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
			baseUrl = baseUrl[..^"/chat/completions".Length];

		string modelsUrl = baseUrl + "/models";

		using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
		if (!string.IsNullOrWhiteSpace(apiKey))
			request.Headers.Add("Authorization", $"Bearer {apiKey}");

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
			throw new Exception($"API error {(int)response.StatusCode}: {responseBody}");

		var models = new List<string>();
		using var doc = JsonDocument.Parse(responseBody);
		foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
		{
			string id = item.GetProperty("id").GetString();
			if (!string.IsNullOrEmpty(id))
				models.Add(id);
		}
		models.Sort(StringComparer.OrdinalIgnoreCase);
		return models;
	}

	public static async Task<string> TranslateAsync(string gameName, string targetLanguage, CancellationToken cancellationToken = default)
	{
		string apiUrl = Settings.Default.AiApiUrl.TrimEnd('/');
		string apiKey = Settings.Default.AiApiKey;

		if (!apiUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
			apiUrl += "/chat/completions";

		if (string.IsNullOrWhiteSpace(apiUrl))
			throw new InvalidOperationException("AI API URL is not configured.");

		string model = Settings.Default.AiModel;
		if (string.IsNullOrWhiteSpace(model)) model = "gpt-4o-mini";

		string langDisplay = targetLanguage switch
		{
			"zh-CN" => "Simplified Chinese",
			"en" => "English",
			_ => targetLanguage
		};

		string systemPrompt = $@"You are a game name translator. Translate the given game name into {langDisplay}.

Rules:
- Output ONLY the translated name, nothing else
- Do NOT add quotes, explanations, prefixes, or any extra text
- If the name is already in {langDisplay}, output it unchanged
- Keep brand names, acronyms, and proper nouns unchanged (e.g., GTA, FIFA, Dark Souls, Resident Evil)
- If the name contains both a brand and a subtitle, translate ONLY the subtitle
- Keep numbers and punctuation as-is";

		string requestBody = JsonSerializer.Serialize(new
		{
			model,
			messages = new[]
			{
				new { role = "system", content = systemPrompt },
				new { role = "user", content = gameName }
			},
			max_tokens = 200,
			temperature = 0.1
		});

		using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
		request.Headers.Add("Authorization", $"Bearer {apiKey}");
		request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
			throw new Exception($"API error {(int)response.StatusCode}: {responseBody}");

		using var doc = JsonDocument.Parse(responseBody);
		string result = doc.RootElement
			.GetProperty("choices")[0]
			.GetProperty("message")
			.GetProperty("content")
			.GetString()
			?.Trim()
			?.Trim('"', '\'', '\n', '\r')
			?? "";

		return result;
	}
}
