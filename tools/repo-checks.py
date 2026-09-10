#!/usr/bin/env python3
"""repo-checks -- mechanical enforcement of Deckard's written invariants.

Every check here exists because the alternative is trusting an agent to remember a
rule stated in prose. Prose does not fail a build. These do.

    tools/repo-checks.py            # run every check against the repo
    tools/repo-checks.py --json     # machine-readable result

Checks:
  text-hygiene   UTF-8, no BOM, LF endings, exactly one trailing newline
  determinism    no ambient randomness or ambient time in engine source
  layering       declared ProjectReference graph matches docs/architecture.md
  source-boundary  no rulebook, no source packet, no local source path committed
  action-pins    third-party GitHub Actions pinned to immutable commit SHAs
  single-queue   no competing task backlog outside GitHub Issues
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

TEXT_SUFFIXES = {
    ".cs", ".csproj", ".props", ".targets", ".slnx", ".json", ".md",
    ".yml", ".yaml", ".sh", ".py", ".editorconfig", ".gitattributes", ".gitignore",
}

# --------------------------------------------------------------------------------
# Determinism. See docs/architecture.md and CLAUDE.md.
#
# Same rules version + same initial state + same seed + same ordered decisions
# => same outcomes and same ordered event history. Every pattern below breaks that
# by pulling entropy or wall-clock time out of the ambient environment.
# --------------------------------------------------------------------------------
BANNED_IN_ENGINE: list[tuple[str, str]] = [
    (r"\bRandom\s*\.\s*Shared\b", "Random.Shared is process-global ambient entropy"),
    (r"\bnew\s+Random\s*\(", "new Random() is ambient entropy; inject IRandomSource"),
    (r"\bRandomNumberGenerator\b", "cryptographic RNG is not replayable"),
    (r"\bGuid\s*\.\s*NewGuid\s*\(", "Guid.NewGuid() as game state is non-reproducible"),
    (r"\bDateTime\s*\.\s*(Now|UtcNow|Today)\b", "ambient clock breaks replay"),
    (r"\bDateTimeOffset\s*\.\s*(Now|UtcNow)\b", "ambient clock breaks replay"),
    (r"\bEnvironment\s*\.\s*TickCount", "process timing is not a rules input"),
    (r"\bStopwatch\s*\.\s*(GetTimestamp|StartNew)", "process timing is not a rules input"),
    (r"\bEnvironment\s*\.\s*GetEnvironmentVariable", "engine must not read the environment"),
    (r"\bTask\s*\.\s*Run\b", "concurrency makes resolution order non-deterministic"),
    (r"\.\s*AsParallel\s*\(", "PLINQ makes iteration order non-deterministic"),
]

# An engine file may opt out only with an explicit, reviewed justification on the line.
ALLOW_MARKER = "deckard:allow-nondeterminism"

# The declared dependency graph. Core is the floor; nothing may point upward.
# Files that legitimately mention the source boundary machinery: the tool that emits
# packets, the checker that hunts for them, and the doc that explains both. Everything
# else mentioning a packet marker or a local rulebook path is a leak.
PACKET_MARKER = "DECKARD SOURCE" + " PACKET"
# Any absolute path into somebody's home directory. Deliberately generic rather than
# naming a particular file: the checker should not need to know, or publish, what
# Brandon called his copy.
LOCAL_PATH_LEAK = re.compile(r"(?:/home/|/Users/|/root/)[A-Za-z0-9_.\-]+/")
SOURCE_AWARE_FILES = {
    "tools/source-slice.py",
    "tools/repo-checks.py",
    "tools/tests/test_repo_checks.py",
    "docs/source-handling.md",
}

ALLOWED_PROJECT_REFS: dict[str, set[str]] = {
    "Deckard.Core": set(),
    "Deckard.Data": {"Deckard.Core"},
    "Deckard.Rules": {"Deckard.Core", "Deckard.Data"},
}


class Failure(str):
    """A single human-readable check failure."""


def tracked_files(root: Path) -> list[Path]:
    result = subprocess.run(
        ["git", "ls-files", "-z"], cwd=root, capture_output=True, text=True, check=False
    )
    if result.returncode != 0:
        return []
    return [root / name for name in result.stdout.split("\0") if name]


# --------------------------------------------------------------------------- checks


def check_text_hygiene(root: Path) -> list[Failure]:
    failures: list[Failure] = []
    for path in tracked_files(root):
        if path.suffix not in TEXT_SUFFIXES and path.name not in TEXT_SUFFIXES:
            continue
        if not path.is_file():
            continue
        raw = path.read_bytes()
        rel = path.relative_to(root)
        if raw.startswith(b"\xef\xbb\xbf"):
            failures.append(Failure(f"{rel}: UTF-8 BOM (spec requires no BOM)"))
        try:
            raw.decode("utf-8")
        except UnicodeDecodeError:
            failures.append(Failure(f"{rel}: not valid UTF-8"))
            continue
        if b"\r\n" in raw:
            failures.append(Failure(f"{rel}: CRLF line endings (spec requires LF)"))
        if raw and not raw.endswith(b"\n"):
            failures.append(Failure(f"{rel}: missing trailing newline"))
        if raw.endswith(b"\n\n"):
            failures.append(Failure(f"{rel}: more than one trailing newline"))
    return failures


def check_determinism(root: Path) -> list[Failure]:
    failures: list[Failure] = []
    engine = root / "src"
    if not engine.is_dir():
        return failures
    for path in sorted(engine.rglob("*.cs")):
        if any(part in {"obj", "bin"} for part in path.parts):
            continue
        rel = path.relative_to(root)
        for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if ALLOW_MARKER in line:
                continue
            for pattern, why in BANNED_IN_ENGINE:
                if re.search(pattern, line):
                    failures.append(
                        Failure(f"{rel}:{lineno}: {why}  [{line.strip()[:70]}]")
                    )
    return failures


def check_layering(root: Path) -> list[Failure]:
    failures: list[Failure] = []
    for project, allowed in ALLOWED_PROJECT_REFS.items():
        csproj = root / "src" / project / f"{project}.csproj"
        if not csproj.is_file():
            failures.append(Failure(f"missing expected project: {csproj.relative_to(root)}"))
            continue
        text = csproj.read_text(encoding="utf-8")
        declared = {
            Path(m).stem
            for m in re.findall(r'ProjectReference\s+Include="([^"]+)"', text)
        }
        extra = declared - allowed
        if extra:
            failures.append(
                Failure(
                    f"{project} declares forbidden ProjectReference(s): {sorted(extra)}; "
                    f"allowed: {sorted(allowed) or 'none'}"
                )
            )
    return failures


def check_source_boundary(root: Path) -> list[Failure]:
    """The rulebook is commercial copyrighted material. Nothing derived from it ships."""
    failures: list[Failure] = []
    manifest_path = root / ".github" / "source-manifest.json"

    for path in tracked_files(root):
        rel = path.relative_to(root)
        if path.suffix.lower() in {".pdf", ".epub", ".mobi"}:
            failures.append(Failure(f"{rel}: rulebook-shaped binary is tracked in git"))
        if not path.is_file() or path.suffix not in TEXT_SUFFIXES:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        if PACKET_MARKER in text and rel.as_posix() not in SOURCE_AWARE_FILES:
            failures.append(Failure(f"{rel}: contains an extracted source packet"))
        if LOCAL_PATH_LEAK.search(text) and rel.as_posix() not in SOURCE_AWARE_FILES:
            failures.append(Failure(f"{rel}: leaks a local authoritative-source path"))

    if manifest_path.is_file():
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        for entry in manifest.get("sources", []):
            if not re.fullmatch(r"[0-9a-f]{64}", entry.get("sha256", "")):
                failures.append(
                    Failure(f"source-manifest.json: {entry.get('sourceId')} has no valid sha256")
                )
            for forbidden in ("path", "localPath", "file"):
                if forbidden in entry:
                    failures.append(
                        Failure(
                            f"source-manifest.json: {entry.get('sourceId')} carries a local "
                            f"'{forbidden}'; the local path belongs in "
                            f"${entry.get('envVar', 'SR6_CORE_PDF')}, never in git"
                        )
                    )
    return failures


def check_action_pins(root: Path) -> list[Failure]:
    """Third-party GitHub Actions must be pinned to immutable commit SHAs.

    A tag is mutable: whoever controls the action repository can repoint v4 at new code,
    and that code runs with this repository's workflow token. A 40-hex commit SHA cannot
    be repointed. This check exists because a floating tag is easy to reintroduce by
    copying an example from documentation.
    """
    failures: list[Failure] = []
    workflows = root / ".github" / "workflows"
    if not workflows.is_dir():
        return failures
    uses = re.compile(r"^\s*(?:-\s*)?uses:\s*([^\s#]+)")
    for path in sorted(workflows.glob("*.yml")) + sorted(workflows.glob("*.yaml")):
        rel = path.relative_to(root)
        for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            match = uses.match(line)
            if not match:
                continue
            ref = match.group(1)
            if ref.startswith("./") or ref.startswith("."):
                continue  # a local composite action in this repository
            if "@" not in ref:
                failures.append(Failure(f"{rel}:{lineno}: action has no version: {ref}"))
                continue
            _, _, version = ref.partition("@")
            if not re.fullmatch(r"[0-9a-f]{40}", version):
                failures.append(
                    Failure(
                        f"{rel}:{lineno}: action pinned to mutable ref '{version}' "
                        f"({ref}); pin to a 40-character commit SHA"
                    )
                )
    return failures


def check_single_queue(root: Path) -> list[Failure]:
    """GitHub Issues are the only live work queue (CLAUDE.md, governing principle 1).

    A markdown checklist in a governing document is how a second, silently stale
    backlog gets started. Roadmap phases are prose; task lists are not.
    """
    failures: list[Failure] = []
    watched = ["CLAUDE.md", "AGENTS.md", "README.md", "docs/roadmap.md", "docs/scope.md"]
    checkbox = re.compile(r"^\s*[-*]\s*\[[ xX]\]")
    for name in watched:
        path = root / name
        if not path.is_file():
            continue
        for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            if checkbox.match(line):
                failures.append(
                    Failure(
                        f"{name}:{lineno}: task checklist outside GitHub Issues "
                        f"[{line.strip()[:60]}]"
                    )
                )
    return failures


CHECKS = {
    "text-hygiene": check_text_hygiene,
    "determinism": check_determinism,
    "layering": check_layering,
    "source-boundary": check_source_boundary,
    "action-pins": check_action_pins,
    "single-queue": check_single_queue,
}


def run(root: Path, names: list[str]) -> dict[str, list[str]]:
    return {name: [str(f) for f in CHECKS[name](root)] for name in names}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="repo-checks.py", description=__doc__.splitlines()[0])
    parser.add_argument("--root", default=str(ROOT))
    parser.add_argument("--only", action="append", choices=sorted(CHECKS), default=None)
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args(argv)

    names = args.only or sorted(CHECKS)
    results = run(Path(args.root).resolve(), names)
    total = sum(len(v) for v in results.values())

    if args.json:
        print(json.dumps({"failures": total, "checks": results}, indent=2))
    else:
        for name in names:
            problems = results[name]
            if problems:
                print(f"FAIL  {name}  ({len(problems)})")
                for problem in problems:
                    print(f"        {problem}")
            else:
                print(f"ok    {name}")
        print()
        print("repo-checks: PASS" if total == 0 else f"repo-checks: {total} failure(s)")
    return 1 if total else 0


if __name__ == "__main__":
    raise SystemExit(main())
