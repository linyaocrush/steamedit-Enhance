#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace steamedit_Enhance.Data;

internal static class PropertyTableIO
{
	private const int MaxZStringBuffer = 1024 * 1024; // 1MB cap to prevent OOM on malformed files

	internal static string ReadZString(BinaryReader reader)
	{
		Span<byte> stackBuf = stackalloc byte[256];
		byte[]? heapBuf = null;
		int len = 0;
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		byte value;
		while (len < remaining && (value = reader.ReadByte()) != 0)
		{
			if (len == (heapBuf?.Length ?? stackBuf.Length))
			{
				int oldCapacity = heapBuf?.Length ?? stackBuf.Length;
				int newSize = oldCapacity * 2;
				if (newSize > MaxZStringBuffer)
					throw new InvalidDataException("ReadZString: string exceeds maximum buffer size. File may be corrupted.");
				byte[] newBuf = new byte[newSize];
				if (heapBuf != null)
					heapBuf.CopyTo(newBuf.AsSpan());
				else
					stackBuf[..oldCapacity].CopyTo(newBuf);
				heapBuf = newBuf;
			}
			if (heapBuf != null)
				heapBuf[len] = value;
			else
				stackBuf[len] = value;
			len++;
		}
		if (len >= remaining)
			throw new InvalidDataException("ReadZString: null terminator not found before end of stream");
		return Encoding.UTF8.GetString(heapBuf ?? stackBuf[..len]);
	}

	internal static void WriteZString(BinaryWriter writer, string str)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(str);
		writer.Write(bytes);
		writer.Write((byte)0);
	}

	private static string ReadString_0x00000000(BinaryReader reader) => ReadZString(reader);

	private static string ReadWideString_0x00000000(BinaryReader reader)
	{
		StringBuilder stringBuilder = new StringBuilder();
		long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
		long maxChars = remaining / 2;
		while (stringBuilder.Length < maxChars)
		{
			char c = (char)reader.ReadUInt16();
			if (c == 0) break;
			stringBuilder.Append(c);
		}
		if (stringBuilder.Length >= maxChars)
			throw new InvalidDataException("ReadWideString: null terminator not found before end of stream");
		return stringBuilder.ToString();
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

	// ── Public API: thin wrappers ──

	public static PropertyTable ReadPropertyTable_0x00000000(BinaryReader reader)
		=> ReadPropertyTableCore(reader, ReadString_0x00000000, ReadWideString_0x00000000);

	public static void WritePropertyTable_0x00000000(BinaryWriter writer, PropertyTable table)
		=> WritePropertyTableCore(writer, table, WriteString_0x00000000, WriteWideString_0x00000000);

	public static byte[] WritePropertyTable_0x00000000(PropertyTable table)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		WritePropertyTable_0x00000000(binaryWriter, table);
		return ((MemoryStream)binaryWriter.BaseStream).ToArray();
	}

	public static PropertyTable ReadPropertyTable_0x07564429(BinaryReader reader, IList<string> stringTable)
		=> ReadPropertyTableCore(reader, r => ReadWideString_0x07564429(r, stringTable), r => ReadWideString_0x07564429(r, stringTable));

	public static void WritePropertyTable_0x07564429(BinaryWriter writer, PropertyTable table, List<string> stringTable, Dictionary<string, uint> stringLookup)
		=> WritePropertyTableCore(writer, table, (w, s) => WriteWideString_0x07564429(w, s, stringTable, stringLookup), (w, s) => WriteWideString_0x07564429(w, s, stringTable, stringLookup));

	public static byte[] WritePropertyTable_0x07564429(PropertyTable table, List<string> stringTable, Dictionary<string, uint> stringLookup)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		WritePropertyTable_0x07564429(binaryWriter, table, stringTable, stringLookup);
		return ((MemoryStream)binaryWriter.BaseStream).ToArray();
	}
}
