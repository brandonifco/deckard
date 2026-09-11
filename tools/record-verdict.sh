#!/usr/bin/env bash
# record-verdict.sh -- record a rules-conformance verdict against one commit.
#
#   tools/record-verdict.sh --sha <head-sha> --reviewer rules-conformance \
#     --verdict pass --packet-sha256 <bodySha256 hex> --pages 44-47
#   tools/record-verdict.sh --sha <head-sha> --reviewer codex \
#     --verdict fail --packet-sha256 <hex> --pages 44-47 --notes "row 9 disagrees"
#   tools/record-verdict.sh --sha <head-sha> --reviewer rules-conformance \
#     --verdict pass --packet-sha256 <hex> --pages 44-47 --pr 108 --rerun-gate
#
# Issue #41: a rules-conformance review that lives only in an agent's chat transcript is
# a claim, not a merge gate. This is the ONE place a verdict becomes a fact GitHub can
# check: a commit status, posted directly against the head SHA it was verified against.
# tools/rules-conformance-gate.py reads these back (as
# repos/{owner}/{repo}/commits/{sha}/status) when it decides whether a PR touching a
# rules surface may merge.
#
# A commit status is intrinsically tied to one SHA -- there is no "amend the PR and keep
# the verdict" failure mode to guard against separately, because amending produces a new
# SHA that simply has no status of its own yet.
#
# `--reviewer rules-conformance` is the in-house agent's verdict; `--reviewer codex` is
# the independent cross-vendor verdict AGENTS.md and docs/agent-team.md require on top of
# it for an Issue labelled risk:rules-conformance. This script does not and cannot verify
# WHICH model actually produced a verdict -- that a human or agent invoking this with
# `--reviewer codex` actually ran Codex, not the same in-house model under a different
# flag, is a process obligation this file records but does not enforce. Say so plainly
# rather than implying otherwise (docs/agent-team.md, and Issue #12's standard).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

SHA=""
REVIEWER=""
VERDICT=""
PACKET_SHA256=""
PAGES=""
NOTES=""
REPO=""
PR=""
RERUN_GATE=0

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

usage() {
  sed -n '2,20p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sha) SHA="${2:-}"; shift 2 ;;
    --reviewer) REVIEWER="${2:-}"; shift 2 ;;
    --verdict) VERDICT="${2:-}"; shift 2 ;;
    --packet-sha256) PACKET_SHA256="${2:-}"; shift 2 ;;
    --pages) PAGES="${2:-}"; shift 2 ;;
    --notes) NOTES="${2:-}"; shift 2 ;;
    --repo) REPO="${2:-}"; shift 2 ;;
    --pr) PR="${2:-}"; shift 2 ;;
    --rerun-gate) RERUN_GATE=1; shift ;;
    -h|--help) usage 0 ;;
    *) die "unknown argument: $1" ;;
  esac
done

command -v gh >/dev/null 2>&1 || die "gh is required"

[[ -n "$SHA" ]] || die "--sha is required -- the exact head commit this verdict covers"
[[ "$SHA" =~ ^[0-9a-fA-F]{7,40}$ ]] || die "--sha does not look like a commit SHA: $SHA"

case "$REVIEWER" in
  rules-conformance|codex) ;;
  "") die "--reviewer is required: rules-conformance (in-house) or codex (independent)" ;;
  *) die "--reviewer must be 'rules-conformance' or 'codex', got: $REVIEWER" ;;
esac
CONTEXT="deckard-verdict/$REVIEWER"

case "$VERDICT" in
  pass) STATE="success"; LABEL="PASS" ;;
  fail) STATE="failure"; LABEL="FAIL" ;;
  "") die "--verdict is required: pass or fail" ;;
  *) die "--verdict must be 'pass' or 'fail', got: $VERDICT" ;;
esac

[[ -n "$PACKET_SHA256" ]] || die "--packet-sha256 is required -- the source-slice.py bodySha256 this verdict was checked against"
[[ "$PACKET_SHA256" =~ ^[0-9a-fA-F]{64}$ ]] || die "--packet-sha256 must be 64 hex characters (source-slice.py's bodySha256), got: $PACKET_SHA256"
[[ -n "$PAGES" ]] || die "--pages is required -- the page range cited, e.g. 44-47"
[[ "$RERUN_GATE" -eq 0 || -n "$PR" ]] || die "--rerun-gate requires --pr"

DESCRIPTION="$LABEL bodySha256=${PACKET_SHA256,,} pages=$PAGES"
if [[ -n "$NOTES" ]]; then
  DESCRIPTION="$DESCRIPTION -- $NOTES"
fi
# The GitHub Statuses API truncates (or may reject) a description over 140 characters.
# Fail loudly here instead of silently posting a verdict whose notes were cut off mid
# sentence -- notes are the one field length here that is not fixed by this script.
if [[ "${#DESCRIPTION}" -gt 140 ]]; then
  die "verdict description is ${#DESCRIPTION} chars (>140); shorten --notes.
       Full text: $DESCRIPTION"
fi

if [[ -z "$REPO" ]]; then
  REPO="$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null)" \
    || die "could not resolve owner/repo; pass --repo owner/repo"
fi

printf 'Recording %s verdict for %s at %s...\n' "$REVIEWER" "$SHA" "$CONTEXT" >&2

gh api --method POST "repos/$REPO/statuses/$SHA" \
  -f "state=$STATE" \
  -f "context=$CONTEXT" \
  -f "description=$DESCRIPTION" \
  >/dev/null

printf '%s verdict recorded: %s\n' "$CONTEXT" "$DESCRIPTION"

# The Actions run that produced the FIRST (necessarily failing, since no verdict existed
# yet) evaluation of rules-conformance-gate for this SHA does not re-run itself just
# because a status was posted afterward -- GitHub does not re-trigger a `pull_request`
# workflow on a plain commit-status event. `workflow_dispatch` against the PR's branch
# re-evaluates the gate for whatever commit is currently at the tip of that branch; when
# that tip is still this SHA (the usual case -- nothing was pushed since), the resulting
# check run lands on exactly the commit this verdict was just recorded for.
if [[ "$RERUN_GATE" -eq 1 ]]; then
  branch="$(gh pr view "$PR" --json headRefName --jq .headRefName)" \
    || die "could not resolve the branch for PR #$PR"
  gh workflow run rules-conformance-gate.yml --repo "$REPO" --ref "$branch" -f "pr=$PR" \
    || die "could not dispatch a re-run of rules-conformance-gate for PR #$PR"
  printf 'Requested a rules-conformance-gate re-run for PR #%s (%s).\n' "$PR" "$branch"
fi
