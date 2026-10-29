param(
	[string]$RepositoryPath = (Join-Path $PSScriptRoot ".."),
	[string]$DefaultVersion = "0.1.0",
	[string]$PrereleaseLabel = "preview",
	[string]$VersionOverride = $env:BEZORO_PACKAGE_VERSION
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$semVerPattern = "^(?:v)?(?<Major>0|[1-9][0-9]*)\.(?<Minor>0|[1-9][0-9]*)\.(?<Patch>0|[1-9][0-9]*)(?:-(?<Prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+(?<Build>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"

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

	$major = [System.Numerics.BigInteger]::Parse($match.Groups["Major"].Value)
	$minor = [System.Numerics.BigInteger]::Parse($match.Groups["Minor"].Value)
	$patch = [System.Numerics.BigInteger]::Parse($match.Groups["Patch"].Value)
	$prerelease = $match.Groups["Prerelease"].Value
	foreach ($identifier in ($prerelease -split '\.')) {
		if ($identifier -match '^[0-9]+$' -and $identifier.Length -gt 1 -and $identifier.StartsWith('0')) {
			return $null
		}
	}
	$coreVersion = "$major.$minor.$patch"
	$version = if ([string]::IsNullOrWhiteSpace($prerelease)) { $coreVersion } else { "$coreVersion-$prerelease" }
	if ($match.Groups["Build"].Success) { $version += "+$($match.Groups['Build'].Value)" }

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

function Compare-SemVer($left, $right) {
	foreach ($part in @('Major', 'Minor', 'Patch')) {
		$comparison = $left.$part.CompareTo($right.$part)
		if ($comparison -ne 0) { return $comparison }
	}
	if ($left.IsStable -ne $right.IsStable) {
		if ($left.IsStable) { return 1 } else { return -1 }
	}
	$leftParts = $left.Prerelease -split '\.'
	$rightParts = $right.Prerelease -split '\.'
	for ($i = 0; $i -lt [Math]::Min($leftParts.Length, $rightParts.Length); $i++) {
		$leftNumeric = $leftParts[$i] -match '^[0-9]+$'
		$rightNumeric = $rightParts[$i] -match '^[0-9]+$'
		if ($leftNumeric -and $rightNumeric) {
			$comparison = ([System.Numerics.BigInteger]::Parse($leftParts[$i])).CompareTo(
				[System.Numerics.BigInteger]::Parse($rightParts[$i]))
		} elseif ($leftNumeric -ne $rightNumeric) {
			$comparison = if ($leftNumeric) { -1 } else { 1 }
		} else {
			$comparison = [StringComparer]::Ordinal.Compare($leftParts[$i], $rightParts[$i])
		}
		if ($comparison -ne 0) { return $comparison }
	}
	return $leftParts.Length.CompareTo($rightParts.Length)
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

	$highest = $null
	foreach ($version in $versions) {
		if ($null -eq $highest -or (Compare-SemVer $version $highest) -gt 0) {
			$highest = $version
		}
	}
	$highest
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

# Validate generated versions too, including numeric prerelease identifiers.
$null = Assert-SemVer "0.0.0-$PrereleaseLabel.0" "PrereleaseLabel"
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
