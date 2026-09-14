# 0013 — Randomness-consumption atomicity

## Status

Accepted — 2026-09-11. Governs every operation that accepts an `IRandomSource` under
`Deckard.Rules` (Issue #93).

## Decision

**No operation may draw from its `IRandomSource` until every check capable of producing
an unresolved or invalid result for that call has completed.** Concretely:

- Every argument-validation failure (a thrown `ArgumentException` or a subtype of it —
  `ArgumentNullException`, `ArgumentOutOfRangeException`, and so on) must occur before the
  first call to `IRandomSource.NextUInt32()` the operation would otherwise make.
- Every path that returns `UnresolvedTestResult` (ADR 0004/0011's unresolved-or-invalid
  union) must be reachable, and must be reached, without any prior draw.
- Once the last such check for a given call has passed — once the operation commits to
  actually rolling — the remainder of that call is **total**: it runs to a resolved result
  without a further unresolved branch, except for a programmer fault (a bug in Deckard's
  own code, not a modeled game condition).

An operation satisfies this invariant by construction, not by testing alone: every
unresolved-or-invalid branch is written to execute before the operation's first draw.
Tests verify the invariant holds; they cannot substitute for the ordering itself.

This is an ordering constraint on operations that already exist. It does not require a
two-phase `Prepare()`/`Execute()` split (see Rejected alternatives), and it does not
change `IRandomSource`, or any resolved outcome any existing operation produces for valid
input.

## Problem

Deckard's central determinism claim (CLAUDE.md, global invariant 1) is that the same seed
and the same ordered decisions reproduce the same outcomes and the same ordered history.
That claim silently assumes something narrower than "the rules are deterministic": it
assumes a caller can always tell, in advance, whether a given call is *going* to draw
from the source at all — because if it isn't, retrying it after refusal must not have
moved the source, or a retried history and a first-attempt history diverge from that
point on even though both made the same ordered decisions.

Nothing states that assumption today. It is true of the code that exists —
`OpposedTest.Resolve` validates both pools before either side rolls; `ExtendedTest` and
`TeamworkTest` both document, in prose, that they roll no dice and consume no randomness
because refusal happens before anything is drawn — but three independent good decisions
by three different call sites are not an enforced rule. Nothing stops a fourth call site,
written without having read the other three, from validating an argument *after* rolling
a few dice for convenience, or from letting an unsupported-rule branch sit downstream of
a draw that was meant to be speculative. The mechanism this Issue exists to name is the
consequence of exactly that: see "Consequence being prevented" below.

## Why `IRandomSource` cannot repair a violation after the fact

The tempting fix, once a violation is found, is to make it recoverable: give
`IRandomSource` a way to roll back or clone itself, so a caller that drew three values
before discovering an argument was invalid could simply undo the draws and continue as
if they never happened.

That is the wrong direction, and ADR 0002 already ruled it out by omission. ADR 0002's
`IRandomSource` interface (`src/Deckard.Core/Randomness/IRandomSource.cs`) exposes exactly
one member, `NextUInt32()` — no `Rollback()`, no `Fork()`/`Clone()`, no
"peek without consuming." That minimal surface is deliberate: layering the d6 mapping,
dice pools, and test resolution on top of one primitive is what keeps each layer
separately testable, and every member added to the primitive is a member every future
`IRandomSource` implementation — a hardware entropy-backed source used for seeding, a
network-replayed source, whatever comes later — must also implement correctly or the
abstraction leaks.

Rollback specifically is worse than merely extra surface. A PCG32 stream (ADR 0002) is a
one-way function of its internal state; there is no general inverse for "undo the last
draw" that does not amount to storing every prior state, which reintroduces exactly the
mutable history-tracking ADR 0002's stateless-stream design was chosen to avoid. Cloning
has a related problem: a clone lets a caller draw speculatively from a copy and discard
it if a check fails, but the *decision* of whether to keep drawing from the original or
the clone becomes itself an unrecorded, ad hoc branch — invisible to the ordered-decision
history the replay contract is built from (ADR 0005), and a second place a future mechanic
could get the ordering wrong.

So the invariant is enforced by discipline in the operations that consume
`IRandomSource`, not by a repair mechanism in the primitive itself. An operation that
violates it has produced a bug, not a recoverable condition.

## Consequence being prevented

A caller that retries an operation after it returns unresolved-or-invalid sees different
randomness on the retry than a caller whose first attempt already had valid input and
never needed to retry — *if* the first attempt already drew from the source before
failing. Two histories that recorded the same ordered decisions ("attempt this test,
[it failed validation], attempt it again with corrected input") would then read different
dice from that point forward, purely because of *how many times* an unresolved attempt
was made before the one that stuck — a fact that is not itself one of the ordered
decisions the replay contract (ADR 0005) claims determines the outcome.

This is not a hypothetical about a distant future mechanic. It is precisely the shape of
gameplay a two-phase mechanic like Edge Actions or Edge rerolls (Phase 4, not yet built)
will have: attempt a test, learn something failed, decide whether to spend a resource and
try again. If any attempt in that sequence can partially consume the source before
declaring itself invalid, the resulting history is unreplayable from a description of the
decisions alone — a caller would also have to record exactly how many wasted draws each
failed attempt made, which is not a decision anyone made, it is an implementation
accident leaking into the replay contract.

## Existing conforming precedents

Exactly three public members under `Deckard.Rules` accept an `IRandomSource` today —
re-derived directly from source (`grep -rn "IRandomSource" src/Deckard.Rules`) rather than
assumed, since an inventory claim in an accepted ADR is durable and a future implementer
will trust it as written:

- `DicePoolRoll.Roll` (`src/Deckard.Rules/Resolution/DicePoolRoll.cs`): the shared
  primitive both `SimpleTest` and `OpposedTest` roll through, and also a public member a
  caller can invoke directly without going through either. Validates `source` is
  non-null and `dicePool` is non-negative before calling `D6.Roll` — a first-class entry
  point in its own right, not merely covered by delegation from the two operations that
  happen to call it.
- `OpposedTest.Resolve` (`src/Deckard.Rules/Resolution/OpposedTest.cs`): validates
  `actorPool` and `defenderPool` are non-negative — and `source` is non-null — before
  either side's `DicePoolRoll.Roll` executes. Its own doc comment already states the
  reason: "a failure must not leave the source in a state that depends on which argument
  failed."
- `SimpleTest.Resolve` (`src/Deckard.Rules/Resolution/SimpleTest.cs`): validates
  `threshold` is non-negative — and `source` is non-null — before calling
  `DicePoolRoll.Roll`, which in turn repeats its own validation of `source` and
  `dicePool` before drawing anything.

`ExtendedTest.Resolve` (`src/Deckard.Rules/Resolution/ExtendedTest.cs`) and
`TeamworkTest.Resolve` (`src/Deckard.Rules/Resolution/TeamworkTest.cs`) are conforming for
a different reason: neither takes an `IRandomSource` parameter at all. Both doc comments
state "it rolls no dice and consumes no randomness: refusal happens before anything is
drawn" — true by construction, since neither type has a source to draw from.

## Options considered

1. **State the invariant as prose guidance in CLAUDE.md or a guide, not an ADR.** Cheaper,
   but CLAUDE.md's own "when an ADR is required" list (`docs/decisions/README.md`) names
   "a foundational determinism or replay decision" as exactly the case an ADR exists for,
   and burying this in a guide would make it revisable without the visibility a foundational
   decision needs.
2. **A two-phase `Prepare()`/`Execute()` API**, where `Prepare` performs every
   validation-and-unresolved check and returns a token only `Execute` can consume, making
   the invariant structurally impossible to violate. Rejected as a Non-goal of the Issue
   this ADR closes: it would change the shape of every existing `Resolve` method for a
   guarantee that ordinary code review and the enforcement tests below already provide,
   and it forecloses simpler future call shapes before any of them have been designed
   against real Edge Action or Edge Boost requirements.
3. **Add rollback or cloning to `IRandomSource`**, making a violation recoverable instead
   of forbidden. Rejected — see "Why `IRandomSource` cannot repair a violation after the
   fact" above.
4. **State the invariant and enforce it only by test, with no ADR** (chosen invariant,
   without the record). Rejected: a rule enforced only by tests that happen to exist today
   is indistinguishable, to a future implementer who has not read every test file, from
   "nobody has decided this yet" — exactly the gap Problem above describes. The ADR is what
   makes the rule discoverable before a fourth call site is written, not after.
5. **State the invariant as an ADR, with enforcement tests per existing entry point**
   (chosen). Makes the rule a decision of record, testable, and citable, without
   constraining how a future operation's code is shaped to satisfy it.

## Chosen design

Option 5. The invariant as stated in Decision above, recorded here and cited by every
`Resolve`-shaped operation's own doc comment going forward (as `OpposedTest`, `ExtendedTest`,
and `TeamworkTest` already do in substance, if not by ADR number). Enforcement is two
layers, neither of which is new machinery:

- **Construction.** An operation is written so every check that can produce an unresolved
  or invalid result runs before its first draw — a discipline applied at the point the
  operation is designed, the same way ADR 0011's totality test is applied at design time
  rather than retrofitted.
- **Tests.** For every resolution entry point that accepts an `IRandomSource` and can
  return an unresolved or invalid result along some path, a test captures a real
  `Pcg32`'s `Pcg32State` (`Pcg32.GetState()`) before the call, drives the call down that
  path, and asserts the captured state equals `Pcg32.GetState()` again afterward —
  **state-equality**, not output-equality. An output-equality check (e.g. asserting a
  `FixedSequenceRandomSource`'s `Consumed` counter is still `0`) is weaker: it can only
  prove *how many* draws happened, whereas comparing `Pcg32State` proves the generator's
  internal state — the two 64-bit fields a real replay actually depends on — did not
  advance by even a partial step. `tests/Deckard.Rules.Tests/Resolution/
  RandomnessConsumptionAtomicityTests.cs` is where these live for the entry points that
  exist today (`DicePoolRoll.Roll`, `SimpleTest.Resolve`, `OpposedTest.Resolve` — the only
  three public members under `Deckard.Rules` that currently accept an `IRandomSource`; see
  "Existing conforming precedents" above for why `ExtendedTest.Resolve` and
  `TeamworkTest.Resolve` need no such test — they take no source to advance in the first
  place).

Today's `DicePoolRoll.Roll`, `SimpleTest.Resolve`, and `OpposedTest.Resolve` are all total
once their arguments validate (ADR 0011, extended here to a non-`Resolve`-named member for
the same reason): every unresolved-or-invalid outcome any of the three can produce is an
argument-validation exception, not an `UnresolvedTestResult` branch. The invariant and its
enforcement tests cover both kinds identically — an `UnresolvedTestResult` return and a
thrown `ArgumentException` are both "did not resolve," and both must leave the source
untouched. A future operation that is not total (ADR 0011's union case) is bound by the
same rule for its `UnresolvedTestResult` branches as for its validation failures.

## Reasoning

**Why atomicity, not partial credit.** The alternative to "no draws before every check
completes" is "a draw already made stays made, even if the call goes on to fail" — i.e.,
treating drawn-but-discarded values as acceptable collateral. That would satisfy
CLAUDE.md's fail-visibly invariant in the narrow sense (the *result* is still explicit and
honest about what happened), but it does not satisfy the determinism invariant, which is
about the *sequence of draws*, not just the final answer. A test that only checks the
returned value would pass either way; only a state-equality test distinguishes "genuinely
consumed nothing" from "consumed something, then discarded the result." That is exactly
why the Issue's own required evidence insists on `Pcg32State` equality rather than
output equality — see Chosen design above.

**Why this binds validation failures as tightly as unresolved-rule results.** ADR 0004's
taxonomy and ADR 0011's totality test are both about *modeled* game outcomes — a rule the
book does not cover, an ambiguity awaiting a decision. An `ArgumentException` is a
different kind of failure: a caller's programmer error, not a game state. But both share
the property this ADR cares about — neither is a resolved outcome, and both must not have
moved the source — so the invariant does not distinguish them. A generator that advanced
because of a caller's bug is exactly as replay-poisoning as one that advanced because of
an unmodeled rule; the invariant is about the source's state, not about why the call
failed.

**Why "except for programmer faults."** The invariant cannot promise that a bug inside
Deckard's own resolution logic — an exception thrown by code that has already drawn, from
a cause nothing in this ADR anticipates — leaves the source untouched. Promising that
would require treating every internal exception as recoverable, which is a much larger
claim than this ADR makes and not one any existing operation needs. The invariant is a
claim about the *designed* control flow of an operation: every intentional
unresolved-or-invalid path runs before the first draw. It says nothing about undefined
behavior from a defect elsewhere.

## Consequences

- Every future `Resolve`-shaped operation that accepts an `IRandomSource` is designed
  against this invariant at design time, the same way ADR 0011's totality test already
  is: before writing the first draw, enumerate every way the call can fail to resolve —
  argument validation and unresolved-rule branches alike — and place all of them earlier
  in the method than that draw.
- A `rules-conformance` or `repo-steward` reviewer checking a new operation that accepts
  an `IRandomSource` asks the implementer to point at where every unresolved-or-invalid
  branch sits relative to the first draw, the same way provenance and totality are already
  asked for.
- `tests/Deckard.Rules.Tests/Resolution/RandomnessConsumptionAtomicityTests.cs` is the
  home for this ADR's enforcement tests. A future operation that adds a new
  unresolved-or-invalid path — including a new argument-validation case on an existing
  operation — extends that file rather than inventing a second convention for it.
- This ADR does not, by itself, require restructuring `Smackdown`, `NotDeadYet`, or
  `SocialSituationEdgeGain` (`src/Deckard.Rules/Edge/`): none of the three accepts an
  `IRandomSource` today, so none can violate this invariant yet. Whichever Issue gives any
  of them a real implementation that does draw from a source inherits this invariant at
  that point, the same as any other future operation.
- `IRandomSource` (`src/Deckard.Core/Randomness/IRandomSource.cs`) is unchanged. No
  rollback, no clone, no peek-without-consuming member is added by this ADR.

## Rejected alternatives

- **Prose guidance instead of an ADR** — see Options considered 1.
- **A two-phase `Prepare()`/`Execute()` API** — see Options considered 2. Left open for a
  future ADR if a mechanic is designed that genuinely cannot satisfy this invariant with a
  single-method shape; nothing known today requires it.
- **Rollback or cloning on `IRandomSource`** — see "Why `IRandomSource` cannot repair a
  violation after the fact" and Options considered 3.
- **Enforcement by test alone, with no ADR** — see Options considered 4.
