# Composable behavior diagnostic measurements

Twenty-one workloads were measured after setup with BenchmarkDotNet ShortRun: one launch, three warmups and three measured iterations. All reported 0 B of managed allocation per operation. CSV values are unmodified; Markdown reports have trailing whitespace normalized. Context JSON files record source revision and filters. The Behaviors workloads were remeasured after the ancestor signal reservation fix; their context records concurrent local validation, which can widen timing intervals.

Host: Debian 13, Intel Xeon Platinum 8573C with five visible cores, SDK 10.0.203 and .NET 10.0.7 x64. These short shared-host measurements are diagnostics for .NET 10. They do not measure Unity Mono/IL2CPP, the .NET Standard implementation on an engine runtime, or Godot export.

| Area | Workload | Parameters | Mean | Error | Allocated |
| --- | --- | --- | ---: | ---: | ---: |
| Conditions | PredicateFastPath | fixed program/machine | 1.192 ns | 0.0875 ns | 0 B |
| Conditions | AllSixTrue | fixed program/machine | 16.644 ns | 2.8439 ns | 0 B |
| Conditions | FirstFalseShortCircuit | fixed program/machine | 4.062 ns | 0.2773 ns | 0 B |
| Conditions | ConditionalBranch | fixed program/machine | 9.897 ns | 1.0020 ns | 0 B |
| Statecharts | UnmatchedSignal | DeclaredStates=2 | 4.260 ns | 0.2610 ns | 0 B |
| Statecharts | Transition | DeclaredStates=2 | 26.843 ns | 5.0519 ns | 0 B |
| Statecharts | UnmatchedSignal | DeclaredStates=100000 | 4.231 ns | 0.5870 ns | 0 B |
| Statecharts | Transition | DeclaredStates=100000 | 26.852 ns | 0.4402 ns | 0 B |
| BehaviorTrees | MemoryRunningAction | SiblingCount=16 | 4.251 ns | 1.9825 ns | 0 B |
| BehaviorTrees | ReactivePriorityChecks | SiblingCount=16 | 168.544 ns | 10.7933 ns | 0 B |
| BehaviorTrees | RestartPair | SiblingCount=16 | 13.408 ns | 0.1750 ns | 0 B |
| BehaviorTrees | MemoryRunningAction | SiblingCount=16384 | 4.110 ns | 0.1072 ns | 0 B |
| BehaviorTrees | ReactivePriorityChecks | SiblingCount=16384 | 148,558.302 ns | 5,711.9691 ns | 0 B |
| BehaviorTrees | RestartPair | SiblingCount=16384 | 13.488 ns | 3.2040 ns | 0 B |
| Behaviors | NestedBudgetOne | Depth=1 | 11.79 ns | 0.464 ns | 0 B |
| Behaviors | NestedFullPath | Depth=1 | 13.86 ns | 12.158 ns | 0 B |
| Behaviors | NestedBudgetOne | Depth=8 | 59.36 ns | 40.550 ns | 0 B |
| Behaviors | NestedFullPath | Depth=8 | 139.10 ns | 376.727 ns | 0 B |
| Behaviors | NestedBudgetOne | Depth=32 | 157.77 ns | 10.962 ns | 0 B |
| Behaviors | NestedFullPath | Depth=32 | 482.93 ns | 49.275 ns | 0 B |
| Behaviors | TransitionAndTick | fixed program/machine | 55.19 ns | 6.565 ns | 0 B |

Memory tree cases keep the first action Running and leave all remaining siblings inactive. Reactive cases explicitly inspect every higher-priority false condition before the action; their larger cost is the chosen semantics. Statechart cases use the same active configuration with different counts of inactive declarations. Composition cases measure one update quantum, not completion of the entire graph; a one-visit budget can distribute progress across updates.

Construction, compilation, validation errors, exceptions, external jobs, host callbacks with their own allocations, and application serialization are outside the measured operations. Very small methods approach the measurement floor. Inspect error intervals and profile real inputs on the target runtime before selecting actor/frame budgets.

Reproduce by running each corresponding benchmark project in Release with --filter "*" --job short --exporters csv and a fresh artifact directory. Context files pin the source; reports include exact runtime/hardware and job settings.
