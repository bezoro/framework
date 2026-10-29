# Bezoro Framework for Unity

This package publishes the precompiled `netstandard2.1` Bezoro Framework assemblies for Unity through a Git URL.
The build pipeline stages the assemblies and their NuGet runtime dependencies under `Runtime`, copies each project README and XML documentation beside its DLL, and preserves the checked-in `.meta` files. Unity consumes compiled .NET Standard 2.1 assemblies; its C# compiler does not compile the framework source, which uses the .NET 10 SDK and modern C# syntax.

## Versioning

The published package version is resolved from SemVer Git tags by `scripts/Get-PackageVersion.ps1`.

- A commit tagged `v1.2.3` or `1.2.3` publishes `1.2.3`.
- Commits after `v1.2.3` publish the next patch preview, for example `1.2.4-preview.1`.
- Repositories with no SemVer tags publish from the initial preview line, for example `0.1.0-preview.1`.

Set `BEZORO_PACKAGE_VERSION` or pass `-PackageVersion` to `scripts/Build-UnityPackage.ps1` to override the resolved version for local staging.

## Install

In Unity, open `Window > Package Manager`, choose `Install package from git URL`, and use:

```text
https://github.com/bezoro/framework.git#upm
```

## Update behavior

Unity Git dependencies are lock-file based. A new push to GitHub updates the `upm` branch, but an existing Unity project stays pinned to the commit recorded in `Packages/packages-lock.json` until you update the package in Unity.

To move to the latest published package:

1. Open the package in Unity Package Manager and click `Update`.
2. Or re-enter the same Git URL.
3. Or remove the package entry from `Packages/packages-lock.json` and reopen the project.

## Included assemblies

- `Bezoro.Chess.UCI.dll`
- `Bezoro.Chess.UCI.Protocol.dll`
- `Bezoro.Core.dll`
- `Bezoro.ECS.dll`
- `Bezoro.Events.dll`
- `Bezoro.GameSystems.dll`
- `Bezoro.Logging.dll`
- `Bezoro.TypingSystem.dll`

Each assembly is accompanied by a sibling README, for example:

- `Runtime/Bezoro.Core.dll`
- `Runtime/Bezoro.Core.README.md`

`Bezoro.ECS.SourceGen` is not part of the Unity package. This package is intended for consuming the precompiled runtime assemblies.

## Runtime dependencies

The build copies the resolved NuGet runtime dependency closure into each .NET Standard build output. The staging script includes those dependencies in the UPM package and rejects conflicting copies of the same DLL. It excludes `Microsoft.CSharp`, `System.Buffers`, `System.Memory`, `System.Numerics.Vectors`, and `System.Threading.Tasks.Extensions`, which the Unity .NET Standard 2.1 profile supplies.

The package includes these additional Microsoft libraries under their upstream MIT licenses:

| Library | Version | Source and license |
| --- | --- | --- |
| System.Collections.Immutable | 8.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime/blob/v8.0.0/LICENSE.TXT) |
| System.Threading.Channels | 8.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime/blob/v8.0.0/LICENSE.TXT) |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime/blob/v6.0.0/LICENSE.TXT) |
| Microsoft.Bcl.AsyncInterfaces | 9.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime/blob/v9.0.0/LICENSE.TXT) |
| System.IO.Pipelines | 9.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime/blob/v9.0.0/LICENSE.TXT) |
| System.Text.Encodings.Web | 9.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime/blob/v9.0.0/LICENSE.TXT) |
| System.Text.Json | 9.0.0 | [dotnet/runtime](https://github.com/dotnet/runtime/blob/v9.0.0/LICENSE.TXT) |

Keep these versions aligned with `Directory.Packages.props` and the resolved transitive packages. If another Unity package supplies these DLLs, resolve the duplicate assembly at the project/package level. Do not import multiple versions of the same assembly.

## Unity and Godot integration

Use Unity's .NET Standard 2.1 compatibility profile. For Godot .NET projects, reference the framework projects or the .NET Standard binaries and retain the same dependencies. Local chess rules do not launch Stockfish. A separate desktop engine process is optional for analysis or engine-controlled opponents.

`ProcessUciTransport` requires a platform that permits starting an external executable. Browser/WebGL and platforms with process restrictions need an `IUciTransport` implementation backed by a native plugin, worker, or analysis server. Compile success does not establish IL2CPP or export compatibility. Validate the actual Unity/Godot build targets, stripping settings, and JSON serialization models before release.
