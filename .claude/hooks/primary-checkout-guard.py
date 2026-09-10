#!/usr/bin/env python3
"""PreToolUse guard: keep implementation work out of the primary checkout.

The primary checkout's steady state is `main`, clean, used for orchestration. Feature
work belongs in an isolated worktree created by tools/dispatch-agent.sh.

Written instructions alone do not survive a long agent session -- an agent that has been
told once, forty tool calls ago, will eventually edit a file where it is standing. This
hook is the mechanical backstop for that.

It is ACCIDENT PREVENTION, not security. A determined agent can trivially bypass it, and
that is fine: the goal is to stop the unintentional commit on main, not to defend against
a hostile process.

Escape hatch, deliberately explicit and documented in CLAUDE.md:

    DECKARD_ALLOW_PRIMARY_MUTATION=1

Exit codes: 0 allow, 2 block (stderr is shown to the agent).
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from pathlib import Path

ESCAPE_HATCH = "DECKARD_ALLOW_PRIMARY_MUTATION"

# Git commands that mutate history, the index-to-HEAD relationship, or the remote.
# `git merge --ff-only` is exempt: fast-forwarding the primary checkout to a new
# origin/main after a merge is exactly what the primary checkout is FOR.
FORBIDDEN_GIT = [
    (r"\bgit\s+(-\S+\s+)*commit\b", "commit in the primary checkout"),
    (r"\bgit\s+(-\S+\s+)*push\b", "push from the primary checkout"),
    (r"\bgit\s+(-\S+\s+)*reset\b", "reset in the primary checkout"),
    (r"\bgit\s+(-\S+\s+)*rebase\b", "rebase in the primary checkout"),
    (r"\bgit\s+(-\S+\s+)*cherry-pick\b", "cherry-pick in the primary checkout"),
    (r"\bgit\s+(-\S+\s+)*stash\b", "stash in the primary checkout"),
    (r"\bgit\s+(-\S+\s+)*apply\b", "patch application in the primary checkout"),
    (r"\bgit\s+(-\S+\s+)*worktree\s+remove\s+--force\b", "force-removing a worktree"),
]

# Checking out anything other than main moves the primary checkout off its steady state.
CHECKOUT = re.compile(r"\bgit\s+(-\S+\s+)*(checkout|switch)\b(?P<rest>[^;&|]*)")
MERGE = re.compile(r"\bgit\s+(-\S+\s+)*merge\b(?P<rest>[^;&|]*)")

# `git add -A` / `git add .` stage whatever happens to be lying around, which is how
# build output, source packets and another task's edits end up in a commit.
BULK_ADD = re.compile(r"\bgit\s+(-\S+\s+)*add\s+(-A\b|--all\b|\.(?:\s|$))")


def emit(reason: str, guidance: str) -> None:
    print(f"BLOCKED by primary-checkout-guard: {reason}\n\n{guidance}", file=sys.stderr)
    sys.exit(2)


def git(args: list[str], cwd: str) -> str | None:
    try:
        result = subprocess.run(
            ["git", *args], cwd=cwd, capture_output=True, text=True, check=False, timeout=5
        )
    except (OSError, subprocess.SubprocessError):
        return None
    return result.stdout.strip() if result.returncode == 0 else None


def is_primary_checkout(path: str) -> tuple[bool, str | None]:
    """True when `path` sits in the primary checkout rather than a linked worktree.

    `git rev-parse --git-common-dir` returns a path RELATIVE to the directory git ran in
    (e.g. "../../.git" from a subdirectory) while --git-dir may return an absolute one.
    Resolving them against the process cwd instead of `path` silently compares unrelated
    paths, which made this function answer False everywhere except the repo root.
    """
    top = git(["rev-parse", "--show-toplevel"], path)
    if not top:
        return False, None
    common = git(["rev-parse", "--git-common-dir"], path)
    gitdir = git(["rev-parse", "--git-dir"], path)
    if common is None or gitdir is None:
        return False, top

    base = Path(path)
    resolved_common = (base / common).resolve() if not Path(common).is_absolute() else Path(common).resolve()
    resolved_gitdir = (base / gitdir).resolve() if not Path(gitdir).is_absolute() else Path(gitdir).resolve()
    return resolved_common == resolved_gitdir, top


WORKTREE_GUIDANCE = (
    "Implementation work belongs in an isolated worktree, not the primary checkout:\n"
    "    tools/dispatch-agent.sh <issue-number>\n"
    "then cd to the path it prints and work there.\n\n"
    f"If you are the orchestrator doing sanctioned primary-checkout work, set\n"
    f"    {ESCAPE_HATCH}=1\n"
    "for that command, and say in the PR or report why it was necessary."
)


def main() -> int:
    if os.environ.get(ESCAPE_HATCH) == "1":
        return 0

    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # Never break the session over an unparseable payload.

    tool = payload.get("tool_name", "")
    tool_input = payload.get("tool_input", {}) or {}
    cwd = payload.get("cwd") or os.getcwd()

    # ---------------------------------------------------------------- file writes
    if tool in {"Write", "Edit", "NotebookEdit"}:
        target = tool_input.get("file_path") or tool_input.get("notebook_path")
        if not target:
            return 0
        target_dir = str(Path(target).expanduser().resolve().parent)
        if not Path(target_dir).is_dir():
            target_dir = cwd
        primary, top = is_primary_checkout(target_dir)
        if primary and top:
            try:
                Path(target).expanduser().resolve().relative_to(Path(top).resolve())
            except ValueError:
                return 0  # Outside the repo entirely; not our business.
            emit(
                f"{tool} targets the primary checkout ({Path(target).name})",
                WORKTREE_GUIDANCE,
            )
        return 0

    # --------------------------------------------------------------------- bash
    if tool != "Bash":
        return 0

    command = tool_input.get("command", "") or ""

    if BULK_ADD.search(command):
        emit(
            "bulk `git add -A` / `git add .`",
            "Stage explicit paths instead:\n"
            "    git add path/to/one.cs path/to/two.cs\n\n"
            "Bulk staging is how build output, source packets and unrelated work end up\n"
            "in a commit that claims to close one Issue.",
        )

    primary, _ = is_primary_checkout(cwd)
    if not primary:
        return 0

    for pattern, description in FORBIDDEN_GIT:
        if re.search(pattern, command):
            emit(description, WORKTREE_GUIDANCE)

    match = CHECKOUT.search(command)
    if match:
        rest = match.group("rest").strip()
        # `git checkout -- <path>` and `git checkout main` leave the primary on main.
        if rest and not rest.startswith("--") and rest.split()[0] not in {"main", "-"}:
            emit(
                f"checking out '{rest.split()[0]}' in the primary checkout",
                WORKTREE_GUIDANCE,
            )

    match = MERGE.search(command)
    if match and "--ff-only" not in match.group("rest"):
        emit(
            "merge in the primary checkout without --ff-only",
            "The primary checkout may only fast-forward to a new origin/main:\n"
            "    git merge --ff-only origin/main\n\n"
            "Anything else creates history in a checkout that is supposed to only follow it.",
        )

    return 0


if __name__ == "__main__":
    sys.exit(main())
