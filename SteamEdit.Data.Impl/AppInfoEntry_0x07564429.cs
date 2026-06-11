using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Common;

namespace SteamEdit.Data.Impl;

internal class AppInfoEntry_0x07564429 : AppInfoEntryBase
{
	public override uint AppInfoVersion => 123094057u;

	protected override void HandleNameLocalizedRemoval(PropertyTable common, PropertyTable nameLocalized)
	{
		if ((_modifications != null && _modifications.Changes.HasProperty("appinfo", "common", "name")) || Settings.Default.ReplaceLocalizedNames)
		{
			common.RemoveProperty("name_localized");
		}
	}

	public override bool Revert()
	{
		if (_modifications != null)
		{
			if (_modifications.OriginalData == null)
			{
				throw new NotImplementedException();
			}
			using (BinaryReader reader = new BinaryReader(new MemoryStream(_modifications.OriginalData)))
			{
				_properties = PropertyTableIO.ReadPropertyTable_Mods(reader);
			}
			if (_properties != null)
			{
				ClearCachedProps();
				_modifications = null;
				RaiseModified();
				return true;
			}
		}
		return false;
	}

	public static AppInfoEntry_0x07564429 FromReader(BinaryReader reader, IList<string> stringTable)
	{
		int num = reader.ReadInt32();
		if (num == 0) return null;

		var entry = new AppInfoEntry_0x07564429 { ID = num };
		try
		{
			int count = reader.ReadInt32();
			using (BinaryReader subReader = new BinaryReader(new MemoryStream(reader.ReadBytes(count))))
			{
				entry._stuffBeforeHash = subReader.ReadBytes(16);
				subReader.ReadBytes(20);
				entry._changeNumber = subReader.ReadUInt32();
				subReader.ReadBytes(20);
				entry._properties = PropertyTableIO.ReadPropertyTable_0x07564429(subReader, stringTable);
				entry.RestoreBaseProperties();

				if (!entry._properties.HasProperty("appinfo", "common", "name_localized"))
				{
					PropertyTable nameLocalized = entry._properties.GetPropertyValue<PropertyTable>(null, "appinfo", "steam_edit", "base_name_localized");
					if (nameLocalized != null)
					{
						entry._properties.SetPropertyValue(PropertyType.Table, nameLocalized, "appinfo", "common", "name_localized");
					}
				}
			}
			entry._originalData = PropertyTableIO.WritePropertyTable_Mods(entry._properties);
			entry.ClearCachedProps();
			return entry;
		}
		catch (Exception e)
		{
			throw new AppInfoLoadException($"Failed to load entry with appId {entry.ID:X8}", e);
		}
	}

	public void Write(BinaryWriter writer, ref List<string> stringTable, ref Dictionary<string, uint> stringLookup)
	{
		PropertyTable table = PrepareWriteTable(out _);
		string s = table.ToString();
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		using var sha = SHA1.Create();
		byte[] hash = sha.ComputeHash(bytes);
		writer.Write(ID);
		byte[] tableData = PropertyTableIO.WritePropertyTable_0x07564429(table, stringTable, stringLookup);
		byte[] tableHash = sha.ComputeHash(tableData);
		using BinaryWriter subWriter = new BinaryWriter(new MemoryStream());
		subWriter.Write(_stuffBeforeHash);
		subWriter.Write(hash);
		subWriter.Write(_changeNumber);
		subWriter.Write(tableHash);
		subWriter.Write(tableData);
		MemoryStream ms = (MemoryStream)subWriter.BaseStream;
		writer.Write((int)ms.Length);
		writer.Write(ms.GetBuffer(), 0, (int)ms.Length);
	}
}
