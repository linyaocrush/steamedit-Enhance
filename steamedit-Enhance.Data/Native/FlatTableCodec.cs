#nullable enable
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace steamedit_Enhance.Data.Native;

/// <summary>
/// Converts between <see cref="PropertyTable"/> object trees and the compact "flat"
/// byte stream understood by the native <c>steamedit_core</c> codec.
/// </summary>
/// <remarks>
/// A flat stream is a depth-first token sequence:
/// <c>kind:u8 [key:lenPrefixedUtf8 value]</c>. A table token (0) has no value and its
/// children follow until an end token (8). Integers are little-endian. String kinds,
/// including keys, are stored as a <c>u32</c> length followed by raw UTF-8 bytes.
/// </remarks>
internal static class FlatTableCodec
{
    private const byte EndOfTable = 8;
    private const byte StrInline = 0;
    private const byte StrIndex = 1;

    public static PropertyTable FromFlat(ReadOnlySpan<byte> flat) => FromFlat(flat, null);

    public static PropertyTable FromFlat(ReadOnlySpan<byte> flat, IList<string>? stringTable)
    {
        int pos = 0;
        return FromFlat(flat, ref pos, stringTable);
    }

    /// <summary>
    /// Parses one table starting at <paramref name="pos"/> and advances it past the
    /// table's end token. Used to read embedded tables (e.g. inside a mods container).
    /// </summary>
    public static PropertyTable FromFlat(ReadOnlySpan<byte> flat, ref int pos, IList<string>? stringTable)
    {
        var root = new PropertyTable();
        var stack = new Stack<PropertyTable>();
        stack.Push(root);

        while (pos < flat.Length)
        {
            byte kind = flat[pos++];
            if (kind == EndOfTable)
            {
                stack.Pop();
                if (stack.Count == 0) break; // end of the requested table
                continue;
            }

            string name = ReadStrRef(flat, ref pos, stringTable);
            PropertyTable current = stack.Peek();
            switch (kind)
            {
                case 0: // Table
                {
                    var child = new PropertyTable();
                    current.AddPropertyValue(name, PropertyType.Table, child);
                    stack.Push(child);
                    break;
                }
                case 1: // String
                    current.AddPropertyValue(name, PropertyType.String, ReadStrRef(flat, ref pos, stringTable));
                    break;
                case 5: // WString
                    current.AddPropertyValue(name, PropertyType.WString, ReadStrRef(flat, ref pos, stringTable));
                    break;
                case 2: // Int32
                {
                    int v = BinaryPrimitives.ReadInt32LittleEndian(flat[pos..]);
                    pos += 4;
                    current.AddPropertyValue(name, PropertyType.Int32, v);
                    break;
                }
                case 3: // Float
                {
                    float v = BinaryPrimitives.ReadSingleLittleEndian(flat[pos..]);
                    pos += 4;
                    current.AddPropertyValue(name, PropertyType.Float, v);
                    break;
                }
                case 7: // Uint64
                {
                    ulong v = BinaryPrimitives.ReadUInt64LittleEndian(flat[pos..]);
                    pos += 8;
                    current.AddPropertyValue(name, PropertyType.Uint64, v);
                    break;
                }
                case 6: // Color
                {
                    byte r = flat[pos], g = flat[pos + 1], b = flat[pos + 2];
                    pos += 3;
                    current.AddPropertyValue(name, PropertyType.Color, Color.FromArgb(r, g, b));
                    break;
                }
                default:
                    throw new InvalidDataException($"Unknown flat property type {kind} at offset {pos - 1}.");
            }
        }

        return root;
    }

    public static byte[] ToFlat(PropertyTable table)
    {
        var writer = new FlatWriter();
        WriteTable(writer, table);
        return writer.ToArray();
    }

    private static void WriteTable(FlatWriter writer, PropertyTable table)
    {
        foreach (Property property in table.Properties)
        {
            writer.U8((byte)property.PropertyType);
            writer.StrRefInline(property.Name);
            switch (property.PropertyType)
            {
                case PropertyType.Table:
                    WriteTable(writer, (PropertyTable)property.Value!);
                    break;
                case PropertyType.String:
                case PropertyType.WString:
                    writer.StrRefInline((string)property.Value!);
                    break;
                case PropertyType.Int32:
                    writer.I32((int)property.Value!);
                    break;
                case PropertyType.Float:
                    writer.F32((float)property.Value!);
                    break;
                case PropertyType.Uint64:
                    writer.U64((ulong)property.Value!);
                    break;
                case PropertyType.Color:
                {
                    var color = (Color)property.Value!;
                    writer.U8(color.R);
                    writer.U8(color.G);
                    writer.U8(color.B);
                    break;
                }
                default:
                    throw new InvalidDataException($"Property type {property.PropertyType} cannot be flattened.");
            }
        }
        writer.U8(EndOfTable);
    }

    private static string ReadStrRef(ReadOnlySpan<byte> flat, ref int pos, IList<string>? stringTable)
    {
        byte mode = flat[pos++];
        if (mode == StrInline)
        {
            uint len = BinaryPrimitives.ReadUInt32LittleEndian(flat[pos..]);
            pos += 4;
            string value = Encoding.UTF8.GetString(flat.Slice(pos, (int)len));
            pos += (int)len;
            return value;
        }
        if (mode == StrIndex)
        {
            uint index = BinaryPrimitives.ReadUInt32LittleEndian(flat[pos..]);
            pos += 4;
            if (stringTable == null || index >= (uint)stringTable.Count)
                throw new InvalidDataException($"String table index {index} out of range.");
            return stringTable[(int)index];
        }
        throw new InvalidDataException($"Unknown flat string mode {mode} at offset {pos - 1}.");
    }

    /// <summary>Minimal growable little-endian byte writer for the flat stream.</summary>
    private sealed class FlatWriter
    {
        private byte[] _buffer;
        private int _length;

        public FlatWriter(int capacity = 256) => _buffer = new byte[capacity];

        public int Length => _length;

        private void Ensure(int extra)
        {
            if (_length + extra > _buffer.Length)
                Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + extra));
        }

        public void U8(byte value)
        {
            Ensure(1);
            _buffer[_length++] = value;
        }

        public void U32(uint value)
        {
            Ensure(4);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_length), value);
            _length += 4;
        }

        public void I32(int value)
        {
            Ensure(4);
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_length), value);
            _length += 4;
        }

        public void F32(float value)
        {
            Ensure(4);
            BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_length), value);
            _length += 4;
        }

        public void U64(ulong value)
        {
            Ensure(8);
            BinaryPrimitives.WriteUInt64LittleEndian(_buffer.AsSpan(_length), value);
            _length += 8;
        }

        public void StrRefInline(string value)
        {
            U8(StrInline);
            int byteCount = Encoding.UTF8.GetByteCount(value);
            Ensure(4 + byteCount);
            BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_length), (uint)byteCount);
            _length += 4;
            Encoding.UTF8.GetBytes(value, _buffer.AsSpan(_length));
            _length += byteCount;
        }

        public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();
    }
}
