# Bezoro Framework

Reusable C# libraries for Unity 6 and other .NET game projects, including Godot .NET. The repository uses the .NET 10 SDK and `LangVersion=latest`. Runtime libraries target `net10.0` and `netstandard2.1`; Unity consumes the precompiled .NET Standard assemblies through the `upm` branch.

## Modules

| Module | Responsibility |
| --- | --- |
| [Core](src/Bezoro.Core/README.md) | Collections, grids, pools, guards, and common utilities |
| [Events](src/Bezoro.Events/README.md) | Typed event routing, queued dispatch, and subscriptions |
| [ECS](src/Bezoro.ECS/README.md) | Worlds, component storage, queries, scheduling, and snapshots |
| [GameSystems](src/Bezoro.GameSystems/README.md) | Activation, health, input, streaming, and timers |
| [Logging](src/Bezoro.Logging/README.md) | Filtering, formatting, and log destinations |
| [TypingSystem](src/Bezoro.TypingSystem/README.md) | Word consumption and typing state |
| [Chess protocol and rules](src/Bezoro.Chess.UCI.Protocol/README.md) | Local chess, match state, optional UCI engines, and game review |
| [Chess game facade](src/Bezoro.Chess.UCI/README.md) | Game-facing board snapshots, moves, and UI event dispatch |

## Unity installation

In Unity Package Manager, choose **Install package from git URL**:

```text
https://github.com/bezoro/framework.git#upm
```

Use the .NET Standard 2.1 API compatibility profile. Unity's compiler does not compile this repository's source. The CI builds the DLLs, includes their runtime dependencies, and publishes the generated UPM branch after validation. See [package installation and platform limits](unity-package/README.md).

## Local chess without Stockfish

```csharp
using Bezoro.Chess.UCI.Protocol.API;

var match = new UciPlayableMatchSession();
var initial = match.Refresh();
match.ApplyMove("e2e4");
var current = match.Refresh();
```

Moves, legal replies, promotion, clocks, and results use local rules. No engine process or network connection is needed. Serialize mutations of a protocol match on its owning game thread. Retain immutable snapshots for readers. The high-level `UciGameEngineSession` also has a constructor without an engine path and supports local play without calling `StartAsync`.

## Optional game review

Start a dedicated UCI client only when analysis or an engine opponent is needed:

```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);
await client.StartAsync(cancellationToken);
var reviewer = new UciGameReviewAnalyzer(client,
    new GameReviewOptions(new SearchParameters { Depth = 18 }));
var review = await reviewer.AnalyzeAsync(Fen.Default,
    new[] { "e2e4", "e7e5", "g1f3" }, ct: cancellationToken);
```

The reviewer evaluates the initial position and each resulting position once, reusing adjacent evaluations. It returns preferred lines, scores, expected-points loss, and move quality. Stockfish WDL estimates describe engine self-play; they are not calibrated human winning chances. Accuracy uses an explicitly documented library formula, not Chess.com's CAPS2. Rating models and special annotations are replaceable policies. See [research and review behavior](docs/chess-review.md).

## Bounded parallel work

Parallelism is explicit. Review can use a caller-owned pool of started clients with `MaxParallelPositions`, `ThreadsPerEngine`, and `HashMegabytesPerEngine`; defaults remain one worker, one engine thread, and a 16 MB hash request. Results stay chronological even when positions finish out of order. Cancellation joins every active worker before returning.

For independent ECS simulations:

```csharp
using Bezoro.ECS.Services;

var simulations = new WorldGroup(worlds, maxDegreeOfParallelism: 2);
simulations.Tick(deltaTime);
```

Each world retains its own systems, resources, and command streams. Mutable objects must not be shared between concurrent worlds. Group calls join all workers; nested scheduler work reuses the active worker rather than multiplying thread counts. A tiny workload can be slower in parallel, so profile the target and keep degree one where appropriate. Unity/Godot scene, rendering, and UI calls stay on the main thread. See [ECS threading contracts](src/Bezoro.ECS/README.md) and [review worker budgets](docs/chess-review.md).

## Build and contribute

```sh
dotnet restore bezoro.framework.sln
dotnet build bezoro.framework.sln -c Release --no-restore
dotnet test bezoro.framework.sln -c Release --no-build
pwsh -NoProfile -File scripts/Export-PublicApi.ps1 -Check
```

Install PowerShell 7 for build-script tests. Chess integration tests use the bundled Windows Stockfish by default. On Linux/macOS, set `BEZORO_STOCKFISH_PATH` to an absolute path to a compatible engine binary. Stockfish is not part of the published Unity package.

See [contribution guidelines](CONTRIBUTING.md), [change notes](CHANGELOG.md), and the [review report](docs/framework-review.md). Runtime compatibility must also be verified in the intended Unity/Godot player build; a successful .NET build does not establish IL2CPP, trimming, WebGL, or console support.
