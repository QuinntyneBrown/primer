#!/usr/bin/env pwsh

# Raw string literals and the ternary operator are PowerShell 7 features. Windows
# PowerShell 5.1 fails on them at parse time, which reports a syntax error rather than the
# real problem.
#requires -Version 7.0

<#
.SYNOPSIS
    Exports the archetype templates from the CLI into the agent-instruction-files skill.

.DESCRIPTION
    The skill at .claude/skills/agent-instruction-files bundles its own copy of the web and
    CLI guidance, so it can write agent files without the primer tool installed. A copy is
    a second source of truth, and a second source of truth drifts.

    This script makes the copy mechanically regenerable instead of hand-maintained: it
    reads the raw string literals out of ArchetypeTemplates.cs, performs the same
    substitution the C# compiler performs on the shared blocks, and writes the result to
    the skill's assets. Run it after changing any archetype template.

    What is deliberately NOT substituted is the {{ProjectName}}, {{Purpose}}, and
    {{NamespacePrefix}} placeholders. Those are written with two braces, which the C#
    $$$""" literal treats as literal text, so they survive extraction and stay for the
    skill's own renderer to fill.

.PARAMETER Check
    Report whether the assets are current and exit 1 if they are not, without writing
    anything. Suitable for a build gate.

.EXAMPLE
    ./eng/scripts/Export-SkillTemplates.ps1

    Regenerates the skill's assets from the current templates.

.EXAMPLE
    ./eng/scripts/Export-SkillTemplates.ps1 -Check

    Fails if the templates have changed since the assets were last exported.
#>

[CmdletBinding()]
param(
    [switch] $Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$source = Join-Path $repositoryRoot 'src' 'Primer' 'Shared' 'Generation' 'Greenfield' 'ArchetypeTemplates.cs'
$assets = Join-Path $repositoryRoot '.claude' 'skills' 'agent-instruction-files' 'assets'

if (-not (Test-Path $source)) {
    throw "Could not find $source. Run this script from inside the primer repository."
}

function Write-Step {
    param([string] $Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------
# Extract one raw string literal by the name it is assigned to.
#
# A C# raw string literal strips the indentation of its closing delimiter from every line,
# so the closing line tells us how far to dedent. Reproducing that here is what keeps the
# exported Markdown flush left.
# ---------------------------------------------------------------------------

function Get-RawLiteral {
    param(
        [string[]] $Lines,
        [string] $Name
    )

    $start = -1

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        if ($Lines[$i] -match "string\s+$([regex]::Escape($Name))\s*(\{ get; \})?\s*=\s*\`$*`"`"`"\s*$") {
            $start = $i + 1
            break
        }
    }

    if ($start -lt 0) {
        throw "Could not find the raw string literal for '$Name' in $source."
    }

    for ($end = $start; $end -lt $Lines.Count; $end++) {
        if ($Lines[$end] -match '^(\s*)"""\s*;\s*$') {
            $indent = $Matches[1].Length
            $body = $Lines[$start..($end - 1)]

            # Dedent by the closing delimiter's indentation, exactly as the compiler does.
            # A blank line carries no indentation to strip, so it passes through as empty.
            return ($body | ForEach-Object {
                    if ($_.Length -ge $indent) { $_.Substring($indent) } else { $_.TrimStart() }
                }) -join "`n"
        }
    }

    throw "The raw string literal for '$Name' is never closed in $source."
}

# ---------------------------------------------------------------------------
# Read and render
# ---------------------------------------------------------------------------

Write-Step "Reading $([IO.Path]::GetRelativePath($repositoryRoot, $source))"

$lines = [IO.File]::ReadAllLines($source)

$shared = @{
    SpeedAndSimplicity  = Get-RawLiteral -Lines $lines -Name 'SpeedAndSimplicity'
    NoArchitectureTests = Get-RawLiteral -Lines $lines -Name 'NoArchitectureTests'
}

$templates = @{
    'agents.web.md' = Get-RawLiteral -Lines $lines -Name 'Web'
    'agents.cli.md' = Get-RawLiteral -Lines $lines -Name 'Cli'
}

$stale = @()

foreach ($name in $templates.Keys | Sort-Object) {
    $content = $templates[$name]

    # The compiler substitutes the triple-brace holes; the double-brace placeholders are
    # literal text and are left for the skill's renderer.
    foreach ($key in $shared.Keys) {
        $content = $content.Replace("{{{$key}}}", $shared[$key])
    }

    if ($content -match '\{\{\{') {
        throw "$name still contains an unsubstituted interpolation hole. A new shared block was added to ArchetypeTemplates.cs; add it to `$shared in this script."
    }

    $content = $content.TrimEnd("`n") + "`n"

    $destination = Join-Path $assets $name
    $current = (Test-Path $destination) ? [IO.File]::ReadAllText($destination) : $null

    if ($current -ceq $content) {
        Write-Step "$name is current"
        continue
    }

    if ($Check) {
        $stale += $name
        Write-Host "    stale: $name" -ForegroundColor Yellow
        continue
    }

    New-Item -ItemType Directory -Path $assets -Force | Out-Null

    # UTF-8 without a BOM and LF endings, matching what the CLI itself writes.
    [IO.File]::WriteAllText($destination, $content, [Text.UTF8Encoding]::new($false))
    Write-Step "Wrote $name ($(($content -split "`n").Count - 1) lines)"
}

if ($Check -and $stale.Count -gt 0) {
    Write-Host ''
    Write-Host "The skill's bundled templates are out of date: $($stale -join ', ')" -ForegroundColor Red
    Write-Host 'Run ./eng/scripts/Export-SkillTemplates.ps1 to regenerate them.' -ForegroundColor Red
    exit 1
}

if ($Check) {
    Write-Host ''
    Write-Host 'The skill templates match the CLI.' -ForegroundColor Green
}
