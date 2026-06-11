using System;
using System.Collections.Generic;
using System.Linq;

namespace SteamEdit.Data;

internal class LaunchTable
{
	private PropertyTable _propertyTable;

	public PropertyTable PropertyTable => new PropertyTable(_propertyTable);

	public PropertyTable PropertyTableRef => _propertyTable;

	public int Count => _propertyTable.Count;

	public IEnumerable<LaunchItem> Items => from prop in _propertyTable.Properties.Where((Property prop) => prop.PropertyType == PropertyType.Table).OrderBy((Property prop) => prop.Name, StringComparer.OrdinalIgnoreCase)
		select prop.GetValue<PropertyTable>() into table
		where !table.TryGetPropertyValue<PropertyTable>("config", out var result) || !result.TryGetPropertyValue<string>("oslist", out var result2) || result2 == "windows"
		select new LaunchItem
		{
			Label = table.GetPropertyValue("description", ""),
			Executable = table.GetPropertyValue("executable", ""),
			Arguments = table.GetPropertyValue("arguments", ""),
			WorkingDir = table.GetPropertyValue("workingdir", "")
		};

	public LaunchTable()
	{
		_propertyTable = new PropertyTable();
	}

	public LaunchTable(LaunchTable other)
	{
		_propertyTable = new PropertyTable(other.PropertyTable);
	}

	public LaunchTable(PropertyTable table)
	{
		_propertyTable = new PropertyTable(table);
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
		_propertyTable.SetPropertyValue(_propertyTable.Count.ToString(), PropertyType.Table, propertyTable);
	}
}
