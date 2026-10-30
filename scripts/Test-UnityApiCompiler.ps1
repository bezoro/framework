param(
    [Parameter(Mandatory)]
    [string]$CompilerPath,
    [Parameter(Mandatory)]
    [string]$ReferenceAssemblyPath,
    [string]$FrameworkBinaryRoot = 'artifacts/unity-package/Runtime',
    [string]$OutputPath = 'artifacts/old-csharp-consumer'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Resolve-RepositoryPath([string]$path) {
    [IO.Path]::GetFullPath($path, $repositoryRoot)
}

$compiler = Resolve-RepositoryPath $CompilerPath
$referenceRoot = Resolve-RepositoryPath $ReferenceAssemblyPath
$frameworkRoot = Resolve-RepositoryPath $FrameworkBinaryRoot
$outputRoot = Resolve-RepositoryPath $OutputPath
if (-not (Test-Path $compiler -PathType Leaf)) { throw "Compiler DLL not found: $compiler" }
$references = @(Get-ChildItem $referenceRoot -File -Filter '*.dll' | Sort-Object Name | ForEach-Object FullName)
if ($references.Count -eq 0) { throw 'No .NET Standard reference assemblies were found.' }
foreach ($name in @('Core', 'Simulation', 'Navigation', 'Persistence', 'Inventory', 'StateMachines', 'Spatial')) {
    $dll = Join-Path $frameworkRoot "Bezoro.$name.dll"
    if (-not (Test-Path $dll -PathType Leaf)) { throw "Framework DLL not found: $dll" }
    $references += $dll
}

New-Item $outputRoot -ItemType Directory -Force | Out-Null
$outputAssembly = Join-Path $outputRoot 'Bezoro.UnityApiSmoke.dll'
$response = Join-Path $outputRoot 'consumer.rsp'
$arguments = @('/nostdlib+', '/target:library', '/langversion:9.0', '/nullable:enable', '/warnaserror+')
$arguments += '/out:"' + $outputAssembly + '"'
$arguments += @($references | ForEach-Object { '/reference:"' + $_ + '"' })
foreach ($file in @('FoundationConsumer.cs', 'ToolkitConsumer.cs')) {
    $arguments += '"' + (Join-Path $repositoryRoot "samples/Bezoro.UnityApiSmoke/$file") + '"'
}
[IO.File]::WriteAllLines($response, $arguments, [Text.UTF8Encoding]::new($false))

# An older compiler can run on the installed SDK runtime without changing project compiler settings.
$previousRollForward = $env:DOTNET_ROLL_FORWARD
try {
    $env:DOTNET_ROLL_FORWARD = 'LatestMajor'
    & dotnet $compiler -noconfig "@$response"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally { $env:DOTNET_ROLL_FORWARD = $previousRollForward }
Write-Host "C# 9 consumer compiled with the supplied compiler: $outputAssembly"
