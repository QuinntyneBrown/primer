# eng

Engineering scripts. Nothing here ships in the package; this directory exists so
that the things maintainers do repeatedly are written down and repeatable rather
than remembered.

## Scripts

| Script | What it does |
|---|---|
| [`scripts/Install-Primer.ps1`](scripts/Install-Primer.ps1) | Builds Primer from this working tree when needed and installs it as a .NET global tool. |

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

## Uninstalling

```powershell
dotnet tool uninstall --global Primer
```
