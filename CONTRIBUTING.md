# Contributing to Primer

Thank you for taking the time to contribute. This document describes how the
repository is built, the workflow it follows, and what a pull request needs in order
to be merged.

## Code of conduct

This project has adopted a [Code of Conduct](CODE_OF_CONDUCT.md). By participating,
you are expected to uphold it.

## Getting started

Primer targets .NET 10. The SDK version is pinned in `global.json`, so install the
[.NET 10 SDK](https://dotnet.microsoft.com/download) and the correct version is
selected automatically.

```console
git clone https://github.com/QuinntyneBrown/primer.git
cd primer
dotnet build Primer.slnx -c Release
dotnet test --project tests/Primer.IntegrationTests -c Release -- --filter-not-trait "tier=slow"
```

The fast tier runs in a few seconds and is what you should run while working. Name the
project rather than the solution: every test in `Primer.PerformanceTests` is slow, and a
project the filter leaves with zero tests is reported as a failure. The slow tier packs
and installs the tool and synthesises large repositories:

```console
dotnet test --project tests/Primer.IntegrationTests -c Release -- --filter-trait "tier=slow"
dotnet test --project tests/Primer.PerformanceTests -c Release
```

Run the performance tests on their own. Measuring them alongside anything else charges
CPU contention to the budget, which makes the result a statement about your machine
rather than about the tool.

## How this repository works

Primer is built with **acceptance-test-driven development**. This is the part of the
workflow most likely to be unfamiliar, and it is not optional.

1. **Every behaviour starts as a requirement.** `docs/specs/L2.md` holds 58 detailed
   requirements, each with acceptance criteria in Given/When/Then form, each refining
   one of the 14 high-level requirements in `docs/specs/L1.md`.
2. **A failing test comes before the implementation.** Write the integration test
   first, watch it fail, then make it pass.
3. **Every test declares what it covers.** A test file opens with a trace header:

   ```csharp
   // Acceptance Test
   // Traces to: L2-013, L2-014
   // Description: Verify primer init writes AGENTS.md with the prescribed sections
   //              inside the line ceiling.
   ```

   Reviewers check this. A test with no trace header, or one naming a requirement that
   does not exist, will be asked to fix it before merge.

If your change introduces behaviour that no requirement describes, add the requirement
first. A pull request that adds behaviour with no requirement behind it will be asked
to add one.

## Architecture rules

These are conventions. Reviewers hold them; no test asserts them:

- Production code lives under `src/`, tests under `tests/`.
- Each command's definition lives in a single file named after the command, and
  declares exactly one command type.
- Commands are discovered by assembly scan. Adding one means adding a file and editing
  nothing shared.
- No feature folder references a type declared inside another feature folder. Anything
  shared lives under `src/Primer/Shared/`.
- No feature reaches the console or the file system directly. Both are reached through
  `IPrimerConsole` and `IFileWriter`.
- Generation output is never generation input. Primer writes into the tree it analyses,
  so analysis disregards the paths Primer writes — otherwise a second run describes the
  repository the first run created.

Warnings are errors for every project, and nullable reference types are enabled. If
the build is failing on an analyser finding, fix the finding rather than suppressing
it — and if a rule is genuinely wrong for this codebase, change `.editorconfig` in the
same pull request and say why.

**Do not add tests that assert these rules.** A `Primer.ArchitectureTests` project once
did, and was deleted as overkill. Structure tests, banned-API scans, and traceability
checks that parse `docs/specs` are all out, permanently — see `AGENTS.md`. The two
permitted test projects are `tests/Primer.IntegrationTests` and
`tests/Primer.PerformanceTests`.

## Pull requests

Before opening one:

```console
dotnet build Primer.slnx -c Release
dotnet format Primer.slnx --verify-no-changes
dotnet test --project tests/Primer.IntegrationTests -c Release -- --filter-not-trait "tier=slow"
```

All three must pass. CI runs the same three plus the packaging and performance tiers.

- Keep the change focused. One concern per pull request.
- Write the commit message to explain **why**, not what — the diff already says what.
- Update `docs/detailed-designs/` if you change how a feature works, and `docs/specs/`
  if you change what it must do.
- Add an entry to [CHANGELOG.md](CHANGELOG.md) under `Unreleased`.

## Reporting bugs and requesting features

Open an issue using the templates. A bug report is far more useful with the exact
command you ran, the output, and `primer --version`.

For anything security-sensitive, do **not** open an issue. Follow
[SECURITY.md](SECURITY.md).

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](LICENSE) that covers this project.
