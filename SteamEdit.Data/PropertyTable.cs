using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SteamEdit.Data;

internal class PropertyTable : IEquatable<PropertyTable>
{
	private List<Property> _properties = new List<Property>();
	private Dictionary<string, Property> _propertyMap = new Dictionary<string, Property>(StringComparer.Ordinal);

	public int Count => _properties.Count;

	public IEnumerable<string> PropertyNames => _properties.Select((Property prop) => prop.Name);

	internal IEnumerable<Property> Properties => _properties;

	internal Property this[string name] => _propertyMap.TryGetValue(name, out var prop) ? prop : null;

	public bool HasProperty(params string[] PropertyPath)
	{
		PropertyTable propertyTable = this;
		foreach (string name in PropertyPath)
		{
			if (propertyTable == null || propertyTable[name] == null)
			{
				return false;
			}
			propertyTable = propertyTable.GetPropertyValue<PropertyTable>(name);
		}
		return true;
	}

	public Property GetRawProperty(params string[] PropertyPath)
	{
		PropertyTable propertyTable = this;
		foreach (string item in PropertyPath.Take(PropertyPath.Length - 1))
		{
			Property property = propertyTable[item];
			if (property != null)
			{
				propertyTable = property.GetValue<PropertyTable>();
			}
			if (property == null || propertyTable == null)
			{
				return null;
			}
		}
		return propertyTable[PropertyPath.Last()];
	}

	public void SetRawProperty(Property prop, params string[] PropertyPath)
	{
		if (prop == null)
		{
			RemoveProperty(PropertyPath);
			return;
		}
		PropertyTable propertyTable = this;
		foreach (string item in PropertyPath.Take(PropertyPath.Length - 1))
		{
			if (propertyTable.HasProperty(item))
			{
				propertyTable = propertyTable.GetPropertyValue<PropertyTable>(item);
			}
			else
			{
				PropertyTable propertyTable2 = new PropertyTable();
				propertyTable.SetPropertyValue(item, PropertyType.Table, propertyTable2);
				propertyTable = propertyTable2;
			}
			if (propertyTable == null)
			{
				return;
			}
		}
		propertyTable.SetPropertyValue(PropertyPath.Last(), prop.PropertyType, prop.Value);
	}

	public T GetPropertyValue<T>(string name, T defValue = default(T))
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
		result = default(T);
		Property property = this[name];
		if (property != null)
		{
			result2 = property.TryGetValue<T>(out result);
		}
		return result2;
	}

	public object GetPropertyAsObject(string name)
	{
		object result = null;
		Property property = this[name];
		if (property != null && property.Value != null)
		{
			result = property.Value;
		}
		return result;
	}

	public T GetPropertyValue<T>(T DefaultValue, params string[] PropertyPath)
	{
		PropertyTable propertyTable = this;
		foreach (string item in PropertyPath.Take(PropertyPath.Length - 1))
		{
			Property property = propertyTable[item];
			if (property != null)
			{
				propertyTable = property.GetValue<PropertyTable>();
			}
			if (property == null || propertyTable == null)
			{
				return DefaultValue;
			}
		}
		return propertyTable.GetPropertyValue(PropertyPath.Last(), DefaultValue);
	}

	public PropertyType GetPropertyType(string name)
	{
		PropertyType result = PropertyType._Invalid_;
		Property property = this[name];
		if (property != null)
		{
			result = property.PropertyType;
		}
		return result;
	}

	public void AddPropertyValue(string name, PropertyType type, object value)
	{
		Property item = new Property(name, type, value);
		_properties.Add(item);
		_propertyMap[name] = item;
	}

	public bool SetPropertyValue(string name, PropertyType type, object value)
	{
		Property property = this[name];
		if (property == null)
		{
			property = new Property(name);
			_properties.Add(property);
			_propertyMap[name] = property;
		}
		bool result = property.PropertyType != type || !property.Value.Equals(value);
		property.PropertyType = type;
		property.Value = value;
		return result;
	}

	public bool SetPropertyValue(PropertyType type, object value, params string[] propertyPath)
	{
		PropertyTable propertyTable = this;
		foreach (string item in propertyPath.Take(propertyPath.Length - 1))
		{
			if (propertyTable.HasProperty(item))
			{
				propertyTable = propertyTable.GetPropertyValue<PropertyTable>(item);
			}
			else
			{
				PropertyTable propertyTable2 = new PropertyTable();
				propertyTable.SetPropertyValue(item, PropertyType.Table, propertyTable2);
				propertyTable = propertyTable2;
			}
			if (propertyTable == null)
			{
				return false;
			}
		}
		return propertyTable.SetPropertyValue(propertyPath.Last(), type, value);
	}

	public void RemoveProperty(string name)
	{
		_properties.RemoveAll((Property prop) => prop.Name == name);
		_propertyMap.Remove(name);
	}

	public void RemoveProperty(params string[] propertyPath)
	{
		Stack<PropertyTable> stack = new Stack<PropertyTable>();
		Stack<string> stack2 = new Stack<string>();
		PropertyTable propertyTable = this;
		foreach (string item in propertyPath.Take(propertyPath.Length - 1))
		{
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
		propertyTable.RemoveProperty(propertyPath.Last());
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

	public bool Equals(PropertyTable other)
	{
		if (other != null && Count == other.Count)
		{
			return _properties.All((Property prop) => other._properties.Any((Property otherProp) => prop.Equals(otherProp)));
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		return Equals(obj as PropertyTable);
	}

	public override int GetHashCode()
	{
		int num = GetType().GetHashCode();
		foreach (Property property in _properties)
		{
			num ^= property.GetHashCode();
		}
		return num;
	}

	private string ToStringInternal(int indent)
	{
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
				throw new NotImplementedException("The property type " + property.PropertyType.ToString() + " is invalid.");
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
