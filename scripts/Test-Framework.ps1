param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoBuild,
    [string[]]$Suite = @(),
    [string]$ResultsDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projects = @(Get-ChildItem (Join-Path $repositoryRoot 'tests') -Recurse -File -Filter '*.Tests.csproj' |
    Where-Object { $_.FullName -notmatch '[/\\](bin|obj)[/\\]' } |
    Sort-Object FullName)
if ($projects.Count -eq 0) { throw 'No test projects were found.' }

# Validate the complete selection before executing any suite.
foreach ($name in $Suite) {
    if ($name -notin $projects.BaseName) { throw "Unknown test suite '$name'. Use the project name without .csproj." }
}
if ($Suite.Count -gt 0) { $projects = @($projects | Where-Object BaseName -In $Suite) }

Push-Location $repositoryRoot
try {
    foreach ($project in $projects) {
        $arguments = @('test', $project.FullName, '-c', $Configuration, '--verbosity', 'minimal')
        if ($NoBuild) { $arguments += '--no-build' }
        if ($ResultsDirectory) {
            $arguments += @('--logger', "trx;LogFileName=$($project.BaseName).trx", '--results-directory', $ResultsDirectory)
        }
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
finally { Pop-Location }
