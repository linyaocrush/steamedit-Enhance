#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using steamedit_Enhance.Data.Native;

namespace steamedit_Enhance.Data;

internal static class PropertyTableIO
{
	private const int MaxZStringBuffer = 1024 * 1024; // 1MB cap to prevent OOM on malformed files

	/// <summary>
	/// 是否优先使用原生（Rust）编解码器。原生库缺失或设置 <c>STEAMEDIT_DISABLE_NATIVE=1</c>
	/// 时自动回退到托管实现。默认开启。
	/// </summary>
	internal static bool UseNative { get; set; } = true;

	/// <summary>当前是否实际走原生路径（原生库可用且未被关闭）。</summary>
	internal static bool Native => UseNative && SteameditNative.Available;

	/// <summary>缓存 <c>IList&lt;string&gt;</c> 字符串表对应的原生 dump，避免每个条目重复序列化。</summary>
	private static readonly ConditionalWeakTable<object, byte[]> s_stringTableDumps = new();

	/// <summary>缓存 <c>IList&lt;string&gt;</c> 字符串表对应的原生句柄，避免每个条目重复构建（O(n²)）。</summary>
	private static readonly ConditionalWeakTable<object, NativeStringTable> s_nativeStringTables = new();

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

	private static string ReadString_0x00000000(BinaryReader reader) => ReadZString(reader);

	private static string ReadWideString_0x00000000(BinaryReader reader)
	{
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		if (remaining <= 0)
			throw new InvalidDataException("ReadWideString: no data remaining");

		int byteCount = (int)Math.Min(remaining, MaxZStringBuffer);
		byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount);
		try
		{
			reader.Read(buffer, 0, byteCount);
			int nullPos = -1;
			for (int i = 0; i < byteCount - 1; i += 2)
			{
				if (buffer[i] == 0 && buffer[i + 1] == 0)
				{
					nullPos = i;
					break;
				}
			}
			if (nullPos < 0)
				throw new InvalidDataException("ReadWideString: null terminator not found");
			// 将流位置回退到 null 终止符之后
			reader.BaseStream.Position -= byteCount - (nullPos + 2);
			return Encoding.Unicode.GetString(buffer, 0, nullPos);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	internal static void WriteString_0x00000000(BinaryWriter writer, string str) => WriteZString(writer, str);

	internal static void WriteWideString_0x00000000(BinaryWriter writer, string str)
	{
		byte[] bytes = Encoding.Unicode.GetBytes(str);
		writer.Write(bytes);
		writer.Write((ushort)0);
	}

	private static string ReadWideString_0x07564429(BinaryReader reader, IList<string> stringTable)
	{
		uint index = reader.ReadUInt32();
		if (index >= (uint)stringTable.Count)
			throw new InvalidDataException(
				$"String table index {index} out of range (table size={stringTable.Count}). File may be corrupted.");
		return stringTable[(int)index];
	}

	internal static void WriteWideString_0x07564429(BinaryWriter writer, string str, List<string> stringTable, Dictionary<string, uint> stringLookup)
	{
		uint value = uint.MaxValue;
		if (!stringLookup.TryGetValue(str, out value))
		{
			value = (uint)stringTable.Count;
			stringTable.Add(str);
			stringLookup.Add(str, value);
		}
		writer.Write(value);
	}

	private static Color ReadColor_0x00000000(BinaryReader reader)
	{
		byte red = reader.ReadByte();
		byte green = reader.ReadByte();
		byte blue = reader.ReadByte();
		return Color.FromArgb(red, green, blue);
	}

	private static void WriteColor_0x00000000(BinaryWriter writer, Color color)
	{
		writer.Write(color.R);
		writer.Write(color.G);
		writer.Write(color.B);
	}

	// ── Core read/write with delegate injection ──

	private static PropertyTable ReadPropertyTableCore(
		BinaryReader reader,
		Func<BinaryReader, string> readName,
		Func<BinaryReader, string> readWString,
		int depth = 0)
	{
		if (depth > PropertyTable.MaxNestingDepth)
			throw new InvalidDataException($"Property table nesting depth {depth} exceeds maximum {PropertyTable.MaxNestingDepth}. File may be corrupted.");

		PropertyTable propertyTable = new PropertyTable();
		PropertyType propertyType;
		while ((propertyType = (PropertyType)reader.ReadByte()) != PropertyType._EndOfTable_)
		{
			string name = readName(reader);
			propertyTable.AddPropertyValue(value: propertyType switch
			{
				PropertyType.Table => ReadPropertyTableCore(reader, readName, readWString, depth + 1),
				PropertyType.String => ReadString_0x00000000(reader),
				PropertyType.WString => readWString(reader),
				PropertyType.Int32 => reader.ReadInt32(),
				PropertyType.Uint64 => reader.ReadUInt64(),
				PropertyType.Float => reader.ReadSingle(),
				PropertyType.Color => ReadColor_0x00000000(reader),
				_ => throw new InvalidDataException("Unknown property type " + (byte)propertyType + " at position " + reader.BaseStream.Position + ". File may be corrupted."),
			}, name: name, type: propertyType);
		}
		return propertyTable;
	}

	private static void WritePropertyTableCore(
		BinaryWriter writer,
		PropertyTable table,
		Action<BinaryWriter, string> writeName,
		Action<BinaryWriter, string> writeWString,
		int depth = 0)
	{
		if (depth > PropertyTable.MaxNestingDepth)
			throw new InvalidDataException($"Property table nesting depth {depth} exceeds maximum {PropertyTable.MaxNestingDepth}.");

		foreach (Property property in table.Properties)
		{
			writer.Write((byte)property.PropertyType);
			writeName(writer, property.Name);
			switch (property.PropertyType)
			{
			case PropertyType.Table:
				WritePropertyTableCore(writer, (PropertyTable)property.Value!, writeName, writeWString, depth + 1);
				break;
			case PropertyType.String:
				WriteString_0x00000000(writer, (string)property.Value!);
				break;
			case PropertyType.WString:
				writeWString(writer, (string)property.Value!);
				break;
			case PropertyType.Int32:
				writer.Write((int)property.Value!);
				break;
			case PropertyType.Uint64:
				writer.Write((ulong)property.Value!);
				break;
			case PropertyType.Float:
				writer.Write((float)property.Value!);
				break;
			case PropertyType.Color:
				WriteColor_0x00000000(writer, (Color)property.Value!);
				break;
			default:
				throw new InvalidDataException("The property type " + property.PropertyType.ToString() + " has not been implemented.");
			}
		}
		writer.Write((byte)PropertyType._EndOfTable_);
	}

	// ── Public API: thin wrappers (native when available, managed otherwise) ──

	public static PropertyTable ReadPropertyTable_0x00000000(BinaryReader reader)
	{
		if (Native)
		{
			long start = reader.BaseStream.Position;
			if (TryGetRemainingSource(reader, start, out ReadOnlySpan<byte> src))
			{
				byte[]? flat = SteameditNative.VdfToFlat(src, out int consumed);
				if (flat == null)
					throw new InvalidDataException(SteameditNative.LastError);
				reader.BaseStream.Position = start + consumed;
				return FlatTableCodec.FromFlat(flat);
			}
		}
		return ReadPropertyTableCore(reader, ReadString_0x00000000, ReadWideString_0x00000000);
	}

	public static void WritePropertyTable_0x00000000(BinaryWriter writer, PropertyTable table)
	{
		if (Native)
		{
			byte[]? data = SteameditNative.FlatToVdf(FlatTableCodec.ToFlat(table));
			if (data == null)
				throw new InvalidDataException(SteameditNative.LastError);
			writer.Write(data);
			return;
		}
		WritePropertyTableCore(writer, table, WriteString_0x00000000, WriteWideString_0x00000000);
	}

	public static byte[] WritePropertyTable_0x00000000(PropertyTable table)
	{
		if (Native)
		{
			byte[]? data = SteameditNative.FlatToVdf(FlatTableCodec.ToFlat(table));
			if (data == null)
				throw new InvalidDataException(SteameditNative.LastError);
			return data;
		}
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		WritePropertyTableCore(binaryWriter, table, WriteString_0x00000000, WriteWideString_0x00000000);
		return ((MemoryStream)binaryWriter.BaseStream).ToArray();
	}

	public static PropertyTable ReadPropertyTable_0x07564429(BinaryReader reader, IList<string> stringTable)
	{
		if (Native)
		{
			long start = reader.BaseStream.Position;
			if (TryGetRemainingSource(reader, start, out ReadOnlySpan<byte> src))
			{
				NativeStringTable nativeTable = GetNativeStringTable(stringTable);
				byte[]? flat = SteameditNative.VdfToFlat07564429(src, nativeTable.Handle, out int consumed);
				if (flat == null)
					throw new InvalidDataException(SteameditNative.LastError);
				reader.BaseStream.Position = start + consumed;
				return FlatTableCodec.FromFlat(flat, stringTable);
			}
		}
		return ReadPropertyTableCore(reader, r => ReadWideString_0x07564429(r, stringTable), r => ReadWideString_0x07564429(r, stringTable));
	}

	public static void WritePropertyTable_0x07564429(BinaryWriter writer, PropertyTable table, List<string> stringTable, Dictionary<string, uint> stringLookup)
		=> WritePropertyTableCore(writer, table, (w, s) => WriteWideString_0x07564429(w, s, stringTable, stringLookup), (w, s) => WriteWideString_0x07564429(w, s, stringTable, stringLookup));

	public static byte[] WritePropertyTable_0x07564429(PropertyTable table, List<string> stringTable, Dictionary<string, uint> stringLookup)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		WritePropertyTable_0x07564429(binaryWriter, table, stringTable, stringLookup);
		return ((MemoryStream)binaryWriter.BaseStream).ToArray();
	}
}
