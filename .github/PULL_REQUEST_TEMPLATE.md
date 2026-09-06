## What this changes

<!-- Describe the change and, more importantly, why it is needed. -->

## Requirements

<!--
Name the L2 requirements this change implements or affects, e.g. L2-013, L2-014.
If it introduces behaviour no requirement describes, add the requirement to
docs/specs/L2.md first and say so here.
-->

Traces to:

## How it was verified

<!-- Beyond "the tests pass": what did you actually run, and against what? -->

## Checklist

- [ ] A failing test was written before the implementation.
- [ ] Each new or changed test file carries a `// Traces to:` header.
- [ ] `dotnet build Primer.slnx -c Release` is clean (warnings are errors).
- [ ] `dotnet format Primer.slnx --verify-no-changes` passes.
- [ ] `dotnet test --solution Primer.slnx -c Release -- --filter-not-trait "tier=slow"` passes.
- [ ] `docs/specs/` updated if what the tool must do has changed.
- [ ] `docs/detailed-designs/` updated if how a feature works has changed.
- [ ] `CHANGELOG.md` updated under `Unreleased`.

## Notes for the reviewer

<!-- Anything you are unsure about, or deliberately left out of scope. -->
