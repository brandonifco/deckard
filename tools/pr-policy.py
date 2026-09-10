#!/usr/bin/env python3
"""pr-policy -- enforce the small set of PR facts worth enforcing mechanically.

This is deliberately not an orchestration state machine. It checks a handful of things
that are cheap to verify and expensive to discover missing later, and it does not attempt
to parse prose into meaning.

    tools/pr-policy.py --pr 12                 # check a real PR via gh
    tools/pr-policy.py --body-file body.md \\
                       --changed-files files.txt --no-verify-issue
    tools/pr-policy.py --pr 12 --json evidence.json

A PR fails policy when:
  1. it does not close exactly one real Issue
  2. "Exact behavioral claim" is empty
  3. "Scope" is empty
  4. "Tests and evidence" is empty
  5. tests/evidence claims success without naming a command that produced it
  6. "Known limitations" is empty
  7. "Agent provenance" is empty
  8. rules files changed but "Rules conformance" is empty or N/A
  9. the linked Issue is state:needs-decision, and so is not implementable
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

# Changing anything under these paths makes the PR "rules work", which means its
# conformance section has to say what was verified against the book.
RULES_PATHS = (
    "src/Deckard.Rules/",
    "src/Deckard.Data/",
    "tests/Deckard.Rules.Tests/",
    "tests/Deckard.Data.Tests/",
    ".github/source-manifest.json",
)

# Machine-generated dependency PRs have no Issue, no behavioural claim and no agent
# provenance, and never will. They are still fully subject to build-and-test; what they
# are exempt from is the narrative that exists to make human and agent work reviewable.
# Deliberately an exact allowlist rather than a "looks like a bot" heuristic.
# Both spellings are required. GitHub's REST API reports a bot as "dependabot[bot]";
# `gh pr view --json author` goes through GraphQL and reports "app/dependabot". This tool
# reads the GraphQL form, but the REST form is what appears in webhooks and in most
# documentation, so a future caller will reach for it. Neither is a heuristic -- both are
# exact strings for the same single allowlisted bot.
#
# Deliberately NOT keyed on the author's `is_bot` flag: that would exempt every bot with
# access to the repository, which is a much wider door than this needs.
BOT_AUTHORS = frozenset({"dependabot[bot]", "app/dependabot"})

CLOSES = re.compile(r"\b(?:closes|fixes|resolves)\s+#(\d+)\b", re.IGNORECASE)
HTML_COMMENT = re.compile(r"<!--.*?-->", re.DOTALL)
FENCE = re.compile(r"```.*?```", re.DOTALL)

# A line that plausibly names a command someone actually ran.
COMMAND_LINE = re.compile(
    r"^\s*[$>]?\s*(\./|/|~?/)?(scripts/|tools/)?"
    r"(validate\.sh|doctor\.sh|dotnet|python3?|pytest|git|gh|make|repo-checks)\b",
    re.MULTILINE,
)

# Phrases that assert success without evidence for it.
BARE_CLAIM = re.compile(
    r"^\W*(all\s+)?(the\s+)?(unit\s+|integration\s+)?tests?\s*"
    r"(all\s+)?(pass(ed|ing)?|are\s+green|green|ok)\W*$",
    re.IGNORECASE,
)


def strip_boilerplate(text: str) -> str:
    """Remove template comments so an untouched template reads as empty, not as content."""
    return HTML_COMMENT.sub("", text).strip()


def split_sections(body: str) -> dict[str, str]:
    sections: dict[str, str] = {}
    current: str | None = None
    buffer: list[str] = []
    for line in body.splitlines():
        heading = re.match(r"^#{1,3}\s+(.*?)\s*$", line)
        if heading:
            if current is not None:
                sections[current] = "\n".join(buffer)
            current = heading.group(1).strip().lower()
            buffer = []
        elif current is not None:
            buffer.append(line)
    if current is not None:
        sections[current] = "\n".join(buffer)
    return sections


def section(sections: dict[str, str], name: str) -> str:
    return strip_boilerplate(sections.get(name.lower(), ""))


def is_effectively_empty(text: str) -> bool:
    """Empty, or nothing but placeholder tokens left over from the template."""
    cleaned = re.sub(r"^[\s\-*>#`]+$", "", text, flags=re.MULTILINE).strip()
    cleaned = re.sub(r"```\s*```", "", cleaned).strip()
    if not cleaned:
        return True
    # "Implemented by:" with nothing after it is an unfilled template line.
    residue = re.sub(r"^\s*[-*]?\s*\w[\w /]*:\s*$", "", cleaned, flags=re.MULTILINE).strip()
    return not residue


def is_na(text: str) -> bool:
    """True when the section leads with N/A.

    "N/A -- reason" is the correct form for non-rules work, so matching only a bare
    "N/A" would let any PR excuse itself from conformance simply by adding a reason.
    """
    stripped = text.strip()
    if not stripped:
        return False
    first_line = stripped.splitlines()[0].strip()
    return bool(re.match(r"^\W*n/?a\b", first_line, re.IGNORECASE))


def is_exempt(author: str | None) -> bool:
    """True for allowlisted bots. Narrow on purpose -- a broad bypass hollows out the policy."""
    return author is not None and author in BOT_AUTHORS


def check(body: str, changed_files: list[str], issue_labels, author: str | None = None) -> list[str]:
    """Return a list of policy failures. Empty means the PR passes."""
    if is_exempt(author):
        return []

    failures: list[str] = []
    sections = split_sections(body)

    # ------------------------------------------------------------- exactly one Issue
    linked = sorted(set(CLOSES.findall(body)))
    if not linked:
        failures.append(
            'no linked Issue: the PR body must contain "Closes #NNN". '
            "Every PR closes exactly one Issue."
        )
    elif len(linked) > 1:
        failures.append(
            f"closes {len(linked)} Issues (#{', #'.join(linked)}); a PR must close exactly one. "
            "Split the work."
        )
    else:
        labels = issue_labels(linked[0])
        if labels is None:
            failures.append(f"Issue #{linked[0]} does not exist or is not visible")
        elif "state:needs-decision" in labels:
            failures.append(
                f"Issue #{linked[0]} is state:needs-decision and is not implementable. "
                "The open question must be resolved and recorded before implementation."
            )
        elif "state:blocked" in labels:
            failures.append(f"Issue #{linked[0]} is state:blocked")

    # ----------------------------------------------------------- required narrative
    for heading, hint in [
        ("Exact behavioral claim", "state what is true now that was not true before"),
        ("Scope", "state what changed and what nearby behaviour deliberately did not"),
        ("Known limitations", "state what remains unsupported; 'none' is a valid answer"),
        ("Agent provenance", "state which agent/model implemented and which verified"),
    ]:
        if is_effectively_empty(section(sections, heading)):
            failures.append(f'"{heading}" is empty -- {hint}')

    # ---------------------------------------------------------------- real evidence
    evidence = section(sections, "Tests and evidence")
    if is_effectively_empty(evidence):
        failures.append('"Tests and evidence" is empty -- paste the commands and results')
    else:
        prose = FENCE.sub("", evidence).strip()
        if not COMMAND_LINE.search(evidence):
            failures.append(
                '"Tests and evidence" names no command. "tests pass" is a claim, not '
                "evidence -- paste what you ran and what it printed."
            )
        elif any(BARE_CLAIM.match(line.strip()) for line in prose.splitlines() if line.strip()) \
                and not FENCE.search(evidence):
            failures.append('"Tests and evidence" asserts success without showing output')

    # ------------------------------------------------------------ rules conformance
    rules_files = [f for f in changed_files if f.startswith(RULES_PATHS)]
    if rules_files:
        conformance = section(sections, "Rules conformance")
        if is_effectively_empty(conformance):
            failures.append(
                f'"Rules conformance" is empty but {len(rules_files)} rules file(s) changed '
                f"({rules_files[0]}...). Cite the source locator and say what was verified."
            )
        elif is_na(conformance):
            failures.append(
                f'"Rules conformance" says N/A but {len(rules_files)} rules file(s) changed '
                f"({rules_files[0]}...). Rules changes require a source locator."
            )

    return failures


# ------------------------------------------------------------------------------- gh


def gh_json(args: list[str]):
    result = subprocess.run(
        ["gh", *args], capture_output=True, text=True, check=False, timeout=30
    )
    if result.returncode != 0:
        return None
    return json.loads(result.stdout)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="pr-policy.py")
    parser.add_argument("--pr", help="pull request number to check via gh")
    parser.add_argument("--body-file", help="read the PR body from this file instead")
    parser.add_argument("--changed-files", help="file listing changed paths, one per line")
    parser.add_argument(
        "--no-verify-issue", action="store_true", help="skip the GitHub Issue lookup"
    )
    parser.add_argument("--author", help="PR author login, when not reading from gh")
    parser.add_argument("--json", help="write a machine-readable evidence artifact here")
    args = parser.parse_args(argv)

    if args.pr:
        data = gh_json(["pr", "view", args.pr, "--json", "body,files,author"])
        if data is None:
            print(f"pr-policy: cannot read PR #{args.pr}", file=sys.stderr)
            return 2
        body = data.get("body") or ""
        changed = [f["path"] for f in data.get("files", [])]
        author = (data.get("author") or {}).get("login")
    elif args.body_file:
        body = Path(args.body_file).read_text(encoding="utf-8")
        changed = (
            [l.strip() for l in Path(args.changed_files).read_text(encoding="utf-8").splitlines() if l.strip()]
            if args.changed_files else []
        )
        author = args.author
    else:
        parser.error("one of --pr or --body-file is required")

    if args.no_verify_issue:
        def issue_labels(_number: str):
            return []
    else:
        def issue_labels(number: str):
            data = gh_json(["issue", "view", number, "--json", "labels"])
            if data is None:
                return None
            return [label["name"] for label in data.get("labels", [])]

    exempt = is_exempt(author)
    failures = check(body, changed, issue_labels, author)

    if args.json:
        Path(args.json).write_text(
            json.dumps(
                {
                    "pr": args.pr,
                    "author": author,
                    "exempt": exempt,
                    "passed": not failures,
                    "failureCount": len(failures),
                    "failures": failures,
                    "changedFiles": changed,
                    "rulesFilesChanged": [f for f in changed if f.startswith(RULES_PATHS)],
                },
                indent=2,
            ) + "\n",
            encoding="utf-8",
        )

    if failures:
        print(f"pr-policy: {len(failures)} failure(s)\n")
        for failure in failures:
            print(f"  - {failure}")
        print("\nSee .github/pull_request_template.md")
        return 1

    if exempt:
        print(f"pr-policy: PASS (author '{author}' is an allowlisted bot; "
              "narrative requirements waived, build-and-test still applies)")
    else:
        print("pr-policy: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
