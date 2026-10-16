#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using steamedit_Enhance.Data.Native;

namespace steamedit_Enhance.Data;

/// <summary>
/// appinfo.vdf 属性表编解码器。
/// 实现完全由原生 Rust 内核（<c>steamedit-core</c>）提供，**没有托管回退**：
/// 原生库不可用时静态构造会抛出带修复指引的异常。
/// 仅字符串表（ZString）的读写保留在此处——它属于文件级结构，不参与属性表编解码。
/// </summary>
internal static class PropertyTableIO
{
	private const int MaxZStringBuffer = 1024 * 1024; // 1MB cap to prevent OOM on malformed files

	/// <summary>缓存 <c>IList&lt;string&gt;</c> 字符串表对应的原生 dump，避免每个条目重复序列化。</summary>
	private static readonly ConditionalWeakTable<object, byte[]> s_stringTableDumps = new();

	/// <summary>缓存 <c>IList&lt;string&gt;</c> 字符串表对应的原生句柄，避免每个条目重复构建（O(n²)）。</summary>
	private static readonly ConditionalWeakTable<object, NativeStringTable> s_nativeStringTables = new();

	/// <summary>全局热路径句柄（进程生命周期内复用）。</summary>
	private static readonly IntPtr s_hotPathSet;

	static PropertyTableIO()
	{
		SteameditNative.EnsureLoaded();
		s_hotPathSet = BuildHotPathSet();
	}

	private static IntPtr BuildHotPathSet()
	{
		string[][] paths = AppInfoPaths.HotPaths;
		using var ms = new MemoryStream();
		using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
		w.Write((uint)paths.Length);
		foreach (string[] path in paths)
		{
			w.Write((uint)path.Length);
			foreach (string segment in path)
			{
				byte[] bytes = Encoding.UTF8.GetBytes(segment);
				w.Write((uint)bytes.Length);
				w.Write(bytes);
			}
		}
		w.Flush();
		return SteameditNative.PathSetNew(ms.ToArray());
	}

	private static NativeStringTable GetNativeStringTable(IList<string> stringTable)
	{
		byte[] dump = GetStringTableDump(stringTable);
		return s_nativeStringTables.GetValue(stringTable, _ => NativeStringTable.FromDump(dump));
	}

	private static byte[] ReadRemaining(BinaryReader reader)
	{
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		if (remaining <= 0) return Array.Empty<byte>();
		if (remaining > int.MaxValue)
			throw new InvalidDataException("Property table region exceeds 2 GB; refusing to read.");
		return reader.ReadBytes((int)remaining);
	}

	/// <summary>
	/// 取得从当前位置到流末尾的字节。若底层是 <see cref="MemoryStream"/> 则直接借用其内部缓冲区
	/// （零拷贝），否则退化为一次拷贝。返回 false 表示没有可读数据。
	/// </summary>
	private static bool TryGetRemainingSource(BinaryReader reader, long start, out ReadOnlySpan<byte> source)
	{
		Stream stream = reader.BaseStream;
		long length = stream.Length - start;
		if (length <= 0)
		{
			source = default;
			return false;
		}
		if (stream is MemoryStream ms && ms.TryGetBuffer(out ArraySegment<byte> segment) && segment.Array != null)
		{
			source = new ReadOnlySpan<byte>(segment.Array, segment.Offset + (int)start, (int)length);
			return true;
		}
		source = ReadRemaining(reader);
		return source.Length > 0;
	}

	private static byte[] GetStringTableDump(IList<string> stringTable)
	{
		object key = stringTable;
		return s_stringTableDumps.GetValue(key, _ =>
		{
			int count = stringTable.Count;
			int size = 4;
			for (int i = 0; i < count; i++)
			{
				string s = stringTable[i] ?? "";
				size += 4 + Encoding.UTF8.GetByteCount(s);
			}
			var buffer = new byte[size];
			int pos = 0;
			BitConverter.TryWriteBytes(buffer.AsSpan(pos), (uint)count);
			pos += 4;
			for (int i = 0; i < count; i++)
			{
				string s = stringTable[i] ?? "";
				int byteCount = Encoding.UTF8.GetByteCount(s);
				BitConverter.TryWriteBytes(buffer.AsSpan(pos), (uint)byteCount);
				pos += 4;
				Encoding.UTF8.GetBytes(s, buffer.AsSpan(pos));
				pos += byteCount;
			}
			return buffer;
		});
	}

	// ── 文件级字符串表（ZString）：不属于属性表编解码，保留托管实现 ──

	internal static string ReadZString(BinaryReader reader)
	{
		Span<byte> stackBuf = stackalloc byte[256];
		byte[]? heapBuf = null;
		byte[] scan = ArrayPool<byte>.Shared.Rent(4096);
		int len = 0;
		bool foundNull = false;
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		try
		{
			while (remaining > 0)
			{
				int toRead = (int)Math.Min(scan.Length, remaining);
				int n = reader.Read(scan, 0, toRead);
				if (n <= 0) break;
				int nullIdx = Array.IndexOf(scan, (byte)0, 0, n);
				int consume = nullIdx >= 0 ? nullIdx : n;
				if (consume > 0)
				{
					if (len + consume > MaxZStringBuffer)
						throw new InvalidDataException("ReadZString: string exceeds maximum buffer size. File may be corrupted.");
					if (len + consume > (heapBuf?.Length ?? stackBuf.Length))
					{
						int oldCapacity = heapBuf?.Length ?? stackBuf.Length;
						int newSize = Math.Max(oldCapacity * 2, len + consume);
						if (newSize > MaxZStringBuffer)
							newSize = MaxZStringBuffer;
						byte[] newBuf = ArrayPool<byte>.Shared.Rent(newSize);
						if (heapBuf != null)
							heapBuf.AsSpan(0, len).CopyTo(newBuf);
						else
							stackBuf[..len].CopyTo(newBuf);
						if (heapBuf != null)
							ArrayPool<byte>.Shared.Return(heapBuf);
						heapBuf = newBuf;
					}
					if (heapBuf != null)
						scan.AsSpan(0, consume).CopyTo(heapBuf.AsSpan(len));
					else
						scan.AsSpan(0, consume).CopyTo(stackBuf[len..]);
					len += consume;
				}
				if (nullIdx >= 0)
				{
					// 把多读的字节退回，保证流位置停在 NUL 之后
					int overshoot = n - (nullIdx + 1);
					if (overshoot > 0)
						reader.BaseStream.Position -= overshoot;
					foundNull = true;
					break;
				}
				remaining -= n;
			}
			if (!foundNull)
				throw new InvalidDataException("ReadZString: null terminator not found before end of stream");
			if (heapBuf != null)
				return Encoding.UTF8.GetString(heapBuf, 0, len);
			return Encoding.UTF8.GetString(stackBuf[..len]);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(scan);
			if (heapBuf != null)
				ArrayPool<byte>.Shared.Return(heapBuf);
		}
	}

	internal static void WriteZString(BinaryWriter writer, string str)
	{
		int byteCount = Encoding.UTF8.GetByteCount(str);
		if (byteCount < 256)
		{
			Span<byte> buf = stackalloc byte[byteCount + 1];
			Encoding.UTF8.GetBytes(str, buf);
			buf[byteCount] = 0;
			writer.Write(buf);
			return;
		}
		byte[] rented = ArrayPool<byte>.Shared.Rent(byteCount + 1);
		try
		{
			Encoding.UTF8.GetBytes(str, rented.AsSpan(0, byteCount));
			rented[byteCount] = 0;
			writer.Write(rented, 0, byteCount + 1);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(rented);
		}
	}

	/// <summary>把原生字符串表 dump（<c>[u32 count][(u32 len)(utf8)]*</c>）按文件格式写出。</summary>
	internal static void WriteStringTableDump(BinaryWriter writer, byte[] dump)
	{
		int pos = 0;
		uint count = BitConverter.ToUInt32(dump, pos);
		pos += 4;
		writer.Write(count);
		for (uint i = 0; i < count; i++)
		{
			uint len = BitConverter.ToUInt32(dump, pos);
			pos += 4;
			writer.Write(dump, pos, (int)len);
			writer.Write((byte)0);
			pos += (int)len;
		}
	}

	// ── 属性表读取（全部走原生内核） ──

	public static PropertyTable ReadPropertyTable_0x00000000(BinaryReader reader)
		=> ReadNative(reader, format29: false, IntPtr.Zero, stringTable: null, sparse: false);

	public static PropertyTable ReadPropertyTableSparse_0x00000000(BinaryReader reader)
		=> ReadNative(reader, format29: false, IntPtr.Zero, stringTable: null, sparse: true);

	public static PropertyTable ReadPropertyTable_0x07564429(BinaryReader reader, IList<string> stringTable)
		=> ReadNative(reader, format29: true, GetNativeStringTable(stringTable).Handle, stringTable, sparse: false);

	public static PropertyTable ReadPropertyTableSparse_0x07564429(BinaryReader reader, IList<string> stringTable)
		=> ReadNative(reader, format29: true, GetNativeStringTable(stringTable).Handle, stringTable, sparse: true);

	private static PropertyTable ReadNative(BinaryReader reader, bool format29, IntPtr strtab, IList<string>? stringTable, bool sparse)
	{
		long start = reader.BaseStream.Position;
		if (!TryGetRemainingSource(reader, start, out ReadOnlySpan<byte> src))
			throw new InvalidDataException("Property table data is empty; cannot decode.");

		byte[]? flat;
		int consumed;
		if (sparse)
			flat = SteameditNative.VdfToFlatSparse(src, format29, strtab, s_hotPathSet, out consumed);
		else if (format29)
			flat = SteameditNative.VdfToFlat07564429(src, strtab, out consumed);
		else
			flat = SteameditNative.VdfToFlat(src, out consumed);

		if (flat == null)
			throw new InvalidDataException(SteameditNative.LastError);
		reader.BaseStream.Position = start + consumed;
		return FlatTableCodec.FromFlat(flat, stringTable);
	}

	// ── 属性表写入（全部走原生内核） ──

	public static void WritePropertyTable_0x00000000(BinaryWriter writer, PropertyTable table)
		=> writer.Write(Encode0x00000000(table));

	public static byte[] WritePropertyTable_0x00000000(PropertyTable table) => Encode0x00000000(table);

	public static void WritePropertyTable_0x07564429(BinaryWriter writer, PropertyTable table, NativeStringTable stringTable)
		=> writer.Write(Encode0x07564429(table, stringTable));

	public static byte[] WritePropertyTable_0x07564429(PropertyTable table, NativeStringTable stringTable)
		=> Encode0x07564429(table, stringTable);

	private static byte[] Encode0x00000000(PropertyTable table)
	{
		byte[]? data = SteameditNative.FlatToVdf(FlatTableCodec.ToFlat(table));
		if (data == null)
			throw new InvalidDataException(SteameditNative.LastError);
		return data;
	}

	private static byte[] Encode0x07564429(PropertyTable table, NativeStringTable stringTable)
	{
		byte[]? data = SteameditNative.FlatToVdf07564429(FlatTableCodec.ToFlat(table), stringTable.Handle);
		if (data == null)
			throw new InvalidDataException(SteameditNative.LastError);
		return data;
	}
}
