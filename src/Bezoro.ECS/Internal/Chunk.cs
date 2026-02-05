using Bezoro.ECS.Types;

namespace Bezoro.ECS.Internal;

internal sealed class Chunk
{
	public Chunk(Type[] componentTypes, int capacity)
	{
		Entities   = new Entity[capacity];
		Components = new Array[componentTypes.Length];

		for (var i = 0; i < componentTypes.Length; i++)
			Components[i] = Array.CreateInstance(componentTypes[i], capacity);
	}

	public Array[] Components { get; }

	public Entity[] Entities { get; }

	public int Count { get; set; }
}
