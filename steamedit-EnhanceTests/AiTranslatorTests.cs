#nullable enable
using System;
using System.Net.Http;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace steamedit_EnhanceTests;

/// <summary>
/// AI 传输层（HTTP、重试、熔断、JSON 解析）已迁移到 Rust 内核并由原生测试覆盖
/// （含本地 HTTP 模拟服务器）。这里只验证留在托管侧的纯逻辑：错误分类、提示词/上下文构建。
/// </summary>
[TestClass]
public class AiTranslatorTests
{
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
}
