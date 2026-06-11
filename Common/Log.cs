using System;
using System.IO;

namespace Common;

internal static class Log
{
	private static readonly object _syncObj;

	private static readonly string _path;

	static Log()
	{
		_syncObj = new object();
		_path = Path.Combine(AppHelper.GetAppDirectory(), "log.txt");
		WriteLines("-------------------------------------------------------------------", DateTime.Now.ToLongDateString() + " - " + DateTime.Now.ToLongTimeString(), "STARTING UP: " + Environment.CommandLine);
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
			File.AppendAllLines(_path, lines);
			Console.WriteLine(string.Join(Environment.NewLine, lines));
		}
	}
}
