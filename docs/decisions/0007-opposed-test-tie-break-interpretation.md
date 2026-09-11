# 0007 — Opposed test tie-break: aggressor mapped to the acting side

## Status

Accepted — 2026-09-11. Governs `Deckard.Rules.Resolution.OpposedTest` (Issue #4). Decided
by Brandon after adversarial conformance review flagged the mapping's justification.

## Decision

On a tied Opposed test (equal hits on both sides), Deckard resolves the win to the acting
side (`OpposedTestWinner.Actor`), with `NetHits` reported as 0. This is a deliberate house
interpretation of a hedged, undefined term in the book, not a sourced rule: the book itself
never commits to a single answer here.

## Problem

Printed p. 35 / PDF p. 36 states that ties typically go to "the aggressor," with an
exception for when net hits are required by an effect. Two problems:

1. "Aggressor" is never defined anywhere in the packet's range. It appears exactly once,
   in this one sentence.
2. The sentence hedges twice: "typically" (not always), and an unspecified exception
   condition (net hits being "required," itself not mechanically defined here).

`OpposedTest.Resolve` (Issue #4) needs a definite answer for every input, including ties,
to stay a total function returning a structured result rather than an unresolved one for
the single most common edge case in the mechanic — any two dice pools rolled at the same
size tie with unremarkable frequency. Something has to break the tie; the book does not
name what, in mechanically actionable terms.

## Authoritative source locations

SR6 Core / Game Concepts / Tests / Opposed Tests / printed p. 35 / PDF p. 36 — the tie-break
sentence and the "you must exceed their effort to succeed" sentence discussed below.

The worked example notation, "Stealth + Agility vs. Perception + Intuition," and its
"Acting player's skill and attribute" / "Defending player's skill and attribute" captions,
are printed p. 36 / PDF p. 37 — a different page, and, per Reasoning below, not a
definition of "aggressor" at all.

## Options considered

1. **Map "aggressor" to the acting side** (the party named first in the test's notation).
   Chosen. Always available — every Opposed test call names an actor and a defender — and
   matches the ordinary-language sense of "aggressor" as the one taking the action, most
   of the time.
2. **Map "aggressor" to the defending side.** No textual support at all; rejected as no
   less arbitrary than option 1, without even the naming intuition option 1 has.
3. **Return an unresolved result on every tie** (ADR 0004, `RequiresInterpretation`),
   forcing every caller to handle "the book doesn't say" explicitly. The most textually
   honest option. Rejected because ties are not a rare edge case — any two pools tied on
   hit count produce one — and refusing to resolve the single most common draw outcome
   would make Opposed test resolution nearly unusable for its ordinary purpose.
4. **Take the aggressor as a caller-supplied parameter to `OpposedTest.Resolve`.** Pushes
   the interpretation question onto every caller instead of settling it once, and invents
   API surface — "which side is the aggressor" — the book gives no basis for constructing:
   a caller would face the same undefined judgment call, just repeatedly.

## Chosen design

`OpposedTest.Resolve(source, actorPool, defenderPool)` resolves a tie
(`actor.Hits == defender.Hits`) to `OpposedTestWinner.Actor`, with `NetHits == 0`. No
configuration point; the interpretation is fixed engine-wide until superseded.

## Reasoning

**Why "actor" over "defender" or an unresolved result.** Among the options above, only
mapping to a side of the roll keeps the API total without inventing parameters the book
gives no basis for. Between the two sides, "acting side" is closest to ordinary usage of
"aggressor" — the party taking the action, most often — and is the only side identifiable
from data `OpposedTest.Resolve` already has (which pool was named first), without
requiring a caller to separately assert who the aggressor is.

**This is weaker evidence than an earlier version of this code implied, and the code must
not overstate it.** An earlier revision cited the p. 36 "Acting player's skill and
attribute" / "Defending player's skill and attribute" captions as though they justified
this mapping. They do not: those captions belong to the written-notation diagram and say
only which half of "X vs. Y" is which. They say nothing about who is or is not the
aggressor, a term that appears once, in a different sentence, on a different page (p. 35).
Citing them as authority for the tie-break was a citation to text that does not say what
it was cited for; the code comments no longer make that claim; this ADR is the actual
justification, and it is explicitly an interpretation rather than a citation.

**The strongest argument against this mapping.** The book's own worked Opposed-test
example is Stealth + Agility vs. Perception + Intuition — a sneaking character opposed by
a perceiving one. The acting side there is the one sneaking. "Acting" and "aggressor"
plausibly diverge exactly here: a character trying to avoid detection is not obviously the
aggressor in any ordinary sense, and if anything the perceiving side is the one actively
contesting the sneak. The book's own choice of illustration does not obviously support
mapping "acting" onto "aggressor" — if anything, it is a case where the two readings could
come apart.

**A second tension, left unresolved by the book.** The same page states that an opposing
party resists "so you must exceed their effort to succeed." On a tie, the actor has not
exceeded the defender's effort under any ordinary reading of "exceed" — yet the very next
paragraph gives ties to the aggressor anyway. The book does not reconcile these two
statements, and this ADR does not attempt to either, beyond noting that a tie-break rule
existing at all is itself evidence the "exceed" language is not meant as a literal
equality-favors-the-defender rule.

**Why not leave it unresolved (ADR 0004) instead.** Ties are the modal case, not an edge
case: two equal-sized pools rolled from any real distribution tie with unremarkable
frequency, and equal-sized pools themselves are common (a mirrored skill contest, for
instance). An `OpposedTest.Resolve` that returned `RequiresInterpretation` on every tie
would fail visibly on one of the most ordinary inputs the mechanic sees, trading one
failure mode (silently guessing) for another (a mechanic unusable across a large fraction
of realistic dice pools) rather than actually serving CLAUDE.md's fail-visibly principle.
A single documented, deliberate default — openly labeled as Deckard's choice rather than
the book's — serves callers better than making every consumer re-derive or special-case
the same undefined judgment call.

## Consequences

- `OpposedTestWinner.Actor` winning a tie is Deckard's interpretation, not a sourced fact.
  Any future Issue building on `OpposedTest` (combat, Matrix contests, etc.) must not
  treat this as settled RAW when describing its own behavior — cite this ADR, not the
  rulebook, for the tie-break specifically.
- The "may change if net hits are required" exception the book gestures at is
  unimplemented: `NetHits` is already 0 on a tie, which happens to satisfy any downstream
  rule gating an effect on `NetHits > 0`, but that is a consequence of the chosen default,
  not a deliberate implementation of the exception clause.
- Superseding this ADR is the correct path if Brandon later prefers a different default
  (e.g., defender-wins-ties, or an unresolved result) — not a quiet code change.

## Rejected alternatives

- **Map to the defending side.** No textual support beyond the same undefined term; see
  Options considered #2.
- **Return `RequiresInterpretation` on every tie.** Textually the most honest option, but
  makes the mechanic fail on its most common input; see Options considered #3 and
  Reasoning.
- **A caller-supplied aggressor flag.** Pushes an undefined judgment call onto every
  caller instead of settling it once; see Options considered #4.
