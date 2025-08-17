// Enables C# 9 init-only setters on older .NET Standard frameworks.
// This type is required by the C# compiler to recognize and allow
// the 'init' accessor keyword when targeting frameworks that do not
// natively support init-only properties.

using System.ComponentModel;

#if NETSTANDARD || NETSTANDARD2_0 || NETSTANDARD2_1
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
	/// <summary>
	///     Enables C# 9 init-only setters on older frameworks.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal static class IsExternalInit { }
}
#endif
