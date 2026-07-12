#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SteamEdit.Data;

internal class PropertyTable : IEquatable<PropertyTable>
{
	internal const int MaxNestingDepth = 32;

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

	public bool HasProperty(params string[] PropertyPath)
	{
		if (PropertyPath.Length == 0) return false;
		PropertyTable? parent = TraverseToParent(PropertyPath);
		return parent != null && parent[PropertyPath[PropertyPath.Length - 1]] != null;
	}

	public Property? GetRawProperty(params string[] PropertyPath)
	{
		PropertyTable? parent = TraverseToParent(PropertyPath);
		return parent?[PropertyPath[PropertyPath.Length - 1]];
	}

	public void SetRawProperty(Property prop, params string[] PropertyPath)
	{
		if (prop == null)
		{
			RemoveProperty(PropertyPath);
			return;
		}
		PropertyTable? parent = TraverseToParent(PropertyPath, createMissing: true);
		parent?.SetPropertyValue(PropertyPath[PropertyPath.Length - 1], prop.PropertyType, prop.Value);
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

	public T GetPropertyValue<T>(T DefaultValue, params string[] PropertyPath)
	{
		if (PropertyPath.Length == 0) return DefaultValue;
		PropertyTable? parent = TraverseToParent(PropertyPath);
		return parent == null ? DefaultValue : parent.GetPropertyValue(PropertyPath[PropertyPath.Length - 1], DefaultValue);
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
			_hashCodeDirty = true; _propertyNamesCache = null;
		}
		bool result = property.PropertyType != type || !Equals(property.Value, value);
		property.PropertyType = type;
		property.Value = value;
		_hashCodeDirty = true;
		return result;
	}

	public bool SetPropertyValue(PropertyType type, object? value, params string[] propertyPath)
	{
		PropertyTable? parent = TraverseToParent(propertyPath, createMissing: true);
		if (parent == null) return false;
		return parent.SetPropertyValue(propertyPath[propertyPath.Length - 1], type, value);
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
		_properties.AddRange(other._properties.Select((Property prop) => new Property(prop)));
		foreach (var prop in _properties)
		{
			_propertyMap[prop.Name] = prop;
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

	private string ToStringInternal(int indent)
	{
		if (indent > MaxNestingDepth)
			throw new InvalidDataException($"Property table nesting depth {indent} exceeds maximum {MaxNestingDepth}.");
		StringBuilder stringBuilder = new StringBuilder();
		string arg = new string('\t', indent);
		foreach (Property property in _properties)
		{
			string arg2 = property.Name.Replace("\\", "\\\\").Replace("\"", "\\\"");
			stringBuilder.AppendFormat("{0}\"{1}\"", arg, arg2);
			switch (property.PropertyType)
			{
			case PropertyType.Table:
				stringBuilder.AppendFormat("\n{0}{{\n{1}{0}}}", arg, property.GetValue<PropertyTable>().ToStringInternal(indent + 1));
				break;
			case PropertyType.String:
			case PropertyType.WString:
				stringBuilder.AppendFormat("\t\t\"{0}\"", property.GetValue<string>().Replace("\\", "\\\\").Replace("\"", "\\\""));
				break;
			case PropertyType.Int32:
			case PropertyType.Float:
			case PropertyType.Color:
			case PropertyType.Uint64:
				stringBuilder.AppendFormat("\t\t\"{0}\"", property.Value);
				break;
			default:
				throw new InvalidDataException("The property type " + property.PropertyType.ToString() + " is invalid.");
			}
			stringBuilder.Append("\n");
		}
		return stringBuilder.ToString();
	}

	public override string ToString()
	{
		return ToStringInternal(0);
	}
}
