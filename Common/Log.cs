#nullable enable
using System;
using System.IO;
using System.Linq;

namespace Common;

internal static class Log
{
	private static readonly object _syncObj;
	private static readonly string _path;
	private static StreamWriter? _writer;
	
	// 默认启用Debug级别仅用于调试构建，生产环境默认关闭
#if DEBUG
	private static volatile bool _enableDebug = true;
	private static volatile bool _enableInfo = true;
	private static volatile bool _enableWarning = true;
	private static volatile bool _enableError = true;
#else
	private static volatile bool _enableDebug = false;
	private static volatile bool _enableInfo = true;
	private static volatile bool _enableWarning = true;
	private static volatile bool _enableError = true;
#endif

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
		_syncObj = new object();
		_path = Path.Combine(AppHelper.GetAppDirectory(), "log.txt");
		_writer = new StreamWriter(_path, append: true) { AutoFlush = true };
		WriteLines(LogLevel.Info, "---", DateTime.Now.ToLongDateString() + " - " + DateTime.Now.ToLongTimeString(), "STARTING UP: " + Environment.CommandLine);
		AppDomain.CurrentDomain.ProcessExit += (_, _) => Close();
	}

	private static string _cachedTimestamp = "";
	private static long _lastTimestampSecond;

	/// <summary>
	/// 配置日志级别启用状态
	/// </summary>
	public static void ConfigureLevels(bool debug, bool info, bool warning, bool error)
	{
		_enableDebug = debug;
		_enableInfo = info;
		_enableWarning = warning;
		_enableError = error;
	}

	/// <summary>
	/// 设置最低日志级别（包含）
	/// </summary>
	public static void SetMinimumLevel(LogLevel level)
	{
		_enableDebug = level <= LogLevel.Debug;
		_enableInfo = level <= LogLevel.Info;
		_enableWarning = level <= LogLevel.Warning;
		_enableError = level <= LogLevel.Error;
	}

	public static void WriteLine(string line)
	{
		WriteLines(LogLevel.Info, line);
	}

	public static void WriteLine(string fmt, params object[] args)
	{
		WriteLines(LogLevel.Info, string.Format(fmt, args));
	}

	public static void WriteLine(LogLevel level, string line)
	{
		WriteLines(level, line);
	}

	public static void WriteLine(LogLevel level, string fmt, params object[] args)
	{
		WriteLines(level, string.Format(fmt, args));
	}

	private static void WriteLines(params string[] lines)
	{
		WriteLines(LogLevel.Info, lines);
	}

	private static void WriteLines(LogLevel level, params string[] lines)
	{
		if (!IsLevelEnabled(level)) return;
		
		long nowSec = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
		if (nowSec != _lastTimestampSecond)
		{
			_cachedTimestamp = DateTime.Now.ToString("HH:mm:ss.fff");
			_lastTimestampSecond = nowSec;
		}
		string timestamp = _cachedTimestamp;
		string levelStr = GetLevelString(level);
		
		lock (_syncObj)
		{
			if (_writer == null) return;
			foreach (string line in lines)
			{
				_writer.WriteLine($"{timestamp} [{levelStr}] {line}");
			}
#if DEBUG
			Console.WriteLine($"[{levelStr}] {string.Join(Environment.NewLine + $"[{levelStr}] ", lines)}");
#endif
		}
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
	{
		lock (_syncObj)
		{
			_writer?.Flush();
		}
		try
		{
			if (!File.Exists(_path)) return [];
			lock (_syncObj)
			{
				return File.ReadLines(_path).TakeLast(count).ToArray();
			}
		}
		catch
		{
			return [];
		}
	}

	public static void Flush()
	{
		lock (_syncObj)
		{
			_writer?.Flush();
		}
	}

	public static void Close()
	{
		lock (_syncObj)
		{
			_writer?.Flush();
			_writer?.Dispose();
			_writer = null;
		}
	}

	/// <summary>
	/// 重置日志系统状态（仅用于测试，避免测试间状态泄漏）
	/// </summary>
	[System.Diagnostics.Conditional("DEBUG")]
	public static void Reset()
	{
		Close();
		_writer = new StreamWriter(_path, append: false) { AutoFlush = true };
		_enableDebug = true;
		_enableInfo = true;
		_enableWarning = true;
		_enableError = true;
	}
}
