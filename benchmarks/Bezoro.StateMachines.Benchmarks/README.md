# State-machine benchmarks

`StateMachineBenchmarks` compiles tables containing 16, 1,024, and 65,536 transitions. Construction occurs in setup and is not part of the measured calls. Each table has four rules per state and eight accepted signals, allowing matched, missing, and explicit self-transition queries against the same definition.

The sequence workload advances 1,024 times with signal zero and reports time per step through `OperationsPerInvoke`. Its result returns the final state so the work is observable. All queries reuse one immutable table and require no buffers or callback objects.

```sh
dotnet run -c Release --project benchmarks/Bezoro.StateMachines.Benchmarks -- --filter '*StateMachineBenchmarks*'
```

`MemoryDiagnoser` measures managed allocation. These workloads do not measure definition construction, host entry/exit actions, or engine scene APIs. No latency claim is made without a recorded run on the target runtime.

## Recorded toolkit diagnostic

[Selected measurements and raw reports](../results/2026-10-30-toolkit/README.md) record the source revision, runtime, workload parameters and error intervals. Only the listed workloads were measured; the project also defines other profiling cases.
