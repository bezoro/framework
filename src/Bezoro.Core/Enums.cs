namespace Bezoro.Core
{
	public enum ElementInclusionStrategy
	{
		Exclude = 0,
		Include
	}

	public enum Enums
	{
		Resize = 0,
		SetToNull
	}

	public enum MergingStrategy
	{
		Fill_Null_Elements_And_Append = 0,
		Fill_Null_Elements_And_Resize,
		Fill_Null_Elements_And_Prepend,
		Fill_Null_Elements_Only,
		Append_Only,
		Prepend_Only
	}
}
