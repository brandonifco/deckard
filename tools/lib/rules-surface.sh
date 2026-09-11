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
# Issue #86: a directory match alone is not enough. NuGet's RestorePackagesWithLockFile
# requires a `packages.lock.json` to sit beside the project file it locks, so one lands
# under src/Deckard.Rules/ etc. even though it carries no rules content and can never
# have a printed-page citation. RULES_SURFACE_EXCLUDED_BASENAMES below is the narrow,
# explicit fix: a path is a rules surface only when it matches the directory regex AND
# its exact basename is not on that list. Basename-only, deliberately -- a pattern
# (e.g. "*.json") would also swallow .github/source-manifest.json, which is deliberately
# part of the rules surface and must stay one. Add an entry only when the file actually
# exists in the tree, each with its own one-line reason.
#
# No `set -euo pipefail` at file scope: this file is normally `source`d, and a sourced
# file's `set` calls change the CALLING shell's options too. Only the standalone-execution
# branch at the bottom opts into strict mode, for itself alone.

RULES_SURFACE_PATHS_REGEX='^(src/Deckard\.(Rules|Data)/|tests/Deckard\.(Rules|Data)\.Tests/|\.github/source-manifest\.json$)'

RULES_SURFACE_EXCLUDED_BASENAMES=(
  # NuGet's RestorePackagesWithLockFile (Issue #43) writes one packages.lock.json beside
  # each project file it locks, landing it inside src/Deckard.Rules/, src/Deckard.Data/
  # and their test projects. It records transitive package hashes, not rules content,
  # and cannot carry a printed-page citation (Issue #86).
  "packages.lock.json"
)

# rules_surface_excluded_basename <basename>
# True (exit 0) when <basename> -- a bare file name, no directory component -- is on the
# exclusion list above.
rules_surface_excluded_basename() {
  local base="$1" excluded
  for excluded in "${RULES_SURFACE_EXCLUDED_BASENAMES[@]}"; do
    if [[ "$base" == "$excluded" ]]; then
      return 0
    fi
  done
  return 1
}

# rules_surface_path_matches <path>
# True (exit 0) when <path> is inside a rules-surface directory (or is the pinned source
# manifest) AND its exact basename is not excluded.
rules_surface_path_matches() {
  local path="$1"
  [[ "$path" =~ $RULES_SURFACE_PATHS_REGEX ]] || return 1
  ! rules_surface_excluded_basename "${path##*/}"
}

# rules_surface_touched <name-status-text>
# $1: the full output of `git diff --name-status <range>`. Returns success (0, "yes, a
# rules surface changed") or failure (1, "no") as its exit code -- the same convention
# `grep -q` uses, so callers can write `if rules_surface_touched "$changed_files"; then`.
rules_surface_touched() {
  local path
  while IFS= read -r path; do
    [[ -z "$path" ]] && continue
    if rules_surface_path_matches "$path"; then
      return 0
    fi
  done < <(cut -f2- <<<"$1" | tr '\t' '\n')
  return 1
}

# Standalone use, for a non-bash caller (tools/rules-conformance-gate.py, tools/pr-policy.py)
# that wants this exact logic without duplicating it: reads `git diff --name-status` text
# from stdin, exits 0 when a rules surface changed, 1 when it did not. `--print-excluded-
# basenames` instead prints the exclusion list above, one name per line, for a caller that
# needs the list itself rather than a single yes/no verdict.
#
# `--classify` (Issue #89) is for a caller that holds a flat list of plain paths -- no
# status column, no rename pairs -- such as `gh pr view --json files`, and wants to know
# WHICH of them are a rules surface rather than re-implementing rules_surface_path_matches
# itself. It reads one path per line from stdin and echoes back only the ones that match,
# one per line, in the same order, in a single pass -- so a caller with a large diff
# classifies it with one subprocess call instead of one per path. This is the shape
# tools/pr-policy.py uses: asking "which of these are a rules surface?" instead of
# fetching the directory list and matching against it locally, which is how pr-policy.py's
# own copy of that list drifted from this file in the first place.
#
# Only runs when this file is executed directly, not when it is `source`d (bash sets $0
# to the sourcing script's own path in that case, which never equals ${BASH_SOURCE[0]}
# here).
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  set -euo pipefail
  if [[ "${1:-}" == "--print-excluded-basenames" ]]; then
    printf '%s\n' "${RULES_SURFACE_EXCLUDED_BASENAMES[@]}"
    exit 0
  fi
  if [[ "${1:-}" == "--classify" ]]; then
    while IFS= read -r path; do
      [[ -z "$path" ]] && continue
      if rules_surface_path_matches "$path"; then
        printf '%s\n' "$path"
      fi
    done
    exit 0
  fi
  input="$(cat)"
  rules_surface_touched "$input"
fi
