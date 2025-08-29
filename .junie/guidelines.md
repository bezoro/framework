# Project Guidelines for Junie

Purpose:
- Junie assists by proposing precise, minimal, and verifiable code changes with strong reasoning and test evidence.
- Always think as hard as possible: analyze the problem, explore alternatives, plan small steps, and validate each step with tests.

General Principles:
- Red-Green-Refactor: write a failing test first, make it pass with the smallest change, then refactor while keeping tests green.
- Prefer small, self-contained, atomic, well-named, and self-documenting methods. Avoid large, multipurpose methods.
- Keep files ≤ 500 lines. If a file would exceed this, refactor by splitting into smaller types or modules; do not use partial classes.
- Maintain backward compatibility unless explicitly asked to introduce a breaking change (document any breaking changes).
- Keep changes scoped and incremental. Avoid speculative features.

Technology and Tooling:
- Language/Runtime: C# (latest available in the project) on .NET.
- Testing: xUnit for test framework, FluentAssertions for assertions, NSubstitute for mocking.
- Build: dotnet build
- Test: dotnet test
- If non-standard build/test steps are required for a change, clearly document them in the PR or change description.

Coding Standards:
- Follow Microsoft’s C# coding conventions:
  - Use file-scoped namespaces.
  - Prefer readonly, init, and immutability.
  - Use expression-bodied members when it improves clarity.
  - Avoid magic numbers/strings; extract constants or configuration.
  - Prefer explicit access modifiers; make members as private/internal as possible.
  - Prefer pattern matching and switch expressions when they increase readability.
  - Prefer async/await for I/O-bound operations; support CancellationToken where applicable.
  - Handle nullability correctly; enable and respect nullable reference types if the project does.
  - Document public APIs with XML comments when not self-evident; keep internal code self-documenting with clear names.
- Organize code into small, cohesive types. One responsibility per type. Avoid “god” classes.
- Methods:
  - Aim for ≤ 20–40 lines per method; extract helpers aggressively.
  - Single level of abstraction per method. Early returns are fine when they improve clarity.
  - Keep parameters minimal; group related parameters into small value objects if needed.
  - Keep cognitive complexity as low as possible; avoid deep nesting, long boolean expressions, and multiple responsibilities per method. Prefer guard clauses, early returns, small helper methods, and simple switch expressions. Target typical methods ≤ 5 and avoid exceeding 10 without strong justification.

Testing Standards:
- Always write tests that reproduce bugs before fixing them.
  - Name: Should clearly describe the scenario and expected behavior (e.g., MethodName_WhenCondition_ShouldOutcome).
  - Locate tests close to the component under test in a parallel folder structure.
  - Organize tests for each production file under a single main test class named `TypeNameTests` with three nested classes: `Unit`, `Integration`, and `Performance`.
    - Example structure:
      ```
      public class TypeNameTests
      {
          public class Unit { /* Unit tests here */ }
          public class Integration { /* Integration tests here */ }
          public class Performance { /* Performance/benchmark tests here */ }
      }
      ```
    - Place method-specific tests in the appropriate nested class; use clear names and Given/When/Then or When_Should patterns.
    - If a nested class grows beyond ~200 lines, split by behavior or method into additional nested classes.
  - Tests must fail before the fix (prove the bug), and pass after the fix.
- Always add tests for new functionality:
  - Cover positive, edge, and error paths.
  - Use FluentAssertions for readable assertions.
  - Use NSubstitute for mocks/stubs; verify interactions only when behavior cannot be asserted via state.
  - Keep tests isolated and deterministic; avoid external side effects (use in-memory fakes or substitutes).
  - NEVER write hacky tests or workarounds to fake a green test; assert observable behavior and public contracts over implementation details; do not disable logic, swallow exceptions, or overuse mocks to bypass real behavior.
  - Ensure each test is high-value: it would catch a real regression, is deterministic, keeps setup minimal, and has a single clear reason to fail.
- Run all tests locally (dotnet test) before submitting changes.
- If the change touches performance-critical code, consider adding performance tests or benchmarks when feasible.
- Never consider a task complete until all tests pass; iterate and refine code and tests as long as needed to achieve a fully green run.

Refactoring and File Size Limits:
- If a file exceeds 500 lines, refactor by:
  - Splitting into smaller classes, or modules by responsibility.
  - Avoiding partial classes; prefer separate, focused types/files and composition over partials.
  - Extracting reusable logic into extension methods or helper services.
  - Moving private nested types into their own files when they grow beyond a few lines.
- Preserve API behavior; tests should remain green during refactors.

Error Handling and Logging:
- Fail fast on invalid input using ArgumentException/ArgumentNullException and guard clauses.
- Use exceptions for exceptional states; do not use exceptions for flow control.
- Log actionable information; avoid leaking sensitive data.

Performance and Memory:
- Default to highly performant, allocation-free implementations; actively minimize allocations, copying, and boxing.
- Use spans and memory-friendly primitives (Span<T>, ReadOnlySpan<T>, Memory<T>, ReadOnlyMemory<T>), pooling (e.g., ArrayPool<T>), ValueTask where appropriate, and avoid closure allocations and unnecessary LINQ in performance-sensitive paths.
- When an allocation-free or zero-copy approach is not feasible, explicitly justify the trade-off in code comments and the change description.

Security and Privacy:
- Validate inputs and sanitize outputs where applicable.
- Do not commit secrets. Use configuration providers or environment variables.
- Be mindful of data exposure in logs and errors.

Dependency Management:
- Prefer standard library features first. Introduce external dependencies only when they provide clear value.
- Keep versions consistent with the project’s dependency strategy.

Workflow for Bug Fixes:
1. Understand and reproduce the issue.
2. Write a failing test that demonstrates the bug (red).
3. Implement the minimal fix (green).
4. Refactor for clarity and maintainability (refactor).
5. Run all tests and ensure full pass.
6. Document the fix in the change description with a brief root cause and why the test proves it.

Workflow for New Features:
1. Clarify requirements and edge cases.
2. Write tests for the desired behavior (start with failing tests).
3. Implement the smallest vertical slice to pass tests.
4. Refactor code to small, self-contained methods and ensure files ≤ 500 lines.
5. Verify all tests pass; update documentation if necessary.

Code Review Checklist (self-check before submission):
- Do new tests fail before the change and pass after?
- Are tests high-value, behavior-focused, deterministic, and free of hacks/workarounds (no fake greens)?
- Are methods small, cohesive, and self-documenting?
- Is cognitive complexity low (typical ≤ 5; justify if > 10)?
- Are files within the 500-line limit (or a refactor plan included)?
- Do names reflect intent?
- Are Microsoft C# conventions followed (naming, spacing, nullability, async, visibility)?
- Are side effects and error paths tested?
- Are changes minimal and focused?

Commit and PR Hygiene:
- Use Conventional Commits for all commit messages.
  - Format: type(scope)!: subject
  - Allowed types: feat, fix, docs, style, refactor, perf, test, build, ci, chore, revert.
  - Subject: imperative mood, no trailing period, ≤ 72 characters.
  - Scope: optional; choose the most specific component/module (e.g., parser, api, infra).
  - "!" indicates a breaking change and must be accompanied by a BREAKING CHANGE footer.
  - Body: explain what and why (not line-by-line how), include performance/security impact and test notes; wrap lines at ~72 columns.
  - Footer: include BREAKING CHANGE: details with migration guidance when applicable; include references (Refs: #123), metadata (Co-authored-by: ...), etc.
- Reference related issue IDs where applicable.
- PR description: what changed, why, how it’s tested, any follow-up work or risks.

Full Commit Message Example:
```
feat(parser)!: replace regex parser with hand-written tokenizer

- Improves parsing performance by 85% on large files
- Reduces memory allocations by 60%
- Adds support for nested expressions
- Breaking: drops support for legacy format v1

The new parser implementation replaces the regex-based approach with
a hand-written tokenizer that processes input as Span<char> without
allocations. This brings major performance improvements and enables
proper nested expression support, which was a key customer request.

Performance impact:

- 85% faster on files > 1MB
- 60% fewer allocations
- Peak memory reduced by 40%

All scenarios covered by new property-based tests (1000 iterations).
Fuzz testing found no crashes in 1M iterations.

BREAKING CHANGE: v1 format support has been removed to simplify the
parser. Users must migrate to v2 format. Migration guide available
at docs/migration-v1-v2.md.

Closes #123
Refs #100, #234
Co-authored-by: Jane Smith <jane@example.com>
```

How Junie Executes:
- Analyze deeply, articulate assumptions, and propose a step-by-step plan.
- Create precise, minimal patches that apply cleanly.
- Include tests in the same set of patches when introducing fixes or features.
- Ensure the project builds and tests pass before finalizing.
- Never mark a task complete until all tests pass; iterate and re-run tests as many times as needed to reach green.
- Apply self-reflection with a rubric before finalizing any response; if the response does not reach top marks across all categories, start again and iterate until it does.

Rubric (self-reflection before finalizing; score each 1–5 and target 5/5):
- Requirements coverage and correctness: fully addresses the prompt with correct behavior, including edge cases.
- Build and test status: builds cleanly; all tests pass; failing tests must be fixed before completion.
- Code quality and style: follows Microsoft C# conventions; small, atomic, self-documenting methods; low cognitive complexity; files ≤ 500 lines; no partial classes.
- Performance and memory: allocation-free or minimal allocations by default; provide explicit justification and measurements if not possible.
- Test quality: high-value, behavior-focused, deterministic; organized into Unit/Integration/Performance nested classes; bug tests fail before fix; new functionality is properly tested.
- Scope and minimalism: changes are minimal, focused, and avoid speculative work.
- Compatibility and safety: preserves API/behavior unless a breaking change is intended and documented.
- Documentation and commit: Conventional Commit message; clear description, rationale, test and performance notes.

Acceptance:
- Finalize only when every category is scored 5/5; if any category is <5, revise the plan/code/tests and re-evaluate.
- When a top score is not feasible due to constraints, explicitly justify which category falls short and why, and propose follow-up work.

Notes:
- If you encounter constraints that prevent adhering to these guidelines, state them explicitly and propose the safest alternative.
