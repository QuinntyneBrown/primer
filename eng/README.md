# eng

Engineering scripts. Nothing here ships in the package; this directory exists so
that the things maintainers do repeatedly are written down and repeatable rather
than remembered.

## Scripts

| Script | What it does |
|---|---|
| [`scripts/Install-Primer.ps1`](scripts/Install-Primer.ps1) | Builds Primer from this working tree when needed and installs it as a .NET global tool. |
| [`scripts/Export-SkillTemplates.ps1`](scripts/Export-SkillTemplates.ps1) | Re-exports the archetype templates from the CLI into the `agent-instruction-files` skill's bundled assets. |

Scripts target PowerShell 7 and run on Windows, macOS, and Linux.

## Install-Primer.ps1

Makes the `primer` command on your machine the code in this working tree, which is
how you try a change end to end rather than only through the test suite.

```powershell
# Build if anything changed, then install or upgrade the global tool
./eng/scripts/Install-Primer.ps1

# Rebuild from scratch and reinstall
./eng/scripts/Install-Primer.ps1 -Force

# Install somewhere else, leaving the `primer` on your PATH alone
./eng/scripts/Install-Primer.ps1 -ToolPath ./.tools

# Install the newest package already in ./artifacts without building
./eng/scripts/Install-Primer.ps1 -SkipBuild
```

Two details worth knowing:

- **The build is skipped when nothing changed.** If a package in `artifacts/` is
  newer than every source file the package depends on, it is reused. Test sources
  are excluded from that comparison, because they cannot change the installed tool.
- **Every build gets a unique version** (`0.1.0-dev.<timestamp>`). A fixed version
  would be served from the NuGet cache on the second install, quietly leaving the
  previous build in place — which is a genuinely confusing way to lose an hour.

Run `Get-Help ./eng/scripts/Install-Primer.ps1 -Full` for the full parameter
reference.

## Export-SkillTemplates.ps1

The `agent-instruction-files` skill under `.claude/skills/` bundles its own copy of
the web and CLI guidance, so it can write agent files on a machine that does not
have the `primer` tool installed. A copy is a second source of truth, and a second
source of truth drifts.

This script makes the copy regenerable rather than hand-maintained. It reads the raw
string literals out of `src/Primer/Shared/Generation/Greenfield/ArchetypeTemplates.cs`,
performs the same substitution the C# compiler performs on the shared blocks, and
writes the result to `.claude/skills/agent-instruction-files/assets/`.

```powershell
# Regenerate the skill's assets from the current templates
./eng/scripts/Export-SkillTemplates.ps1

# Fail if the assets have drifted; this is what CI runs
./eng/scripts/Export-SkillTemplates.ps1 -Check
```

Run it after changing any archetype template. Editing an asset by hand is how the
copy and the original quietly stop agreeing, and the next export overwrites the edit
anyway.

The `{{ProjectName}}`, `{{NamespacePrefix}}`, and `{{Purpose}}` placeholders survive
extraction on purpose: they are written with two braces, which a C# raw string literal
treats as literal text, and the skill's own renderer fills them.

## Uninstalling

```powershell
dotnet tool uninstall --global Primer
```
