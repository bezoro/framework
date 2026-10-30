# Behavior composition tests

These tests exercise optional adapters through their public APIs. Definitions are reused; each mutable child runtime is exclusively owned by one graph.

Coverage includes tree/flat-machine/statechart nesting with one shared visit budget, linear progress through deep continuations, reactive parents, round-robin parallel bindings, state changes while suspended, stale completion signals, terminal mapping and immediate resource cleanup, reentry, constructor failures, sparse state ranges, faulty callbacks, reentrancy, and reuse after a failed Stop. A separate allocation check includes warm Start/Tick/Stop operations. Constructor comparisons verify that inactive declared states and wide unvisited branches do not increase adapter execution storage.

```sh
dotnet test tests/Bezoro.Behaviors.Tests -c Release
dotnet test tests/Bezoro.Behaviors.Tests -c Release -p:CheckForOverflowUnderflow=true
```

Host callback behavior, engine scene access, actual asynchronous job cancellation, and target-player exports remain application responsibilities. The token checks demonstrate rejection of late results rather than stopping external work automatically.
