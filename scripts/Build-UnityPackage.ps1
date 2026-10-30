param(
	[string]$Configuration = "Release",
	[string]$OutputPath = "artifacts/unity-package",
	[string]$PackageVersion = $env:BEZORO_PACKAGE_VERSION
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$templateRoot = Join-Path $repositoryRoot "unity-package"
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
$runtimeRoot = Join-Path $outputRoot "Runtime"
$versionScript = Join-Path $PSScriptRoot "Get-PackageVersion.ps1"

if (-not (Test-Path $templateRoot)) {
	throw "Unity package template not found at '$templateRoot'."
}

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
	$PackageVersion = & $versionScript -RepositoryPath $repositoryRoot
}
else {
	$PackageVersion = & $versionScript -RepositoryPath $repositoryRoot -VersionOverride $PackageVersion
}

# Refuse to recursively remove source directories or their ancestors.
$relativeOutput = [System.IO.Path]::GetRelativePath($repositoryRoot, $outputRoot)
$topLevelOutput = ($relativeOutput -split '[\\/]')[0]
if ($relativeOutput -eq '.' -or $relativeOutput -eq '..' -or
    $relativeOutput.StartsWith("..$([System.IO.Path]::DirectorySeparatorChar)") -or
    $topLevelOutput -in @('src', 'tests', 'scripts', 'unity-package', '.git', 'api', 'docs', 'samples', 'benchmarks')) {
    throw "OutputPath must be a staging directory inside the repository, separate from source directories."
}

if (Test-Path $outputRoot) {
	Remove-Item $outputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
Copy-Item (Join-Path $templateRoot "*") $outputRoot -Recurse -Force
New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null

$manifestPath = Join-Path $outputRoot "package.json"
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.version = $PackageVersion
$manifest | ConvertTo-Json -Depth 16 | Set-Content $manifestPath -Encoding utf8

$legacyPluginsRoot = Join-Path $runtimeRoot "Plugins"
if (Test-Path $legacyPluginsRoot) {
	Remove-Item $legacyPluginsRoot -Recurse -Force
}

# These assemblies belong to Unity's .NET Standard 2.1 profile. Shipping NuGet
# copies introduces duplicate definitions in Unity, so use the host's versions.
$hostAssemblies = @(
    'Microsoft.CSharp.dll', 'System.Buffers.dll', 'System.Memory.dll',
    'System.Numerics.Vectors.dll', 'System.Threading.Tasks.Extensions.dll'
)
$stagedAssemblies = @{}

# Runtime READMEs are flattened beside DLLs. Resolve their source-relative links
# against the reviewed source revision rather than nonexistent package folders.
$documentationRevision = 'main'
try {
    $revision = & git -C $repositoryRoot rev-parse --verify HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $revision -match '^[0-9a-fA-F]{40}$') {
        $documentationRevision = $revision
    }
}
catch { }
$documentationBase = "https://github.com/bezoro/framework/blob/$documentationRevision/"

function Copy-RuntimeReadme([string]$SourcePath, [string]$DestinationPath) {
    # Reparse an explicit file URI so fragments remain anchors instead of literal path characters.
    $sourceUri = [Uri]::new(([Uri]::new([IO.Path]::GetFullPath($SourcePath))).AbsoluteUri)
    $content = Get-Content $SourcePath -Raw
    $content = [regex]::Replace($content, '\]\((?:<(?<angle>[^>]+)>|(?<target>[^)\s]+))(?<title>\s+"[^"]*")?\)', [System.Text.RegularExpressions.MatchEvaluator] {
        param($match)
        $target = if ($match.Groups['angle'].Success) { $match.Groups['angle'].Value } else { $match.Groups['target'].Value }
        if ($target.StartsWith('#') -or $target.StartsWith('/') -or
            $target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:') { return $match.Value }
        $resolved = [Uri]::new($sourceUri, $target)
        if (-not $resolved.IsFile) { return $match.Value }
        $relative = [IO.Path]::GetRelativePath($repositoryRoot, $resolved.LocalPath).Replace('\', '/')
        if ($relative -eq '..' -or $relative.StartsWith('../')) { return $match.Value }
        $escaped = ($relative -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
        return '](' + $documentationBase + $escaped + $resolved.Query + $resolved.Fragment + $match.Groups['title'].Value + ')'
    })
    Set-Content -Path $DestinationPath -Value $content -Encoding utf8
}

$projects = Get-ChildItem (Join-Path $repositoryRoot "src") -Recurse -Filter "*.csproj" |
	Where-Object { $_.BaseName -ne "Bezoro.ECS.SourceGen" } |
	Sort-Object FullName

foreach ($project in $projects) {
	$assemblyName = $project.BaseName
	$buildOutputDirectory = Join-Path $project.Directory.FullName "bin\$Configuration\netstandard2.1"
	$readmePath = Join-Path $project.Directory.FullName "README.md"

	if (-not (Test-Path $buildOutputDirectory)) {
		throw "Expected build output for '$assemblyName' at '$buildOutputDirectory'. Run 'dotnet build bezoro.framework.sln -c $Configuration' first."
	}

	if (-not (Test-Path $readmePath)) {
		throw "Expected README for '$assemblyName' at '$readmePath'."
	}

	$artifactPath = Join-Path $buildOutputDirectory "$assemblyName.dll"
	if (-not (Test-Path $artifactPath)) {
		throw "Missing runtime assembly '$artifactPath'. Rebuild before staging."
	}

	foreach ($assembly in Get-ChildItem $buildOutputDirectory -Filter '*.dll' -File) {
		if ($assembly.Name -in $hostAssemblies) { continue }
		$hash = (Get-FileHash $assembly.FullName -Algorithm SHA256).Hash
		if ($stagedAssemblies.ContainsKey($assembly.Name) -and $stagedAssemblies[$assembly.Name] -ne $hash) {
			throw "Conflicting runtime dependency '$($assembly.Name)' in build outputs. Clean and rebuild the solution."
		}
		$stagedAssemblies[$assembly.Name] = $hash
		Copy-Item $assembly.FullName (Join-Path $runtimeRoot $assembly.Name) -Force
	}

	Copy-RuntimeReadme $readmePath (Join-Path $runtimeRoot "$assemblyName.README.md")
	$xmlPath = Join-Path $buildOutputDirectory "$assemblyName.xml"
	if (Test-Path $xmlPath) {
		Copy-Item $xmlPath (Join-Path $runtimeRoot "$assemblyName.xml") -Force
	}
}

Write-Host "Unity package $PackageVersion staged at $outputRoot"
