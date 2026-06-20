#nullable enable
using System;
using System.Drawing;

namespace SteamEdit.Data;

internal class Property
{
	private static readonly Type?[] _types = new Type?[9]
	{
		typeof(PropertyTable),
		typeof(string),
		typeof(int),
		typeof(float),
		null,
		typeof(string),
		typeof(Color),
		typeof(ulong),
		null
	};

	private PropertyType _propType;

	private object? _value;

	public string Name { get; private set; }

	public PropertyType PropertyType
	{
		get
		{
			return _propType;
		}
		internal set
		{
			if (_propType != value)
			{
				_propType = value;
				_value = null;
			}
		}
	}

	public Type? ValueType => _types[(int)_propType];

	internal object? Value
	{
		get
		{
			return _value;
		}
		set
		{
			_value = null;
			if (value == null) return;
			if (ValueType == null) return;
			if (ValueType.IsAssignableFrom(value.GetType()))
			{
				_value = value;
			}
			else if (value is string text)
			{
				switch (_propType)
				{
				case PropertyType.String:
					_value = text;
					break;
				case PropertyType.Int32:
					if (int.TryParse(text, out int intVal))
						_value = intVal;
					else
						throw new FormatException($"Cannot convert \"{text}\" to Int32 for property \"{Name}\".");
					break;
				case PropertyType.Float:
					if (float.TryParse(text, out float floatVal))
						_value = floatVal;
					else
						throw new FormatException($"Cannot convert \"{text}\" to Float for property \"{Name}\".");
					break;
				case PropertyType.WString:
					_value = text;
					break;
				case PropertyType.Uint64:
					if (ulong.TryParse(text, out ulong ulongVal))
						_value = ulongVal;
					else
						throw new FormatException($"Cannot convert \"{text}\" to Uint64 for property \"{Name}\".");
					break;
				case PropertyType.Color:
					break;
				}
			}
		}
	}

	public T GetValue<T>()
	{
		TryGetValue<T>(out var result);
		return result;
	}

	public bool TryGetValue<T>(out T result)
	{
		result = default!;
		bool result2 = false;
		int propType = (int)_propType;
		if (propType >= 0 && propType < _types.Length)
		{
			Type? type = _types[propType];
			if (type != null && typeof(T).IsAssignableFrom(type))
			{
				result = (T)_value!;
				result2 = true;
			}
		}
		return result2;
	}

	public Property(string name)
	{
		Name = name;
		_propType = PropertyType._Invalid_;
		_value = null;
	}

	public Property(string name, PropertyType type, object? value)
	{
		Name = name;
		_propType = type;
		_value = value;
	}

	public Property(Property other)
	{
		Name = other.Name;
		_propType = other._propType;
		if (_propType == PropertyType.Table)
		{
			_value = new PropertyTable((PropertyTable)other._value!);
		}
		else
		{
			_value = other._value;
		}
	}

	public override bool Equals(object? obj)
	{
		bool result = false;
		if (obj is Property property)
		{
			result = Name == property.Name && _propType == property._propType && Equals(_value, property.Value);
		}
		return result;
	}

	public override int GetHashCode()
	{
		return GetType().GetHashCode() ^ Name.GetHashCode() ^ _propType.GetHashCode() ^ (_value?.GetHashCode() ?? 0);
	}
}
