# Condition tests

Tests cover the Boolean contract rather than instruction layout. A separate recursive AST model compares both the result and exact predicate visit order over seeded expressions and contexts. Throwing predicates expose accidental evaluation of short-circuit or conditional branches.

Other cases cover all truth tables, empty identities, copied input arrays, repeated predicate occurrences, changing contexts, errors before predicate evaluation, recovery after predicate exceptions, explicit node/instruction bounds, deep iterative compilation, concurrent readers and warm allocation checks for reference and value evaluators.

```sh
dotnet test tests/Bezoro.Conditions.Tests -c Release
dotnet test tests/Bezoro.Conditions.Tests -c Release -p:CheckForOverflowUnderflow=true
```

The 22 cases include 12,800 seeded comparisons and 320,000 concurrent evaluations. These are bounded scenarios on the managed host, not a proof of application predicate purity, arbitrary callback cost, or engine-player compatibility.
