#nullable enable
using System;

namespace Common;

/// <summary>
/// AI 翻译错误类型。
/// </summary>
internal enum AiErrorKind
{
	/// <summary>HTTP 状态码错误（4xx/5xx），StatusCode 属性包含具体状态码。</summary>
	ApiError,
	/// <summary>网络连接失败或超时。</summary>
	NetworkError,
	/// <summary>响应解析失败（JSON 格式异常或内容类型不匹配）。</summary>
	ParseError,
}

/// <summary>
/// AI 翻译操作抛出的异常，携带结构化错误信息供调用方差异化处理。
/// </summary>
internal class AiTranslationException : Exception
{
	public AiErrorKind Kind { get; }
	public int? StatusCode { get; }

	public AiTranslationException(AiErrorKind kind, string message, int? statusCode = null, Exception? innerException = null)
		: base(message, innerException)
	{
		Kind = kind;
		StatusCode = statusCode;
	}
}
