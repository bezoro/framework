#if NETSTANDARD || NETSTANDARD2_0 || NETSTANDARD2_1
using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
	/// <summary>
	///     Enables C# 9 init-only members on older target frameworks.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal static class IsExternalInit { }
}
#endif
