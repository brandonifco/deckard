"""Tests for tools/new-issue.sh.

The `start-issue` skill told agents this script "refuses to file a mechanics Issue with
no source locator". It did not: its only validation was that the body was not entirely
comments. An agent trusting a gate that is not there is worse off than one told the
truth, so the gate now exists and these tests hold it to the claim.

Nothing here contacts GitHub: every case is decided before `gh` is invoked.
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
SCRIPT = ROOT / "tools" / "new-issue.sh"

WITH_LOCATOR = """## Purpose
Implement glitch detection.

## Source
SR6 Core / Glitches / printed p. 44 / PDF p. 45

## Exact scope
Determine glitches and critical glitches.
"""

WITHOUT_LOCATOR = """## Purpose
Implement glitch detection.

## Source
N/A

## Exact scope
Determine glitches and critical glitches.
"""

PROSE_BUT_NO_PAGE = """## Purpose
Implement glitch detection.

## Source
The chapter about tests, somewhere near the front of the book.

## Exact scope
Determine glitches and critical glitches.
"""


class NewIssueTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp(prefix="deckard-issue-"))
        self.addCleanup(shutil.rmtree, self.tmp, ignore_errors=True)

    def editor_writing(self, body: str) -> str:
        """A fake $EDITOR that overwrites the template with `body`."""
        source = self.tmp / "body.md"
        source.write_text(body, encoding="utf-8")
        editor = self.tmp / "fake-editor.sh"
        editor.write_text(f'#!/usr/bin/env bash\ncat "{source}" > "$1"\n', encoding="utf-8")
        editor.chmod(editor.stat().st_mode | stat.S_IEXEC)
        return str(editor)

    def run_script(self, body: str, *labels: str):
        env = dict(os.environ)
        env["EDITOR"] = self.editor_writing(body)
        # A PATH without `gh`, so a test can never reach the network. Anything that gets
        # past the gate fails on the missing binary, which is a distinguishable outcome.
        args = ["--title", "probe"]
        for label in labels:
            args += ["--label", label]
        return subprocess.run(
            ["bash", str(SCRIPT), *args],
            capture_output=True, text=True, env=env, cwd=str(ROOT), check=False,
        )

    def test_mechanics_issue_without_a_page_is_refused(self):
        result = self.run_script(WITHOUT_LOCATOR, "area:rules")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("names\n       no page", result.stderr.replace("\r", ""))

    def test_prose_source_without_a_page_number_is_refused(self):
        result = self.run_script(PROSE_BUT_NO_PAGE, "area:rules")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("no page", result.stderr)

    def test_data_issues_are_also_mechanics_issues(self):
        result = self.run_script(WITHOUT_LOCATOR, "area:data")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("no page", result.stderr)

    def test_mechanics_issue_with_a_locator_passes_the_gate(self):
        """It then proceeds to `gh`, which is past the guard under test."""
        result = self.run_script(WITH_LOCATOR, "area:rules")
        self.assertNotIn("no page", result.stderr)

    def test_non_mechanics_issue_needs_no_locator(self):
        result = self.run_script(WITHOUT_LOCATOR, "area:tooling")
        self.assertNotIn("no page", result.stderr)

    def test_unlabelled_issue_needs_no_locator(self):
        result = self.run_script(WITHOUT_LOCATOR)
        self.assertNotIn("no page", result.stderr)

    def test_empty_body_is_refused(self):
        result = self.run_script("<!-- still the template -->\n", "area:tooling")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("still empty", result.stderr)

    def test_title_is_required(self):
        result = subprocess.run(
            ["bash", str(SCRIPT)], capture_output=True, text=True, cwd=str(ROOT), check=False
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("--title is required", result.stderr)


if __name__ == "__main__":
    unittest.main()
