param(
	[string]$RepositoryPath = (Join-Path $PSScriptRoot ".."),
	[string]$DefaultVersion = "0.1.0",
	[string]$PrereleaseLabel = "preview",
	[string]$VersionOverride = $env:BEZORO_PACKAGE_VERSION
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$semVerPattern = "^(?:v)?(?<Major>0|[1-9]\d*)\.(?<Minor>0|[1-9]\d*)\.(?<Patch>0|[1-9]\d*)(?:-(?<Prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+(?<Build>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"

function ConvertTo-SemVerInfo {
	param(
		[string]$Value
	)

	if ([string]::IsNullOrWhiteSpace($Value)) {
		return $null
	}

	$match = [regex]::Match($Value.Trim(), $script:semVerPattern)
	if (-not $match.Success) {
		return $null
	}

	$major = [int]$match.Groups["Major"].Value
	$minor = [int]$match.Groups["Minor"].Value
	$patch = [int]$match.Groups["Patch"].Value
	$prerelease = $match.Groups["Prerelease"].Value
	$coreVersion = "$major.$minor.$patch"
	$version = if ([string]::IsNullOrWhiteSpace($prerelease)) { $coreVersion } else { "$coreVersion-$prerelease" }

	[pscustomobject]@{
		Tag = $Value.Trim()
		Version = $version
		Major = $major
		Minor = $minor
		Patch = $patch
		Prerelease = $prerelease
		IsStable = [string]::IsNullOrWhiteSpace($prerelease)
		StableRank = if ([string]::IsNullOrWhiteSpace($prerelease)) { 1 } else { 0 }
	}
}

function Assert-SemVer {
	param(
		[string]$Value,
		[string]$Name
	)

	$version = ConvertTo-SemVerInfo $Value
	if ($null -eq $version) {
		throw "$Name '$Value' must be a SemVer value like '1.2.3' or '1.2.3-preview.1'."
	}

	$version
}

function Select-HighestSemVer {
	param(
		[string[]]$Tags,
		[switch]$StableOnly
	)

	$versions = foreach ($tag in $Tags) {
		$version = ConvertTo-SemVerInfo $tag
		if ($null -ne $version -and (-not $StableOnly -or $version.IsStable)) {
			$version
		}
	}

	$versions |
		Sort-Object `
			@{ Expression = "Major"; Descending = $true },
			@{ Expression = "Minor"; Descending = $true },
			@{ Expression = "Patch"; Descending = $true },
			@{ Expression = "StableRank"; Descending = $true },
			@{ Expression = "Prerelease"; Descending = $true } |
		Select-Object -First 1
}

function Invoke-Git {
	param(
		[string[]]$Arguments
	)

	$output = & git -C $script:repositoryRoot @Arguments 2>$null
	if ($LASTEXITCODE -ne 0) {
		return $null
	}

	@($output)
}

function Get-PreviewVersion {
	param(
		[object]$BaseVersion,
		[int]$Height
	)

	"$($BaseVersion.Major).$($BaseVersion.Minor).$($BaseVersion.Patch)-$PrereleaseLabel.$Height"
}

function Get-NextPatchPreviewVersion {
	param(
		[object]$BaseVersion,
		[int]$Height
	)

	"$($BaseVersion.Major).$($BaseVersion.Minor).$($BaseVersion.Patch + 1)-$PrereleaseLabel.$Height"
}

if (-not [string]::IsNullOrWhiteSpace($VersionOverride)) {
	$override = Assert-SemVer $VersionOverride "VersionOverride"
	Write-Output $override.Version
	return
}

$defaultSemVer = Assert-SemVer $DefaultVersion "DefaultVersion"
if (-not $defaultSemVer.IsStable) {
	throw "DefaultVersion '$DefaultVersion' must be stable because prerelease labels are generated from commit height."
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
	Write-Output (Get-PreviewVersion $defaultSemVer 0)
	return
}

$script:repositoryRoot = [System.IO.Path]::GetFullPath($RepositoryPath)
$insideWorkTree = @(Invoke-Git @("rev-parse", "--is-inside-work-tree"))
if ($null -eq $insideWorkTree -or $insideWorkTree.Count -eq 0 -or $insideWorkTree[0] -ne "true") {
	Write-Output (Get-PreviewVersion $defaultSemVer 0)
	return
}

$exactTags = @(Invoke-Git @("tag", "--points-at", "HEAD", "--list"))
$exactTag = Select-HighestSemVer $exactTags
if ($null -ne $exactTag) {
	Write-Output $exactTag.Version
	return
}

$mergedTags = @(Invoke-Git @("tag", "--merged", "HEAD", "--list"))
$baseTag = Select-HighestSemVer $mergedTags -StableOnly
if ($null -ne $baseTag) {
	$heightOutput = @(Invoke-Git @("rev-list", "--count", "$($baseTag.Tag)..HEAD"))
	$height = [int]$heightOutput[0]
	Write-Output (Get-NextPatchPreviewVersion $baseTag $height)
	return
}

$headHeightOutput = @(Invoke-Git @("rev-list", "--count", "HEAD"))
$headHeight = if ($null -eq $headHeightOutput -or $headHeightOutput.Count -eq 0) { 0 } else { [int]$headHeightOutput[0] }
Write-Output (Get-PreviewVersion $defaultSemVer $headHeight)
