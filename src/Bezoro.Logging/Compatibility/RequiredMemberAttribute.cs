#if NETSTANDARD || NETSTANDARD2_0 || NETSTANDARD2_1
using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
	/// <summary>
	///     Indicates that a type has required members or that a member is required.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
	{
		public string FeatureName { get; } = featureName;
		public bool   IsOptional  { get; init; }
	}

	/// <summary>
	///     Enables C# 11 required members on older frameworks.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[AttributeUsage(
		AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
		Inherited = false
	)]
	internal sealed class RequiredMemberAttribute : Attribute { }
}
#endif
