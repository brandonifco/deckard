#!/usr/bin/env python3
"""rules-conformance-gate -- the merge gate that makes rules-conformance review a fact
about GitHub, not a claim a PR body makes about itself.

Issue #41: `pr-policy` already requires a "Rules conformance" section in the PR body
whenever a rules surface changed, but that section is prose -- typing a page number into
a text box does not demonstrate that an adversarial review against the source packet
actually ran, let alone that it passed. This script is the one place that demonstration
is checked mechanically, for the commit GitHub is about to merge.

    tools/rules-conformance-gate.py --pr 108
    tools/rules-conformance-gate.py --pr 108 --json gate-evidence.json
    tools/rules-conformance-gate.py --changed-files files.txt --labels risk:rules-conformance \
                                     --statuses-file statuses.json

GitHub's ruleset cannot require a status check conditionally on which paths a PR
touches: a required check applies to every PR on the branch, or none. So this check
always runs, and decides for itself whether it has anything to check:

  * the PR's diff touches no rules surface (src/Deckard.Rules/, src/Deckard.Data/,
    their test projects, .github/source-manifest.json) -- PASS, trivially. Docs and
    tooling work is never blocked by a review with nothing to review.
  * it touches one -- PASS only if a verdict is recorded for THIS EXACT HEAD COMMIT.
    Verdicts are GitHub commit statuses (see tools/record-verdict.sh), which are
    intrinsically tied to one SHA: amending the PR produces a new SHA with no status of
    its own, so an old verdict cannot silently keep covering new code.

The "did a rules surface change" question is not re-decided here: it is delegated to
tools/lib/rules-surface.sh, the same file tools/review-packet.sh uses, so a reviewer's
packet and this gate can never disagree about what counts as rules work.

For an Issue labelled risk:rules-conformance, a second, independent verdict is required
too (docs/agent-team.md, AGENTS.md "Codex: independent rules conformance") -- the
in-house agent's verdict alone is not enough for the mechanics this label exists to flag.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RULES_SURFACE_LIB = ROOT / "tools" / "lib" / "rules-surface.sh"

# Every verdict context this gate knows how to require. The reviewer name IS the context
# suffix -- tools/record-verdict.sh posts to exactly one of these.
IN_HOUSE_CONTEXT = "deckard-verdict/rules-conformance"
INDEPENDENT_CONTEXT = "deckard-verdict/codex"
RISK_LABEL = "risk:rules-conformance"

# A verdict's `description` (the GitHub Statuses API field, <=140 chars) must name the
# packet it was checked against: the source-slice bodySha256 (tools/source-slice.py,
# Issue #40) and the page range cited. This does not -- cannot -- re-verify the hash
# against a freshly extracted packet: the rulebook never enters CI (CLAUDE.md, "The book
# never enters the repository"), so there is nothing here to compare it to. What this
# proves is accountability -- a specific packet was named -- not cryptographic truth;
# the truth of the review itself is what rules-conformance and Codex exist to check.
VERDICT_RE = re.compile(
    r"^(PASS|FAIL)\b.*\bbodySha256=([0-9a-fA-F]{64})\b.*\bpages=(\S+)", re.DOTALL
)

# Reused rather than reimplemented: pr-policy.py already owns the "which Issue does this
# PR close" regex, and a second copy is exactly the kind of drift Issue #41's own source
# text warns against ("reuse its logic rather than writing a second copy that can drift").
_pr_policy_spec = importlib.util.spec_from_file_location(
    "deckard_pr_policy", Path(__file__).resolve().parent / "pr-policy.py"
)
assert _pr_policy_spec and _pr_policy_spec.loader
_pr_policy = importlib.util.module_from_spec(_pr_policy_spec)
_pr_policy_spec.loader.exec_module(_pr_policy)
CLOSES = _pr_policy.CLOSES


@dataclass
class GateResult:
    passed: bool
    applies: bool
    reasons: list[str] = field(default_factory=list)


def rules_surface_touched(changed_files_name_status: str) -> bool:
    """Delegates to tools/lib/rules-surface.sh -- the one definition, shared with
    tools/review-packet.sh. See that file for why a naive regex over `git diff
    --name-status` text misses a rename."""
    result = subprocess.run(
        ["bash", str(RULES_SURFACE_LIB)],
        input=changed_files_name_status,
        capture_output=True,
        text=True,
        timeout=10,
    )
    return result.returncode == 0


def required_contexts(labels: list[str] | None) -> tuple[list[str], list[str]]:
    """Returns (required_contexts, notes). `labels=None` means the linked Issue's labels
    could not be determined -- fail SAFE by requiring the independent verdict too, rather
    than silently accepting only the in-house one because a lookup happened to fail."""
    notes: list[str] = []
    contexts = [IN_HOUSE_CONTEXT]
    if labels is None:
        contexts.append(INDEPENDENT_CONTEXT)
        notes.append(
            "could not determine the linked Issue's labels; requiring the independent "
            f"verdict ({INDEPENDENT_CONTEXT}) as a precaution"
        )
    elif RISK_LABEL in labels:
        contexts.append(INDEPENDENT_CONTEXT)
        notes.append(f"linked Issue is labelled {RISK_LABEL}: independent verdict required too")
    return contexts, notes


def evaluate(
    changed_files_name_status: str,
    labels: list[str] | None,
    statuses: list[dict],
) -> GateResult:
    """Pure decision logic -- no gh, no git, no network. `statuses` is the combined-status
    list for the head commit: [{"context": ..., "state": ..., "description": ...}, ...],
    the shape `gh api repos/{owner}/{repo}/commits/{sha}/status --jq .statuses` returns."""
    if not rules_surface_touched(changed_files_name_status):
        return GateResult(
            passed=True,
            applies=False,
            reasons=["no rules-surface file changed for this head commit; gate passes trivially"],
        )

    contexts, notes = required_contexts(labels)
    by_context = {s.get("context"): s for s in statuses}
    reasons: list[str] = list(notes)
    failures: list[str] = []

    for ctx in contexts:
        status = by_context.get(ctx)
        if status is None:
            failures.append(f"no verdict recorded at '{ctx}' for this head commit")
            continue
        state = status.get("state")
        if state != "success":
            failures.append(f"'{ctx}' verdict for this head commit is '{state}', not success")
            continue
        description = status.get("description") or ""
        if not VERDICT_RE.search(description):
            failures.append(
                f"'{ctx}' verdict does not name a packet bodySha256 and page range "
                f"(description: {description!r})"
            )
            continue
        reasons.append(f"'{ctx}': verified ({description})")

    if failures:
        reasons = failures + reasons
        return GateResult(passed=False, applies=True, reasons=reasons)
    return GateResult(passed=True, applies=True, reasons=reasons or ["all required verdicts recorded"])


# ------------------------------------------------------------------------------- gh/git


def gh_json(args: list[str]):
    result = subprocess.run(
        ["gh", *args], capture_output=True, text=True, check=False, timeout=30
    )
    if result.returncode != 0:
        return None
    return json.loads(result.stdout)


def fetch_pr_refs(pr: str) -> tuple[str, str] | None:
    """Returns (base_ref_name, head_sha). `gh pr view` has no `baseRefOid` field -- only
    `baseRefName` (a branch name whose tip moves) and `headRefOid` (the PR's own SHA, and
    the one commit statuses -- and this gate -- are actually keyed to). `resolve_ref`
    below turns the branch name into something `git diff` can use, exactly as
    tools/review-packet.sh resolves its own `--base origin/main` default."""
    data = gh_json(["pr", "view", pr, "--json", "baseRefName,headRefOid"])
    if data is None:
        return None
    return data.get("baseRefName"), data.get("headRefOid")


def resolve_ref(ref: str) -> str:
    """Best-effort: return something `git diff` can use for `ref`, fetching from origin
    when it is not already present locally. `ref` may already be a full SHA (verified
    directly) or a branch name (tried plain, then as `origin/<ref>`)."""

    def verified(candidate: str) -> bool:
        result = subprocess.run(
            ["git", "rev-parse", "--verify", "-q", f"{candidate}^{{commit}}"],
            cwd=ROOT, capture_output=True, text=True,
        )
        return result.returncode == 0

    if verified(ref):
        return ref
    remote_candidate = f"origin/{ref}"
    if verified(remote_candidate):
        return remote_candidate
    subprocess.run(["git", "fetch", "-q", "origin", ref], cwd=ROOT, capture_output=True, text=True)
    if verified(ref):
        return ref
    if verified(remote_candidate):
        return remote_candidate
    return ref  # let git's own diff error surface the real problem


def fetch_pr_body(pr: str) -> str | None:
    data = gh_json(["pr", "view", pr, "--json", "body"])
    if data is None:
        return None
    return data.get("body") or ""


def fetch_issue_labels(issue: str) -> list[str] | None:
    data = gh_json(["issue", "view", issue, "--json", "labels"])
    if data is None:
        return None
    return [label["name"] for label in data.get("labels", [])]


def fetch_statuses(repo: str, sha: str) -> list[dict] | None:
    data = gh_json(["api", f"repos/{repo}/commits/{sha}/status"])
    if data is None:
        return None
    return data.get("statuses", [])


def git_diff_name_status(base_sha: str, head_sha: str) -> str:
    result = subprocess.run(
        ["git", "diff", "--name-status", f"{base_sha}...{head_sha}"],
        cwd=ROOT, capture_output=True, text=True, check=False,
    )
    if result.returncode != 0:
        raise RuntimeError(f"git diff --name-status failed: {result.stderr.strip()}")
    return result.stdout


def resolve_labels_for_pr(pr: str) -> list[str] | None:
    body = fetch_pr_body(pr)
    if body is None:
        return None
    linked = sorted(set(CLOSES.findall(body)))
    if len(linked) != 1:
        # pr-policy.py already fails a PR that closes zero or several Issues; this gate
        # does not re-litigate that. It only needs to know whether to require the
        # independent verdict, and an unresolved link means it cannot know -- fail safe.
        return None
    return fetch_issue_labels(linked[0])


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="rules-conformance-gate.py")
    parser.add_argument("--pr", help="pull request number to check via gh")
    parser.add_argument("--repo", help="owner/repo, for --statuses-file-less lookups via gh")
    parser.add_argument("--base-sha", help="base commit, for a local git diff")
    parser.add_argument("--head-sha", help="head commit, for a local git diff and status lookup")
    parser.add_argument("--changed-files", help="file with `git diff --name-status` text (offline)")
    parser.add_argument("--labels", help="comma-separated Issue labels (offline, skips gh issue view)")
    parser.add_argument("--statuses-file", help="JSON file of statuses (offline, skips gh api)")
    parser.add_argument("--json", help="write a machine-readable evidence artifact here")
    args = parser.parse_args(argv)

    head_sha = args.head_sha

    if args.changed_files:
        changed_files = Path(args.changed_files).read_text(encoding="utf-8")
    else:
        if not (args.base_sha and head_sha):
            if not args.pr:
                parser.error("one of --changed-files or (--pr | --base-sha and --head-sha) is required")
            refs = fetch_pr_refs(args.pr)
            if refs is None:
                print(f"rules-conformance-gate: cannot read PR #{args.pr}", file=sys.stderr)
                return 2
            base_ref, head_sha = refs
            base_sha = resolve_ref(base_ref)
        else:
            base_sha = args.base_sha
        changed_files = git_diff_name_status(base_sha, head_sha)

    # Most PRs touch no rules surface at all -- the common case this gate exists to
    # leave alone. Deciding that up front, with the exact same check evaluate() would
    # apply anyway, means an ordinary docs/tooling PR costs this script zero `gh issue
    # view` or `gh api .../status` calls rather than a trivially-discarded round trip.
    labels: list[str] | None
    statuses: list[dict]
    if not rules_surface_touched(changed_files):
        labels, statuses = [], []
    else:
        if args.labels is not None:
            labels = [l.strip() for l in args.labels.split(",") if l.strip()]
        elif args.pr:
            labels = resolve_labels_for_pr(args.pr)
        else:
            labels = None

        if args.statuses_file:
            statuses = json.loads(Path(args.statuses_file).read_text(encoding="utf-8"))
        else:
            if not head_sha:
                parser.error("--head-sha (or --pr) is required to look up recorded verdicts")
            repo = args.repo
            if not repo:
                repo_data = gh_json(["repo", "view", "--json", "nameWithOwner"])
                repo = (repo_data or {}).get("nameWithOwner")
            if not repo:
                print("rules-conformance-gate: cannot resolve owner/repo; pass --repo", file=sys.stderr)
                return 2
            statuses = fetch_statuses(repo, head_sha)
            if statuses is None:
                print(f"rules-conformance-gate: cannot read statuses for {head_sha}", file=sys.stderr)
                return 2

    result = evaluate(changed_files, labels, statuses)

    if args.json:
        Path(args.json).write_text(
            json.dumps(
                {
                    "pr": args.pr,
                    "headSha": head_sha,
                    "applies": result.applies,
                    "passed": result.passed,
                    "reasons": result.reasons,
                },
                indent=2,
            ) + "\n",
            encoding="utf-8",
        )

    label = "rules-conformance-gate"
    if result.passed:
        print(f"{label}: PASS")
    else:
        print(f"{label}: FAIL")
    for reason in result.reasons:
        print(f"  - {reason}")

    return 0 if result.passed else 1


if __name__ == "__main__":
    raise SystemExit(main())
