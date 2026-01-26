using System.Collections.Generic;
using System.Numerics;
using Bezoro.GameSystems.StreamingSystem.Abstractions;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

internal sealed class TestEntity : IStreamableEntity
{
	private readonly List<string> _events = new();

	public TestEntity(int id, Vector3 position)
	{
		EntityId          = id;
		StreamingPosition = position;
	}

	public int                   EntityId          { get; }
	public IReadOnlyList<string> Events            => _events;
	public bool                  IsStreamedIn      { get; private set; }
	public Vector3               StreamingPosition { get; set; }

	public void OnStreamIn()
	{
		IsStreamedIn = true;
		_events.Add("StreamIn");
	}

	public void OnStreamOut()
	{
		IsStreamedIn = false;
		_events.Add("StreamOut");
	}
}
