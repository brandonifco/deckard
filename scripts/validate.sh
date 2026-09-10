#!/usr/bin/env bash
# validate.sh -- the single canonical gate for Deckard.
#
# Humans call it. Claude calls it. Subagents call it. CI calls it. There is exactly one
# definition of "this change is acceptable", and it lives here rather than being
# reimplemented in a workflow file where the two would silently drift apart.
#
#   ./scripts/validate.sh full      merge-equivalent gate (default)
#   ./scripts/validate.sh fast      Debug only; for the inner development loop
#   ./scripts/validate.sh sdk-pin   just prove the SDK pin resolves
#
# `full` must not require network access beyond ordinary NuGet restore, and must not
# require the authoritative rulebook: the tooling tests are hermetic on purpose so that
# CI can prove the source boundary without ever possessing a copy of the book.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

MODE="${1:-full}"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

SOLUTION="Deckard.slnx"
FAILED=0
STEP=0

if [[ -t 1 ]]; then BOLD=$'\033[1m'; RED=$'\033[31m'; GREEN=$'\033[32m'; OFF=$'\033[0m'
else BOLD=""; RED=""; GREEN=""; OFF=""; fi

step() {
  STEP=$((STEP + 1))
  printf '\n%s==> [%d] %s%s\n' "$BOLD" "$STEP" "$1" "$OFF"
}

fail() {
  printf '%sFAIL%s %s\n' "$RED" "$OFF" "$1"
  FAILED=1
}

run() {
  local label="$1"; shift
  if "$@"; then
    printf '%sok%s   %s\n' "$GREEN" "$OFF" "$label"
  else
    fail "$label"
  fi
}

# --------------------------------------------------------------------- sdk pin
verify_sdk_pin() {
  step "SDK pin"
  local pinned actual
  pinned="$(python3 -c 'import json;print(json.load(open("global.json"))["sdk"]["version"])')"
  actual="$(dotnet --version)"
  if [[ "$pinned" != "$actual" ]]; then
    fail "SDK pin: global.json requires $pinned but 'dotnet --version' reports $actual"
    echo "     Deckard pins an exact SDK patch with rollForward=disable so that a" >&2
    echo "     different compiler can never silently change build output." >&2
    return 1
  fi
  printf '%sok%s   SDK %s (rollForward=disable)\n' "$GREEN" "$OFF" "$actual"
}

if [[ "$MODE" == "sdk-pin" ]]; then
  verify_sdk_pin
  exit $FAILED
fi

verify_sdk_pin || exit 1

# ------------------------------------------------------------------- restore
step "Restore"
run "dotnet restore" dotnet restore "$SOLUTION"

# -------------------------------------------------------------------- format
step "Format"
run "dotnet format --verify-no-changes" \
    dotnet format "$SOLUTION" --verify-no-changes --no-restore

# ------------------------------------------------------- build + test (Debug)
step "Build + test (Debug)"
run "build Debug (0 warnings)" \
    dotnet build "$SOLUTION" -c Debug --no-restore -warnaserror
run "test Debug" \
    dotnet test "$SOLUTION" -c Debug --no-build --nologo

if [[ "$MODE" != "fast" ]]; then
  # ----------------------------------------------------- build + test (Release)
  # Release is not ceremonial here: different optimisation settings have historically
  # been where "works on my machine" determinism bugs surface.
  step "Build + test (Release)"
  run "build Release (0 warnings)" \
      dotnet build "$SOLUTION" -c Release --no-restore -warnaserror
  run "test Release" \
      dotnet test "$SOLUTION" -c Release --no-build --nologo
fi

# ------------------------------------------------------------- repo invariants
step "Repository invariants"
run "repo-checks" tools/repo-checks.py

# ------------------------------------------------------------- tooling tests
step "Tooling tests"
run "python tooling tests" python3 -m unittest discover -s tools/tests -q

# ------------------------------------------------------------ whitespace
step "Whitespace"
run "git diff --check" git diff --check
run "git diff --cached --check" git diff --cached --check

# ------------------------------------------------------------------- verdict
echo
if [[ "$FAILED" -eq 0 ]]; then
  printf '%s%svalidate.sh %s: PASS%s\n' "$BOLD" "$GREEN" "$MODE" "$OFF"
else
  printf '%s%svalidate.sh %s: FAIL%s\n' "$BOLD" "$RED" "$MODE" "$OFF"
fi
exit "$FAILED"
