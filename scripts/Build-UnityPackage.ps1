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
  serializedVersion: 3
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
    Any:
      enabled: 1
      settings: {}
    Editor:
      enabled: 0
      settings:
        DefaultValueInitialized: true
    WindowsStoreApps:
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
