# Bezoro Framework for Unity

This package publishes the precompiled `netstandard2.1` Bezoro Framework assemblies for Unity through a Git URL.
The build pipeline stages the assemblies under `Runtime`, copies each project README beside its corresponding DLL as `<AssemblyName>.README.md`, and copies the checked-in Unity-authored `.meta` files from this package template.

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
