#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace steamedit_EnhanceTests;

internal sealed class MockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}

[TestClass]
public class AiTranslatorTests
{
    private string _savedApiUrl = "";
    private string _savedApiKey = "";
    private string _savedModel = "";

    [TestInitialize]
    public void InitHttpTests()
    {
        _savedApiUrl = Settings.Default.AiApiUrl;
        _savedApiKey = Settings.Default.AiApiKey;
        _savedModel = Settings.Default.AiModel;
        Settings.Default.AiApiUrl = "https://api.openai.com/v1/chat/completions";
        Settings.Default.AiApiKey = "test-key";
        Settings.Default.AiModel = "gpt-4o-mini";
    }

    [TestCleanup]
    public void CleanupHttpTests()
    {
        Settings.Default.AiApiUrl = _savedApiUrl;
        Settings.Default.AiApiKey = _savedApiKey;
        Settings.Default.AiModel = _savedModel;
    }

    [TestMethod]
    public void AiTranslationException_ApiError_HasCorrectKind()
    {
        var ex = new AiTranslationException(AiErrorKind.ApiError, "API error 401: Unauthorized", 401);
        Assert.AreEqual(AiErrorKind.ApiError, ex.Kind);
        Assert.AreEqual(401, ex.StatusCode);
        Assert.AreEqual("API error 401: Unauthorized", ex.Message);
    }

    [TestMethod]
    public void AiTranslationException_NetworkError_HasNullStatusCode()
    {
        var inner = new HttpRequestException("Connection refused");
        var ex = new AiTranslationException(AiErrorKind.NetworkError, "Network error: Connection refused", innerException: inner);
        Assert.AreEqual(AiErrorKind.NetworkError, ex.Kind);
        Assert.IsNull(ex.StatusCode);
        Assert.AreSame(inner, ex.InnerException);
    }

    [TestMethod]
    public void AiTranslationException_ParseError_HasCorrectKind()
    {
        var ex = new AiTranslationException(AiErrorKind.ParseError, "Failed to parse AI response: invalid JSON");
        Assert.AreEqual(AiErrorKind.ParseError, ex.Kind);
        Assert.IsNull(ex.StatusCode);
    }

    [TestMethod]
    public void AiTranslationException_IsException()
    {
        var ex = new AiTranslationException(AiErrorKind.ApiError, "test");
        Assert.IsInstanceOfType(ex, typeof(Exception));
    }

    [TestMethod]
    public void BuildContext_AllFields_ReturnsFormattedString()
    {
        string? result = AiTranslator.BuildContext("Valve", "Valve", "game");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Developer: Valve"));
        Assert.IsTrue(result.Contains("Publisher: Valve"));
        Assert.IsTrue(result.Contains("Type: game"));
    }

    [TestMethod]
    public void BuildContext_OnlyDeveloper_ReturnsDeveloperOnly()
    {
        string? result = AiTranslator.BuildContext("Valve", "", "");
        Assert.IsNotNull(result);
        Assert.AreEqual("Developer: Valve", result);
    }

    [TestMethod]
    public void BuildContext_AllEmpty_ReturnsNull()
    {
        string? result = AiTranslator.BuildContext("", "", "");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void BuildContext_AllNull_ReturnsNull()
    {
        string? result = AiTranslator.BuildContext(null!, null!, null!);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void BuildContext_WhitespaceOnly_ReturnsNull()
    {
        string? result = AiTranslator.BuildContext("  ", "  ", "  ");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void DefaultSystemPrompt_ContainsTargetLangPlaceholder()
    {
        Assert.IsTrue(AiTranslator.DefaultSystemPrompt.Contains("{target_lang}"));
    }

    [TestMethod]
    public void DefaultSystemPrompt_HasNoTranslationRulesByDefault()
    {
        Assert.IsFalse(AiTranslator.DefaultSystemPrompt.Contains("translation rules"));
    }

    [TestMethod]
    public void DefaultUserMessage_IsGameNamePlaceholder()
    {
        Assert.AreEqual("{game_name}", AiTranslator.DefaultUserMessage);
    }

    [TestMethod]
    public void BuildContext_SpecialCharacters_Handled()
    {
        string? result = AiTranslator.BuildContext("Valve, Inc.", "Valve (Steam)", "game/app");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Developer: Valve, Inc."));
        Assert.IsTrue(result.Contains("Publisher: Valve (Steam)"));
        Assert.IsTrue(result.Contains("Type: game/app"));
    }

    [TestMethod]
    public void BuildContext_VeryLongStrings_NoTruncation()
    {
        string longStr = new string('A', 1000);
        string? result = AiTranslator.BuildContext(longStr, "", "");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains(longStr));
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    [TestMethod]
    public async Task TranslateWithContext_SendsCorrectRequest()
    {
        var mockHandler = new MockHttpHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.IsTrue(request.RequestUri!.ToString().EndsWith("/chat/completions"));
            Assert.IsTrue(request.Headers.Contains("Authorization"));

            var body = request.Content!.ReadAsStringAsync().Result;
            using var doc = JsonDocument.Parse(body);
            Assert.AreEqual("gpt-4o-mini", doc.RootElement.GetProperty("model").GetString());

            return JsonResponse("{\"choices\":[{\"message\":{\"content\":\"\u6d4b\u8bd5\u540d\u79f0\"}}]}");
        });

        using var httpClient = new HttpClient(mockHandler);
        var translator = new AiTranslator(httpClient);
        var result = await translator.TranslateWithContextAsyncImpl("Test Game", "zh-CN");
        Assert.AreEqual("测试名称", result);
    }

    [TestMethod]
    public async Task TranslateWithContext_HandlesSuccessResponse()
    {
        var mockHandler = new MockHttpHandler(_ => JsonResponse("{\"choices\":[{\"message\":{\"content\":\"\u6e38\u620f\"}}]}"));

        using var httpClient = new HttpClient(mockHandler);
        var translator = new AiTranslator(httpClient);
        var result = await translator.TranslateWithContextAsyncImpl("Game", "zh-CN");
        Assert.AreEqual("游戏", result);
    }

    [TestMethod]
    public async Task TranslateWithContext_RetriesOnFailure()
    {
        int callCount = 0;
        var mockHandler = new MockHttpHandler(_ =>
        {
            callCount++;
            if (callCount <= 2)
                return new HttpResponseMessage((HttpStatusCode)503);
            return JsonResponse("{\"choices\":[{\"message\":{\"content\":\"retried\"}}]}");
        });

        using var httpClient = new HttpClient(mockHandler);
        var translator = new AiTranslator(httpClient);
        var result = await translator.TranslateWithContextAsyncImpl("Game", "zh-CN");
        Assert.AreEqual("retried", result);
        Assert.IsTrue(callCount >= 3, $"Expected at least 3 calls, got {callCount}");
    }

    [TestMethod]
    public async Task TranslateWithContext_ThrowsOnTimeout()
    {
        var mockHandler = new MockHttpHandler(_ =>
        {
            throw new HttpRequestException("The operation timed out", null, HttpStatusCode.RequestTimeout);
        });

        using var httpClient = new HttpClient(mockHandler);
        var translator = new AiTranslator(httpClient);
        await Assert.ThrowsExceptionAsync<AiTranslationException>(() =>
            translator.TranslateWithContextAsyncImpl("Game", "zh-CN"));
    }

    [TestMethod]
    public async Task FetchModels_SendsCorrectRequest()
    {
        var mockHandler = new MockHttpHandler(request =>
        {
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.IsTrue(request.RequestUri!.ToString().EndsWith("/models"));
            Assert.IsTrue(request.Headers.Contains("Authorization"));

            return JsonResponse("{\"data\":[{\"id\":\"gpt-4\"},{\"id\":\"gpt-3.5-turbo\"}]}");
        });

        using var httpClient = new HttpClient(mockHandler);
        var translator = new AiTranslator(httpClient);
        var models = await translator.FetchModelsAsyncImpl("https://api.openai.com/v1/chat/completions", "test-key");
        Assert.AreEqual(2, models.Count);
        Assert.AreEqual("gpt-3.5-turbo", models[0]);
        Assert.AreEqual("gpt-4", models[1]);
    }
}
