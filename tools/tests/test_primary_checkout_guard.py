"""Tests for .claude/hooks/primary-checkout-guard.py.

This guard had a real bug on first write: `git rev-parse --git-common-dir` returns a
path relative to the directory git ran in, so resolving it against the process cwd made
the check answer "not the primary checkout" everywhere except the repository root. The
guard appeared to work -- it blocked commits at the root -- while silently permitting
every file write in every subdirectory.

That is the failure mode these tests exist for. A guard nobody tests is a guard nobody
can trust, and a guard that fails open is worse than no guard, because it is believed.
"""
from __future__ import annotations

import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GUARD = ROOT / ".claude" / "hooks" / "primary-checkout-guard.py"

ALLOW, BLOCK = 0, 2


class GuardTestCase(unittest.TestCase):
    """Each test gets a real throwaway git repo, plus a real linked worktree."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.tmp = Path(tempfile.mkdtemp(prefix="deckard-guard-"))
        cls.primary = cls.tmp / "primary"
        cls.primary.mkdir()
        run = lambda *a: subprocess.run(a, cwd=cls.primary, check=True, capture_output=True)
        run("git", "init", "-q", "-b", "main")
        run("git", "config", "user.email", "t@example.com")
        run("git", "config", "user.name", "T")
        (cls.primary / "src").mkdir()
        (cls.primary / "src" / "a.cs").write_text("// a\n")
        run("git", "add", "src/a.cs")
        run("git", "commit", "-qm", "seed")
        cls.worktree = cls.tmp / "wt"
        run("git", "worktree", "add", "-q", "-b", "issue-1-x", str(cls.worktree))

    @classmethod
    def tearDownClass(cls) -> None:
        shutil.rmtree(cls.tmp, ignore_errors=True)

    def guard(self, payload: dict, env_extra: dict | None = None) -> int:
        import os

        env = dict(os.environ)
        env.pop("DECKARD_ALLOW_PRIMARY_MUTATION", None)
        env.update(env_extra or {})
        return subprocess.run(
            [sys.executable, str(GUARD)],
            input=json.dumps(payload), text=True, capture_output=True, env=env, check=False,
        ).returncode

    def bash(self, command: str, cwd: Path, **kw) -> int:
        return self.guard({"tool_name": "Bash", "cwd": str(cwd),
                           "tool_input": {"command": command}}, **kw)

    def write(self, file_path: Path, cwd: Path, tool: str = "Write", **kw) -> int:
        return self.guard({"tool_name": tool, "cwd": str(cwd),
                           "tool_input": {"file_path": str(file_path)}}, **kw)


class PrimaryCheckoutMutationTests(GuardTestCase):
    def test_commit_in_primary_is_blocked(self):
        self.assertEqual(self.bash("git commit -m x", self.primary), BLOCK)

    def test_commit_from_a_subdirectory_is_blocked(self):
        """The original bug: relative --git-common-dir defeated the check below root."""
        self.assertEqual(self.bash("git commit -m x", self.primary / "src"), BLOCK)

    def test_push_is_blocked(self):
        self.assertEqual(self.bash("git push origin main", self.primary), BLOCK)

    def test_reset_is_blocked(self):
        self.assertEqual(self.bash("git reset --hard HEAD~1", self.primary), BLOCK)

    def test_rebase_is_blocked(self):
        self.assertEqual(self.bash("git rebase main", self.primary), BLOCK)

    def test_stash_is_blocked(self):
        self.assertEqual(self.bash("git stash -u", self.primary), BLOCK)

    def test_cherry_pick_is_blocked(self):
        self.assertEqual(self.bash("git cherry-pick abc123", self.primary), BLOCK)

    def test_checking_out_a_feature_branch_is_blocked(self):
        self.assertEqual(self.bash("git checkout issue-1-x", self.primary), BLOCK)
        self.assertEqual(self.bash("git switch issue-1-x", self.primary), BLOCK)

    def test_force_removing_a_worktree_is_blocked(self):
        self.assertEqual(
            self.bash("git worktree remove --force /somewhere", self.primary), BLOCK
        )

    def test_non_fast_forward_merge_is_blocked(self):
        self.assertEqual(self.bash("git merge origin/feature", self.primary), BLOCK)


class SanctionedPrimaryOperationTests(GuardTestCase):
    def test_fast_forward_sync_is_allowed(self):
        """Following origin/main is what the primary checkout is for."""
        self.assertEqual(self.bash("git merge --ff-only origin/main", self.primary), ALLOW)

    def test_checkout_main_is_allowed(self):
        self.assertEqual(self.bash("git checkout main", self.primary), ALLOW)

    def test_checkout_of_a_path_is_allowed(self):
        self.assertEqual(self.bash("git checkout -- src/a.cs", self.primary), ALLOW)

    def test_read_only_git_is_allowed(self):
        for command in ("git status", "git log --oneline", "git diff", "git fetch origin"):
            self.assertEqual(self.bash(command, self.primary), ALLOW, command)

    def test_running_the_gate_is_allowed(self):
        self.assertEqual(self.bash("./scripts/validate.sh full", self.primary), ALLOW)

    def test_dispatching_an_agent_is_allowed(self):
        self.assertEqual(self.bash("tools/dispatch-agent.sh 42", self.primary), ALLOW)


class FileWriteTests(GuardTestCase):
    def test_write_into_primary_is_blocked(self):
        self.assertEqual(self.write(self.primary / "src" / "b.cs", self.primary), BLOCK)

    def test_edit_of_a_primary_root_file_is_blocked(self):
        self.assertEqual(
            self.write(self.primary / "CLAUDE.md", self.primary, tool="Edit"), BLOCK
        )

    def test_write_into_a_nested_primary_directory_is_blocked(self):
        nested = self.primary / "src" / "deep" / "deeper"
        nested.mkdir(parents=True, exist_ok=True)
        self.assertEqual(self.write(nested / "c.cs", self.primary), BLOCK)

    def test_write_outside_any_repo_is_allowed(self):
        self.assertEqual(self.write(self.tmp / "scratch.txt", self.tmp), ALLOW)


class WorktreeTests(GuardTestCase):
    """A linked worktree is where implementation belongs. Nothing is blocked there."""

    def test_commit_in_a_worktree_is_allowed(self):
        self.assertEqual(self.bash("git commit -m x", self.worktree), ALLOW)

    def test_push_from_a_worktree_is_allowed(self):
        self.assertEqual(self.bash("git push -u origin issue-1-x", self.worktree), ALLOW)

    def test_write_in_a_worktree_is_allowed(self):
        self.assertEqual(self.write(self.worktree / "src" / "a.cs", self.worktree), ALLOW)

    def test_write_in_a_worktree_subdirectory_is_allowed(self):
        sub = self.worktree / "src"
        self.assertEqual(self.write(sub / "new.cs", sub), ALLOW)


class BulkStagingTests(GuardTestCase):
    """`git add -A` is blocked everywhere, worktrees included."""

    def test_add_all_is_blocked_in_primary(self):
        self.assertEqual(self.bash("git add -A", self.primary), BLOCK)

    def test_add_dot_is_blocked_in_a_worktree(self):
        self.assertEqual(self.bash("git add .", self.worktree), BLOCK)

    def test_add_all_long_flag_is_blocked(self):
        self.assertEqual(self.bash("git add --all", self.worktree), BLOCK)

    def test_explicit_paths_are_allowed(self):
        self.assertEqual(self.bash("git add src/a.cs src/b.cs", self.worktree), ALLOW)

    def test_add_patch_is_allowed(self):
        self.assertEqual(self.bash("git add -p src/a.cs", self.worktree), ALLOW)


class EscapeHatchTests(GuardTestCase):
    def test_escape_hatch_permits_sanctioned_primary_work(self):
        self.assertEqual(
            self.bash("git commit -m x", self.primary,
                      env_extra={"DECKARD_ALLOW_PRIMARY_MUTATION": "1"}),
            ALLOW,
        )

    def test_escape_hatch_permits_writes(self):
        self.assertEqual(
            self.write(self.primary / "src" / "d.cs", self.primary,
                       env_extra={"DECKARD_ALLOW_PRIMARY_MUTATION": "1"}),
            ALLOW,
        )


class RobustnessTests(GuardTestCase):
    def test_unparseable_payload_does_not_break_the_session(self):
        result = subprocess.run(
            [sys.executable, str(GUARD)], input="not json", text=True,
            capture_output=True, check=False,
        )
        self.assertEqual(result.returncode, ALLOW)

    def test_unrelated_tools_are_ignored(self):
        self.assertEqual(
            self.guard({"tool_name": "Read", "cwd": str(self.primary),
                        "tool_input": {"file_path": str(self.primary / "src" / "a.cs")}}),
            ALLOW,
        )


if __name__ == "__main__":
    unittest.main()
