# Unity API consumption check

This managed consumer intentionally compiles with C# 9 and targets .NET Standard 2.1. Framework projects retain `LangVersion=latest`. The check catches public APIs that require newer consumer syntax or types absent from that target.

The solution normally builds it against project references. After staging the Unity package, compile the same consumer against the actual DLLs:

```sh
dotnet build samples/Bezoro.UnityApiSmoke -c Release -p:FrameworkBinaryRoot=/absolute/path/to/framework/artifacts/unity-package/Runtime
```

It exercises the foundations and toolkit, including spans, readonly results, generational handles, binary cursors with stack buffers, inventories, state evaluation, spatial queries, scalar helpers, and asynchronous save interfaces. The toolkit console example also executes both synchronous consumer methods. This is a compiler/API check. It does not launch the Unity Editor, perform IL2CPP linking, or establish platform export compatibility.
