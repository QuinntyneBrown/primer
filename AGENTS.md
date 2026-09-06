# Primer

## Purpose

A command-line interface (CLI) that creates contextual agent files for a repository and helps ensure the required Model Context Protocol (MCP) components are installed.

## Speed Is Not the Goal

Quality and completeness beat finishing fast. Finish the whole task — edge cases,
error paths, no stubs or `TODO`s. If it is bigger than it looked, complete it and
say what it cost rather than quietly narrowing scope.

## Technology

- Use .NET and `System.CommandLine`.
- Package the application as an installable .NET tool.
- Use Microsoft.Extensions libraries and patterns, including:
  - Dependency injection (DI)
  - Options
  - Configuration

## Architecture and Design

- Implement requirements radically simply: the least code that satisfies the
  acceptance criteria, and nothing more. Simple in design, never reduced in scope.
- Apply SOLID principles throughout the codebase.
- Organize features and behaviors into vertical slices.
- Follow the “one file per command” design pattern.
- Keep source code in the `\src` folder.
- Keep tests in the `\tests` folder.
- No feature folder may reference a type declared in another. Anything two features
  share belongs under `src/Primer/Shared/`.
- Commands are discovered by assembly scan through `ICommandModule`. Adding one means
  adding a file and editing nothing shared - never a registration table.
- Features reach the console and the file system only through `IPrimerConsole` and
  `IFileWriter`, never `System.Console` or `System.IO.File` directly.
- Generation output is never generation input. Primer writes files into the tree it
  analyses, so analysis disregards the paths Primer writes. Without that rule a second
  run describes the repository its first run created, and `AGENTS.md` never settles.

These are conventions, held by design review. Do not add a test that asserts them -
see below.

## Agent File Generation

The CLI must provide a command that:

1. Creates an `AGENTS.md` file containing repository-specific guidance for coding agents.
2. Creates a `CLAUDE.md` file that links to `AGENTS.md` rather than duplicating its contents.
3. Supports multiple agent modes and tools, including Codex, Gemini, Claude, and Copilot.

Generated `AGENTS.md` files should follow established best practices for agent instructions, particularly the guidance published by Addy Osmani:

- [15 Best Practices for Writing AGENTS.md Files](https://addyosmani.com/agents/15-agents-md/)
- [AGENTS.md: A New Standard for Guiding Coding Agents](https://addyosmani.com/blog/agents-md/)

## Testing Approach

Use acceptance test-driven development (ATDD):

- Begin with a failing integration test.
- Link each test to explicit acceptance criteria written using the **Given–When–Then** format.
- Implement the behavior required to make the test pass.
- Keep acceptance criteria, integration tests, and implementation aligned.

### Never write architecture or meta tests

Never add a test that asserts on the shape of the codebase rather than on its
behavior. This rule is permanent and has no exceptions. It rules out:

- structure, layout, or naming tests - where projects live, what files are called,
  one-class-per-file, which folder references which;
- banned-API scans, such as searching source for `Console.` or `File.`;
- traceability tests that parse `docs/specs` or read the trace headers of test files;
- tests that read `Directory.Build.props`, `.editorconfig`, `*.slnx`, or CI workflow
  files in order to assert their contents.

A previous `Primer.ArchitectureTests` project did all of this and was deleted as
overkill. Do not reintroduce it under any name. The constraints it asserted are real,
but they are held by the compiler (`TreatWarningsAsErrors`, `Nullable`), by
`dotnet format`, and by review. A test suite exists to prove the tool behaves
correctly; the codebase's own shape is not a behavior.

Two test projects are permitted, and no others:

- `tests/Primer.IntegrationTests` - the acceptance suite.
- `tests/Primer.PerformanceTests` - the startup and generation budgets.

## Folder Structure

```text
primer/
├── src/
│   └── Primer/
│       ├── Commands/
│       └── ...
├── tests/
│   └── Primer.IntegrationTests/
│       └── ...
└── docs/
    ├── prompt.md
    └── specs/
        ├── L1.md high level requirements
        └── L2.md detailed requirments linked to a L1.md also acceptance criteria that shall be linked to intergration tests 
```
