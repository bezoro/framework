# AGENTS.md

## Package Version Source

Context: Unity package staging and CI release builds.
Rule: Resolve package versions with `scripts/Get-PackageVersion.ps1` from SemVer Git tags; use `BEZORO_PACKAGE_VERSION` or `-PackageVersion` only for explicit local overrides.
Reason: `unity-package/package.json`, release assembly metadata, and CI publishing should stay on one version source.
