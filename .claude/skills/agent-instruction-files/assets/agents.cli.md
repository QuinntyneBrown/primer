# {{ProjectName}}

## Purpose

{{Purpose}}

## Speed Is Not the Goal

Quality and completeness beat finishing fast. Finish the whole task - edge cases,
error paths, no stubs or `TODO`s. If it is bigger than it looked, complete it and
say what it cost rather than quietly narrowing scope.

## Technology

- Use .NET and `System.CommandLine`.
- Package the application as an installable .NET tool.
- Use Microsoft.Extensions libraries and patterns: dependency injection, Options,
  and Configuration.

## Architecture and Design

- Implement requirements radically simply: the least code that satisfies the
  acceptance criteria, and nothing more. Simple in design, never reduced in scope.
- Apply SOLID principles throughout the codebase.
- Organize features and behaviors into vertical slices.
- Follow the one-file-per-command pattern: a command's definition and its handler
  live in a single file named for the command.
- One file per type. Every class, interface, record, and enum gets its own file,
  named for the type it holds.
- Keep source in `src` and tests in `tests`, both at the repository root.
- Reach the console and the file system through injected abstractions, never
  through `System.Console` or `System.IO.File` directly, so behavior can be tested.

## Testing Approach

Use acceptance test-driven development (ATDD):

- Begin with a failing integration test.
- Link each test to explicit acceptance criteria written using the Given-When-Then
  format.
- Implement the behavior required to make the test pass.
- Keep acceptance criteria, tests, and implementation aligned.

Integration tests against the built command surface are the acceptance tests. Each
runs against a temporary directory of its own, so the suite is parallel-safe and
never touches the developer's own working tree.

### Never write architecture tests

Never add a test that asserts the shape of the codebase rather than its behavior:
no structure, layout, or naming tests; no banned-API scans; no traceability tests
that parse the specifications. Those constraints belong to the compiler, the
formatter, and review. A test suite exists to prove behavior.

## Folder Structure

```text
{{ProjectName}}/
|-- src/
|-- tests/
`-- docs/
    `-- specs/
```
