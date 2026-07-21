# Unity Immutable 8 Compatibility Plan

**Goal:** Rebuild and publish the Bezoro Unity package with `System.Collections.Immutable` 8.0.0 so Unity 6000.5 can load it without CS1705.

## Task 1: Lock the dependency policy with a regression test

- Add `UnityDependencyCompatibilityTests` under `tests/Bezoro.Build.Tests`.
- Assert the central `System.Collections.Immutable` package version is exactly `8.0.0`.
- Run the focused test and confirm it fails while the repository still specifies 10.0.0.

## Task 2: Apply and validate the framework fix

- Change the central version in `Directory.Packages.props` from 10.0.0 to 8.0.0.
- Rerun the focused test.
- Run the full solution build and test suite.
- Build the Unity package and inspect the UCI assembly references to confirm version 8.0.0.0.

## Task 3: Publish upstream

- Commit only the central pin, regression test, and this plan on `agent/unity-immutable8`.
- Push the branch and open a draft pull request.
- Publish the validated Unity package artifact to the upstream `upm` branch.

## Task 4: Restore and validate the Unity consumer

- Restore `com.bezoro.framework` as a Git dependency in `Packages/manifest.json`.
- Remove the temporary embedded framework package created during diagnosis.
- Refresh package resolution and verify both Unity compilation and the generated solution build.
