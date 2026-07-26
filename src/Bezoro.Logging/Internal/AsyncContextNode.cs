namespace Bezoro.Logging.Internal;

internal sealed class AsyncContextNode
{
	internal AsyncContextNode(string name, AsyncContextNode? parent)
	{
		Name = name;
		Parent = parent;
		Depth = (parent?.Depth ?? 0) + 1;
	}

	internal int Depth { get; }

	internal string Name { get; }

	internal AsyncContextNode? Parent { get; }

	internal string[] ToHierarchy()
	{
		var hierarchy = new string[Depth];

		for (AsyncContextNode? node = this; node != null; node = node.Parent)
			hierarchy[node.Depth - 1] = node.Name;

		return hierarchy;
	}
}
