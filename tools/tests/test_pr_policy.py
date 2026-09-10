"""Tests for tools/pr-policy.py.

The policy check is only worth having if it fails the PRs it is supposed to fail. Two
things are tested here that are easy to get wrong:

  * the UNTOUCHED template must FAIL. A template made of HTML comments looks like content
    to a naive emptiness check, which would let every unfilled PR sail through.
  * a properly filled template must PASS. A checker that rejects correct work gets
    disabled within a week.
"""
from __future__ import annotations

import importlib.util
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

_spec = importlib.util.spec_from_file_location("pr_policy", ROOT / "tools" / "pr-policy.py")
assert _spec and _spec.loader
pr_policy = importlib.util.module_from_spec(_spec)
sys.modules["pr_policy"] = pr_policy
_spec.loader.exec_module(pr_policy)

GOOD = """## Linked Issue

Closes #7

## Exact behavioral claim

Deckard.Core exposes a PCG32 IRandomSource whose sequence is pinned to the published
reference vectors.

## Scope

Adds PcgRandomSource and its tests. Dice rolling and test resolution are unchanged and
still unimplemented.

## Rules conformance

N/A -- no Shadowrun mechanic is touched; this is the PRNG substrate below the dice layer.

## Tests and evidence

```
$ ./scripts/validate.sh full
validate.sh full: PASS
```

## Determinism impact

Introduces the random substrate. Nothing consumes it yet, so no existing sequence changes.

## Decisions and tradeoffs

None beyond ADR 0002.

## Known limitations

No d6 mapping yet; that is Issue #8.

## Agent provenance

- Implemented by: engine-dev (Sonnet)
- Verified by: Codex

## Unrelated changes

None.
"""


def no_labels(_number):
    return []


class TemplateTests(unittest.TestCase):
    def test_untouched_template_fails(self):
        """An unfilled template is comments only, and must not read as content."""
        template = (ROOT / ".github" / "pull_request_template.md").read_text(encoding="utf-8")
        failures = pr_policy.check(template, [], no_labels)
        self.assertTrue(failures, "the empty template must fail policy")
        joined = " ".join(failures)
        for expected in ("no linked Issue", "Exact behavioral claim", "Scope",
                         "Tests and evidence"):
            self.assertIn(expected, joined)

    def test_properly_filled_template_passes(self):
        self.assertEqual(pr_policy.check(GOOD, ["src/Deckard.Core/Pcg.cs"], no_labels), [])


class LinkedIssueTests(unittest.TestCase):
    def test_missing_closes_fails(self):
        body = GOOD.replace("Closes #7", "Related to #7")
        self.assertIn("no linked Issue", " ".join(pr_policy.check(body, [], no_labels)))

    def test_two_closes_fails(self):
        body = GOOD.replace("Closes #7", "Closes #7\nCloses #9")
        self.assertIn("must close exactly one", " ".join(pr_policy.check(body, [], no_labels)))

    def test_fixes_and_resolves_are_recognised(self):
        for verb in ("Fixes", "Resolves"):
            body = GOOD.replace("Closes #7", f"{verb} #7")
            self.assertEqual(pr_policy.check(body, [], no_labels), [], verb)

    def test_nonexistent_issue_fails(self):
        failures = pr_policy.check(GOOD, [], lambda n: None)
        self.assertIn("does not exist", " ".join(failures))

    def test_needs_decision_issue_fails(self):
        failures = pr_policy.check(GOOD, [], lambda n: ["state:needs-decision"])
        self.assertIn("not implementable", " ".join(failures))

    def test_blocked_issue_fails(self):
        failures = pr_policy.check(GOOD, [], lambda n: ["state:blocked"])
        self.assertIn("state:blocked", " ".join(failures))

    def test_ready_issue_passes(self):
        self.assertEqual(pr_policy.check(GOOD, [], lambda n: ["state:ready"]), [])


class RequiredSectionTests(unittest.TestCase):
    def _blank(self, heading: str) -> str:
        lines = GOOD.splitlines()
        out, skipping = [], False
        for line in lines:
            if line.startswith("## "):
                skipping = line[3:].strip().lower() == heading.lower()
                out.append(line)
                continue
            if not skipping:
                out.append(line)
        return "\n".join(out)

    def test_empty_behavioral_claim_fails(self):
        failures = pr_policy.check(self._blank("Exact behavioral claim"), [], no_labels)
        self.assertIn("Exact behavioral claim", " ".join(failures))

    def test_empty_scope_fails(self):
        self.assertIn("Scope", " ".join(pr_policy.check(self._blank("Scope"), [], no_labels)))

    def test_empty_known_limitations_fails(self):
        failures = pr_policy.check(self._blank("Known limitations"), [], no_labels)
        self.assertIn("Known limitations", " ".join(failures))

    def test_empty_provenance_fails(self):
        failures = pr_policy.check(self._blank("Agent provenance"), [], no_labels)
        self.assertIn("Agent provenance", " ".join(failures))

    def test_unfilled_provenance_labels_fail(self):
        """`- Implemented by:` with nothing after it is an unfilled template line."""
        body = GOOD.replace(
            "- Implemented by: engine-dev (Sonnet)\n- Verified by: Codex",
            "- Implemented by:\n- Verified by:",
        )
        self.assertIn("Agent provenance", " ".join(pr_policy.check(body, [], no_labels)))


class EvidenceTests(unittest.TestCase):
    def _evidence(self, text: str) -> str:
        return GOOD.replace(
            "```\n$ ./scripts/validate.sh full\nvalidate.sh full: PASS\n```", text
        )

    def test_empty_evidence_fails(self):
        failures = pr_policy.check(self._evidence(""), [], no_labels)
        self.assertIn("Tests and evidence", " ".join(failures))

    def test_bare_tests_pass_fails(self):
        failures = pr_policy.check(self._evidence("All tests pass."), [], no_labels)
        self.assertIn("names no command", " ".join(failures))

    def test_green_without_a_command_fails(self):
        failures = pr_policy.check(self._evidence("Tests are green"), [], no_labels)
        self.assertIn("names no command", " ".join(failures))

    def test_a_named_command_with_output_passes(self):
        body = self._evidence("```\n$ dotnet test\nPassed! 42 tests\n```")
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_command_without_a_fence_still_needs_output(self):
        body = self._evidence("./scripts/validate.sh full\ntests pass")
        self.assertIn("without showing output", " ".join(pr_policy.check(body, [], no_labels)))


class RulesConformanceTests(unittest.TestCase):
    RULES_FILE = ["src/Deckard.Rules/DiceTest.cs"]

    def test_na_conformance_fails_when_rules_changed(self):
        failures = pr_policy.check(GOOD, self.RULES_FILE, no_labels)
        self.assertIn("says N/A", " ".join(failures))

    def test_na_conformance_passes_when_no_rules_changed(self):
        self.assertEqual(pr_policy.check(GOOD, ["docs/roadmap.md"], no_labels), [])

    def test_real_locator_passes(self):
        body = GOOD.replace(
            "N/A -- no Shadowrun mechanic is touched; this is the PRNG substrate below "
            "the dice layer.",
            "SR6 Core / Success Tests / printed p. 40 / PDF p. 41. Verified all 6 rows "
            "of the printed threshold table.",
        )
        self.assertEqual(pr_policy.check(body, self.RULES_FILE, no_labels), [])

    def test_data_changes_count_as_rules_work(self):
        failures = pr_policy.check(GOOD, ["src/Deckard.Data/Skills.json"], no_labels)
        self.assertIn("Rules conformance", " ".join(failures))

    def test_rules_tests_count_as_rules_work(self):
        failures = pr_policy.check(GOOD, ["tests/Deckard.Rules.Tests/T.cs"], no_labels)
        self.assertIn("Rules conformance", " ".join(failures))

    def test_manifest_change_counts_as_rules_work(self):
        failures = pr_policy.check(GOOD, [".github/source-manifest.json"], no_labels)
        self.assertIn("Rules conformance", " ".join(failures))

    def test_core_only_change_is_not_rules_work(self):
        self.assertEqual(pr_policy.check(GOOD, ["src/Deckard.Core/Pcg.cs"], no_labels), [])


if __name__ == "__main__":
    unittest.main()
