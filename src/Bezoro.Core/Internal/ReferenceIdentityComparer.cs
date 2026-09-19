using System.Runtime.CompilerServices;

namespace Bezoro.Core.Internal;

internal sealed class ReferenceIdentityComparer<T> : IEqualityComparer<T> where T : class
{
	public static ReferenceIdentityComparer<T> Instance { get; } = new();

	private ReferenceIdentityComparer() { }

	public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
	public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
