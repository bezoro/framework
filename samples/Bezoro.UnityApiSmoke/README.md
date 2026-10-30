# Unity API consumption check

This managed consumer intentionally compiles with C# 9 and targets .NET Standard 2.1. Framework projects retain `LangVersion=latest`. The check catches public APIs that require newer consumer syntax or types absent from that target.

The solution normally builds it against project references. After staging the Unity package, compile the same consumer against the actual DLLs:

```sh
dotnet build samples/Bezoro.UnityApiSmoke -c Release -p:FrameworkBinaryRoot=/absolute/path/to/framework/artifacts/unity-package/Runtime
```

It exercises all new foundations, including spans, readonly results, generational handles, and asynchronous save interfaces. This is a compiler/API check. It does not launch the Unity Editor, perform IL2CPP linking, or establish platform export compatibility.
