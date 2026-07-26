namespace Bezoro.Logging.Internal;

internal sealed class AsyncContextScope : IDisposable
{
	private static readonly AsyncLocal<AsyncContextScope?> Current = new();
	private int _disposed;

	internal AsyncContextScope(string contextName)
	{
		Name = contextName;
		Parent = Current.Value;
		Depth = (Parent?.Depth ?? 0) + 1;
		Current.Value = this;
	}

	internal int Depth { get; }

	internal string Name { get; }

	internal AsyncContextScope? Parent { get; }

	internal static IReadOnlyList<string>? CurrentHierarchy => Current.Value?.ToHierarchy();

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0)
			Current.Value = Parent;
	}

	private string[] ToHierarchy()
	{
		var hierarchy = new string[Depth];

		for (AsyncContextScope? scope = this; scope != null; scope = scope.Parent)
			hierarchy[scope.Depth - 1] = scope.Name;

		return hierarchy;
	}
}
