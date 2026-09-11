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


class BotExemptionTests(unittest.TestCase):
    """Dependency bots have no Issue and never will; humans and agents still do.

    The exemption has to be narrow. If it widened to "anything that looks like a bot",
    any PR could opt out of the policy by choosing a login.
    """

    EMPTY = "no sections at all"

    def test_dependabot_with_an_empty_body_passes(self):
        for login in ("dependabot[bot]", "app/dependabot"):
            self.assertEqual(
                pr_policy.check(self.EMPTY, ["Directory.Packages.props"], no_labels,
                                author=login),
                [],
                f"{login!r} must be exempt",
            )

    def test_the_graphql_login_form_is_allowlisted(self):
        """Regression: the first fix allowlisted only the REST spelling.

        tools/pr-policy.py reads the author from `gh pr view --json author`, which
        returns {"is_bot": true, "login": "app/dependabot"}. Allowlisting only
        "dependabot[bot]" meant the exemption never fired against the real interface,
        and the original tests did not catch it because they asserted the same wrong
        string the implementation used.
        """
        self.assertTrue(pr_policy.is_exempt("app/dependabot"))

    def test_the_same_empty_body_from_a_human_still_fails(self):
        failures = pr_policy.check(self.EMPTY, ["Directory.Packages.props"], no_labels,
                                   author="brandonifco")
        self.assertTrue(failures)
        self.assertIn("no linked Issue", " ".join(failures))

    def test_no_author_information_does_not_exempt(self):
        """A missing author must fail closed, not open."""
        self.assertTrue(pr_policy.check(self.EMPTY, [], no_labels, author=None))

    def test_a_lookalike_bot_name_is_not_exempt(self):
        for impostor in ("dependabot", "Dependabot[bot]", "dependabot[bot] ", "not-dependabot[bot]"):
            self.assertTrue(
                pr_policy.check(self.EMPTY, [], no_labels, author=impostor),
                f"{impostor!r} must not be exempt",
            )

    def test_exemption_still_applies_when_a_bot_touches_rules_files(self):
        """The name previously said the opposite of the assertion.

        The behaviour is deliberate: the exemption waives the NARRATIVE sections, and a
        bot cannot write a rules-conformance section whatever it touches. Correctness is
        still gated by build-and-test. A dependency bot reaching into src/Deckard.Rules/
        would be anomalous, but failing policy is the wrong lever -- the bot cannot fix
        it, so the PR would simply be stuck. Recorded here rather than left implicit.
        """
        self.assertEqual(
            pr_policy.check(self.EMPTY, ["src/Deckard.Rules/X.cs"], no_labels,
                            author="dependabot[bot]"),
            [],
        )

    def test_is_exempt_helper(self):
        self.assertTrue(pr_policy.is_exempt("dependabot[bot]"))
        self.assertFalse(pr_policy.is_exempt("brandonifco"))
        self.assertFalse(pr_policy.is_exempt(None))


class LocatorShapeTests(unittest.TestCase):
    """A conformance section must name a page, not assert agreement with the book."""

    RULES_FILE = ["src/Deckard.Rules/DiceTest.cs"]

    def _conformance(self, text: str) -> str:
        return GOOD.replace(
            "N/A -- no Shadowrun mechanic is touched; this is the PRNG substrate below "
            "the dice layer.",
            text,
        )

    def test_matches_the_book_is_refused(self):
        failures = pr_policy.check(self._conformance("Matches the book."),
                                   self.RULES_FILE, no_labels)
        self.assertIn("names no page", " ".join(failures))

    def test_verified_against_the_source_is_refused(self):
        failures = pr_policy.check(self._conformance("Verified against the source packet."),
                                   self.RULES_FILE, no_labels)
        self.assertIn("names no page", " ".join(failures))

    def test_a_full_locator_passes(self):
        body = self._conformance(
            "SR6 Core / Tests / printed pp. 35-36 / PDF pp. 36-37. Verified all 6 rows."
        )
        self.assertEqual(pr_policy.check(body, self.RULES_FILE, no_labels), [])

    def test_a_singular_page_locator_passes(self):
        body = self._conformance("SR6 Core / Glitches / printed p. 44 / PDF p. 45.")
        self.assertEqual(pr_policy.check(body, self.RULES_FILE, no_labels), [])

    def test_locator_is_not_required_for_non_rules_work(self):
        self.assertEqual(pr_policy.check(GOOD, ["docs/roadmap.md"], no_labels), [])


class SourceBaselineTests(unittest.TestCase):
    """Changing the pinned baseline requires an ADR. Four documents said so; now it holds."""

    def _conformance(self, text: str) -> str:
        return GOOD.replace(
            "N/A -- no Shadowrun mechanic is touched; this is the PRNG substrate below "
            "the dice layer.",
            text,
        )

    LOCATOR = "SR6 Core / Tests / printed pp. 35-36 / PDF pp. 36-37."

    def test_manifest_change_without_an_adr_is_refused(self):
        failures = pr_policy.check(self._conformance(self.LOCATOR),
                                   [".github/source-manifest.json"], no_labels)
        self.assertIn("no ADR", " ".join(failures))

    def test_manifest_change_with_an_adr_passes(self):
        body = self._conformance(self.LOCATOR)
        self.assertEqual(
            pr_policy.check(
                body,
                [".github/source-manifest.json", "docs/decisions/0005-new-printing.md"],
                no_labels,
            ),
            [],
        )

    def test_an_adr_alone_needs_no_manifest_change(self):
        self.assertEqual(
            pr_policy.check(GOOD, ["docs/decisions/0005-thing.md"], no_labels), []
        )


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


class PackagesLockFileTests(unittest.TestCase):
    """Issue #86: NuGet's RestorePackagesWithLockFile writes packages.lock.json beside
    each project file it locks, landing it under a rules-surface directory even though
    it is a hash manifest of transitive package versions, not rules content, and can
    never carry a printed-page citation. This is the exact case that blocked PR #82.
    """

    LOCK_FILES = [
        "src/Deckard.Rules/packages.lock.json",
        "src/Deckard.Data/packages.lock.json",
        "tests/Deckard.Rules.Tests/packages.lock.json",
        "tests/Deckard.Data.Tests/packages.lock.json",
    ]

    def test_lock_files_alone_do_not_require_rules_conformance(self):
        for path in self.LOCK_FILES:
            with self.subTest(path=path):
                self.assertEqual(pr_policy.check(GOOD, [path], no_labels), [])

    def test_rules_files_in_excludes_the_lock_files(self):
        self.assertEqual(pr_policy.rules_files_in(self.LOCK_FILES), [])

    def test_a_real_rules_file_alongside_a_lock_file_still_requires_conformance(self):
        """The lock file must not hide a genuine rules change riding in the same diff."""
        changed = ["src/Deckard.Rules/packages.lock.json", "src/Deckard.Rules/DiceTest.cs"]
        failures = pr_policy.check(GOOD, changed, no_labels)
        self.assertIn("says N/A", " ".join(failures))
        self.assertEqual(pr_policy.rules_files_in(changed), ["src/Deckard.Rules/DiceTest.cs"])

    def test_source_manifest_is_still_rules_work(self):
        """The case most likely to break: a wider exclusion (e.g. by extension) would
        also swallow .github/source-manifest.json, which must stay a rules surface."""
        self.assertEqual(
            pr_policy.rules_files_in([".github/source-manifest.json"]),
            [".github/source-manifest.json"],
        )
        failures = pr_policy.check(GOOD, [".github/source-manifest.json"], no_labels)
        self.assertIn("Rules conformance", " ".join(failures))

    def test_a_dicepool_change_still_requires_conformance(self):
        failures = pr_policy.check(
            GOOD, ["src/Deckard.Rules/Resolution/DicePoolRoll.cs"], no_labels
        )
        self.assertIn("Rules conformance", " ".join(failures))


class LinkedIssueCodeFenceTests(unittest.TestCase):
    """#52: PR #51 quoted a generated packet containing "Closes #38" as evidence for
    tools/review-packet.sh, and was failed for closing two Issues. Quoted text is not a
    declaration; each shape it can be quoted in must be ignored for the count, while a
    real second declaration -- or no declaration at all -- must still fail.
    """

    def _evidence(self, text: str) -> str:
        return GOOD.replace(
            "```\n$ ./scripts/validate.sh full\nvalidate.sh full: PASS\n```", text
        )

    def test_closes_inside_a_backtick_fence_does_not_count_as_a_second_issue(self):
        body = self._evidence(
            "```\n$ tools/review-packet.sh --issue 38 --pr 50 --branch x --base y "
            "--output z\n  -> ## Pull request ... Closes #38 ...\n```"
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_closes_inside_a_tilde_fence_does_not_count(self):
        body = self._evidence("~~~\nCloses #99\n~~~\n\n$ ./scripts/validate.sh full\nPASS")
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_closes_inside_an_indented_code_block_does_not_count(self):
        body = self._evidence(
            "    Closes #99 (quoted from a generated packet)\n\n"
            "$ ./scripts/validate.sh full\nPASS"
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_closes_inside_an_inline_code_span_does_not_count(self):
        body = self._evidence(
            "The template line `Closes #99` is quoted here as an example.\n\n"
            "$ ./scripts/validate.sh full\nPASS"
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_real_declaration_plus_the_same_issue_quoted_as_evidence_counts_once(self):
        """The exact PR #51 shape: Issue #7 declared for real, then quoted back as proof."""
        body = self._evidence(
            "```\n$ tools/review-packet.sh --pr 50\n"
            "  -> ## Pull request ... Closes #7 ...\n```"
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_a_genuine_second_declaration_outside_any_fence_still_fails(self):
        body = GOOD.replace("Closes #7", "Closes #7\nCloses #9")
        self.assertIn("must close exactly one", " ".join(pr_policy.check(body, [], no_labels)))

    def test_no_declaration_anywhere_still_fails(self):
        body = GOOD.replace("Closes #7", "Related to #7")
        self.assertIn("no linked Issue", " ".join(pr_policy.check(body, [], no_labels)))

    def test_only_a_fenced_closes_with_no_real_declaration_still_fails(self):
        body = GOOD.replace("Closes #7", "Related to #7").replace(
            "```\n$ ./scripts/validate.sh full\nvalidate.sh full: PASS\n```",
            "```\nCloses #7\n```",
        )
        self.assertIn("no linked Issue", " ".join(pr_policy.check(body, [], no_labels)))


class CommandLineMatchTests(unittest.TestCase):
    """#60: PR #59's evidence was failed for pasting `CI=true ./scripts/validate.sh full`
    -- the more thorough of its two runs (#48) -- because COMMAND_LINE required one of a
    fixed set of binary names right after an optional scripts/tools prefix. An env-var
    prefix, and a script under tools/ or scripts/ that is not one of the named few, must
    both count; prose that names no command at all must still fail.
    """

    def _evidence(self, text: str) -> str:
        return GOOD.replace(
            "```\n$ ./scripts/validate.sh full\nvalidate.sh full: PASS\n```", text
        )

    def test_env_prefixed_validate_full_satisfies_the_check(self):
        body = self._evidence(
            "```\n$ CI=true ./scripts/validate.sh full\nvalidate.sh full: PASS\n```"
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_multiple_env_assignments_are_allowed(self):
        body = self._evidence(
            "```\n$ CI=true DOTNET_NOLOGO=1 ./scripts/validate.sh full\nPASS\n```"
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_env_prefix_without_a_dollar_prompt_still_matches(self):
        body = self._evidence("CI=true ./scripts/validate.sh full\nvalidate.sh full: PASS")
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_python_invoked_tools_script_satisfies_the_check(self):
        body = self._evidence(
            "```\n$ python3 tools/rules-conformance-gate.py --pr 54\nOK\n```"
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_bare_tools_script_not_in_the_allowlist_satisfies_the_check(self):
        """tools/review-packet.sh is invoked directly, with no interpreter prefix and no
        entry in COMMAND_LINE's fixed name list -- the exact gap #60 flags: the list
        goes stale every time a script is added.
        """
        body = self._evidence(
            "```\n$ tools/review-packet.sh --issue 38 --pr 50 --branch x --base y "
            "--output z\nwrote packet\n```"
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_bare_scripts_script_not_in_the_allowlist_satisfies_the_check(self):
        body = self._evidence("```\n$ scripts/bootstrap-dotnet.sh\ninstalled\n```")
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_source_slice_invocation_satisfies_the_check(self):
        body = self._evidence(
            '```\n$ tools/source-slice.py --printed-pages 44-45 --expect "Edge"\nwrote packet\n```'
        )
        self.assertEqual(pr_policy.check(body, [], no_labels), [])

    def test_bare_tests_pass_still_fails(self):
        failures = pr_policy.check(self._evidence("All tests pass."), [], no_labels)
        self.assertIn("names no command", " ".join(failures))

    def test_prose_with_no_command_still_fails(self):
        body = self._evidence("Everything looks correct and works as expected.")
        self.assertIn("names no command", " ".join(pr_policy.check(body, [], no_labels)))

    def test_prose_mentioning_an_env_var_without_a_command_still_fails(self):
        """`CI=true` alone, describing the environment rather than prefixing a command."""
        body = self._evidence("CI=true is set for every pipeline run, so tests pass.")
        self.assertIn("names no command", " ".join(pr_policy.check(body, [], no_labels)))


if __name__ == "__main__":
    unittest.main()
