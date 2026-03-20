# Bezoro.Chess.UCI.Protocol.Tests
Unit and integration tests for `Bezoro.Chess.UCI.Protocol`.

## Test Areas
| Folder | Source Mirror | Description |
| --- | --- | --- |
| `API/Types` | `src/Bezoro.Chess.UCI.Protocol/API/Types` | FEN parsing, search result parsing, PV parsing, and metadata types. |
| `Domain` | `src/Bezoro.Chess.UCI.Protocol/Domain` | `ProcessUciTransport`, `UciEngineClient`, command emission, lifecycle, and integration behavior. |
| `Domain/Common/Helpers` | `src/Bezoro.Chess.UCI.Protocol/Domain/Common/Helpers` | UCI parsing and validation helpers. |
| `TestHelpers` | Test-only | Shared builders, fake transports, fixtures, and constants. |
| `TestResources/Engine` | Test-only | Bundled Stockfish executable/resources used by integration tests. |

## Quick Start
```bash
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj
```

## Useful Commands
```bash
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~UciEngineClient"
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "FullyQualifiedName~ProcessUciTransport"
dotnet test tests/Bezoro.Chess.UCI.Protocol.Tests/Bezoro.Chess.UCI.Protocol.Tests.csproj --filter "Category=Integration"
```

## What These Tests Guarantee
- Standard UCI commands are serialized correctly.
- Handshake lines (`id`, `option`, `uciok`, `readyok`) are parsed and surfaced correctly.
- Transport lifecycle is safe across start, stop, disposal, and backpressure conditions.
- Engine-specific escape hatches (`d`, `go perft 1`) continue to work for supported engines.
