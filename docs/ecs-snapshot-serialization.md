# ECS Snapshot Serialization

`Bezoro.ECS` intentionally keeps snapshot behavior explicit and engine-agnostic.  
There is no mandatory built-in binary format; snapshots are modeled as app-owned DTOs and replayed through `World` APIs.

## Recommended Pattern

1. Capture resources needed to rebuild simulation state.
2. Capture entities/components through compiled queries.
3. Serialize the DTO with your serializer of choice (`System.Text.Json`, MessagePack, etc.).
4. On load, `Reset()` the world and replay entities/components in deterministic order.

## Capture Example

```csharp
public sealed record WorldSnapshot(List<EntitySnapshot> Entities);
public sealed record EntitySnapshot(Position Position, Velocity? Velocity);

public static WorldSnapshot Capture(World world)
{
    var entities = new List<EntitySnapshot>();
    var handle = world.Compile<PositionWithOptionalVelocityQuery>();
    using var cursor = world.Execute(handle);
    cursor.MoveNext();

    for (var i = 0; i < cursor.Current.Length; i++)
    {
        var position = cursor.Get<Position>(i);
        Velocity? velocity = world.TryGet(cursor.Current[i], out Velocity resolved) ? resolved : null;
        entities.Add(new(position, velocity));
    }

    return new(entities);
}
```

## Restore Example

```csharp
public static void Restore(World world, WorldSnapshot snapshot)
{
    world.Reset();
    foreach (var entitySnapshot in snapshot.Entities)
    {
        var entity = world.Spawn(entitySnapshot.Position);
        if (entitySnapshot.Velocity is { } velocity)
            world.Add(entity, velocity);
    }
}
```

## Notes

- Keep snapshot DTOs versioned so old saves can be upgraded cleanly.
- Include relation reconstruction data (`source`, `target`, relation type) if you use `AddRelation<TRelation>`.
- Apply type allow-lists during deserialization (see `SnapshotDeserializationOptions`) when loading untrusted data.
