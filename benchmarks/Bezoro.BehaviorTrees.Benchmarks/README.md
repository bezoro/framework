# Behavior tree benchmarks

```sh
dotnet run -c Release --project benchmarks/Bezoro.BehaviorTrees.Benchmarks -- --filter '*BehaviorTreeBenchmarks*'
```

The memory sequence starts with a running action and 16 or 16,384 inactive siblings. Each measured memory update visits exactly that action. The reactive selector puts the action after the same number of false higher-priority conditions and deliberately evaluates those conditions each cycle. The different costs follow the selected semantics; reactivity is explicit.

Definitions, handlers and runner buffers are created before measurement. Restart retains path storage and interrupts its action before beginning the next activation. Cases use struct handlers through the generic ref overload. Record runtime, hardware, source revision and error intervals when collecting results. These workloads do not establish Unity player or IL2CPP timings.
