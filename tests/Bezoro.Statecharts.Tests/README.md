# Statechart tests

Behavioral and memory contracts for hierarchical and orthogonal state execution. The character fixture models Grounded/Walking and InAir/Jumping alongside independent damage reactions. Tests verify that all guards observe the previous configuration, target paths skip unrelated initial children, and unrelated regions retain activation identities.

External, Local, and Internal transitions have separate lifecycle regressions. Priority/depth/conflict cases include an Internal effect whose state would be exited by a competing transition. Guards of structurally preempted sources deliberately throw if reached, proving that unnecessary branches are skipped.

Fault fixtures exercise enter, exit, guard, and reentrancy failures. Cleanup preserves failures, attempts every remaining exit, and never invokes an exit twice. Token tests cover default, foreign, stopped, and reentered identities.

A 100,000-alternative-state definition checks that runner allocation follows maximum active state count. A 20,000-level hierarchy exercises iterative compilation and lifecycle execution. A warmed character loop checks zero managed allocation, while concurrent owners share one immutable definition.

```sh
dotnet test tests/Bezoro.Statecharts.Tests -c Release
dotnet test tests/Bezoro.Statecharts.Tests -c Release -p:CheckForOverflowUnderflow=true
```

The tests do not launch Unity, IL2CPP, or Godot exports.
