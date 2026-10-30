# Unity API consumption check

This managed consumer intentionally compiles with C# 9 and targets .NET Standard 2.1. Framework projects retain `LangVersion=latest`. The check catches public APIs that require newer consumer syntax or types absent from that target.

The solution normally builds it against project references. After staging the Unity package, compile the same consumer against the actual DLLs:

```sh
dotnet build samples/Bezoro.UnityApiSmoke -c Release -p:FrameworkBinaryRoot=/absolute/path/to/framework/artifacts/unity-package/Runtime
```

It exercises the foundations and toolkit, including spans, readonly results, generational handles, binary cursors with stack buffers, inventories, state evaluation, spatial queries, scalar helpers, and asynchronous save interfaces. The toolkit console example also executes both synchronous consumer methods. This is a compiler/API check. It does not launch the Unity Editor, perform IL2CPP linking, or establish platform export compatibility.

## Checking an older compiler

LangVersion selects syntax in the SDK's compiler. It does not reproduce an older compiler's lifetime inference. An additional check with Microsoft.Net.Compilers.Toolset 3.8.0, compiler version 3.8.0-5.20562.2, found that out-spans receiving stack-backed views must first be initialized from that buffer. Both consumers now use empty views to establish that lifetime without a heap allocation.

After staging the package, pass an extracted compiler DLL and the .NET Standard 2.1 reference pack to the optional helper:

```powershell
./scripts/Test-UnityApiCompiler.ps1 `
  -CompilerPath /path/to/toolset/tasks/netcoreapp3.1/bincore/csc.dll `
  -ReferenceAssemblyPath /path/to/netstandard.library.ref/2.1.0/ref/netstandard2.1
```

The helper supplies references and compiles the two consumers. It does not download packages, replace the framework compiler, or run in ordinary builds. The recorded Roslyn 3.8 check also executed the produced IL and both methods returned 4. That is evidence for this compiler, not a claim about Unity's exact compiler or IL2CPP.
