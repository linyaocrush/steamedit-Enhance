#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using steamedit_Enhance.Data;

namespace steamedit_EnhanceTests;

internal sealed class MockFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lockedFiles = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => _files.ContainsKey(path);

    public Stream OpenRead(string path)
    {
        if (!_files.TryGetValue(path, out var data))
            throw new FileNotFoundException($"File not found: {path}");
        return new MemoryStream(data);
    }

    public Stream CreateFile(string path)
    {
        var stream = new TrackedMemoryStream(path, _files);
        _files[path] = Array.Empty<byte>();
        return stream;
    }

    public void FileDelete(string path) => _files.Remove(path);

    public void FileCopy(string sourcePath, string destinationPath, bool overwrite)
    {
        if (!_files.TryGetValue(sourcePath, out var data))
            throw new FileNotFoundException($"File not found: {sourcePath}");
        if (_files.ContainsKey(destinationPath) && !overwrite)
            throw new IOException($"File exists: {destinationPath}");
        _files[destinationPath] = (byte[])data.Clone();
    }

    public void FileMove(string sourcePath, string destinationPath, bool overwrite)
    {
        if (!_files.TryGetValue(sourcePath, out var data))
            throw new FileNotFoundException($"File not found: {sourcePath}");
        if (_files.ContainsKey(destinationPath) && !overwrite)
            throw new IOException($"File exists: {destinationPath}");
        _files[destinationPath] = data;
        _files.Remove(sourcePath);
    }

    public void LockFile(string path) => _lockedFiles.Add(path);

    public void CreateDirectory(string path) { }

    // 测试辅助方法
    public void WriteAllBytes(string path, byte[] data) => _files[path] = data;
    public byte[] ReadAllBytes(string path) => _files.TryGetValue(path, out var data) ? data : throw new FileNotFoundException(path);
    public int FileCount => _files.Count;

    private sealed class TrackedMemoryStream : MemoryStream
    {
        private readonly string _path;
        private readonly Dictionary<string, byte[]> _files;
        private bool _disposed;

        public TrackedMemoryStream(string path, Dictionary<string, byte[]> files)
        {
            _path = path;
            _files = files;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _files[_path] = ToArray();
            }
            base.Dispose(disposing);
        }
    }
}
