# 0010 — Independent verdict: an ordered vendor fallback chain, not one vendor

## Status

Accepted — 2026-09-11. Decided by Brandon in the decision comment on Issue #83
("Decision recorded — Brandon, 2026-09-11"). This ADR transcribes that decision; it
does not extend or reinterpret it.

## Decision

The independent verdict `rules-conformance-gate` requires for a `risk:rules-conformance`
Issue is no longer pinned to one vendor. It is produced by an **ordered fallback chain**:

1. **Codex** — the default. Try it first, always.
2. **Gemini** — used when Codex is unavailable.
3. **In-house** (`rules-conformance`, run a second, independent time) — used only when
   neither Codex nor Gemini is available.

Each link in the chain posts a commit status at its own named context —
`deckard-verdict/codex`, `deckard-verdict/gemini`, `deckard-verdict/in-house-independent`
— rather than a single shared `deckard-verdict/independent`. `tools/rules-conformance-gate.py`
treats these three contexts as a set: the gate passes when the always-required in-house
verdict (`deckard-verdict/rules-conformance`) **and** any ONE of the three are present and
passing for the exact head commit.

**The chain advances on a vendor being unavailable, never on disagreement, and a
recorded verdict from any vendor is binding.** Brandon's decision was explicit about the
trigger: "normally, default to codex. otherwise try gemini first, if they are not
available, test in-house" — the condition for moving down the chain is that a vendor
could not be reached, not that it reviewed and disagreed. A vendor that returned a
verdict was available. `docs/agent-team.md` already states the rule for what happens
when independent reviewers disagree: "Where they disagree, the source packet decides —
not seniority, not the model, not the implementer's explanation." A gate that let a later
context's pass overwrite an earlier context's recorded fail would decide that
disagreement by retry order instead — exactly what that sentence forbids. So a fail
recorded at *any* of the three independent contexts blocks the gate outright, even when a
different context in the chain later passes; only the absence of a verdict at a context
(that vendor was never invoked) may be skipped over when checking whether the chain is
satisfied.

**The in-house fallback is a real weakening of the guarantee, not an equivalent option.**
The entire stated reason a second verdict exists (`AGENTS.md`) is that a reviewer trained
the same way as the implementer tends to reproduce the implementer's misreading —
cross-vendor independence is the property being bought. `in-house-independent` does not
have that property: it is the same vendor family reviewing itself a second time. Brandon
accepted this cost deliberately, in exchange for not being blocked when neither Codex nor
Gemini is reachable, on the explicit condition that the recorded verdict names which path
was actually taken, so the trade is visible on a merged commit rather than buried in a
transcript.

## Problem

`tools/rules-conformance-gate.py` required exactly one context,
`deckard-verdict/codex`, for the independent half of a `risk:rules-conformance` verdict,
and `tools/record-verdict.sh` accepted only `rules-conformance` or `codex` as
`--reviewer`. While reviewing #61, Codex returned an account usage limit not resetting
until 2026-09-15. The in-house verdict was recorded; the independent one could not be,
and #61 — already correct — could not merge regardless. The only ways forward were
waiting four days, or recording a Codex verdict Codex did not produce, which is precisely
the dishonesty Issue #41 built this gate to prevent. A single hard-coded vendor is a
single point of failure for every future rules PR, not just #61.

## Options considered

1. **Keep one hard-coded vendor (Codex) and wait out rate limits when it is
   unavailable.** Rejected: it blocks all rules work on one vendor's availability, with
   no bound other than that vendor's own schedule.
2. **Widen `INDEPENDENT_CONTEXT` to a single generic `deckard-verdict/independent`
   context, accepting a verdict from any reviewer under that one name.** Rejected: it
   would make a genuine cross-vendor review mechanically indistinguishable from a
   same-vendor fallback on the merged commit — exactly the kind of fact a reader should
   not have to excavate a transcript to recover.
3. **An ordered fallback chain — Codex, then Gemini, then in-house — with each vendor
   posting to its own named context.** Chosen. Codex remains the default so cross-vendor
   independence is the common case, Gemini gives a second genuinely independent option
   before falling back to the same-vendor case, and the named contexts keep the fallback
   visible rather than hidden.
4. **Automatic vendor selection or dispatch logic inside `tools/record-verdict.sh`.**
   Rejected — Issue #83 non-goal. The script validates and records a verdict; it does not
   decide which reviewer should run. That decision, and the fact of which one actually
   ran, stays with the human or orchestrating agent invoking it.

## Chosen design

- `tools/rules-conformance-gate.py`: `IN_HOUSE_CONTEXT` is unchanged and always required.
  `INDEPENDENT_CONTEXT` (a single string) becomes `INDEPENDENT_CONTEXTS` (a 3-tuple of
  `deckard-verdict/codex`, `deckard-verdict/gemini`, `deckard-verdict/in-house-independent`).
  `evaluate()` requires the in-house context AND at least one of `INDEPENDENT_CONTEXTS` to
  be present with `state == "success"` and a well-formed description. This is not a plain
  OR across the tuple: `_check_verdict()` distinguishes a context with no status recorded
  at all (skippable) from one with a status recorded that is not a passing verdict
  (binding), and `evaluate()` fails the gate if *any* independent context is recorded as a
  fail, regardless of whether a different context in the chain passed. An earlier revision
  of this PR collapsed both cases into one boolean, which let a recorded Codex fail be
  cleared by recording Gemini as a pass afterward — caught in review before merge; the
  test suite now covers it directly (`test_codex_fail_is_not_overridden_by_a_gemini_pass`
  and siblings in `tools/tests/test_rules_conformance_gate.py`).
- `tools/record-verdict.sh`: `--reviewer` accepts `rules-conformance`, `codex`, `gemini`,
  or `in-house-independent`, and rejects anything else exactly as before. The context a
  verdict posts to is still derived mechanically as `deckard-verdict/$REVIEWER`, so the
  vendor-naming requirement falls out of the existing mechanism rather than needing a
  special case. The script does not choose among the four values — the caller states
  which reviewer ran, and this script's only job is validating and recording that claim,
  never dispatching to produce it.
- `AGENTS.md` and `docs/agent-team.md` state the chain and the in-house fallback's cost.
  `docs/agent-team.md`'s "Recording a verdict" section points at this ADR for the list
  and reasoning rather than keeping a second copy that can drift from it.
- The 140-character description bound in `tools/record-verdict.sh` is unchanged.

## Reasoning

Codex stays the default because cross-vendor independence — a reviewer that was not
trained the same way as the implementer — is the property Issue #41 built this gate to
buy, and Codex is the vendor already in routine use for it. Gemini is next because it is
a second, independently-trained vendor: falling back to it before ever touching the
in-house path preserves genuine cross-vendor review through the common failure mode (one
vendor's rate limit or outage), not just the rare one (both vendors down). In-house is
last, and named `in-house-independent` rather than reusing `rules-conformance`, because
even though it does not carry cross-vendor independence, it still must be run as a
genuinely separate pass — with no sight of the first verdict's findings, per the
independence rule in `AGENTS.md` — and its own context makes clear on the merged commit
that this is what happened, distinct from either genuine cross-vendor case.

Naming the vendor in the context, rather than collapsing all three into one generic
`deckard-verdict/independent` context, is the load-bearing part of this decision: the
whole reason Brandon accepted the in-house fallback's cost was that the cost stays
visible per-commit rather than disappearing into "some independent verdict was
recorded." A generic context would let the fallback quietly become the normal path with
nothing on the merged commit to show it, which is exactly the risk Issue #83 itself
names.

This ADR does not add any new enforcement of vendor authenticity. `tools/record-verdict.sh`
could not verify which model produced a verdict before this change, and still cannot: a
human or agent invoking it with `--reviewer codex` having actually run Codex remains a
process obligation the tooling records but does not check, exactly as `docs/agent-team.md`
already states for the mechanism this ADR widens.

## Consequences

- A rules PR blocked only by one vendor's outage or rate limit now has two other paths to
  a recordable independent verdict, so a single vendor's availability no longer gates
  every rules merge.
- Every merged commit that used the in-house fallback carries `deckard-verdict/in-house-independent`
  in its statuses, not a generic label — an auditor can find every commit that took the
  weaker path without opening a single transcript.
- The in-house fallback being always available creates an ongoing risk that it becomes
  the path of least resistance rather than the last resort the chain intends. Nothing in
  this ADR or the tooling prevents that by itself; the named context is what makes the
  pattern visible if it happens, not a mechanism that stops it.
- `tools/record-verdict.sh` still does not, and per Issue #83's non-goals must not,
  choose a reviewer automatically. Whoever invokes it is responsible for having actually
  tried Codex, then Gemini, before reaching for `in-house-independent`.

## Rejected alternatives

- **Status quo (Codex only).** A single point of failure for every rules merge, and the
  proximate reason this Issue was filed; see Options considered #1.
- **A single generic `deckard-verdict/independent` context accepting any reviewer.**
  Destroys exactly the legibility Brandon asked for — whether a second verdict was
  genuinely cross-vendor or a same-vendor fallback would no longer be visible on the
  merged commit; see Options considered #2.
- **Automatic vendor selection inside `record-verdict.sh`.** Out of scope by Issue #83's
  own non-goals; the script records what the caller states, it does not decide what to
  try; see Options considered #4.
