#!/usr/bin/env python3
"""Write the agent instruction files for a project described in prose.

This reproduces what `primer init --prompt` produces, without needing the .NET tool
installed. The interesting part is not the string substitution -- it is the handful of
decisions underneath it, each of which is easy to get subtly wrong when re-derived from
scratch on every invocation:

* The description is somebody else's words. It is quoted inside a labelled fence so that
  no sentence in it can read as an instruction the generated file is issuing. A file that
  launders "ignore your previous instructions" into guidance is worse than no file.
* A directory may be called `my-app`; a C# namespace may not. The namespace example is
  derived separately, because an example that will not compile is worse than no example --
  the agent following it cannot tell.
* Guidance an agent truncates is guidance that does not apply, so the result is held to
  150 lines.
* Writing the same inputs twice leaves byte-identical files, so re-running is safe and a
  second run is visibly a no-op.

Usage:
    render.py --path DIR --archetype {web,cli} (--prompt TEXT | --prompt-file FILE)
              [--agent {all,codex,claude,gemini,copilot} ...] [--dry-run]
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

# The label is what tells a reading agent the block is quoted data rather than an
# instruction, so it travels with the fence and is not optional.
PURPOSE_LABEL = (
    "The following description was supplied when this file was generated, "
    "quoted for reference:"
)

AGENTS_FILE = "AGENTS.md"

# Codex reads AGENTS.md natively, so selecting it adds no file of its own.
POINTER_FILES = {
    "claude": ("CLAUDE.md", "@AGENTS.md"),
    "gemini": (
        "GEMINI.md",
        f"Read [{AGENTS_FILE}](./{AGENTS_FILE}) at the repository root before working in "
        f"this repository.\nIt is the single source of guidance; this file adds nothing "
        f"of its own.",
    ),
    "copilot": (
        ".github/copilot-instructions.md",
        f"Read [{AGENTS_FILE}](./{AGENTS_FILE}) at the repository root before working in "
        f"this repository.\nIt is the single source of guidance; this file adds nothing "
        f"of its own.",
    ),
    "codex": None,
}

MAX_LINES = 150
TRUNCATION_NOTE = (
    "_Content was truncated to stay within the 150-line guidance; "
    "the repository itself is the fuller source._"
)

BLANK_RUNS = re.compile(r"\n{3,}")


def fence(text: str, label: str) -> str:
    """Wrap supplied text in a labelled fenced block, so it reads as data."""
    return f"{label}\n```text\n{text.strip(chr(10))}\n```\n"


def escape_inline(text: str) -> str:
    """Prepare text for a code span, where a backtick would close the span early and let
    what follows read as prose the file appears to have authored."""
    return text.replace("`", "'")


def namespace_prefix(project_name: str) -> str:
    """The project name as a namespace can spell it."""
    identifier = []
    start_of_word = True

    for character in project_name:
        if not character.isalnum():
            start_of_word = True
            continue
        identifier.append(character.upper() if start_of_word else character)
        start_of_word = False

    joined = "".join(identifier)

    # A namespace cannot open with a digit, and an empty one helps nobody.
    if not joined or joined[0].isdigit():
        return "App" + joined
    return joined


def apply_line_budget(body: str) -> str:
    lines = body.split("\n")
    if len(lines) <= MAX_LINES:
        return body
    return "\n".join(lines[: MAX_LINES - 2]) + "\n\n" + TRUNCATION_NOTE


def normalize(content: str) -> str:
    """UTF-8 without a BOM, LF endings, and exactly one trailing newline."""
    return content.replace("\r\n", "\n").rstrip("\n") + "\n"


def render(template: str, project_name: str, description: str) -> str:
    filled = (
        template.replace("{{ProjectName}}", escape_inline(project_name))
        .replace("{{NamespacePrefix}}", namespace_prefix(project_name))
        .replace("{{Purpose}}", fence(description, PURPOSE_LABEL))
    )
    return apply_line_budget(BLANK_RUNS.sub("\n\n", filled).strip("\n"))


def resolve_agents(selected: list[str]) -> list[str]:
    if not selected or "all" in selected:
        return ["codex", "claude", "gemini", "copilot"]
    return selected


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--path", required=True, help="Directory to write the files into.")
    parser.add_argument("--archetype", required=True, choices=["web", "cli"])
    parser.add_argument("--prompt", help="The project description.")
    parser.add_argument("--prompt-file", help="Read the description from a file instead.")
    parser.add_argument(
        "--agent",
        action="append",
        default=[],
        choices=["all", "codex", "claude", "gemini", "copilot"],
        help="Repeatable. Defaults to all.",
    )
    parser.add_argument("--dry-run", action="store_true", help="Report without writing.")
    args = parser.parse_args()

    if bool(args.prompt) == bool(args.prompt_file):
        parser.error("Supply exactly one of --prompt or --prompt-file.")

    description = (
        Path(args.prompt_file).read_text(encoding="utf-8")
        if args.prompt_file
        else args.prompt
    )

    if not description.strip():
        parser.error("The description is empty; there is nothing to generate from.")

    target = Path(args.path).resolve()
    if not target.is_dir():
        parser.error(f"Not a directory: {target}")

    template_path = Path(__file__).resolve().parent.parent / "assets" / f"agents.{args.archetype}.md"
    if not template_path.is_file():
        parser.error(
            f"Missing template {template_path}. Run eng/scripts/Export-SkillTemplates.ps1."
        )

    body = render(template_path.read_text(encoding="utf-8"), target.name, description)

    planned: list[tuple[Path, str]] = [(target / AGENTS_FILE, normalize(body))]

    for agent in resolve_agents(args.agent):
        pointer = POINTER_FILES[agent]
        if pointer is None:
            continue
        relative, content = pointer
        planned.append((target / relative, normalize(content)))

    for path, content in planned:
        relative = path.relative_to(target).as_posix()

        if args.dry_run:
            print(f"would write: {relative}")
            continue

        existing = path.read_text(encoding="utf-8") if path.is_file() else None
        if existing == content:
            # Not opening the file is what preserves its timestamp.
            print(f"unchanged: {relative}")
            continue

        path.parent.mkdir(parents=True, exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="") as handle:
            handle.write(content)
        print(f"{'update' if existing is not None else 'create'}: {relative}")

    lines = len(normalize(body).split("\n")) - 1
    print(f"\n{AGENTS_FILE}: {lines} lines (ceiling {MAX_LINES})")
    if TRUNCATION_NOTE in body:
        print("warning: the description was long enough that guidance was truncated.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
