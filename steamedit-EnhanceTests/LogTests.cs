#nullable enable
using System;
using System.IO;
using System.Linq;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace steamedit_EnhanceTests;

[TestClass]
public class LogTests
{
    [TestCleanup]
    public void Cleanup()
    {
        Log.Reset();
    }

    [TestMethod]
    public void WriteLine_InfoLevel_WritesToFile()
    {
        string testMsg = "Test info message " + Guid.NewGuid();

        Log.WriteLine(Log.LogLevel.Info, testMsg);
        Log.Flush();

        string content = File.ReadAllText(Log.FilePath);
        Assert.IsTrue(content.Contains(testMsg));
    }

    [TestMethod]
    public void WriteLine_DebugLevel_WhenInfoMinimum_DoesNotWrite()
    {
        Log.SetMinimumLevel(Log.LogLevel.Info);
        string testMsg = "DebugShouldNotAppear_" + Guid.NewGuid();

        Log.WriteLine(Log.LogLevel.Debug, testMsg);
        Log.Flush();

        string content = File.ReadAllText(Log.FilePath);
        Assert.IsFalse(content.Contains(testMsg));
    }

    [TestMethod]
    public void SetMinimumLevel_Warning_InfoNotWritten()
    {
        Log.SetMinimumLevel(Log.LogLevel.Warning);
        string infoMsg = "InfoShouldNotAppear_" + Guid.NewGuid();
        string warnMsg = "WarnShouldAppear_" + Guid.NewGuid();

        Log.WriteLine(Log.LogLevel.Info, infoMsg);
        Log.WriteLine(Log.LogLevel.Warning, warnMsg);
        Log.Flush();

        string content = File.ReadAllText(Log.FilePath);
        Assert.IsFalse(content.Contains(infoMsg));
        Assert.IsTrue(content.Contains(warnMsg));
    }

    [TestMethod]
    public void GetRecentLines_ReturnsRecentContent()
    {
        string unique = "RECENT_LINE_" + Guid.NewGuid();

        Log.WriteLine(Log.LogLevel.Info, unique);
        var lines = Log.GetRecentLines(10);

        Assert.IsTrue(lines.Length > 0);
        Assert.IsTrue(lines.Any(l => l.Contains(unique)));
    }

    [TestMethod]
    public void WriteLine_StringFormat_WritesFormattedMessage()
    {
        string testMsg = "FORMAT_{0}_TEST";
        int val = 42;

        Log.WriteLine(testMsg, val);
        Log.Flush();

        string content = File.ReadAllText(Log.FilePath);
        Assert.IsTrue(content.Contains($"FORMAT_{val}_TEST"));
    }

    [TestMethod]
    public void Close_ThenWrite_DoesNotThrow()
    {
        Log.Close();

        Log.WriteLine("After close test");
        Log.Flush();
    }

    [TestMethod]
    public void MultipleThreads_ConcurrentWrites_NoCrash()
    {
        Log.SetMinimumLevel(Log.LogLevel.Debug);
        int threadCount = 10;
        var threads = new System.Threading.Thread[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int id = i;
            threads[i] = new System.Threading.Thread(() =>
            {
                for (int j = 0; j < 20; j++)
                    Log.WriteLine(Log.LogLevel.Debug, $"Thread {id} message {j}");
            });
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Log.Flush();
        string content = File.ReadAllText(Log.FilePath);
        Assert.IsTrue(content.Contains("Thread 0 message 0"));
    }

    [TestMethod]
    public void Reset_ClearsAndReopens()
    {
        Log.WriteLine("BeforeReset_" + Guid.NewGuid());
        Log.Reset();
        string afterMsg = "AfterReset_" + Guid.NewGuid();
        Log.WriteLine(afterMsg);
        Log.Flush();

        string content = File.ReadAllText(Log.FilePath);
        Assert.IsTrue(content.Contains(afterMsg));
    }
}
