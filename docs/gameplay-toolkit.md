# Gameplay toolkit

This round builds on the gameplay foundations in PR #10. It adds three standalone assemblies and focused Core utilities. Runtime projects retain .NET 10, .NET Standard 2.1, and `LangVersion=latest`; Unity consumes compiled DLLs. None of the new modules calls Unity or Godot APIs or adds a NuGet dependency.

## Choosing a module

| Module | Use | Ownership |
| --- | --- | --- |
| [Inventory](../src/Bezoro.Inventory/README.md) | Item definitions, bounded stacks, complete transfers and validated restore | Share the immutable catalog; serialize both mutable inventories during a transfer. |
| [StateMachines](../src/Bezoro.StateMachines/README.md) | Quest, interaction, menu, or AI state transitions | Share the immutable definition; each agent owns its current state value and applies returned steps. |
| [Spatial](../src/Bezoro.Spatial/README.md) | Nearby point queries, pickup searches, interest regions | Separate mutation from queries. Read-only queries can run concurrently with separate output buffers. |
| [Binary cursors](../src/Bezoro.Core/Binary/README.md) | Explicit checkpoint or packet fields | A reader or writer borrows its span; copying a cursor copies its position and aliases storage. |
| [Bit sets and spans](../src/Bezoro.Core/Collections/README.BitSet.md) | Compact flags, sorted bounds and in-place removals | Mutations require an owner. Retain the returned shorter span after removal. |
| [Numeric helpers](../src/Bezoro.Core/Numerics/README.md) | Signed chunk coordinates, cyclic indices, scalar interpolation and damping | Pure operations with finite-input and range policies. |

Inventory and StateMachines have no runtime project dependencies. Spatial references Core for shared generational handles. No module depends on ECS; applications can attach an owner or immutable definition to an ECS world resource when needed.

## Memory and operation costs

An inventory slot is an eight-byte readonly ItemStack. Storage is fixed at construction, and transfer requests use long quantities so aggregate capacity does not overflow an int. Preflight scans prove the complete operation can succeed before either inventory changes. They do not create a rollback log or call application code during mutation. Sharing the same catalog instance is the compatibility contract for a transfer.

State machines copy and sort sparse transitions. Each transition uses twelve bytes on the validated runtime, and lookup is O(log transition count). Declaring many possible states or signals does not allocate their Cartesian product. Missing rules preserve state; an explicit self transition remains observable. IDs carry numeric meaning within an application schema, not an owner identity.

Spatial storage is bounded by an explicit point capacity and cell budget. Each point occupies one bucket, so queries need no visited-set scratch storage. Dense swap-back removal repairs bucket links and preserves the remaining generational handles. Output order is unspecified. An insufficient buffer receives no partial writes and reports the complete required count. Circle inclusion uses its binary64 bounding box and squared distance; it does not promise exact real-number geometry at rounding boundaries.

BitSet stores ceil(length / 64) words, masks unused tail bits, and has an allocation-free struct enumerator. .NET 10 uses bit intrinsics; .NET Standard uses a portable implementation. Count scans the words. Lower/upper bounds require an input sorted with the supplied comparer; a custom comparer controls its own costs, and passing a struct through IComparer can box at the call site.

Binary cursors allocate no storage and never grow a destination. Each Try operation either consumes/writes the complete field or preserves its cursor and destination. Prefix lengths are bounded before slicing. Prefix writes copy overlapping payload bytes before writing the prefix. Floats preserve IEEE bits, including nonfinite values; an application must reject values that violate its schema.

The numeric helpers validate finite scalar inputs. Integer division helpers require positive divisors, so negative coordinates use mathematical floor/ceiling rather than truncation. Damping uses a rate per unit of elapsed time; use seconds consistently if the rate is per second. Floating-point and transcendental results do not establish bitwise cross-platform replay.

## Host example

The [toolkit console host](../samples/Bezoro.Toolkit.ConsoleDemo/README.md) collects three nearby coins, advances a quest, transfers the balance to a stash, and restores a 16-byte checkpoint. It also executes both C# 9 consumer methods. This checks that the examples produce the documented results rather than merely compiling.

An application owns transactions spanning modules. Inventory's two-phase transfer does not make a pickup that mutates both an inventory and a spatial index globally atomic. Keep that orchestration on one owner, or add an application command/rollback policy before introducing callbacks or competing writers.

## Validation boundaries

Module suites cover invalid input, finite extremes, overflow checking, differential reference models, stale handles, destination preservation, concurrency where promised, and warm allocation checks. Independent reviews use separate models and reported one spatial boundary inconsistency; the corrected filter now agrees across cell sizes.

The C# 9 consumer compiles against .NET Standard projects and staged DLLs with the selected SDK. It does not launch Unity's exact Roslyn compiler, IL2CPP, or a Godot exporter. Validate intended engine/player targets and stripping settings separately.

The shared test runner discovers every test project ending in .Tests.csproj. Main integration and publication still run the complete suite on both CI platforms. Working-branch pushes do not start CI, and this round uses local validation instead of dispatching another workflow for portable managed modules.
