# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Nothing yet.

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
