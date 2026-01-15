using System.ComponentModel;

#if NETSTANDARD || NETSTANDARD2_0 || NETSTANDARD2_1
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
	/// <summary>
	///     Enables C# 11 required members on older frameworks.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[AttributeUsage(
		AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
		Inherited = false)]
	internal sealed class RequiredMemberAttribute : Attribute { }

	/// <summary>
	///     Indicates that a type has required members or that a member is required.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	internal sealed class CompilerFeatureRequiredAttribute : Attribute
	{
		public CompilerFeatureRequiredAttribute(string featureName)
		{
			FeatureName = featureName;
		}

		public string FeatureName { get; }
		public bool   IsOptional  { get; init; }
	}
}
#endif
