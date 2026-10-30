# Gameplay foundations demo

This console host composes deterministic terrain and loot streams, weighted grid navigation, a generational actor handle, a temporary attribute modifier, fixed-step simulation, a bounded tick history, and an atomic checkpoint round-trip. It does not launch Unity, Godot, or Stockfish.

```sh
dotnet run -c Release --project samples/Bezoro.Gameplay.ConsoleDemo
```

The host feeds 120 recorded frames of 16 ms into a 20 ms simulation step. It executes 96 ticks and removes the attack buff at tick 25. Scheduled items carry data; the update loop applies them on its own thread.

The checkpoint payload has an application-owned schema and stores raw random state, simulation tick, and base attack. Schema 1 fixes the PCG32 v1 algorithm; an application changing its random algorithm must migrate or version its schema. Loading verifies the same next random output and validates the snapshot's numeric fields. Slot and modifier handles are process-local lifetime tokens, so they are not persisted. A complete game would also save semantic actor/component data, pending effect expiry ticks, and any retained fixed-step backlog, then reconstruct fresh owners and handles after loading.

The demo writes a uniquely named temporary directory and removes it before exiting. Its validation exceptions cause a nonzero exit code, making it useful as an integration smoke check. These checks establish module composition on .NET, not Unity/Godot player compatibility or a universal replay format.
