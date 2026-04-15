param(
	[string]$Configuration = "Release",
	[string]$OutputPath = "artifacts/unity-package"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$templateRoot = Join-Path $repositoryRoot "unity-package"
$outputRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
$runtimeRoot = Join-Path $outputRoot "Runtime"

if (-not (Test-Path $templateRoot)) {
	throw "Unity package template not found at '$templateRoot'."
}

if (Test-Path $outputRoot) {
	Remove-Item $outputRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $runtimeRoot -Force | Out-Null
Copy-Item (Join-Path $templateRoot "*") $outputRoot -Recurse -Force

$projects = Get-ChildItem (Join-Path $repositoryRoot "src") -Recurse -Filter "*.csproj" |
	Where-Object { $_.BaseName -ne "Bezoro.ECS.SourceGen" } |
	Sort-Object FullName

foreach ($project in $projects) {
	$assemblyName = $project.BaseName
	$buildOutputDirectory = Join-Path $project.Directory.FullName "bin\$Configuration\netstandard2.1"

	if (-not (Test-Path $buildOutputDirectory)) {
		throw "Expected build output for '$assemblyName' at '$buildOutputDirectory'. Run 'dotnet build bezoro.framework.sln -c $Configuration' first."
	}

	foreach ($extension in ".dll", ".pdb", ".xml") {
		$artifactPath = Join-Path $buildOutputDirectory "$assemblyName$extension"
		if (Test-Path $artifactPath) {
			Copy-Item $artifactPath $runtimeRoot -Force
		}
	}
}

Write-Host "Unity package staged at $outputRoot"
