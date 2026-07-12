#nullable enable
#if DEBUG
// BenchmarkDotNet 基准测试仅在 Release 构建下运行
#else
using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests.Benchmarks;

[MemoryDiagnoser]
public class PropertyTableIOBenchmarks
{
    private byte[] _testData = null!;

    [GlobalSetup]
    public void Setup()
    {
        var table = new PropertyTable();
        table.AddPropertyValue("name", PropertyType.String, "Test Game");
        table.AddPropertyValue("appid", PropertyType.Int32, 12345);
        table.AddPropertyValue("developer", PropertyType.String, "Valve");
        table.AddPropertyValue("publisher", PropertyType.String, "Valve");
        table.AddPropertyValue("is_free", PropertyType.Int32, 0);
        var nested = new PropertyTable();
        nested.AddPropertyValue("language", PropertyType.String, "schinese");
        nested.AddPropertyValue("interface", PropertyType.Int32, 1);
        nested.AddPropertyValue("full_audio", PropertyType.Int32, 0);
        nested.AddPropertyValue("subtitles", PropertyType.Int32, 1);
        table.AddPropertyValue("supported_languages", PropertyType.Table, nested);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            PropertyTableIO.WritePropertyTable_0x00000000(writer, table);
        _testData = ms.ToArray();
    }

    [Benchmark]
    public void ReadPropertyTable()
    {
        using var ms = new MemoryStream(_testData);
        using var reader = new BinaryReader(ms);
        PropertyTableIO.ReadPropertyTable_0x00000000(reader);
    }
}
#endif
