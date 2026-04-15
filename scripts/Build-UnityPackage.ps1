param(
	[string]$Configuration = "Release",
	[string]$OutputPath = "artifacts/unity-package"
)

$ErrorActionPreference = "Stop"

function Get-DeterministicUnityGuid([string]$value)
{
	$bytes = [System.Text.Encoding]::UTF8.GetBytes($value)
	$hash = [System.Security.Cryptography.MD5]::HashData($bytes)
	return [Convert]::ToHexString($hash).ToLowerInvariant()
}

function Get-FolderMetaContent([string]$guid)
{
	return @"
fileFormatVersion: 2
guid: $guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
}

function Get-PluginMetaContent([string]$guid)
{
	return @"
fileFormatVersion: 2
guid: $guid
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 1
  validateReferences: 1
  platformData:
  - first:
      : Any
    second:
      enabled: 0
      settings:
        Exclude Editor: 0
        Exclude Linux64: 0
        Exclude OSXUniversal: 0
        Exclude Win: 0
        Exclude Win64: 0
  - first:
      Any:
    second:
      enabled: 1
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: AnyCPU
        DefaultValueInitialized: true
        OS: AnyOS
  - first:
      Standalone: Linux64
    second:
      enabled: 1
      settings:
        CPU: x86_64
  - first:
      Standalone: OSXUniversal
    second:
      enabled: 1
      settings:
        CPU: x86_64
  - first:
      Standalone: Win
    second:
      enabled: 1
      settings:
        CPU: x86
  - first:
      Standalone: Win64
    second:
      enabled: 1
      settings:
        CPU: x86_64
  - first:
      Windows Store Apps: WindowsStoreApps
    second:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData:
  assetBundleName:
  assetBundleVariant:
"@
}

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

$runtimeFolderMetaPath = "$runtimeRoot.meta"
$runtimeFolderGuid = Get-DeterministicUnityGuid("com.bezoro.framework/Runtime")
Set-Content -Path $runtimeFolderMetaPath -Value (Get-FolderMetaContent $runtimeFolderGuid) -NoNewline

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
			$destinationPath = Join-Path $runtimeRoot "$assemblyName$extension"
			Copy-Item $artifactPath $destinationPath -Force

			if ($extension -eq ".dll") {
				$metaGuid = Get-DeterministicUnityGuid("com.bezoro.framework/Runtime/$assemblyName.dll")
				$metaPath = "$destinationPath.meta"
				Set-Content -Path $metaPath -Value (Get-PluginMetaContent $metaGuid) -NoNewline
			}
		}
	}
}

Write-Host "Unity package staged at $outputRoot"
