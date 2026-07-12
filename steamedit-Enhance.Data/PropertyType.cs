#nullable enable
namespace steamedit_Enhance.Data;

internal enum PropertyType
{
	_Invalid_ = -1,
	Table = 0,
	String = 1,
	Int32 = 2,
	Float = 3,
	// Value 4 is unused in Steam's VDF format
	WString = 5,
	Color = 6,
	Uint64 = 7,
	_EndOfTable_ = 8
}
