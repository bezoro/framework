namespace Bezoro.Core.Common.Enums;

public enum MergingStrategy
{
	Fill_Null_Elements_And_Append = 0,
	Fill_Null_Elements_And_Resize,
	Fill_Null_Elements_And_Prepend,
	Fill_Null_Elements_Only,
	Append_Only,
	Prepend_Only
}
