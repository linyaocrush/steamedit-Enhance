using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace SteamEdit.Data;

internal static class PropertyTableIO
{
	internal static string ReadZString(BinaryReader reader)
	{
		byte[] buffer;
		int count;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			byte value;
			while ((value = reader.ReadByte()) != 0)
			{
				memoryStream.WriteByte(value);
			}
			buffer = memoryStream.GetBuffer();
			count = (int)memoryStream.Length;
		}
		return Encoding.UTF8.GetString(buffer, 0, count);
	}

	internal static void WriteZString(BinaryWriter writer, string str)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(str);
		writer.Write(bytes);
		writer.Write((byte)0);
	}

	private static string ReadString_0x00000000(BinaryReader reader)
	{
		byte[] buffer;
		int count;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			byte value;
			while ((value = reader.ReadByte()) != 0)
			{
				memoryStream.WriteByte(value);
			}
			buffer = memoryStream.GetBuffer();
			count = (int)memoryStream.Length;
		}
		return Encoding.UTF8.GetString(buffer, 0, count);
	}

	private static string ReadWideString_0x00000000(BinaryReader reader)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (char c = (char)reader.ReadUInt16(); c != 0; c = (char)reader.ReadUInt16())
		{
			stringBuilder.Append(c);
		}
		return stringBuilder.ToString();
	}

	internal static void WriteString_0x00000000(BinaryWriter writer, string str)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(str);
		writer.Write(bytes);
		writer.Write((byte)0);
	}

	internal static void WriteWideString_0x00000000(BinaryWriter writer, string str)
	{
		byte[] bytes = Encoding.Unicode.GetBytes(str);
		writer.Write(bytes);
		writer.Write((byte)0);
	}

	private static string ReadWideString_0x07564429(BinaryReader reader, IList<string> stringTable)
	{
		uint index = reader.ReadUInt32();
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

	public static PropertyTable ReadPropertyTable_0x00000000(BinaryReader reader)
	{
		PropertyTable propertyTable = new PropertyTable();
		PropertyType propertyType;
		while ((propertyType = (PropertyType)reader.ReadByte()) != PropertyType._EndOfTable_)
		{
			string name = ReadString_0x00000000(reader);
			object obj = null;
			propertyTable.AddPropertyValue(value: propertyType switch
			{
				PropertyType.Table => ReadPropertyTable_0x00000000(reader), 
				PropertyType.String => ReadString_0x00000000(reader), 
				PropertyType.WString => ReadWideString_0x00000000(reader), 
				PropertyType.Int32 => reader.ReadInt32(), 
				PropertyType.Uint64 => reader.ReadUInt64(), 
				PropertyType.Float => reader.ReadSingle(), 
				PropertyType.Color => ReadColor_0x00000000(reader), 
				_ => throw new NotImplementedException("The property type " + propertyType.ToString() + " has not been implemented."), 
			}, name: name, type: propertyType);
		}
		return propertyTable;
	}

	public static void WritePropertyTable_0x00000000(BinaryWriter writer, PropertyTable table)
	{
		foreach (Property property in table.Properties)
		{
			writer.Write((byte)property.PropertyType);
			WriteString_0x00000000(writer, property.Name);
			switch (property.PropertyType)
			{
			case PropertyType.Table:
				WritePropertyTable_0x00000000(writer, (PropertyTable)property.Value);
				break;
			case PropertyType.String:
				WriteString_0x00000000(writer, (string)property.Value);
				break;
			case PropertyType.WString:
				WriteWideString_0x00000000(writer, (string)property.Value);
				break;
			case PropertyType.Int32:
				writer.Write((int)property.Value);
				break;
			case PropertyType.Uint64:
				writer.Write((ulong)property.Value);
				break;
			case PropertyType.Float:
				writer.Write((float)property.Value);
				break;
			case PropertyType.Color:
				WriteColor_0x00000000(writer, (Color)property.Value);
				break;
			default:
				throw new NotImplementedException("The value type " + property.PropertyType.ToString() + " has not been implemented.");
			}
		}
		writer.Write((byte)8);
	}

	public static byte[] WritePropertyTable_0x00000000(PropertyTable table)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		WritePropertyTable_0x00000000(binaryWriter, table);
		return ((MemoryStream)binaryWriter.BaseStream).ToArray();
	}

	public static PropertyTable ReadPropertyTable_0x07564429(BinaryReader reader, IList<string> stringTable)
	{
		PropertyTable propertyTable = new PropertyTable();
		PropertyType propertyType;
		while ((propertyType = (PropertyType)reader.ReadByte()) != PropertyType._EndOfTable_)
		{
			string name = ReadWideString_0x07564429(reader, stringTable);
			object obj = null;
			propertyTable.AddPropertyValue(value: propertyType switch
			{
				PropertyType.Table => ReadPropertyTable_0x07564429(reader, stringTable), 
				PropertyType.String => ReadString_0x00000000(reader), 
				PropertyType.WString => ReadWideString_0x07564429(reader, stringTable), 
				PropertyType.Int32 => reader.ReadInt32(), 
				PropertyType.Uint64 => reader.ReadUInt64(), 
				PropertyType.Float => reader.ReadSingle(), 
				PropertyType.Color => ReadColor_0x00000000(reader), 
				_ => throw new NotImplementedException("The property type " + propertyType.ToString() + " has not been implemented."), 
			}, name: name, type: propertyType);
		}
		return propertyTable;
	}

	public static void WritePropertyTable_0x07564429(BinaryWriter writer, PropertyTable table, List<string> stringTable, Dictionary<string, uint> stringLookup)
	{
		foreach (Property property in table.Properties)
		{
			writer.Write((byte)property.PropertyType);
			WriteWideString_0x07564429(writer, property.Name, stringTable, stringLookup);
			switch (property.PropertyType)
			{
			case PropertyType.Table:
				WritePropertyTable_0x07564429(writer, (PropertyTable)property.Value, stringTable, stringLookup);
				break;
			case PropertyType.String:
				WriteString_0x00000000(writer, (string)property.Value);
				break;
			case PropertyType.WString:
				WriteWideString_0x07564429(writer, (string)property.Value, stringTable, stringLookup);
				break;
			case PropertyType.Int32:
				writer.Write((int)property.Value);
				break;
			case PropertyType.Uint64:
				writer.Write((ulong)property.Value);
				break;
			case PropertyType.Float:
				writer.Write((float)property.Value);
				break;
			case PropertyType.Color:
				WriteColor_0x00000000(writer, (Color)property.Value);
				break;
			default:
				throw new NotImplementedException("The value type " + property.PropertyType.ToString() + " has not been implemented.");
			}
		}
		writer.Write((byte)8);
	}

	public static byte[] WritePropertyTable_0x07564429(PropertyTable table, List<string> stringTable, Dictionary<string, uint> stringLookup)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		WritePropertyTable_0x07564429(binaryWriter, table, stringTable, stringLookup);
		return ((MemoryStream)binaryWriter.BaseStream).ToArray();
	}

	public static PropertyTable ReadPropertyTable_Mods(BinaryReader reader)
	{
		PropertyTable propertyTable = new PropertyTable();
		PropertyType propertyType;
		while ((propertyType = (PropertyType)reader.ReadByte()) != PropertyType._EndOfTable_)
		{
			string name = ReadString_0x00000000(reader);
			object obj = null;
			propertyTable.AddPropertyValue(value: propertyType switch
			{
				PropertyType.Table => ReadPropertyTable_Mods(reader), 
				PropertyType.String => ReadString_0x00000000(reader), 
				PropertyType.WString => ReadWideString_0x00000000(reader), 
				PropertyType.Int32 => reader.ReadInt32(), 
				PropertyType.Uint64 => reader.ReadUInt64(), 
				PropertyType.Float => reader.ReadSingle(), 
				PropertyType.Color => ReadColor_0x00000000(reader), 
				_ => throw new NotImplementedException("The property type " + propertyType.ToString() + " has not been implemented."), 
			}, name: name, type: propertyType);
		}
		return propertyTable;
	}

	public static void WritePropertyTable_Mods(BinaryWriter writer, PropertyTable table)
	{
		foreach (Property property in table.Properties)
		{
			writer.Write((byte)property.PropertyType);
			WriteString_0x00000000(writer, property.Name);
			switch (property.PropertyType)
			{
			case PropertyType.Table:
				WritePropertyTable_Mods(writer, (PropertyTable)property.Value);
				break;
			case PropertyType.String:
				WriteString_0x00000000(writer, (string)property.Value);
				break;
			case PropertyType.WString:
				WriteWideString_0x00000000(writer, (string)property.Value);
				break;
			case PropertyType.Int32:
				writer.Write((int)property.Value);
				break;
			case PropertyType.Uint64:
				writer.Write((ulong)property.Value);
				break;
			case PropertyType.Float:
				writer.Write((float)property.Value);
				break;
			case PropertyType.Color:
				WriteColor_0x00000000(writer, (Color)property.Value);
				break;
			default:
				throw new NotImplementedException("The value type " + property.PropertyType.ToString() + " has not been implemented.");
			}
		}
		writer.Write((byte)8);
	}

	public static byte[] WritePropertyTable_Mods(PropertyTable table)
	{
		using BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream());
		WritePropertyTable_Mods(binaryWriter, table);
		return ((MemoryStream)binaryWriter.BaseStream).ToArray();
	}
}
