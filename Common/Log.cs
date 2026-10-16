#nullable enable
using System;
using System.IO;
using steamedit_Enhance.Data.Native;

namespace Common;

/// <summary>
/// 文件日志的薄封装：文件句柄、级别过滤、行格式化与“最近若干行”读取全部由
/// Rust 内核 (<c>ste_log_*</c>) 负责；托管侧只做界面/调试台回显与本地时间串。
/// </summary>
internal static class Log
{
	private static readonly string _path;

	// 托管侧镜像一份级别开关，用于在格式化/调用内核前提前短路（内核同样会再判一次）。
	private static volatile bool _enableDebug;
	private static volatile bool _enableInfo = true;
	private static volatile bool _enableWarning = true;
	private static volatile bool _enableError = true;

	public static string FilePath => _path;

	public enum LogLevel
	{
		Debug = 0,
		Info = 1,
		Warning = 2,
		Error = 3
	}

	static Log()
	{
		_path = Path.Combine(AppHelper.GetAppDirectory(), "log.txt");
		// 默认启用 Debug 仅用于调试构建，生产环境默认关闭。
#if DEBUG
		InitNative(true, true, true, true);
#else
		InitNative(false, true, true, true);
#endif
		WriteLines(LogLevel.Info, "---",
			DateTime.Now.ToLongDateString() + " - " + DateTime.Now.ToLongTimeString(),
			"STARTING UP: " + Environment.CommandLine);
		Flush();
		AppDomain.CurrentDomain.ProcessExit += (_, _) => Close();
	}

	private static void InitNative(bool debug, bool info, bool warning, bool error)
	{
		_enableDebug = debug;
		_enableInfo = info;
		_enableWarning = warning;
		_enableError = error;
		SteameditNative.LogInit(_path);
		SteameditNative.LogConfigure(debug, info, warning, error);
	}

	/// <summary>配置日志级别启用状态。</summary>
	public static void ConfigureLevels(bool debug, bool info, bool warning, bool error)
	{
		_enableDebug = debug;
		_enableInfo = info;
		_enableWarning = warning;
		_enableError = error;
		SteameditNative.LogConfigure(debug, info, warning, error);
	}

	/// <summary>设置最低日志级别（包含）。</summary>
	public static void SetMinimumLevel(LogLevel level)
		=> ConfigureLevels(
			level <= LogLevel.Debug,
			level <= LogLevel.Info,
			level <= LogLevel.Warning,
			level <= LogLevel.Error);

	public static void WriteLine(string line) => WriteLines(LogLevel.Info, line);

	public static void WriteLine(string fmt, params object[] args)
		=> WriteLines(LogLevel.Info, string.Format(fmt, args));

	public static void WriteLine(LogLevel level, string line) => WriteLines(level, line);

	public static void WriteLine(LogLevel level, string fmt, params object[] args)
		=> WriteLines(level, string.Format(fmt, args));

	private static void WriteLines(params string[] lines) => WriteLines(LogLevel.Info, lines);

	private static void WriteLines(LogLevel level, params string[] lines)
	{
		if (!IsLevelEnabled(level)) return;

		string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
		var levelCode = (uint)level;
		foreach (string line in lines)
		{
			SteameditNative.LogWrite(levelCode, timestamp, line);
		}
#if DEBUG
		string levelStr = GetLevelString(level);
		Console.WriteLine($"[{levelStr}] {string.Join(Environment.NewLine + $"[{levelStr}] ", lines)}");
#endif
	}

	private static bool IsLevelEnabled(LogLevel level)
	{
		return level switch
		{
			LogLevel.Debug => _enableDebug,
			LogLevel.Info => _enableInfo,
			LogLevel.Warning => _enableWarning,
			LogLevel.Error => _enableError,
			_ => true
		};
	}

	private static string GetLevelString(LogLevel level)
	{
		return level switch
		{
			LogLevel.Debug => "DBG",
			LogLevel.Info => "INF",
			LogLevel.Warning => "WRN",
			LogLevel.Error => "ERR",
			_ => "???"
		};
	}

	public static string[] GetRecentLines(int count = 100)
		=> SteameditNative.LogRecent(count);

	public static void Flush() => SteameditNative.LogFlush();

	public static void Close() => SteameditNative.LogClose();

	/// <summary>
	/// 重置日志系统状态（仅用于测试，避免测试间状态泄漏）。
	/// </summary>
	[System.Diagnostics.Conditional("DEBUG")]
	public static void Reset()
	{
		_enableDebug = true;
		_enableInfo = true;
		_enableWarning = true;
		_enableError = true;
		SteameditNative.LogReset(_path);
		SteameditNative.LogConfigure(true, true, true, true);
	}
}
