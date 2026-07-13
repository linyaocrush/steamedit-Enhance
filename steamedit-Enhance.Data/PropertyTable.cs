#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace steamedit_Enhance.Data;

internal class PropertyTable : IEquatable<PropertyTable>
{
	internal const int MaxNestingDepth = 64;

	private List<Property> _properties = new List<Property>();
	private Dictionary<string, Property> _propertyMap = new Dictionary<string, Property>(StringComparer.Ordinal);
	private int _hashCode;
	private bool _hashCodeDirty = true;
	private string[]? _propertyNamesCache;

	public int Count => _properties.Count;

	public IEnumerable<string> PropertyNames
	{
		get
		{
			if (_propertyNamesCache == null)
			{
				_propertyNamesCache = new string[_properties.Count];
				for (int i = 0; i < _properties.Count; i++)
					_propertyNamesCache[i] = _properties[i].Name;
			}
			return _propertyNamesCache;
		}
	}

	internal IEnumerable<Property> Properties => _properties;

	internal Property? this[string name] => _propertyMap.TryGetValue(name, out var prop) ? prop : null;

	private static void ValidatePathDepth(int length)
	{
		if (length > MaxNestingDepth)
			throw new InvalidDataException($"Path depth {length} exceeds maximum {MaxNestingDepth}.");
	}

	/// <summary>
	/// 沿路径遍历到父级 PropertyTable（路径中除最后一个元素外的所有元素）。
	/// createMissing=true 时自动创建缺失的中间 Table；否则遇到缺失返回 null。
	/// </summary>
	private PropertyTable? TraverseToParent(string[] path, bool createMissing = false)
	{
		if (path.Length == 0) throw new ArgumentException("Path must not be empty.", nameof(path));
		ValidatePathDepth(path.Length);
		PropertyTable current = this;
		for (int i = 0; i < path.Length - 1; i++)
		{
			Property? prop = current[path[i]];
			if (prop != null)
			{
				PropertyTable? next = prop.GetValue<PropertyTable>();
				if (next == null) return null;
				current = next;
			}
			else if (createMissing)
			{
				PropertyTable next = new PropertyTable();
				current.SetPropertyValue(path[i], PropertyType.Table, next);
				current = next;
			}
			else
			{
				return null;
			}
		}
		return current;
	}

	private PropertyTable? TraverseToParent(string[] path, out string lastName, bool createMissing = false)
	{
		lastName = path[^1];
		return TraverseToParent(path, createMissing);
	}

	private PropertyTable? TraverseToParent(ReadOnlySpan<string> path, out string lastName, bool createMissing = false)
	{
		lastName = path[^1];
		if (path.Length == 0) throw new ArgumentException("Path must not be empty.", nameof(path));
		ValidatePathDepth(path.Length);
		PropertyTable current = this;
		for (int i = 0; i < path.Length - 1; i++)
		{
			Property? prop = current[path[i]];
			if (prop != null)
			{
				PropertyTable? next = prop.GetValue<PropertyTable>();
				if (next == null) return null;
				current = next;
			}
			else if (createMissing)
			{
				PropertyTable next = new PropertyTable();
				current.SetPropertyValue(path[i], PropertyType.Table, next);
				current = next;
			}
			else
			{
				return null;
			}
		}
		return current;
	}

	public bool HasProperty(params string[] propertyPath)
		=> HasProperty(propertyPath.AsSpan());

	public bool HasProperty(ReadOnlySpan<string> propertyPath)
	{
		if (propertyPath.Length == 0) return false;
		PropertyTable? parent = TraverseToParent(propertyPath, out var lastName);
		return parent != null && parent[lastName] != null;
	}

	public Property? GetRawProperty(params string[] propertyPath)
		=> GetRawProperty(propertyPath.AsSpan());

	public Property? GetRawProperty(ReadOnlySpan<string> propertyPath)
	{
		PropertyTable? parent = TraverseToParent(propertyPath, out var lastName);
		return parent?[lastName];
	}

	public void SetRawProperty(Property prop, params string[] propertyPath)
	{
		if (prop == null)
		{
			RemoveProperty(propertyPath);
			return;
		}
		PropertyTable? parent = TraverseToParent(propertyPath, out var lastName, createMissing: true);
		parent?.SetPropertyValue(lastName, prop.PropertyType, prop.Value);
	}

	public T GetPropertyValue<T>(string name, T defValue = default!)
	{
		if (!TryGetPropertyValue<T>(name, out var result))
		{
			return defValue;
		}
		return result;
	}

	public bool TryGetPropertyValue<T>(string name, out T result)
	{
		bool result2 = false;
		result = default!;
		Property? property = this[name];
		if (property != null)
		{
			result2 = property.TryGetValue<T>(out result);
		}
		return result2;
	}

	public object? GetPropertyAsObject(string name)
	{
		object? result = null;
		Property? property = this[name];
		if (property != null && property.Value != null)
		{
			result = property.Value;
		}
		return result;
	}

	public T GetPropertyValue<T>(T defaultValue, params string[] propertyPath)
		=> GetPropertyValueCore(defaultValue, propertyPath.AsSpan());

	public T GetPropertyValue<T>(T defaultValue, ReadOnlySpan<string> propertyPath)
		=> GetPropertyValueCore(defaultValue, propertyPath);

	private T GetPropertyValueCore<T>(T defaultValue, ReadOnlySpan<string> propertyPath)
	{
		if (propertyPath.Length == 0) return defaultValue;
		PropertyTable? parent = TraverseToParent(propertyPath, out var lastName);
		return parent == null ? defaultValue : parent.GetPropertyValue(lastName, defaultValue);
	}

	public PropertyType GetPropertyType(string name)
	{
		PropertyType result = PropertyType._Invalid_;
		Property? property = this[name];
		if (property != null)
		{
			result = property.PropertyType;
		}
		return result;
	}

	public void AddPropertyValue(string name, PropertyType type, object? value)
	{
		Property item = new Property(name, type, value);
		_properties.Add(item);
		_propertyMap[name] = item;
		_hashCodeDirty = true; _propertyNamesCache = null;
	}

	public bool SetPropertyValue(string name, PropertyType type, object? value)
	{
		Property? property = this[name];
		if (property == null)
		{
			property = new Property(name);
			_properties.Add(property);
			_propertyMap[name] = property;
			_propertyNamesCache = null;
		}
		bool result = property.PropertyType != type || !Equals(property.Value, value);
		property.PropertyType = type;
		property.Value = value;
		_hashCodeDirty = true;
		return result;
	}

	public bool SetPropertyValue(PropertyType type, object? value, params string[] propertyPath)
		=> SetPropertyValueCore(type, value, propertyPath.AsSpan());

	public bool SetPropertyValue(PropertyType type, object? value, ReadOnlySpan<string> propertyPath)
		=> SetPropertyValueCore(type, value, propertyPath);

	private bool SetPropertyValueCore(PropertyType type, object? value, ReadOnlySpan<string> propertyPath)
	{
		PropertyTable? parent = TraverseToParent(propertyPath, out var lastName, createMissing: true);
		if (parent == null) return false;
		return parent.SetPropertyValue(lastName, type, value);
	}

	public void RemoveProperty(string name)
	{
		if (_propertyMap.TryGetValue(name, out var prop))
		{
			_properties.Remove(prop);
			_propertyMap.Remove(name);
			_hashCodeDirty = true; _propertyNamesCache = null;
		}
	}

	public void RemoveProperty(params string[] propertyPath)
	{
		ValidatePathDepth(propertyPath.Length);
		Stack<PropertyTable> stack = new Stack<PropertyTable>();
		Stack<string> stack2 = new Stack<string>();
		PropertyTable propertyTable = this;
		for (int i = 0; i < propertyPath.Length - 1; i++)
		{
			string item = propertyPath[i];
			stack.Push(propertyTable);
			stack2.Push(item);
			if (propertyTable.HasProperty(item))
			{
				propertyTable = propertyTable.GetPropertyValue<PropertyTable>(item);
				if (propertyTable == null)
				{
					return;
				}
				continue;
			}
			return;
		}
		propertyTable.RemoveProperty(propertyPath[propertyPath.Length - 1]);
		if (propertyTable.Count == 0)
		{
			while (stack.Count > 0)
			{
				PropertyTable propertyTable2 = stack.Pop();
				string name = stack2.Pop();
				propertyTable2.RemoveProperty(name);
			}
		}
	}

	public void Clear()
	{
		_properties.Clear();
		_propertyMap.Clear();
		_hashCodeDirty = true;
		_propertyNamesCache = null;
	}

	public PropertyTable()
	{
	}

	public PropertyTable(PropertyTable other)
	{
		int count = other._properties.Count;
		_properties.Capacity = count;
		_propertyMap = new Dictionary<string, Property>(count, StringComparer.Ordinal);
		foreach (var prop in other._properties)
		{
			var clone = new Property(prop);
			_properties.Add(clone);
			_propertyMap[clone.Name] = clone;
		}
	}

	public bool Equals(PropertyTable? other)
	{
		if (other == null || Count != other.Count) return false;
		return _properties.All(prop => other._propertyMap.TryGetValue(prop.Name, out var otherProp) && prop.Equals(otherProp));
	}

	public override bool Equals(object? obj)
	{
		return Equals(obj as PropertyTable);
	}

	public override int GetHashCode()
	{
		if (_hashCodeDirty)
		{
			int num = GetType().GetHashCode();
			foreach (Property property in _properties)
			{
				num = num * 31 + property.GetHashCode();
			}
			_hashCode = num;
			_hashCodeDirty = false;
		}
		return _hashCode;
	}

	public void WriteTo(TextWriter writer)
	{
		WriteToInternal(writer, 0);
	}

	private void WriteToInternal(TextWriter writer, int indent)
	{
		if (indent > MaxNestingDepth)
			throw new InvalidDataException($"Property table nesting depth {indent} exceeds maximum {MaxNestingDepth}.");
		string arg = new string('\t', indent);
		foreach (Property property in _properties)
		{
			string arg2 = property.Name.Replace("\\", "\\\\").Replace("\"", "\\\"");
			writer.Write(arg);
			writer.Write('"');
			writer.Write(arg2);
			writer.Write('"');
			switch (property.PropertyType)
			{
			case PropertyType.Table:
				writer.Write('\n');
				writer.Write(arg);
				writer.Write("{\n");
				property.GetValue<PropertyTable>().WriteToInternal(writer, indent + 1);
				writer.Write(arg);
				writer.Write('}');
				break;
			case PropertyType.String:
			case PropertyType.WString:
				writer.Write("\t\t\"");
				writer.Write(property.GetValue<string>().Replace("\\", "\\\\").Replace("\"", "\\\""));
				writer.Write('"');
				break;
			case PropertyType.Int32:
			case PropertyType.Float:
			case PropertyType.Color:
			case PropertyType.Uint64:
				writer.Write("\t\t\"");
				writer.Write(property.Value);
				writer.Write('"');
				break;
			default:
				throw new InvalidDataException("The property type " + property.PropertyType.ToString() + " is invalid.");
			}
			writer.Write('\n');
		}
	}

	public override string ToString()
	{
		using var sw = new StringWriter();
		WriteTo(sw);
		return sw.ToString();
	}
}
