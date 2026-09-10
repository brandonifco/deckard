#!/usr/bin/env python3
"""PreToolUse guard: keep implementation work out of the primary checkout.

The primary checkout's steady state is `main`, clean, used for orchestration. Feature
work belongs in an isolated worktree created by tools/dispatch-agent.sh.

Written instructions alone do not survive a long agent session -- an agent told once,
forty tool calls ago, will eventually edit a file where it is standing. This hook is the
mechanical backstop for that.

It is ACCIDENT PREVENTION, not security. A determined agent can bypass it trivially, and
that is fine: the goal is to stop the unintentional commit on main, not to defend against
a hostile process. What it does need to cover is the ordinary accident -- and the first
version did not. `git -C <primary> commit` (the exact form the open-pr skill teaches),
`git -c user.name=x commit`, `git revert`, `git clean -xfd`, `sed -i CLAUDE.md` and
`echo x >> CLAUDE.md` all walked straight through it.

Commands are parsed into shell segments and tokenised rather than regex-matched, because
the regex approach could not see git's two-token global options at all.

Escape hatch, deliberately explicit and documented in CLAUDE.md:

    DECKARD_ALLOW_PRIMARY_MUTATION=1

Exit codes: 0 allow, 2 block (stderr is shown to the agent).
"""
from __future__ import annotations

import json
import os
import re
import shlex
import subprocess
import sys
from pathlib import Path

ESCAPE_HATCH = "DECKARD_ALLOW_PRIMARY_MUTATION"

# Git subcommands that mutate history, the index-to-HEAD relationship, the working tree,
# or the remote. `merge` and `checkout` are handled separately: both have sanctioned
# forms in the primary checkout.
FORBIDDEN_VERBS = {
    "commit": "commit in the primary checkout",
    "push": "push from the primary checkout",
    "reset": "reset in the primary checkout",
    "rebase": "rebase in the primary checkout",
    "cherry-pick": "cherry-pick in the primary checkout",
    "revert": "revert in the primary checkout",
    "am": "patch application in the primary checkout",
    "apply": "patch application in the primary checkout",
    "stash": "stash in the primary checkout",
    "clean": "git clean in the primary checkout",
    "update-ref": "direct ref manipulation in the primary checkout",
    "filter-branch": "history rewriting in the primary checkout",
}

# Shell commands that mutate files in place.
FILE_MUTATORS = {"rm", "mv", "cp", "truncate", "install", "shred", "chmod", "chown", "ln"}

SHELL_SPLIT = re.compile(r"(?:&&|\|\||[;|&\n])")
REDIRECT = re.compile(r"(?<![0-9<>])>>?\s*([^\s;|&<>]+)")
BULK_ADD_ARGS = {"-A", "--all", "."}


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
    paths, which made this answer False everywhere except the repository root.
    """
    if not Path(path).is_dir():
        return False, None
    top = git(["rev-parse", "--show-toplevel"], path)
    if not top:
        return False, None
    common = git(["rev-parse", "--git-common-dir"], path)
    gitdir = git(["rev-parse", "--git-dir"], path)
    if common is None or gitdir is None:
        return False, top

    base = Path(path)
    resolve = lambda p: (Path(p) if Path(p).is_absolute() else base / p).resolve()
    return resolve(common) == resolve(gitdir), top


WORKTREE_GUIDANCE = (
    "Implementation work belongs in an isolated worktree, not the primary checkout:\n"
    "    tools/dispatch-agent.sh <issue-number>\n"
    "then cd to the path it prints and work there.\n\n"
    "If you are the orchestrator doing sanctioned primary-checkout work, set\n"
    f"    {ESCAPE_HATCH}=1\n"
    "for that command, and say in the PR or report why it was necessary."
)


def tokenize(segment: str) -> list[str]:
    try:
        return shlex.split(segment)
    except ValueError:
        # Unbalanced quotes or a heredoc. Fall back to whitespace splitting rather than
        # failing open on a command we could not parse.
        return segment.split()


def parse_git(tokens: list[str], cwd: str) -> tuple[str, str, list[str]] | None:
    """Return (effective_cwd, subcommand, rest) for a git invocation, else None.

    Consumes git's GLOBAL options, including the two-token forms `-C <path>` and
    `-c <key>=<value>`. The first version's regex could not match those, so
    `git -C <primary> commit` and `git -c user.name=x commit` were both invisible to it.
    """
    index = 0
    while index < len(tokens) and tokens[index] in {"sudo", "env", "command", "nohup"}:
        index += 1
    if index >= len(tokens) or Path(tokens[index]).name != "git":
        return None
    index += 1

    effective_cwd = cwd
    while index < len(tokens):
        token = tokens[index]
        if token == "-C" and index + 1 < len(tokens):
            target = tokens[index + 1]
            effective_cwd = str((Path(effective_cwd) / target).resolve())
            index += 2
        elif token == "-c" and index + 1 < len(tokens):
            index += 2
        elif token.startswith("--git-dir") or token.startswith("--work-tree"):
            index += 2 if "=" not in token else 1
        elif token.startswith("-"):
            index += 1
        else:
            return effective_cwd, token, tokens[index + 1 :]
    return None


def check_git(tokens: list[str], cwd: str) -> None:
    parsed = parse_git(tokens, cwd)
    if parsed is None:
        return
    effective_cwd, verb, rest = parsed

    # `git add -A` is blocked everywhere, worktrees included: bulk staging is how build
    # output, source packets and another task's edits reach a commit that claims to close
    # exactly one Issue.
    if verb == "add" and any(arg in BULK_ADD_ARGS for arg in rest):
        emit(
            "bulk `git add -A` / `git add .`",
            "Stage explicit paths instead:\n"
            "    git add path/to/one.cs path/to/two.cs\n\n"
            "Bulk staging is how build output, source packets and unrelated work end up\n"
            "in a commit that claims to close one Issue.",
        )

    primary, _ = is_primary_checkout(effective_cwd)
    if not primary:
        return

    if verb in FORBIDDEN_VERBS:
        emit(FORBIDDEN_VERBS[verb], WORKTREE_GUIDANCE)

    if verb in {"checkout", "switch"}:
        positional = [a for a in rest if not a.startswith("-")]
        # `git checkout -- <path>` restores files and leaves the branch alone.
        if "--" not in rest and positional and positional[0] != "main":
            emit(f"checking out '{positional[0]}' in the primary checkout", WORKTREE_GUIDANCE)
        if "--detach" in rest:
            emit("detaching HEAD in the primary checkout", WORKTREE_GUIDANCE)

    if verb == "restore" and ("--staged" in rest or "-S" in rest):
        emit("unstaging in the primary checkout", WORKTREE_GUIDANCE)

    if verb == "branch" and ("-f" in rest or "--force" in rest or "-D" in rest):
        emit("forcibly moving or deleting a branch in the primary checkout", WORKTREE_GUIDANCE)

    if verb == "merge" and not any(a == "--ff-only" for a in rest):
        emit(
            "merge in the primary checkout without --ff-only",
            "The primary checkout may only fast-forward to a new origin/main:\n"
            "    git merge --ff-only origin/main\n\n"
            "Anything else creates history in a checkout that is supposed to only follow it.",
        )

    if verb == "worktree" and rest and rest[0] == "add":
        for arg in rest[1:]:
            if arg.startswith("-"):
                continue
            target = (Path(effective_cwd) / arg).resolve()
            top = git(["rev-parse", "--show-toplevel"], effective_cwd)
            if top and _is_within(target, Path(top).resolve()):
                emit(
                    f"creating a worktree inside the repository ({arg})",
                    "Worktrees live OUTSIDE the repository. One inside it eventually gets\n"
                    "committed, scanned by a tool that did not expect it, or deleted by a\n"
                    "clean step. Use tools/dispatch-agent.sh, or set DECKARD_WORKTREE_ROOT\n"
                    "to a directory outside the repo.",
                )
            break
    if verb == "worktree" and rest and rest[0] == "remove" and (
        "--force" in rest or "-f" in rest
    ):
        emit("force-removing a worktree", WORKTREE_GUIDANCE)


def _is_within(child: Path, parent: Path) -> bool:
    try:
        child.relative_to(parent)
        return True
    except ValueError:
        return False


def primary_root(cwd: str) -> Path | None:
    primary, top = is_primary_checkout(cwd)
    return Path(top).resolve() if primary and top else None


def check_shell_writes(segment: str, tokens: list[str], cwd: str) -> None:
    """Block in-place file mutation targeting the primary checkout.

    The first version guarded only the Write/Edit tools, so every shell write path was
    open: `sed -i`, `echo >>`, `cat > file <<EOF`, `tee`, `rm -rf`. Agents edit through
    the shell routinely, which made that a large hole in a guard that reads as complete.
    """
    root = primary_root(cwd)
    if root is None:
        return

    def blocked_if_inside(raw: str, what: str) -> None:
        target = (Path(cwd) / raw).resolve()
        if _is_within(target, root):
            emit(f"{what} targeting the primary checkout ({raw})", WORKTREE_GUIDANCE)

    for match in REDIRECT.finditer(segment):
        blocked_if_inside(match.group(1), "shell redirection")

    if not tokens:
        return
    command = Path(tokens[0]).name

    if command == "sed" and any(a == "-i" or a.startswith("-i") and "i" in a for a in tokens[1:]):
        for arg in tokens[1:]:
            if not arg.startswith("-") and arg not in {"-i"}:
                blocked_if_inside(arg, "in-place sed")

    if command == "tee":
        for arg in tokens[1:]:
            if not arg.startswith("-"):
                blocked_if_inside(arg, "tee")

    if command in FILE_MUTATORS:
        for arg in tokens[1:]:
            if not arg.startswith("-"):
                blocked_if_inside(arg, f"`{command}`")


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

    if tool in {"Write", "Edit", "NotebookEdit"}:
        target = tool_input.get("file_path") or tool_input.get("notebook_path")
        if not target:
            return 0
        resolved = Path(target).expanduser()
        resolved = (Path(cwd) / resolved).resolve() if not resolved.is_absolute() else resolved.resolve()
        probe = str(resolved.parent) if resolved.parent.is_dir() else cwd
        primary, top = is_primary_checkout(probe)
        if primary and top and _is_within(resolved, Path(top).resolve()):
            emit(f"{tool} targets the primary checkout ({resolved.name})", WORKTREE_GUIDANCE)
        return 0

    if tool != "Bash":
        return 0

    command = tool_input.get("command", "") or ""
    for segment in SHELL_SPLIT.split(command):
        segment = segment.strip()
        if not segment:
            continue
        tokens = tokenize(segment)
        check_git(tokens, cwd)
        check_shell_writes(segment, tokens, cwd)

    return 0


if __name__ == "__main__":
    sys.exit(main())
