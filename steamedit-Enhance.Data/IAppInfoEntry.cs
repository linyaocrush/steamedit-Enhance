#nullable enable
using System;
using Common;

namespace steamedit_Enhance.Data;

internal interface IAppInfoEntry
{
	int ID { get; }

	uint AppInfoVersion { get; }

	uint ChangeNumber { get; }

	ModifiedEntry? Modification { get; set; }

	bool IsModified { get; }

	bool IsHidden { get; set; }

	bool IsCompleted { get; set; }

	string Name { get; set; }

	bool HasSortAs { get; }

	string SortAs { get; set; }

	string NormalizedSortAs { get; }

	string InstallPath { get; }

	AppInfoEntryType Type { get; set; }

	LaunchTable Launches { get; set; }

	event EventHandler? Modified;

	byte[] GetOriginalDataCopy();

	T GetPropertyValue<T>(T DefaultValue, params string[] PropertyPath);

	void SetPropertyValue(PropertyType Type, object? Value, params string[] PropertyPath);

	void RevertModifiedProperty(params string[] PropertyPath);

	bool Revert();

	PropertyType? GetOriginalPropertyType(params string[] PropertyPath);

	object? GetOriginalPropertyValue(params string[] PropertyPath);
}
