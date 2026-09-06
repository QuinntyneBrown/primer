# Primer

## Purpose

Build a command-line interface (CLI) that creates contextual agent files for a repository and helps ensure the required Model Context Protocol (MCP) components are installed.

## Technology

- Use .NET and `System.CommandLine`.
- Package the application as an installable .NET tool.
- Use Microsoft.Extensions libraries and patterns, including:
  - Dependency injection (DI)
  - Options
  - Configuration

## Architecture and Design

- Apply SOLID principles throughout the codebase.
- Organize features and behaviors into vertical slices.
- Follow the “one file per command” design pattern.
- Keep source code in the `\src` folder.
- Keep tests in the `\tests` folder.

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
    └── prompt.md
```
