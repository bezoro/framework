# Bezoro Framework for Unity

This package publishes the precompiled `netstandard2.1` Bezoro Framework assemblies for Unity through a Git URL.
The build pipeline stages the assemblies under `Runtime` and copies the checked-in Unity-authored `.meta` files from this package template.

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

`Bezoro.ECS.SourceGen` is not part of the Unity package. This package is intended for consuming the precompiled runtime assemblies.
