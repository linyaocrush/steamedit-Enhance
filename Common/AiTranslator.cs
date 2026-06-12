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
	private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

	public static async Task<List<string>> FetchModelsAsync(string apiUrl, string apiKey, CancellationToken cancellationToken = default)
	{
		apiUrl = apiUrl.TrimEnd('/');

		// Strip /chat/completions to get base URL, then call /models
		string baseUrl = apiUrl;
		if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
			baseUrl = baseUrl[..^"/chat/completions".Length];

		string modelsUrl = baseUrl + "/models";
		Log.WriteLine("AI: Fetching models from {0}", modelsUrl);

		using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
		if (!string.IsNullOrWhiteSpace(apiKey))
			request.Headers.Add("Authorization", $"Bearer {apiKey}");

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			Log.WriteLine("AI: Models fetch failed: {0}", (int)response.StatusCode);
			throw new Exception($"API error {(int)response.StatusCode}: {responseBody}");
		}

		var models = new List<string>();
		using var doc = JsonDocument.Parse(responseBody);
		if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in data.EnumerateArray())
			{
				if (item.TryGetProperty("id", out var idProp))
				{
					string id = idProp.GetString();
					if (!string.IsNullOrEmpty(id))
						models.Add(id);
				}
			}
		}
		else
		{
			Log.WriteLine("AI: Unexpected models response format");
		}
		models.Sort(StringComparer.OrdinalIgnoreCase);
		Log.WriteLine("AI: Got {0} models", models.Count);
		return models;
	}

	public static async Task<string> TranslateAsync(string gameName, string targetLanguage, string glossary = null, CancellationToken cancellationToken = default)
	{
		string apiUrl = Settings.Default.AiApiUrl?.TrimEnd('/');
		string apiKey = Settings.Default.AiApiKey;

		if (string.IsNullOrWhiteSpace(apiUrl))
			throw new InvalidOperationException("AI API URL is not configured.");

		if (!apiUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
			apiUrl += "/chat/completions";

		string model = Settings.Default.AiModel;
		if (string.IsNullOrWhiteSpace(model)) model = "gpt-4o-mini";

		Log.WriteLine("AI: Translating \"{0}\" -> {1} (model={2})", gameName, targetLanguage, model);

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

		if (!string.IsNullOrWhiteSpace(glossary))
		{
			systemPrompt += $@"

The following is a glossary of game names the user has already translated. Follow their translation style and terminology strictly:
{glossary}";
			Log.WriteLine("AI: Using glossary with {0} chars", glossary.Length);
		}

		string requestBody = JsonSerializer.Serialize(new
		{
			model,
			messages = new[]
			{
				new { role = "system", content = systemPrompt },
				new { role = "user", content = gameName }
			},
			max_tokens = 4096,
			temperature = 0.1
		});

		Log.WriteLine("AI: Requesting {0}", apiUrl);

		using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
		if (!string.IsNullOrWhiteSpace(apiKey))
			request.Headers.Add("Authorization", $"Bearer {apiKey}");
		request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

		using var response = await _httpClient.SendAsync(request, cancellationToken);
		string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

		Log.WriteLine("AI: Response {0}, body length={1}", (int)response.StatusCode, responseBody.Length);

		if (!response.IsSuccessStatusCode)
		{
			Log.WriteLine("AI: Translation failed: {0}", responseBody.Length > 200 ? responseBody[..200] : responseBody);
			throw new Exception($"API error {(int)response.StatusCode}: {responseBody}");
		}

		using var doc = JsonDocument.Parse(responseBody);
		string result = null;

		try
		{
			var choices = doc.RootElement.GetProperty("choices");
			if (choices.GetArrayLength() > 0)
			{
				var choice = choices[0];
				// Try OpenAI format: choices[0].message.content
				if (choice.TryGetProperty("message", out var message))
				{
					result = message.GetProperty("content").GetString();
				}
				// Some APIs use choices[0].text (legacy completions format)
				else if (choice.TryGetProperty("text", out var text))
				{
					result = text.GetString();
				}
			}
		}
		catch (Exception ex)
		{
			Log.WriteLine("AI: Failed to parse response: {0}", ex.Message);
		}

		result = result?.Trim().Trim('"', '\'', '\n', '\r') ?? "";
		Log.WriteLine("AI: Result: \"{0}\"", result);
		return result;
	}
}
