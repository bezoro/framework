#if NETSTANDARD || NETSTANDARD2_0 || NETSTANDARD2_1
using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
	/// <summary>
	///     Indicates that the .locals init flag should not be set in method headers.
	///     This is a polyfill for .NET Standard 2.1 compatibility.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	[AttributeUsage(
		AttributeTargets.Module |
		AttributeTargets.Class |
		AttributeTargets.Struct |
		AttributeTargets.Interface |
		AttributeTargets.Constructor |
		AttributeTargets.Method |
		AttributeTargets.Property |
		AttributeTargets.Event,
		Inherited = false
	)]
	internal sealed class SkipLocalsInitAttribute : Attribute { }
}
#endif
