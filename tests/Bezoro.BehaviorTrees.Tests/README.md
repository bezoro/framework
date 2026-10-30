# Behavior tree tests

Tests cover memory and reactive control flow, condition short-circuiting, finite repeats and copied subtrees, lifecycle events, activation invalidation, callback faults and reentrancy, depth-bounded storage and cooperative visits.

A separate bounded interpreter generates 1,200 terminal trees and compares results, visit counts and callback order under small random budgets. Deep paths run with a one-visit budget to expose starvation. Counters ensure completed memory branches and inactive siblings are not reevaluated. An allocation check exercises 100,000 warm updates through a struct handler.

```sh
dotnet test tests/Bezoro.BehaviorTrees.Tests -c Release
dotnet test tests/Bezoro.BehaviorTrees.Tests -c Release -p:CheckForOverflowUnderflow=true
```

Independent runners share definitions in concurrent tests. A single runner remains an externally serialized owner. Managed tests do not launch engine editors or exports.
