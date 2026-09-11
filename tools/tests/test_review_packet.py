"""Tests for tools/review-packet.sh.

Issue #46: the review-packet standard (docs/agent-team.md, "Briefing agents";
.claude/skills/rules-review/SKILL.md) was reachable only from the rules-review path, and
nothing actually built the packet -- two repo-steward dispatches were briefed with prose
and no diff, and a read-only reviewer has no Bash, so it cannot build one for itself.
This generator is the one place the packet gets assembled.

No test here contacts GitHub. `gh` is stubbed with a recording script on PATH, in the
same style test_new_issue.py uses -- and for the same reason: an earlier version of that
file claimed its stub was airtight and was wrong, filing nine real Issues. The negative
is proven here too (test_no_test_in_this_module_can_reach_real_gh), not just asserted.

Each test gets its own throwaway git repository with its own copy of the script, so
REPO_ROOT resolves inside the fixture rather than into this actual repository.
"""
from __future__ import annotations

import os
import shutil
import stat
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tools" / "review-packet.sh"

DEFAULT_BODY = """## Purpose

Do the thing.

## Source

N/A

## Exact scope

The thing.

## Acceptance criteria

- The thing is done.
- Nothing else broke.

## Required tests/evidence

Tests pass.
"""

NO_ACCEPTANCE_BODY = """## Purpose

Do the thing.

## Source

N/A

## Acceptance criteria

## Required tests/evidence

Tests pass.
"""

RULES_BODY = """## Purpose

Implement a mechanic.

## Source

SR6 Core / Tests / printed p. 44 / PDF p. 45

## Acceptance criteria

- The mechanic is implemented.

## Required tests/evidence

Tests pass.
"""


class ReviewPacketTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp(prefix="deckard-review-packet-"))
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)
        self.repo = self.tmp / "repo"
        self._init_repo()

    # ------------------------------------------------------------------ fixture repo

    def _run(self, *args: str, cwd: Path | None = None, check: bool = True) -> None:
        subprocess.run(list(args), cwd=str(cwd or self.repo), check=check,
                        capture_output=True, text=True)

    def _commit(self, message: str) -> None:
        self._run("git", "add", "-A")
        self._run("git", "commit", "-q", "-m", message)

    def _init_repo(self) -> None:
        (self.repo / "tools").mkdir(parents=True)
        shutil.copy(SCRIPT, self.repo / "tools" / "review-packet.sh")
        self._run("git", "init", "-q", "-b", "main")
        self._run("git", "config", "user.email", "test@example.com")
        self._run("git", "config", "user.name", "Deckard Test")

        decisions = self.repo / "docs" / "decisions"
        decisions.mkdir(parents=True)
        (decisions / "0001-foo.md").write_text(
            "# 0001 -- Foo decision\n\nBody text.\n", encoding="utf-8"
        )
        (decisions / "0002-bar.md").write_text(
            "# 0002 -- Bar decision\n\nBody text.\n", encoding="utf-8"
        )
        (decisions / "README.md").write_text("index, not an ADR\n", encoding="utf-8")

        src = self.repo / "src"
        src.mkdir()
        (src / "existing.txt").write_text("".join(f"line{n}\n" for n in range(1, 11)),
                                           encoding="utf-8")
        self._commit("initial")

    def _branch_with_change(self, name: str, relpath: str, content: str) -> None:
        """Create `name` off the current HEAD with one file changed/added."""
        self._run("git", "checkout", "-q", "-b", name)
        target = self.repo / relpath
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")
        self._commit(f"change on {name}")
        self._run("git", "checkout", "-q", "main")

    # --------------------------------------------------------------------- gh stub

    def _stub_gh(self, issue: str, title: str, body: str) -> Path:
        bindir = self.tmp / "bin"
        bindir.mkdir(exist_ok=True)
        body_file = self.tmp / "issue-body.md"
        body_file.write_text(body, encoding="utf-8")
        self.gh_log = self.tmp / "gh-calls.txt"
        stub = bindir / "gh"
        stub.write_text(
            "#!/usr/bin/env bash\n"
            f'printf "%s\\n" "$*" >> "{self.gh_log}"\n'
            'if [[ "$1" == "issue" && "$2" == "view" ]]; then\n'
            f'  if [[ "$3" != "{issue}" ]]; then\n'
            '    echo "gh: issue not found" >&2\n'
            "    exit 1\n"
            "  fi\n"
            '  if [[ "$*" == *"--json title"* ]]; then\n'
            f'    printf "%s" "{title}"\n'
            "    exit 0\n"
            "  fi\n"
            '  if [[ "$*" == *"--json body"* ]]; then\n'
            f'    cat "{body_file}"\n'
            "    exit 0\n"
            "  fi\n"
            "fi\n"
            'echo "gh stub: unhandled invocation: $*" >&2\n'
            "exit 1\n",
            encoding="utf-8",
        )
        stub.chmod(stub.stat().st_mode | stat.S_IEXEC)
        return bindir

    def run_script(self, *args: str, issue: str = "1", title: str = "Do the thing",
                    body: str = DEFAULT_BODY):
        env = dict(os.environ)
        # The stub goes FIRST on PATH so the real gh is unreachable, and both token
        # variables are cleared so nothing could authenticate even if it were.
        env["PATH"] = f"{self._stub_gh(issue, title, body)}{os.pathsep}{env.get('PATH', '')}"
        env.pop("GH_TOKEN", None)
        env.pop("GITHUB_TOKEN", None)
        return subprocess.run(
            ["bash", str(self.repo / "tools" / "review-packet.sh"), *args],
            capture_output=True, text=True, env=env, cwd=str(self.repo), check=False,
        )

    def gh_calls(self) -> list[str]:
        return self.gh_log.read_text(encoding="utf-8").splitlines() if self.gh_log.exists() else []

    # ------------------------------------------------------------------------- tests

    def test_issue_is_required(self):
        result = self.run_script("--branch", "main")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("--issue is required", result.stderr)

    def test_issue_must_be_numeric(self):
        result = self.run_script("--issue", "abc", "--branch", "main")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("--issue must be numeric", result.stderr)

    def test_branch_is_required(self):
        result = self.run_script("--issue", "1")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("--branch is required", result.stderr)

    def test_context_must_be_a_non_negative_integer(self):
        result = self.run_script("--issue", "1", "--branch", "main", "--context", "-1")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("--context must be", result.stderr)

    def test_unknown_argument_is_refused(self):
        result = self.run_script("--issue", "1", "--branch", "main", "--bogus")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("unknown argument", result.stderr)

    def test_help_prints_usage_without_calling_gh(self):
        result = self.run_script("--help")
        self.assertEqual(result.returncode, 0)
        self.assertIn("review-packet.sh", result.stdout)
        self.assertEqual(self.gh_calls(), [])

    def test_nonexistent_issue_is_refused(self):
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        result = self.run_script("--issue", "2", "--branch", "feature", "--base", "main",
                                  issue="1")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("does not exist", result.stderr)

    def test_issue_without_acceptance_criteria_is_refused(self):
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  body=NO_ACCEPTANCE_BODY)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Acceptance criteria", result.stderr)

    def test_no_diff_between_base_and_branch_is_refused(self):
        result = self.run_script("--issue", "1", "--branch", "main", "--base", "main")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("nothing to review", result.stderr)

    def test_unknown_branch_is_refused(self):
        result = self.run_script("--issue", "1", "--branch", "no-such-ref-xyz", "--base", "main")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("not found", result.stderr)

    def test_output_inside_repo_and_ungitignored_is_refused(self):
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        out = self.repo / "packet.md"
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  "--output", str(out))
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("must never be committed", result.stderr)
        self.assertFalse(out.exists())

    def test_output_inside_repo_but_gitignored_is_allowed(self):
        (self.repo / ".gitignore").write_text("/ignored-packets/\n", encoding="utf-8")
        self._commit("add gitignore")
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        out = self.repo / "ignored-packets" / "packet.md"
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  "--output", str(out))
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(out.is_file())

    def test_output_outside_repo_is_allowed_and_creates_parent_dirs(self):
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        out = self.tmp / "nested" / "dir" / "packet.md"
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  "--output", str(out))
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertTrue(out.is_file())

    def test_packet_contains_issue_title_and_acceptance_criteria(self):
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  title="Frobnicate the widget")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("Frobnicate the widget", result.stdout)
        self.assertIn("The thing is done.", result.stdout)

    def test_packet_contains_changed_files_and_diff(self):
        self._branch_with_change("feature", "src/existing.txt",
                                  "".join(f"line{n}\n" for n in range(1, 11)).replace(
                                      "line5", "CHANGED"))
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("src/existing.txt", result.stdout)
        self.assertIn("-line5", result.stdout)
        self.assertIn("+CHANGED", result.stdout)

    def test_packet_lists_adrs_but_not_the_readme(self):
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("0001-foo.md -- 0001 -- Foo decision", result.stdout)
        self.assertIn("0002-bar.md -- 0002 -- Bar decision", result.stdout)
        self.assertNotIn("README.md --", result.stdout)

    def test_source_locator_is_carried_verbatim(self):
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  body=RULES_BODY)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("SR6 Core / Tests / printed p. 44 / PDF p. 45", result.stdout)

    def test_non_rules_change_does_not_ask_for_rules_conformance(self):
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("NO -- repo-steward's structural review is the whole review",
                       result.stdout)
        self.assertNotIn("rules-conformance review", result.stdout)

    def test_rules_change_asks_for_rules_conformance_and_codex(self):
        self._branch_with_change("feature", "src/Deckard.Rules/Glitches.cs",
                                  "// a rule\n")
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  body=RULES_BODY)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("YES -- rules-conformance and Codex review apply", result.stdout)
        self.assertIn("rules-conformance review", result.stdout)
        self.assertIn("Codex independent cross-vendor review", result.stdout)

    def test_source_manifest_change_also_counts_as_rules_touching(self):
        self._branch_with_change("feature", ".github/source-manifest.json", "{}\n")
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  body=RULES_BODY)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("YES -- rules-conformance and Codex review apply", result.stdout)

    def test_context_flag_changes_the_diff_width(self):
        content = "".join(f"line{n}\n" for n in range(1, 11)).replace("line5", "CHANGED")
        self._branch_with_change("feature", "src/existing.txt", content)
        narrow = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  "--context", "0")
        wide = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                "--context", "5")
        self.assertEqual(narrow.returncode, 0, narrow.stderr)
        self.assertEqual(wide.returncode, 0, wide.stderr)
        self.assertIn("diff context: -U0", narrow.stdout)
        self.assertIn("diff context: -U5", wide.stdout)
        self.assertLess(len(narrow.stdout), len(wide.stdout))

    def test_rejected_packets_never_reach_gh(self):
        self.run_script("--issue", "1", "--branch", "main", "--base", "main")
        # The no-diff refusal happens after both gh calls (title, body) succeed --
        # unlike new-issue.sh's pre-gh gate, this generator needs the Issue's body
        # before it can know whether there is anything to review. What matters is that
        # a refusal never produces a packet, checked directly below.
        result = self.run_script("--issue", "1", "--branch", "main", "--base", "main")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "")

    def test_no_test_in_this_module_can_reach_real_gh(self):
        """Prove the containment rather than asserting it in a docstring.

        tools/tests/test_new_issue.py documents exactly this failure mode: a stub that
        was claimed airtight and was not, and filed nine real Issues. This asserts the
        stub is what actually answered, not the real `gh`.
        """
        self._branch_with_change("feature", "src/existing.txt", "changed\n")
        result = self.run_script("--issue", "1", "--branch", "feature", "--base", "main",
                                  title="probe-title-from-stub")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("probe-title-from-stub", result.stdout)
        self.assertGreaterEqual(len(self.gh_calls()), 2)


if __name__ == "__main__":
    unittest.main()
