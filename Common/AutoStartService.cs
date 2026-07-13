#nullable enable
using System;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Common;

internal static class AutoStartService
{
	private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string RunValueName = "steamedit_Enhance-Enhance";

	public static bool IsEnabled()
	{
		try
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
			return key?.GetValue(RunValueName) != null;
		}
		catch (Exception ex)
		{
			Log.WriteLine("Failed to read auto-start registry: {0}", ex.Message);
			return false;
		}
	}

	public static bool Set(bool autoStart, bool autoFix, bool forceStart)
	{
		try
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
			if (key == null) return false;

			if (autoStart)
			{
				string exePath = Application.ExecutablePath;
				string args = "";
				if (autoFix) args += " -autofix";
				if (forceStart) args += " -forcestart";
				key.SetValue(RunValueName, $"\"{exePath}\"{args}");
				key.DeleteValue("steamedit-Enhance", throwOnMissingValue: false);
			}
			else
			{
				key.DeleteValue(RunValueName, throwOnMissingValue: false);
			}
			return true;
		}
		catch (Exception ex)
		{
			Log.WriteLine("Failed to update auto-start registry: {0}", ex.Message);
			return false;
		}
	}
}
