# Primer

[![ci](https://github.com/QuinntyneBrown/primer/actions/workflows/ci.yml/badge.svg)](https://github.com/QuinntyneBrown/primer/actions/workflows/ci.yml)
[![license: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

Primer generates the instruction files coding agents read — `AGENTS.md` and the
per-agent pointer files beside it — from facts it derives about your repository, and
verifies that the Model Context Protocol (MCP) servers your repository depends on are
installed.

It is a .NET tool. It reads your repository and writes two or three small Markdown
files. It runs no build, starts no process, and never writes outside the repository.

## Why

An agent instruction file is only useful while it is true. The usual failure is not
that a repository has no `AGENTS.md` — it is that the file describes a build command
that changed eight months ago, and an agent follows it.

Primer treats that file as derived output rather than prose:

- **Every fact is grounded.** A command Primer cannot infer is omitted, not guessed
  at. A path it emits exists. There are no placeholders for a reader to mistake for
  instructions.
- **Drift is detectable.** `primer check` regenerates in memory and compares. Run it
  in CI and a stale instruction file fails the build like any other stale artefact.
- **One source, many agents.** `CLAUDE.md`, `GEMINI.md`, and
  `.github/copilot-instructions.md` point at `AGENTS.md` rather than restating it, so
  three of the four cannot disagree with each other.

## Install

```console
dotnet tool install --global Primer
```

Or as a repository-local tool:

```console
dotnet new tool-manifest
dotnet tool install Primer
```

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

## Quick start

```console
cd your-repository
primer init --dry-run   # show what would be written
primer init             # write it
primer check            # exit 0 when current, 4 when drifted
```

Running `init` twice changes nothing the second time:

```console
$ primer init
create: AGENTS.md
create: CLAUDE.md

$ primer init
unchanged: AGENTS.md
unchanged: CLAUDE.md
```

## What it generates

For a Node repository with a lock file and an `.editorconfig`:

```markdown
## Project Overview

This repository is `checkout-service`.
It is built with node.

## Commands

- Test: `npm run test`

## Code Style

- `.editorconfig` is authoritative for formatting.

## Boundaries

- A lock file changes only through its package manager, never by hand.
- `AGENTS.md` is generated. Regenerate it with `primer init`; edits to it are replaced.
```

The file is generated in full, and `primer init` replaces it in full. Nothing in it is
yours to edit: guidance you want to keep belongs in a file Primer does not write, and
`AGENTS.md` can link to it.

`CLAUDE.md` is one line:

```markdown
@AGENTS.md
```

That is an import, not a link: Claude Code resolves it and loads `AGENTS.md` into
context, so an agent reading `CLAUDE.md` alone gets the whole of the guidance with
nothing duplicated to drift.

## Commands

| Command | What it does |
|---|---|
| `primer init` | Generate `AGENTS.md` and the selected agent pointer files, from the repository or from a description. |
| `primer check` | Report whether the generated files still match the repository. |
| `primer mcp list` | List the MCP servers this repository requires. |
| `primer mcp check` | Report which required servers are missing or outdated. |
| `primer mcp install` | Install the missing servers, with explicit consent. |

`primer init` options:

| Option | Effect |
|---|---|
| `--agent <name>` | Narrow generation to `codex`, `claude`, `gemini`, `copilot`, or `all`. Repeatable. Defaults to `all`. |
| `--recursive` | Also write nested guidance for each independent project in a multi-project repository. |
| `--prompt <text>` | Describe a project that does not exist yet, instead of analysing this one. |
| `--prompt-file <path>` | Read that description from a file. Mutually exclusive with `--prompt`. |
| `--archetype <web\|cli>` | The solution shape to generate, when the description does not settle it. |

## Starting a project that does not exist yet

Analysing a repository is no help at the moment a project starts, because there is
nothing to read. Describe it instead:

```console
mkdir my-app && cd my-app
primer init --prompt "an Angular front end in the browser with a .NET backend API"
```

No `git init` is needed first. Primer picks a solution archetype from the description
and writes that archetype's structure and conventions:

- **web** — `backend/`, `frontend/`, `design-system/`, and `e2e/`, with Clean
  Architecture, thin controllers over MediatR, an Angular multi-project workspace of
  `api`, `components`, and `domain` libraries beside the application project under
  `frontend/projects/`, every service reached through an interface and an injection
  token, and Playwright page objects.
- **cli** — `src/` and `tests/` at the root, `System.CommandLine`, packaged as a .NET
  tool, with integration tests as the acceptance tests.

Selection is a fixed table of signal phrases: deterministic, offline, and no API key.
A description it cannot settle is refused rather than guessed at, because the wrong
folder structure is not something the reader can spot:

```console
$ primer init --prompt "a lending board for a church congregation"
error: The description does not determine a solution archetype
  next:    Re-run with --archetype naming one of: web, cli.
```

Pass `--archetype web` and it proceeds. The description itself becomes the Purpose
section, quoted inside a fenced block so nothing in it reads as an instruction.

Greenfield output is a **seed**. Once the code exists, run plain `primer init` and the
file is regenerated from what is actually there.

## Global options

Accepted by every command with identical meaning.

| Option | Effect |
|---|---|
| `--path <dir>` | Operate on this directory instead of the current one. |
| `--verbosity <level>` | `quiet`, `minimal`, `normal`, `detailed`, or `diagnostic`. |
| `--format <text\|json>` | Render results as text or as one JSON document. |
| `--no-color` | Never emit colour, whatever the terminal supports. |
| `--dry-run` | Report what would change without changing anything. |
| `--yes` | Approve changes without prompting. Required in a non-interactive session. |

## Exit codes

Fixed, so scripts and CI can branch on them.

| Code | Meaning |
|---|---|
| `0` | Success. |
| `1` | Unexpected failure. |
| `2` | Usage or parse error. |
| `3` | Configuration or input rejected before work began. |
| `4` | Verification failed: drift detected, or a required component is missing. |

## Configuration

Optional `primer.json` at the repository root. Settings resolve in this order, each
overriding the one before it: built-in defaults, `primer.json`, `PRIMER_`-prefixed
environment variables, command-line arguments.

```json
{
  "Primer": {
    "TemplatePath": "templates",
    "Budget": {
      "MaxDepth": 4,
      "MaxFileCount": 50000,
      "MaxFileBytes": 1048576
    },
    "McpRequirements": [
      { "Name": "docs", "VersionRange": ">=1.0.0", "InstallMethod": "npx" }
    ],
    "McpClients": [
      { "Name": "claude-code", "ConfigPath": "~/.claude.json", "Format": "Json" }
    ]
  }
}
```

Drop a file at `templates/agents.md` to replace the built-in template. Primer rejects
a template that names a placeholder it cannot supply, rather than emitting an empty
section.

## Safety

Primer runs against repositories you did not necessarily write, and `mcp install`
changes your machine. The guarantees are deliberate:

- **No repository code is executed.** Build and test commands are read from marker
  files and CI workflows. A `package.json` lifecycle script is data, never a command.
  `init` and `check` start no child process at all.
- **Nothing is written outside the repository.** Traversal segments, absolute paths,
  and symbolic links that resolve outside the root are all refused.
- **Generated files are replaced, not merged.** `primer init` writes `AGENTS.md` and
  the pointer files in full on every run, so anything you add to one is lost the next
  time it runs. Keep guidance you author in a file Primer does not write.
- **Writes are atomic.** An interrupted write leaves the previous content intact, not
  a half-written file.
- **Secrets do not leak.** `.env` files are never opened, and credential-shaped values
  are redacted from anything retained.
- **Repository text is data, not instructions.** Text that reads as a directive to an
  agent is never emitted as guidance; it appears only inside a labelled fence.
- **MCP installs require consent.** Nothing is applied without approval, artefacts
  resolve at pinned versions over HTTPS only, checksums are verified, and a redirect
  to an undeclared host is refused rather than followed.

## Documentation

| Where | What |
|---|---|
| [`docs/specs/L1.md`](docs/specs/L1.md) | 14 high-level requirements. |
| [`docs/specs/L2.md`](docs/specs/L2.md) | 62 detailed requirements with acceptance criteria. |
| [`docs/detailed-designs/`](docs/detailed-designs/) | 14 feature designs with C4, class, and sequence diagrams. |
| [`AGENTS.md`](AGENTS.md) | How to work in this repository. |

Every requirement is covered by a test, and a traceability gate in CI fails the build
if that stops being true.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). This repository is built with
acceptance-test-driven development: a failing test precedes every implementation, and
each test names the requirements it covers.

## Security

Report vulnerabilities privately. See [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE).
