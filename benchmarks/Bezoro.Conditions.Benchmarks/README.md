# Condition benchmarks

Precompiled programs and reusable scratch measure a single-predicate fast path, a successful six-predicate conjunction, short circuit at the first predicate, and a conditional branch. Construction, compilation and scratch allocation are outside measurement. Predicates read runtime context fields through a struct evaluator.

```sh
dotnet run -c Release --project benchmarks/Bezoro.Conditions.Benchmarks -- --filter '*ConditionBenchmarks*'
```

Record runtime, hardware, source revision and error intervals when collecting results. Near-floor operations and fixed branch patterns are diagnostics rather than Unity/Godot timing guarantees. Allocation checks cover interpreter operations after setup; custom predicates own their own costs.
