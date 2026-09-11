#!/usr/bin/env bash
# rules-surface.sh -- the one definition of what counts as a Deckard "rules surface"
# change, shared by tools/review-packet.sh and tools/rules-conformance-gate.py (Issue
# #41). Before this file existed, review-packet.sh carried the only copy of this logic;
# a second, hand-rolled copy in the merge-gate script is exactly the kind of drift
# CLAUDE.md's "one definition" principle exists to prevent, so the gate invokes this
# file as a subprocess instead of reimplementing it.
#
# `git diff --name-status` puts a rename or copy's destination on the SAME line as its
# source: "R100<TAB>old/path<TAB>new/path" (copies are "C<score>", same two-path shape).
# A regex anchored with `^` and tested against that whole line only ever sees the
# leading status code, never a path -- a rename that moved a file INTO src/Deckard.Rules
# was invisible under the naive version of this check. `rules_surface_touched` strips
# the status column and puts every remaining tab-separated field on its own line before
# matching, so a rename in EITHER direction is visible to the anchored regex.
#
# No `set -euo pipefail` at file scope: this file is normally `source`d, and a sourced
# file's `set` calls change the CALLING shell's options too. Only the standalone-execution
# branch at the bottom opts into strict mode, for itself alone.

RULES_SURFACE_PATHS_REGEX='^(src/Deckard\.(Rules|Data)/|tests/Deckard\.(Rules|Data)\.Tests/|\.github/source-manifest\.json$)'

# rules_surface_touched <name-status-text>
# $1: the full output of `git diff --name-status <range>`. Returns success (0, "yes, a
# rules surface changed") or failure (1, "no") as its exit code -- the same convention
# `grep -q` uses, so callers can write `if rules_surface_touched "$changed_files"; then`.
rules_surface_touched() {
  cut -f2- <<<"$1" | tr '\t' '\n' | grep -qE "$RULES_SURFACE_PATHS_REGEX"
}

# Standalone use, for a non-bash caller (tools/rules-conformance-gate.py) that wants this
# exact logic without duplicating it: reads `git diff --name-status` text from stdin,
# exits 0 when a rules surface changed, 1 when it did not. Only runs when this file is
# executed directly, not when it is `source`d (bash sets $0 to the sourcing script's own
# path in that case, which never equals ${BASH_SOURCE[0]} here).
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  set -euo pipefail
  input="$(cat)"
  rules_surface_touched "$input"
fi
