#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Common;

internal static class AiTranslator
{
	private static readonly HttpClient _httpClient = new HttpClient(
			new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
		{ Timeout = TimeSpan.FromSeconds(120) };

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

		HttpResponseMessage response;
		try
		{
			response = await _httpClient.SendAsync(request, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw; // 用户取消，直接传播
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			Log.WriteLine("AI: Network error fetching models: {0}", ex.Message);
			throw new AiTranslationException(AiErrorKind.NetworkError, $"Network error: {ex.Message}", innerException: ex);
		}
		string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			Log.WriteLine("AI: Models fetch failed: {0}", (int)response.StatusCode);
			throw new AiTranslationException(AiErrorKind.ApiError, $"API error {(int)response.StatusCode}: {responseBody}", (int)response.StatusCode);
		}

		var contentType = response.Content.Headers.ContentType?.MediaType;
		if (contentType != null && !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
		{
			Log.WriteLine("AI: Unexpected content type: {0}", contentType);
			throw new AiTranslationException(AiErrorKind.ParseError, $"Expected JSON response but got {contentType}");
		}

		var models = new List<string>();
		using var doc = JsonDocument.Parse(responseBody);
		if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in data.EnumerateArray())
			{
				if (item.TryGetProperty("id", out var idProp))
				{
					string? id = idProp.GetString();
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

	public const string DefaultSystemPrompt = @"You are a game name translator. Translate the given game name into {target_lang}.

Rules:
- Output ONLY the translated name, nothing else
- Do NOT add quotes, explanations, prefixes, or any extra text
- If the name is already in {target_lang}, output it unchanged
- Keep brand names, acronyms, and proper nouns unchanged (e.g., GTA, FIFA, Dark Souls, Resident Evil)
- If the name contains both a brand and a subtitle, translate ONLY the subtitle
- Keep numbers and punctuation as-is";

	public const string DefaultUserMessage = "{game_name}";

	private static async Task<string> CallChatCompletionAsync(
		string systemPrompt,
		string userMessage,
		string apiUrl,
		string apiKey,
		string model,
		int maxTokens = 4096,
		float temperature = 0.1f,
		CancellationToken cancellationToken = default)
	{
		apiUrl = apiUrl.TrimEnd('/');

		if (string.IsNullOrWhiteSpace(apiUrl))
			throw new InvalidOperationException("AI API URL is not configured.");

		if (!apiUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
			apiUrl += "/chat/completions";

		if (string.IsNullOrWhiteSpace(model)) model = "gpt-4o-mini";

		string requestBody = JsonSerializer.Serialize(new
		{
			model,
			messages = new[]
			{
				new { role = "system", content = systemPrompt },
				new { role = "user", content = userMessage }
			},
			max_tokens = maxTokens,
			temperature
		});

		Log.WriteLine("AI: Requesting {0}", apiUrl);

		const int maxRetries = 3;
		string responseBody = null!;
		for (int attempt = 0; ; attempt++)
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
			if (!string.IsNullOrWhiteSpace(apiKey))
				request.Headers.Add("Authorization", $"Bearer {apiKey}");
			request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

			HttpResponseMessage response;
			try
			{
				response = await _httpClient.SendAsync(request, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw; // 用户取消，直接传播
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
			{
				if (attempt < maxRetries - 1)
				{
					Log.WriteLine("AI: Network error (attempt {0}/{1}): {2}", attempt + 1, maxRetries, ex.Message);
					await Task.Delay(1000 << attempt, cancellationToken);
					continue;
				}
				Log.WriteLine("AI: Network error: {0}", ex.Message);
				throw new AiTranslationException(AiErrorKind.NetworkError, $"Network error: {ex.Message}", innerException: ex);
			}
			responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

			Log.WriteLine("AI: Response {0}, body length={1}", (int)response.StatusCode, responseBody.Length);

			if (!response.IsSuccessStatusCode)
			{
				int statusCode = (int)response.StatusCode;
				if ((statusCode == 429 || statusCode == 503) && attempt < maxRetries - 1)
				{
					Log.WriteLine("AI: Transient error {0} (attempt {1}/{2}), retrying...", statusCode, attempt + 1, maxRetries);
					await Task.Delay(1000 << attempt, cancellationToken);
					continue;
				}
				Log.WriteLine("AI: Request failed: {0}", responseBody.Length > 200 ? responseBody[..200] : responseBody);
				throw new AiTranslationException(AiErrorKind.ApiError, $"API error {statusCode}: {responseBody}", statusCode);
			}

			var ct = response.Content.Headers.ContentType?.MediaType;
			if (ct != null && !ct.Contains("json", StringComparison.OrdinalIgnoreCase))
			{
				Log.WriteLine("AI: Unexpected content type: {0}", ct);
				throw new AiTranslationException(AiErrorKind.ParseError, $"Expected JSON response but got {ct}");
			}

			break;
		}

		using var doc = JsonDocument.Parse(responseBody);
		string? result = null;

		try
		{
			var choices = doc.RootElement.GetProperty("choices");
			if (choices.GetArrayLength() > 0)
			{
				var choice = choices[0];
				if (choice.TryGetProperty("message", out var message))
				{
					result = message.GetProperty("content").GetString();
				}
				else if (choice.TryGetProperty("text", out var text))
				{
					result = text.GetString();
				}
			}
		}
		catch (Exception ex)
		{
			Log.WriteLine("AI: Failed to parse response: {0}", ex.Message);
			throw new AiTranslationException(AiErrorKind.ParseError, $"Failed to parse AI response: {ex.Message}", innerException: ex);
		}

		return result?.Trim() ?? "";
	}

	public static async Task<string> TranslateWithContextAsync(
		string gameName,
		string targetLanguage,
		string? context = null,
		string? customSystemPrompt = null,
		string? customUserMessage = null,
		CancellationToken cancellationToken = default,
		string? existingTranslation = null)
	{
		string langDisplay = targetLanguage switch
		{
			"zh-CN" => "Simplified Chinese",
			"zh-TW" => "Traditional Chinese",
			"en" => "English",
			_ => targetLanguage
		};

		// Build system prompt
		string systemPrompt = string.IsNullOrWhiteSpace(customSystemPrompt)
			? DefaultSystemPrompt
			: customSystemPrompt;

		// Substitute variables in system prompt
		systemPrompt = systemPrompt.Replace("{target_lang}", langDisplay);
		systemPrompt = systemPrompt.Replace("{context}",
			!string.IsNullOrWhiteSpace(context) ? context : "");

		// Append AI-analyzed translation rules
		string translationRules = Settings.Default.AiTranslationRules;
		if (!string.IsNullOrWhiteSpace(translationRules))
		{
			systemPrompt += "\n\nBelow are AI-generated translation rules based on the user's past translations. Follow these rules strictly:\n\""
				+ translationRules + "\"";
		}

		// Build user message
		string userMessage = string.IsNullOrWhiteSpace(customUserMessage)
			? DefaultUserMessage
			: customUserMessage;

		userMessage = userMessage.Replace("{game_name}", gameName);
		userMessage = userMessage.Replace("{target_lang}", langDisplay);
		userMessage = userMessage.Replace("{context}",
			!string.IsNullOrWhiteSpace(context) ? context : "");

		// If context exists but wasn't used in template, append it
		if (!string.IsNullOrWhiteSpace(context) && !userMessage.Contains(context))
		{
			userMessage += "\n\nContext:\n" + context;
		}

		// If an existing/modified name is provided, append as reference
		if (!string.IsNullOrWhiteSpace(existingTranslation))
		{
			userMessage += "\n\nExisting translation (for reference, may need improvement):\n" + existingTranslation;
		}

		Log.WriteLine("AI: Translating \"{0}\" -> {1}", gameName, targetLanguage);

		string apiUrl = Settings.Default.AiApiUrl ?? "";
		string apiKey = Settings.Default.AiApiKey ?? "";
		string model = Settings.Default.AiModel ?? "gpt-4o-mini";
		string result = await CallChatCompletionAsync(systemPrompt, userMessage, apiUrl, apiKey, model, 4096, 0.1f, cancellationToken);
		result = result.Trim('"', '\'', '\n', '\r');
		Log.WriteLine("AI: Result: \"{0}\"", result);

		return result;
	}

	public static string? BuildContext(string developer, string publisher, string type)
	{
		var parts = new List<string>();
		if (!string.IsNullOrWhiteSpace(developer)) parts.Add("Developer: " + developer);
		if (!string.IsNullOrWhiteSpace(publisher)) parts.Add("Publisher: " + publisher);
		if (!string.IsNullOrWhiteSpace(type)) parts.Add("Type: " + type);

		return parts.Count > 0 ? string.Join("\n", parts) : null;
	}

	public static async Task<string> AnalyzeTranslationRulesAsync(
		string glossaryJsonContent, string? customSystemPrompt = null, CancellationToken cancellationToken = default)
	{
		string systemPromptSection = "";
		if (!string.IsNullOrWhiteSpace(customSystemPrompt))
		{
			systemPromptSection = @"

The user's current system prompt already contains the following instructions/rules:
---
" + customSystemPrompt + @"
---

IMPORTANT: Do NOT summarize or repeat any rules that are already covered in the system prompt above. Only output NEW rules that are NOT already mentioned. If all observed patterns are already covered by the system prompt, output ""(No new rules found)"".
";
		}

		string analysisPrompt = @"You are a translation style analyst. Analyze the following game name translation pairs and summarize the translation rules/patterns the user follows.

Output a concise list of rules in English. Focus on:
- How brand names and proper nouns are handled
- How subtitles are translated
- Terminology consistency patterns
- Any recurring translation conventions

Do NOT list individual translations. Only output generalized rules. Keep the output under 500 words.

IMPORTANT: Start your response directly with the first rule (e.g., ""- Brand names...""). Do NOT write any introductory sentence like ""Here are the rules"" or ""Based on the analysis""."
+ systemPromptSection + @"

Translation pairs:
" + glossaryJsonContent;

		Log.WriteLine("AI: Analyzing translation rules");

		string apiUrl = Settings.Default.AiApiUrl ?? "";
		string apiKey = Settings.Default.AiApiKey ?? "";
		string model = Settings.Default.AiModel ?? "gpt-4o-mini";
		string result = await CallChatCompletionAsync(
			"You are a translation style analyst. Output ONLY the rules as a bullet list. Do NOT include any preamble, greeting, introduction, or concluding remarks. Start directly with the first rule.",
			analysisPrompt, apiUrl, apiKey, model, 4096, 0.3f, cancellationToken);

		Log.WriteLine("AI: Translation rules analysis complete, length={0}", result.Length);
		return result;
	}
}
