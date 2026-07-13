#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace steamedit_Enhance.Data;

internal class LaunchTable
{
	private PropertyTable _propertyTable;
	private int _nextIndex;
	private List<LaunchItem>? _cachedItems;

	public PropertyTable PropertyTable => _propertyTable;

	public int Count => _propertyTable.Count;

	public IEnumerable<LaunchItem> Items => _cachedItems ??= BuildItems();

	private List<LaunchItem> BuildItems()
	{
		var items = new List<(string Key, LaunchItem Item)>();
		foreach (var prop in _propertyTable.Properties)
		{
			if (prop.PropertyType != PropertyType.Table)
				continue;

			var table = prop.GetValue<PropertyTable>();

			if (ShouldSkipNonWindows(table))
				continue;

			items.Add((prop.Name, new LaunchItem
			{
				Label = table.GetPropertyValue("description", ""),
				Executable = table.GetPropertyValue("executable", ""),
				Arguments = table.GetPropertyValue("arguments", ""),
				WorkingDir = table.GetPropertyValue("workingdir", "")
			}));
		}
		items.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
		return items.ConvertAll(t => t.Item);
	}

	private static bool ShouldSkipNonWindows(PropertyTable table)
	{
		if (!table.TryGetPropertyValue<PropertyTable>("config", out var config))
			return false;
		if (!config.TryGetPropertyValue<string>("oslist", out var oslist))
			return false;
		return !string.Equals(oslist, "windows", StringComparison.OrdinalIgnoreCase);
	}

	public LaunchTable()
	{
		_propertyTable = new PropertyTable();
		_nextIndex = 0;
	}

	public LaunchTable(LaunchTable other)
	{
		_propertyTable = new PropertyTable(other.PropertyTable);
		_nextIndex = other._nextIndex;
	}

	public LaunchTable(PropertyTable table)
	{
		_propertyTable = new PropertyTable(table);
		_nextIndex = table.Count;
	}

	public LaunchTable(IEnumerable<LaunchItem> items)
	{
		_propertyTable = new PropertyTable();
		foreach (LaunchItem item in items)
		{
			Add(item);
		}
	}

	public void Clear()
	{
		_propertyTable.Clear();
		_nextIndex = 0;
		_cachedItems = null;
	}

	public void Add(LaunchItem item)
	{
		PropertyTable propertyTable = new PropertyTable();
		propertyTable.SetPropertyValue("executable", PropertyType.String, item.Executable);
		if (item.Label.Length > 0)
		{
			propertyTable.SetPropertyValue("description", PropertyType.String, item.Label);
		}
		if (item.Arguments.Length > 0)
		{
			propertyTable.SetPropertyValue("arguments", PropertyType.String, item.Arguments);
		}
		if (item.WorkingDir.Length > 0)
		{
			propertyTable.SetPropertyValue("workingdir", PropertyType.String, item.WorkingDir);
		}
		_propertyTable.SetPropertyValue(_nextIndex.ToString(), PropertyType.Table, propertyTable);
		_nextIndex++;
		_cachedItems = null;
	}
}
