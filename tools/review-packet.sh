#!/usr/bin/env bash
# review-packet.sh -- build a bounded review packet for one Issue's changes.
#
#   tools/review-packet.sh --issue 42 --branch issue-42-foo
#   tools/review-packet.sh --issue 42 --branch issue-42-foo --base origin/main --context 6
#   tools/review-packet.sh --issue 42 --branch issue-42-foo --output /tmp/packet-42.md
#
# docs/agent-team.md and .claude/skills/rules-review/SKILL.md say never to brief a
# verifier with "review this PR" -- hand it a bounded packet instead. A read-only
# reviewer (repo-steward, rules-conformance) has no Bash, so it cannot produce a diff
# for itself; this script is the one place that diff gets built, so every reviewer sees
# the same bytes assembled the same way regardless of which PR briefed them.
#
# Prints the packet to stdout by default -- the tool never touches the repository
# unless told to. Pass --output to write a file, which must resolve outside the
# repository (or to a path the repository already ignores): review packets are
# ephemeral, exactly like the source packets tools/source-slice.py produces, and must
# never be committed.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

ISSUE=""
BRANCH=""
BASE="origin/main"
CONTEXT=3
OUTPUT=""

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

usage() {
  sed -n '2,17p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --issue) ISSUE="${2:-}"; shift 2 ;;
    --branch) BRANCH="${2:-}"; shift 2 ;;
    --base) BASE="${2:-}"; shift 2 ;;
    --context) CONTEXT="${2:-}"; shift 2 ;;
    --output) OUTPUT="${2:-}"; shift 2 ;;
    -h|--help) usage 0 ;;
    *) die "unknown argument: $1" ;;
  esac
done

[[ -n "$ISSUE" ]] || die "--issue is required"
[[ "$ISSUE" =~ ^[0-9]+$ ]] || die "--issue must be numeric, got: $ISSUE"
[[ -n "$BRANCH" ]] || die "--branch is required -- the tip of the change (a branch name or a commit)"
[[ "$CONTEXT" =~ ^[0-9]+$ ]] || die "--context must be a non-negative integer, got: $CONTEXT"
command -v gh >/dev/null 2>&1 || die "gh is required"

# A packet written inside the repository, tracked or not, is one `git add -A` away from
# being committed -- the exact failure mode docs/source-handling.md exists to prevent
# for source packets. Refuse the unsafe case rather than trusting whoever calls this to
# remember; stdout (the default) never touches the repository at all.
if [[ -n "$OUTPUT" ]]; then
  mkdir -p "$(dirname "$OUTPUT")"
  abs_out="$(realpath -m "$OUTPUT")"
  case "$abs_out" in
    "$REPO_ROOT"/*)
      if ! git check-ignore -q "$abs_out"; then
        die "--output resolves inside the repository and is not gitignored: $abs_out
       Review packets are ephemeral and must never be committed (docs/source-handling.md
       treats source packets the same way). Write outside the repo -- e.g.
       --output /tmp/packet-$ISSUE.md -- or omit --output to print to stdout."
      fi
      ;;
  esac
fi

# ------------------------------------------------------------------------- the Issue

title="$(gh issue view "$ISSUE" --json title --jq .title 2>/dev/null)" \
  || die "Issue #$ISSUE does not exist or is not visible."
# A bare `var=$(failing-command)` under `set -e` exits the whole script on the spot with
# no diagnostic -- scripts/validate.sh documents this exact trap twice. The `||` makes
# this an OR-list, which set -e does not treat as a failure at this statement.
body="$(gh issue view "$ISSUE" --json body --jq .body 2>/dev/null)" \
  || die "could not fetch the body of Issue #$ISSUE"

section() {
  # sed drops leading blank lines only -- the template always has one right after the
  # "## Heading" line, and keeping it would put a stray gap between this packet's own
  # heading and the Issue's actual text on every section.
  awk -v h="^## $1"'$' '$0 ~ h {f=1; next} /^## /{f=0} f' <<<"$body" | sed -e '/./,$!d'
}

acceptance="$(section "Acceptance criteria")"
if [[ -z "$(printf '%s' "$acceptance" | tr -d '[:space:]')" ]]; then
  die "Issue #$ISSUE has no '## Acceptance criteria' section, or it is empty.
       A reviewer briefed without acceptance criteria has nothing to check the PR
       against. Fix the Issue before generating a packet for it."
fi

source_section="$(section "Source")"

# ------------------------------------------------------------------------------- refs

# A live PR's branch often exists only on origin. Try fetching before giving up, but
# never fail solely because the network is unavailable -- dispatch-agent.sh takes the
# same stance for the same reason.
resolve_ref() {
  local ref="$1"
  if git rev-parse --verify -q "${ref}^{commit}" >/dev/null 2>&1; then
    return 0
  fi
  if git remote get-url origin >/dev/null 2>&1; then
    git fetch -q origin "$ref" 2>/dev/null || true
  fi
  git rev-parse --verify -q "${ref}^{commit}" >/dev/null 2>&1
}

resolve_ref "$BRANCH" || die "branch/ref not found, even after fetching from origin: $BRANCH"
resolve_ref "$BASE" || die "base ref not found, even after fetching from origin: $BASE"

range="${BASE}...${BRANCH}"

# Three dots: diff against the merge base, not the two tips directly, so a base that
# has moved on since the branch was cut doesn't pollute the diff with unrelated
# history. This is the same computation `gh pr diff` and GitHub's own PR view use.
changed_files="$(git diff --name-status "$range")" || die "git diff --name-status failed for $range"
if [[ -z "$changed_files" ]]; then
  die "no diff between $BASE and $BRANCH -- nothing to review.
       Check that --branch points at the PR's tip and --base at where it diverged."
fi

diff_text="$(git diff -U"$CONTEXT" "$range")" || die "git diff failed for $range"

# ------------------------------------------------------------------- rules detection

# Whether rules-conformance and Codex are also expected on top of repo-steward -- see
# .claude/skills/rules-review/SKILL.md "Order the reviews cheapest first". A generic
# structural packet does not decide this by re-reading the Issue's labels: it looks at
# what actually changed, which is the fact the reviewer downstream will also see.
RULES_PATHS='^(src/Deckard\.(Rules|Data)/|tests/Deckard\.(Rules|Data)\.Tests/|\.github/source-manifest\.json$)'
rules_touch=0
# `git diff --name-status` puts a rename or a copy on ONE line: "R100<TAB>old<TAB>new"
# (copies are "C<score>" -- same two-path shape). `cut -f2-` keeps both paths but joined
# by the same tab, so the `^`-anchored regex above only ever tests the line's first
# path. A rename that moves a file INTO src/Deckard.Rules (old outside, new inside) was
# invisible here: only the "old" path anchored the line, and it isn't a rules path.
# Splitting every remaining tab onto its own line makes each path -- old and new, or the
# single path on an M/A/D line -- its own anchor-testable line.
if cut -f2- <<<"$changed_files" | tr '\t' '\n' | grep -qE "$RULES_PATHS"; then
  rules_touch=1
fi

gates="./scripts/validate.sh full            (canonical gate; must be green)
tools/repo-checks.py                   (bundled in validate.sh full)
pr-policy PR-template check            (.github workflow; runs on the opened PR)"
if [[ "$rules_touch" -eq 1 ]]; then
  gates="$gates
rules-conformance review               (adversarial, against the cited source packet)
Codex independent cross-vendor review  (dice/tests/Edge/initiative/damage/high-impact tables)"
fi

# ------------------------------------------------------------------------------ ADRs

adrs="$(for f in "$REPO_ROOT"/docs/decisions/*.md; do
  # Guards an unmatched glob (no docs/decisions/ directory, or an empty one), which
  # would otherwise hand this loop the literal pattern string as a nonexistent "file".
  [[ -f "$f" ]] || continue
  name="$(basename "$f")"
  [[ "$name" == "README.md" ]] && continue
  # grep exits non-zero when a file has no '# ' line; under pipefail that would take
  # down the whole script via the var=$(...) trap noted above, so fall back to "".
  heading="$(grep -m1 '^# ' "$f" | sed 's/^# *//')" || heading=""
  printf -- '- docs/decisions/%s -- %s\n' "$name" "${heading:-(no heading found)}"
done)"

# ---------------------------------------------------------------------------- packet

packet="$(cat <<EOF
================================================================================
DECKARD REVIEW PACKET -- ephemeral, never commit this file
================================================================================
issue      : #$ISSUE -- $title
branch     : $BRANCH
base       : $BASE (merge-base diff: $range)
diff context: -U$CONTEXT

Never brief a reviewer with "review this PR" -- this file is the packet instead. See
docs/agent-team.md ("Briefing agents") and .claude/skills/rules-review/SKILL.md.
================================================================================

## Acceptance criteria (Issue #$ISSUE)

$acceptance

## Source locator (Issue #$ISSUE, verbatim)

${source_section:-N/A}

Non-N/A? Fetch the actual excerpt separately with tools/source-slice.py -- this packet
never carries rulebook text. N/A means Issue #$ISSUE is not rules work.

## Changed files

$changed_files

## Diff ($range, -U$CONTEXT)

$diff_text

## Determinism risk prompt

Does this diff touch random consumption, ordering, replay, wall-clock time, or
environment/filesystem reads? Would it change how many random values a mechanic
consumes -- that shifts every subsequent result. Check literal patterns against
BANNED_IN_ENGINE in tools/repo-checks.py (also enforced mechanically over src/**/*.cs
by \`tools/repo-checks.py --only determinism\`, part of validate.sh full).

## Decisions already recorded (all ADRs -- judge relevance yourself)

$adrs

## Gates expected to pass

$gates

## Touches src/Deckard.Rules, src/Deckard.Data, their tests, or the source manifest?

$([[ "$rules_touch" -eq 1 ]] && echo "YES -- rules-conformance and Codex review apply, not just repo-steward." || echo "NO -- repo-steward's structural review is the whole review.")
EOF
)"

if [[ -n "$OUTPUT" ]]; then
  printf '%s\n' "$packet" > "$OUTPUT"
  printf 'wrote %s  (%d files changed)\n' "$OUTPUT" "$(wc -l <<<"$changed_files")" >&2
else
  printf '%s\n' "$packet"
fi
