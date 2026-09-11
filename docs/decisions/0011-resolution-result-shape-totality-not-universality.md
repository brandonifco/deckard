# 0011 — Resolution result shape: totality determines the union, not universality

## Status

Accepted — 2026-09-11. Supersedes
[0004](0004-unresolved-rule-taxonomy.md) (docs/decisions/0004-unresolved-rule-taxonomy.md).
Governs every `Resolve` operation under `Deckard.Rules.Resolution` (Issue #62). Decided by
Brandon after `rules-conformance` review of Issue #4 found the first two operations to ship
already violated ADR 0004's stated shape.

## Decision

An engine operation that cannot resolve a mechanic still returns an explicit unresolved
**domain result** carrying one of ADR 0004's closed reasons — that half of ADR 0004 is
unchanged. What changes is which operations are required to be *capable* of returning one:

- An operation **may** return a resolved type directly when, for every valid input in its
  domain, the core rulebook determines exactly one answer — no unsupported branch, no
  out-of-scope branch, no ambiguity awaiting a decision.
- An operation **must** return a resolved-or-unresolved union when any input in its domain
  can reach a rule that is unsupported, out of scope, or ambiguous.
- **The burden sits on the operation returning a resolved type directly.** Totality is the
  claim that must be justified; the union is the default. An implementer who cannot state
  why the operation is total uses the union.

ADR 0004's five-value `UnresolvedReason` vocabulary
(`UnsupportedRule`, `RequiresInterpretation`, `OutsideCurrentScope`,
`UnsupportedInteraction`, `MissingRulesData`) is carried forward unchanged. So is ADR
0007's tie-break ruling for `OpposedTest`. This ADR replaces only ADR 0004's universal-union
requirement — "every resolution operation returns a result type that is either resolved or
unresolved" — with the three-clause test above.

## Problem

ADR 0004 stated the shape without qualification: *"Every resolution operation returns a
result type that is either resolved or unresolved."* The first two operations to ship do
not do this. `SimpleTest.Resolve` returns `SimpleTestResult`; `OpposedTest.Resolve` returns
`OpposedTestResult`. Neither can be unresolved, and neither shares a type with
`UnresolvedTestResult`, which is reachable only from the `ExtendedTest` and `TeamworkTest`
stubs.

So the two operations that actually resolve rules today are exactly the two with no
unresolved path, and ADR 0004's stated consequence — *"Callers must handle the unresolved
case. That is intended friction"* — does not exist where the rule said it would. This
surfaced during `rules-conformance` review of Issue #4, where it was the structural reason
a genuinely ambiguous rule (the Opposed test tie) had to be forced into a boolean answer
rather than expressed as `RequiresInterpretation`. Brandon has since ruled on that specific
tie (ADR 0007); this ADR is not about the tie. It is about the shape every future mechanic
copies from the first ones.

Two ways to close the gap between code and ADR 0004 were on the table: make the code match
the ADR (union everywhere), or make the ADR match what the code discovered by being built.
This is the record of choosing the latter, and stating precisely how far it goes.

## Options considered

1. **Make resolution results a union unconditionally.** Every `Resolve` returns something
   that is either a resolved result or `UnresolvedTestResult`, so a caller cannot read hits
   from an unresolved test without acknowledging it. Closest to ADR 0004 as originally
   written, and the friction is the point.
2. **Replace the universal requirement with a totality test, decided per operation**
   (chosen). An operation whose rules are total for valid input returns a resolved type
   directly; the union is required only where an operation can actually reach a rule that
   is unsupported, out of scope, or ambiguous pending a decision.

## Chosen design

The three-clause rule in Decision above, applied per `Resolve` operation at the point it is
designed, not retrofitted after the fact. Two worked pairs from existing code:

**Total — return a resolved type directly.**

- `SimpleTest.Resolve(source, dicePool, threshold)`: for every non-negative pool and
  threshold, the book's rule (roll, count hits, compare to threshold) determines exactly one
  outcome. There is no branch where the rule is unsupported, out of scope, or undecided.
  Negative input is rejected by argument validation before resolution begins — invalid
  input, not an unresolved rule (see the type's own `ArgumentOutOfRangeException` doc
  comment). Returns `SimpleTestResult` directly.
- `OpposedTest.Resolve(source, actorPool, defenderPool)`: roll both pools, compare hits.
  The tied case was genuinely ambiguous in the source, which is exactly the condition that
  would otherwise force the union — but ADR 0007 already decided it, as a recorded
  interpretation rather than a citation to the book. Once Brandon decides an ambiguity, it
  is no longer "ambiguity awaiting a decision" for this operation; it is settled engine
  behavior with its own ADR to cite. `OpposedTest.Resolve` is therefore total across its
  whole domain today and returns `OpposedTestResult` directly. If a future packet or
  Brandon later reopens the tie-break (superseding ADR 0007 itself), this operation would
  need to be re-evaluated against this same test — not silently kept total.
- `SimpleTest` and `OpposedTest` are unchanged by this ADR: this is a description of the
  shape they already have, not a migration.

**Not total — must return the union.**

- `ExtendedTest.Resolve()`: Extended tests are a named basic test type
  (SR6 Core / Game Concepts / Tests / printed p. 35 / PDF p. 36) whose resolution shape is
  not designed yet (Issue #4's Non-goals deferred it explicitly). Every call today reaches
  an unsupported rule. Returns `UnresolvedTestResult` with `UnresolvedReason.UnsupportedRule`.
- `TeamworkTest.Resolve()`: same reasoning — a named composite test mechanic
  (printed p. 36 / PDF p. 37) whose design is deferred to its own Issue. Returns
  `UnresolvedTestResult` with `UnresolvedReason.UnsupportedRule`.
- Both are also unchanged by this ADR, for the same reason: they already have the shape
  this decision blesses.

A mixed case, for future reviewers: an operation is not total merely because *most* inputs
resolve cleanly. If *any* reachable input in its domain hits an unsupported, out-of-scope,
or undecided-ambiguous branch, the whole operation must return the union — a caller cannot
tell in advance which inputs are the safe ones, so the type itself must carry the
possibility. Totality is a claim about the entire domain, not about the common case.

## Reasoning

ADR 0004's "intended friction" argument only works while the friction is rare and
meaningful. If every caller unwraps a union whose unresolved arm is unreachable — as it was
for both operations that had actually shipped — unwrapping becomes reflexive, and the signal
is lost precisely where it matters: on the operation that genuinely cannot resolve a rule.
Making `unresolved` mean something is worth more than making it universal.

This is not a weakening of the fail-visibly invariant (CLAUDE.md, global invariant 2). That
invariant is about what happens when a rule *cannot* be resolved: it must never silently
default, skip, or guess. It says nothing about operations that have no such case at all.
Requiring `SimpleTest.Resolve` to wrap every roll in a union it can never actually populate
with `UnresolvedTestResult` does not make the engine more honest about what it cannot do; it
makes every caller of a total operation pay a tax that buys no additional safety.

The burden-of-proof clause is the load-bearing part for future review. It would be easy to
mistake "this operation happens not to hit any unsupported branch yet" for "this operation
is total" and skip the union prematurely as scope grows — a spell list expands, a new
Matrix action is added, a table gains a row this engine doesn't yet cover. Placing the
burden on the resolved-type claim, not on the union, means a reviewer's question is always
"why is this total?" rather than "why isn't this a union?" — the more dangerous drift runs
in that second direction, silently, and this ADR is written to make it the harder one to
commit by accident.

## Consequences

- Every future `Resolve` operation is designed against the three-clause test above at
  design time: is the rulebook total over the operation's whole input domain, with no
  unsupported branch, no out-of-scope branch, and no undecided ambiguity? If yes, and the
  implementer can state why, it returns a resolved type directly. If no — or if the answer
  is not obviously yes — it returns the resolved-or-unresolved union.
- A rules-conformance reviewer checking a new `Resolve` operation asks the implementer to
  justify totality explicitly, the same way provenance is required for a rule's source. An
  unjustified resolved-type return is now a defect to flag, not a style preference.
- `UnresolvedReason`'s five-value vocabulary, and its concrete carrier `UnresolvedTestResult`,
  are unchanged and continue to govern every operation that does return the union.
- ADR 0007's tie-break ruling is unaffected in substance; this ADR only explains, in the
  worked examples above, why `OpposedTest` remaining total is consistent with — not an
  exception to — the rule stated here.
- An operation that is total today can stop being total later, if a future packet or
  Issue extends its domain into territory the book does not resolve (e.g., a new modifier
  interacting with `SimpleTest` in a way the current threshold-comparison rule does not
  cover). That is a signal to introduce the union at that point, not to force the new case
  to silently fit the old resolved type.
- ADR 0004's Status becomes `Superseded by 0011`; its Decision, Problem, Options considered,
  Chosen design, Reasoning, and Consequences sections are left as originally written; the
  reasoning trail for the taxonomy itself — why a closed reason vocabulary over exceptions,
  nulls, or a boolean flag — remains there and is not restated here.

## Rejected alternatives

- **Union everywhere, unconditionally (Options considered #1).** Textually the most direct
  fidelity to ADR 0004 as first written, and the option that preserves maximal caller
  friction. Rejected because the friction stops being informative once it is unconditional:
  `SimpleTest` and `OpposedTest` would each carry an unreachable unresolved arm forever,
  training every caller to unwrap it as reflex rather than as a meaningful check — exactly
  the failure mode identified in Problem above, just left in place instead of fixed.
