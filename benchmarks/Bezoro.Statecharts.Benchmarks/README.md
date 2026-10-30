# Statechart benchmarks

The sparse benchmark compares two declared states with 100,000 alternatives, while exactly one state is active. Matching signals perform one external transition; unmatched signals find no matching outgoing edge. Definition/runner construction and application callbacks are outside measurements.

```sh
dotnet run -c Release --project benchmarks/Bezoro.Statecharts.Benchmarks -- --filter '*StatechartBenchmarks*'
```

The workload makes inactive declaration count visible rather than assuming every state needs per-frame work. Runtime metadata and hardware still affect cache behavior. Record source revision, runtime, parameters, and error intervals before using measurements for frame budgets; .NET 10 measurements do not establish Unity/IL2CPP or Godot timing.
