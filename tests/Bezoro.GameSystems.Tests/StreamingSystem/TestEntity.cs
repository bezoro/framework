using System.Collections.Generic;
using System.Numerics;
using Bezoro.GameSystems.StreamingSystem.Abstractions;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

internal sealed class TestEntity(int id, Vector3 position) : IStreamableEntity
{
	private readonly List<string> _events = new();

	public int                   EntityId          { get; } = id;
	public IReadOnlyList<string> Events            => _events;
	public bool                  IsStreamedIn      { get; private set; }
	public Vector3               StreamingPosition { get; set; } = position;

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
