# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **`primer init --prompt` / `--prompt-file` generate guidance for a project that does
  not exist yet.** Analysing a repository is no help at the moment a project starts, so
  describe it instead and Primer writes the `AGENTS.md` before any code exists. No git
  repository is required, because describing a project usually comes before `git init`.
  A fixed, offline table of signal phrases picks one of two archetypes: a web
  application (`backend/`, `frontend/`, `design-system/`, `e2e/`, Clean Architecture over
  MediatR, an Angular workspace, Playwright page objects) or a command-line tool (`src/`
  and `tests/` at the root, `System.CommandLine`, packaged as a .NET tool). A description
  the table cannot settle is refused with exit `2` rather than guessed at, and
  `--archetype web|cli` decides it; the wrong folder structure is not something a reader
  can spot. The description becomes the Purpose section, quoted inside a labelled fence so
  no sentence in it reads as an instruction. Greenfield output is a seed: once code
  exists, a plain `primer init` regenerates from what is actually there.

### Changed

- **`primer init` writes a file for every supported agent by default.** `--agent` now
  narrows that set rather than opting into it: an unwanted pointer costs one line,
  while a missing one costs that tool its guidance entirely. `primer check` follows the
  same default, so a repository generated before this change reports the agent files it
  is missing until `primer init` is run again. `--agent claude` restores the previous
  output.

### Fixed

- **Generation no longer reads its own output back as a repository fact.** Writing
  `.github/copilot-instructions.md` creates a `.github` directory, which analysis then
  reported as project structure — so a second `primer init` produced a different
  `AGENTS.md` than the first, and `primer check` reported drift on a repository nobody
  had touched. Analysis now disregards the paths Primer writes, and a directory holding
  nothing but generated output is not repository structure. A `.github` directory with
  workflows or templates in it is still reported.

### Removed

- **`Primer.ArchitectureTests`.** Nine tests asserting the shape of the source tree and
  the traceability of `docs/specs` to test headers, none of which proved the tool works.
  The rules they asserted remain, stated in `AGENTS.md` and `CONTRIBUTING.md` and held
  by the compiler, `dotnet format`, and review. Requirements `L2-038`, `L2-039`,
  `L2-040`, and `L2-043` are withdrawn with them; surviving identifiers are unchanged,
  so existing test trace headers still resolve.

## [0.1.0] — Unreleased

Initial implementation. Every requirement in `docs/specs/L2.md` is implemented and
covered by a test.

### Added

- **`primer init`** generates `AGENTS.md` from analysed repository facts, and pointer
  files for Codex, Claude, Gemini, and Copilot. `CLAUDE.md` is a bare `@AGENTS.md`
  import; the others direct the reader to `AGENTS.md`. Nothing is restated, so nothing
  can drift out of agreement.
- **`primer check`** regenerates in memory and compares against disk, exiting `4` on
  drift and writing nothing under any outcome.
- **`primer mcp list` / `check` / `install`** declare required MCP servers as data,
  probe the configured agent clients, report the gap with a remediation command, and
  install missing servers only with explicit consent.
- **Repository analysis** detects .NET, Node, Python, and Go from marker files, infers
  exact build, test, and lint commands — preferring what CI actually runs — and
  summarises structure within configured depth, file-count, and read-size caps.
- **Safe writes**: preview with `--dry-run`, idempotent regeneration, human-authored
  content outside the managed region preserved byte-for-byte, atomic replacement, and
  UTF-8 without a byte-order mark using the line ending the repository declares.
- **Configuration** through `primer.json`, `PRIMER_` environment variables, and
  command-line arguments, in that order of precedence, validated before any file is
  read or written.
- Distributed as a .NET tool named `primer`, targeting .NET 10.

### Security

- Analysis starts no child process. Repository lifecycle scripts are read as data.
- Writes are confined to the repository root, including through symbolic links.
- `.env` files are never read; credential-shaped values are redacted.
- Repository text is fenced as data rather than emitted as agent guidance, and
  delimiter-forging sequences are neutralised.
- MCP artefacts resolve at pinned versions over HTTPS, are checksum-verified, and
  redirects to undeclared hosts are refused.

[Unreleased]: https://github.com/QuinntyneBrown/primer/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/QuinntyneBrown/primer/releases/tag/v0.1.0
