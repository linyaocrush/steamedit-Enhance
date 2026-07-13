#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

internal sealed class MockFileSystem : IFileSystem
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lockedFiles = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Op, string Arg1, string? Arg2)> Operations { get; } = new();
    public bool CopyShouldThrow { get; set; }

    public bool FileExists(string path)
    {
        Operations.Add(("Exists", path, null));
        return Files.ContainsKey(path);
    }

    public Stream OpenRead(string path)
    {
        Operations.Add(("OpenRead", path, null));
        if (!Files.TryGetValue(path, out var data))
            throw new FileNotFoundException($"File not found: {path}");
        return new MemoryStream(data);
    }

    public Stream CreateFile(string path)
    {
        Operations.Add(("Create", path, null));
        var ms = new TrackedMemoryStream(data => Files[path] = data);
        Files[path] = Array.Empty<byte>();
        return ms;
    }

    public void FileDelete(string path)
    {
        Operations.Add(("Delete", path, null));
        Files.Remove(path);
    }

    public void FileCopy(string sourcePath, string destinationPath, bool overwrite)
    {
        Operations.Add(("Copy", sourcePath, destinationPath));
        if (CopyShouldThrow) throw new IOException("Copy failed");
        if (!Files.TryGetValue(sourcePath, out var data))
            throw new FileNotFoundException($"File not found: {sourcePath}");
        if (Files.ContainsKey(destinationPath) && !overwrite)
            throw new IOException($"File exists: {destinationPath}");
        Files[destinationPath] = (byte[])data.Clone();
    }

    public void FileMove(string sourcePath, string destinationPath, bool overwrite)
    {
        Operations.Add(("Move", sourcePath, destinationPath));
        if (!Files.TryGetValue(sourcePath, out var data))
            throw new FileNotFoundException($"File not found: {sourcePath}");
        if (Files.ContainsKey(destinationPath) && !overwrite)
            throw new IOException($"File exists: {destinationPath}");
        Files[destinationPath] = data;
        Files.Remove(sourcePath);
    }

    public void LockFile(string path)
    {
        Operations.Add(("Lock", path, null));
        _lockedFiles.Add(path);
    }

    public void CreateDirectory(string path)
    {
        Operations.Add(("CreateDir", path, null));
    }

    // 测试辅助方法
    public void WriteAllBytes(string path, byte[] data) => Files[path] = data;
    public byte[] ReadAllBytes(string path) => Files.TryGetValue(path, out var data) ? data : throw new FileNotFoundException(path);
    public int FileCount => Files.Count;

    private sealed class TrackedMemoryStream : MemoryStream
    {
        private readonly Action<byte[]> _onClose;
        private bool _disposed;

        public TrackedMemoryStream(Action<byte[]> onClose) => _onClose = onClose;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _onClose(GetBuffer().AsSpan(0, (int)Position).ToArray());
            }
            base.Dispose(disposing);
        }
    }
}
