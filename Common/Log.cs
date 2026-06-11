using System;
using System.IO;

namespace Common;

internal static class Log
{
	private static readonly object _syncObj;
	private static readonly string _path;
	private static StreamWriter _writer;
	private static int _writeCount;
	private const int FlushInterval = 20;

	static Log()
	{
		_syncObj = new object();
		_path = Path.Combine(AppHelper.GetAppDirectory(), "log.txt");
		_writer = new StreamWriter(_path, append: true) { AutoFlush = false };
		WriteLines("---", DateTime.Now.ToLongDateString() + " - " + DateTime.Now.ToLongTimeString(), "STARTING UP: " + Environment.CommandLine);
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
			foreach (string line in lines)
			{
				_writer.WriteLine(line);
			}
			Console.WriteLine(string.Join(Environment.NewLine, lines));
			if (++_writeCount >= FlushInterval)
			{
				_writer.Flush();
				_writeCount = 0;
			}
		}
	}

	public static void Flush()
	{
		lock (_syncObj)
		{
			_writer?.Flush();
		}
	}
}
