# Write files safely

## Overview

Primer writes into repositories that already contain work. This feature covers
the guarantees that make that acceptable: a run can be previewed before it
happens, a repeated run changes nothing, human-authored content survives, and a
failure part-way through leaves no damaged file behind.

**dry run** — mode in which every intended change is reported and no file is
written

**idempotence** — property that repeating a run against an unchanged repository
produces byte-identical results and reports no change

**atomic write** — write that either completes fully or leaves the previous
content intact, with no partial or zero-length file at the target path

The managed region is what makes preservation possible for `AGENTS.md`. Content
between the delimiters belongs to the generator, and content outside them belongs
to whoever wrote it. A regeneration replaces the first and leaves the second
untouched, so a team can add its own guidance to a generated file without
choosing between local edits and current output.

A file that carries no delimiters is not assumed to be safe to replace. An
`AGENTS.md` without a managed region is treated as human-authored and left alone
unless `--force` is passed, in which case the prior content is backed up first.
Pointer files are the exception, because their entire content is generated: one
whose content already matches is unchanged, and one that differs is refused
without `--force`.

Encoding is fixed rather than inherited from the ambient environment. Generated
files are UTF-8 without a byte-order mark and end with exactly one trailing
newline, and line endings follow what the repository declares rather than what
the host platform prefers.

## Description

- **`IFileWriter`** and **`AtomicFileWriter`** — write through a temporary file in
  the destination directory, then replace the target, so an interrupted write
  cannot leave a partial file.
- **`WritePlan`** — the ordered set of `GeneratedFile` records with the
  `FileAction` each shall take. Every command produces a plan before writing.
- **`FileAction`** — enumeration of `Create`, `Update`, and `Unchanged`. A file
  reported `Unchanged` is not opened for writing, so its timestamp is preserved.
- **`IDryRunReporter`** and **`DryRunReporter`** — render the plan. For a new file
  it prints the intended content; for an existing one it prints a unified diff.
- **`ManagedRegionMerger`** — replaces the delimited block within an existing file
  and returns the surrounding content unchanged. A file with a begin delimiter and
  no end delimiter is malformed and is refused.
- **`OverwritePolicy`** — decides between merge, refuse, and force-overwrite from
  the file kind, the presence of a managed region, and `--force`.
- **`BackupWriter`** — writes a timestamped copy of prior content before any
  forced overwrite, and reports the backup path.
- **`LineEndingPolicy`** — resolves the line ending from `.gitattributes`, then
  `.editorconfig`, defaulting to `LF`.
- **`EncodingPolicy`** — fixes UTF-8 without a byte-order mark and one trailing
  newline.
- **`FilePermissions`** — creates files at mode `0644` on POSIX hosts.

## Requirements

The feature realizes the following level-2 (L2) requirements. Each L2
requirement refines a level-1 (L1) requirement, cited by identifier.

| L2 ID | Refines (L1) | Requirement |
|-------|--------------|-------------|
| `L2-023` | `L1-006` | `--dry-run` shall report the intended content or a unified diff for every target, shall write no file, and shall report each path as `create`, `update`, or `unchanged`. |
| `L2-024` | `L1-006` | Content outside the managed region shall be preserved byte-for-byte, a file without delimiters shall not be modified without `--force`, a forced overwrite shall write a backup first, and a malformed region shall exit `3`. |
| `L2-025` | `L1-006` | Repeating a run against an unchanged repository shall produce byte-identical files, shall report every path as `unchanged`, shall not update timestamps, and shall leave exactly one delimiter pair. |
| `L2-026` | `L1-006` | Generated files shall be UTF-8 without a byte-order mark, shall end with exactly one trailing newline, shall follow the line ending the repository declares and `LF` otherwise, and a failed write shall leave the original content intact. |
| `L2-051` | `L1-011` | Files shall be created at mode `0644` or narrower on POSIX hosts, no analytics request shall be made absent telemetry configuration, and an unwritable target shall exit `3` rather than escalating privileges. |

## Diagrams

### System context

Writing is the only point at which Primer changes the repository, and the
developer can interpose a preview before it happens.

![C4 system context for writing files safely](diagrams/c4-context.png)

### Containers

The working tree holds three relevant stores: the target files, the backups a
forced overwrite produces, and the declarations that fix line endings.

![C4 container view for writing files safely](diagrams/c4-container.png)

### Components

The overwrite policy decides the action for each target, and the atomic writer is
the single component that touches the file system.

![C4 component view for writing files safely](diagrams/c4-component.png)

### Class structure

The policies are separate types so that encoding, line endings, and overwrite
rules are each testable without a file system.

![Class diagram for writing files safely](diagrams/class-structure.png)

### Behaviour — plan, preview, and write

The plan is complete before the first byte is written, so a dry run and a real
run differ only in whether the writer is invoked.

![Sequence diagram for writing files safely](diagrams/sequence-write-safely.png)
