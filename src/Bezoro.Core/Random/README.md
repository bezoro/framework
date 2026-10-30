# Deterministic randomness

`Bezoro.Core.Randomness` supplies PCG32 state, range sampling, shuffling, and integer-weight selection. It has no global generator, clock-based seed, hidden lock, or engine dependency. State transitions and sampling allocate no managed objects on valid calls. Constructing a `WeightedIndexTable` allocates the table and one cumulative-weight array.

```csharp
using Bezoro.Core.Randomness;

var random = Pcg32State.Create(seed: 42, stream: 54);
int die = Pcg32.NextInt32(ref random, 1, 7);
double probabilitySample = Pcg32.NextDouble(ref random);

var loot = new WeightedIndexTable(new ulong[] { 1, 0, 5, 20 });
int itemIndex = loot.Pick(ref random);

int[] deck = { 0, 1, 2, 3, 4, 5 };
Pcg32.Shuffle(ref random, deck.AsSpan());
```

These APIs are available from the precompiled Core DLL in Unity 6 and from either supported framework target in Godot .NET. The example uses syntax compatible with Unity's C# 9 compiler. Add `using System;` when accessing `AsSpan` without implicit usings.

## State ownership and replay

`Pcg32State` is a 16-byte readonly value with a 64-bit raw `Value` and a 63-bit `Stream` selector. `Create(seed, stream)` runs the PCG reference seed procedure; its default stream selector is zero. A copied state is independent of its source. The convenience methods take the caller's variable by `ref` so repeated draws do not require a new owner object:

```csharp
var checkpoint = random;
uint first = Pcg32.NextUInt32(ref random);
random = checkpoint;
uint sameFirst = Pcg32.NextUInt32(ref random);

// A pure transition leaves its input unchanged.
Pcg32State next = checkpoint.Next(out uint value);
```

Persist `Pcg32.AlgorithmId`, `state.Value`, and `state.Stream`. Verify the algorithm identifier when loading a replay, then restore with `new Pcg32State(savedValue, savedStream)`. The constructor restores raw state; it does not seed or advance the generator. Do not persist `GetHashCode`, whose purpose is ordinary in-process hash collections.

`default(Pcg32State)` is deliberately valid raw state zero with stream zero. Its initial two raw outputs are zero, but the LCG advances and does not remain at zero. It is different from `Pcg32State.Create(0, 0)`. Use the factory when initializing a new game and the raw constructor or a copied snapshot when restoring one.

The stream selector is restricted to `0..2^63-1`. PCG derives the odd increment as `(stream << 1) | 1`; accepting larger selectors would silently alias another stream. Each stream has a full 64-bit state period. Different streams are a way to separate simulation ownership, not a cryptographic independence guarantee.

One state variable has one writer. For parallel procedural generation, derive a stable seed/stream from the world's logical identity, give each worker its own state value, and share immutable tables. The order in which a shared generator is consumed would otherwise make results depend on scheduling. This library does not allocate worker threads or serialize draws through a lock.

## Sampling contract

`Pcg32.AlgorithmId` is `pcg32-xsh-rr-64-32-v1`. Its version covers the following replay-visible behavior:

| Operation | Range and raw draw consumption |
| --- | --- |
| `NextUInt32(ref state)` | Complete unsigned 32-bit range; one draw |
| `NextInt32(ref state)` | Complete signed 32-bit range with the same raw bits; one draw |
| `NextUInt64(ref state)` | Two concatenated draws, first draw in the high 32 bits |
| `NextInt64(ref state)` | Complete signed 64-bit range with the same concatenated raw bits; two draws |
| Bounded unsigned methods | `[0, exclusiveUpperBound)`; rejection sampling, at least one 32-bit draw or one 64-bit pair |
| `NextInt32(ref state, minInclusive, maxExclusive)` | `[minInclusive, maxExclusive)`; unbiased 32-bit rejection sampling, including ranges crossing zero |
| `NextInt64(ref state, minInclusive, maxExclusive)` | `[minInclusive, maxExclusive)`; unbiased 64-bit rejection sampling, including ranges crossing zero |
| `NextSingle(ref state)` | `[0, 1)` from the high 24 bits of one draw |
| `NextDouble(ref state)` | `[0, 1)` from the high 53 bits of two concatenated draws |
| `Shuffle(ref state, values)` | Descending Fisher-Yates with unbiased bounded indices; zero draws for zero or one element |

Bounds of zero and empty/reversed signed ranges throw before state advances. To obtain the entire signed range, including `int.MaxValue` or `long.MaxValue`, use the unbounded overload. Rejecting samples from the low incomplete remainder removes modulo bias; the number of draws therefore depends on the bound and raw outputs. A bound of one still consumes a draw or pair. Changing which operations a game calls changes its replay stream even when its seed remains the same.

The algorithm is [PCG XSH RR 64/32](https://www.pcg-random.org/using-pcg-c-basic.html), with multiplier `6364136223846793005`, an odd stream increment, and the reference two-step seed initialization. Tests pin the published seed `42`, stream `54` sequence, including `a15c02b7 7b47f409 ba1d3330 83d2f293 bfa4784b cbed606e`. They also pin floating-point bit patterns, shuffle order, and a rejection-sampling sequence. See the [PCG reference implementations](https://www.pcg-random.org/download.html).

This generator is for simulation, loot tables, shuffling, and procedural content. Use a cryptographic random source for credentials, unguessable tokens, or security-sensitive outcomes. Seed selection and synchronization of multiplayer authority remain application decisions.

## Weighted index tables

Weights are unsigned 64-bit integers. This makes proportions exact and avoids float rounding, negative weights, and NaN. Zero-weight entries remain addressable but are never selected. An empty distribution, an all-zero distribution, or a sum above `ulong.MaxValue` throws during construction. Scaling fractional design weights into integer units is an explicit application decision.

The table owns its cumulative weights; changing the input array after construction has no effect. `Pick(ref state)` generates a full-width unbiased ticket in `[0, TotalWeight)` and finds its index in `O(log n)` time. The table does not copy or retain game objects: use its result to index your own immutable item definitions.

`GetIndexForTicket(ticket)` exposes the same exact interval mapping for deterministic ticket sources, diagnostics, and exhaustive distribution checks. `GetWeight(index)` returns an original entry's weight. Neither method exposes writable storage. Construction takes `O(n)` time and `O(n)` memory; all sampling methods use constant auxiliary memory.

## Benchmarks

The Core benchmark project includes raw draws, bounded signed/unsigned draws, unit doubles, selection from 256 weights, and shuffling 32 values. State and arrays are initialized outside measured operations.

```sh
dotnet run -c Release --project benchmarks/Bezoro.Core.Benchmarks -- --filter '*DeterministicRandomBenchmarks*'
```

Measure Unity Mono/IL2CPP or Godot exports separately. Desktop .NET benchmarks do not establish a target game's frame budget.
