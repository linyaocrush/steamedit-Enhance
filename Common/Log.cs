#nullable enable
using System;
using System.IO;

namespace Common;

internal static class Log
{
	private static readonly object _syncObj;
	private static readonly string _path;
	private static StreamWriter? _writer;

	static Log()
	{
		_syncObj = new object();
		_path = Path.Combine(AppHelper.GetAppDirectory(), "log.txt");
		_writer = new StreamWriter(_path, append: true) { AutoFlush = true };
		WriteLines("---", DateTime.Now.ToLongDateString() + " - " + DateTime.Now.ToLongTimeString(), "STARTING UP: " + Environment.CommandLine);
		AppDomain.CurrentDomain.ProcessExit += (_, _) => Close();
	}

	public static void WriteLine(string line)
	{
		WriteLines(line);
	}

	public static void WriteLine(string fmt, params object[] args)
	{
		WriteLines(string.Format(fmt, args));
	}

	private static void WriteLines(params string[] lines)
	{
		lock (_syncObj)
		{
			if (_writer == null) return;
			foreach (string line in lines)
			{
				_writer.WriteLine(line);
			}
#if DEBUG
			Console.WriteLine(string.Join(Environment.NewLine, lines));
#endif
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
}
