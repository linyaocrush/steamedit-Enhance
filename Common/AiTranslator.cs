#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using steamedit_Enhance.Data.Native;

namespace Common;

/// <summary>
/// AI 游戏名翻译。提示词模板、变量替换与本地化显示名解析仍留在托管侧（依赖界面语言资源）；
/// 所有 HTTP 交互——OpenAI 兼容的 <c>/chat/completions</c>、<c>/models</c>、重试退避与
/// 熔断器——全部由 Rust 内核 (<c>ste_net_ai_*</c>) 承担。
/// </summary>
internal static class AiTranslator
{
	public const string DefaultSystemPrompt = @"You are a game name translator. Translate the given game name into {target_lang}.

Rules:
- Output ONLY the translated name, nothing else
- Do NOT add quotes, explanations, prefixes, or any extra text
- If the name is already in {target_lang}, output it unchanged
- Keep brand names, acronyms, and proper nouns unchanged (e.g., GTA, FIFA, Dark Souls, Resident Evil)
- If the name contains both a brand and a subtitle, translate ONLY the subtitle
- Keep numbers and punctuation as-is";

	public const string DefaultUserMessage = "{game_name}";

	/// <summary>把内核错误分类映射回 <see cref="AiTranslationException"/>。</summary>
	private static AiTranslationException NativeError()
	{
		var (kind, status) = SteameditNative.AiError();
		string message = SteameditNative.LastError;
		return kind switch
		{
			SteameditNative.AiKindApi => new AiTranslationException(AiErrorKind.ApiError, message, status),
			SteameditNative.AiKindParse => new AiTranslationException(AiErrorKind.ParseError, message),
			SteameditNative.AiKindCircuit => new AiTranslationException(AiErrorKind.CircuitOpen, message),
			_ => new AiTranslationException(AiErrorKind.NetworkError, message),
		};
	}

	/// <summary>调用一次 chat completion；失败时抛出 <see cref="AiTranslationException"/>。</summary>
	private static string CallChatCompletion(
		string systemPrompt, string userMessage, int maxTokens, float temperature)
	{
		string apiUrl = Settings.Default.AiApiUrl ?? "";
		if (string.IsNullOrWhiteSpace(apiUrl))
			throw new InvalidOperationException("AI API URL is not configured.");
		string apiKey = Settings.Default.AiApiKey ?? "";
		string model = Settings.Default.AiModel ?? "gpt-4o-mini";
		int timeout = Settings.Default.AiTimeoutSeconds;

		Log.WriteLine("AI: Requesting {0}", apiUrl);
		string? result = SteameditNative.AiChat(
			systemPrompt, userMessage, apiUrl, apiKey, model, maxTokens, temperature, timeout);
		if (result == null)
		{
			Log.WriteLine("AI: Request failed: {0}", SteameditNative.LastError);
			throw NativeError();
		}
		return result;
	}

	public static async Task<List<string>> FetchModelsAsync(
		string apiUrl, string apiKey, CancellationToken cancellationToken = default)
	{
		string url = apiUrl ?? "";
		string key = apiKey ?? "";
		int timeout = Settings.Default.AiTimeoutSeconds;

		Log.WriteLine("AI: Fetching models from {0}", url);
		string[]? models = await Task.Run(
			() => SteameditNative.AiModels(url, key, timeout),
			cancellationToken).ConfigureAwait(false);
		if (models == null)
		{
			Log.WriteLine("AI: Models fetch failed: {0}", SteameditNative.LastError);
			throw NativeError();
		}
		Log.WriteLine("AI: Got {0} models", models.Length);
		return new List<string>(models);
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
		string langDisplay = EnumMappings.GetAiDisplayName(targetLanguage);

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

		cancellationToken.ThrowIfCancellationRequested();
		string result = await Task.Run(
			() => CallChatCompletion(systemPrompt, userMessage, 4096, 0.1f),
			cancellationToken).ConfigureAwait(false);
		result = result.Trim('"', '\'', '\n', '\r');
		Log.WriteLine("AI: Result: \"{0}\"", result);

		return result;
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

		cancellationToken.ThrowIfCancellationRequested();
		string result = await Task.Run(
			() => CallChatCompletion(
				"You are a translation style analyst. Output ONLY the rules as a bullet list. Do NOT include any preamble, greeting, introduction, or concluding remarks. Start directly with the first rule.",
				analysisPrompt, 4096, 0.3f),
			cancellationToken).ConfigureAwait(false);

		Log.WriteLine("AI: Translation rules analysis complete, length={0}", result.Length);
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
}
