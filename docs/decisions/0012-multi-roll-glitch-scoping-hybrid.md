# 0012 — Multi-roll glitch scoping: per-roll glitch, test-level critical glitch

## Status

Accepted — 2026-09-11. Governs glitch-severity computation for any test spanning more
than one roll (Extended, Teamwork; Issue #61). Decided by Brandon after `rules-conformance`
review of PR #78 found the book's plain glitch condition stated two different ways
(printed pp. 35, 44), with a third printed location (p. 36, Teamwork) applying a per-roll
consequence consistent with only one of those two statements — escalated as a genuine
source ambiguity with gameplay consequences.

## Decision

For a test that spans more than one roll:

- **Whether a roll glitches is determined per roll.** A roll glitches when more than half
  of that roll's own dice come up 1s. The denominator is that roll's own dice count, never
  the test's dice totaled across rolls.
- **Whether a glitch is critical is determined at the test level.** A glitching roll is a
  critical glitch only while the test as a whole has produced no hits at all, accumulated
  across every roll of the test made so far — not merely when the glitching roll itself
  produced no hits.

This settles the scoping question Issue #61 escalated. It does not implement Extended or
Teamwork tests, it does not decide how `DicePoolRoll` or a future multi-roll type should
expose these two severities structurally, and — as Consequences below records honestly —
it does not settle every interaction a multi-roll structure can raise. Teamwork in
particular has open questions this ADR states rather than answers.

## Problem

The book states the plain glitch condition in two places that do not scope it the same
way, and applies it in a third place without stating or scoping it at all:

- **SR6 Core / Game Concepts / Tests / printed p. 35 / PDF p. 36** states the plain glitch
  condition with no test qualifier at all — in terms of the dice you rolled, full stop —
  and immediately defers elsewhere for detail.
- **SR6 Core / Game Concepts / Glitches and Critical Glitches / printed p. 44 / PDF p. 45**
  states both the glitch and critical-glitch conditions scoped to the test, in two
  different phrases: "on a test" for the glitch condition, "on the test" for the
  critical-glitch condition.
- **SR6 Core / Game Concepts / Tests / Teamwork Tests / printed p. 36 / PDF p. 37** does
  not itself state or scope the glitch condition. It presupposes the condition and applies
  it to a single helper's roll inside a larger, multi-roll structure, prescribing a
  consequence distinct from the leader's own result.

For a test spanning several rolls — Extended tests accumulate hits across repeated rolls of
a shrinking pool (SR6 Core / Game Concepts / Tests / Extended Tests / printed p. 36 /
PDF p. 37); Teamwork tests combine a leader's roll with one or more helpers' rolls, same
pages — at least three readings are live: total every roll's ones and dice together before
judging either condition; judge both conditions against each roll alone; or a hybrid of the
two, one per condition. The choice has real gameplay consequences: a fully totaled reading
makes a long Extended test progressively less able to glitch at all, since the totaled
denominator grows every roll while the book's own mechanic shrinks the pool by one die each
time. Per CLAUDE.md ("Stop and ask Brandon: a genuine source ambiguity with gameplay
consequences"), resolving this was not an implementing agent's call; it was escalated on
Issue #61 and is recorded here.

For completeness: **SR6 Core / Game Concepts / Tests / Buying Hits / printed p. 36 /
PDF p. 37** describes a test resolved with zero rolls (hits computed directly from pool
size). It is outside this ADR's scope — no dice are rolled, so neither glitch condition has
anything to apply to — but is noted because a framing of "one roll versus several" alone
does not have a cell for it.

## Authoritative source locations

- SR6 Core / Game Concepts / Tests / printed p. 35 / PDF p. 36 — plain glitch condition, unscoped.
- SR6 Core / Game Concepts / Glitches and Critical Glitches / printed p. 44 / PDF p. 45 — glitch
  and critical-glitch conditions, both test-scoped, in two different phrases.
- SR6 Core / Game Concepts / Tests / Extended Tests / printed p. 36 / PDF p. 37 — the
  multi-roll, shrinking-pool accumulation mechanic.
- SR6 Core / Game Concepts / Tests / Teamwork Tests / printed p. 36 / PDF p. 37 — the
  leader/helper roll structure and the helper-glitch consequence.
- SR6 Core / Game Concepts / Tests / Teamwork Tests in Initiative / printed p. 36 /
  PDF p. 37 — the leader may defer or act without completing their part of the test,
  losing the helpers' contributed dice; relevant to the open question recorded in
  Consequences.
- SR6 Core / Game Concepts / Tests / Buying Hits / printed p. 36 / PDF p. 37 — the
  zero-roll resolution path, noted above.

## Options considered

1. **Totaled** — sum ones and dice across every roll of the test so far, and judge both the
   glitch trigger and criticality against that running total. Closest textual fit for a
   test-scoped reading of both p. 44 sentences taken together as one rule about "the test's
   dice." Rejected: it makes a long Extended test asymptotically unable to glitch (see
   Problem), and it has no coherent application to Teamwork, where a helper's dice never
   join the leader's pool at all — only the helper's resulting hits do, as extra dice added
   to the leader's own pool. A totaled reading would need an unstated exception for
   Teamwork; that exception would swallow the rule.
2. **Per roll** — judge both the glitch trigger and criticality against each roll's own
   dice and hits alone, never accumulating anything. Matches what `DicePoolRoll.Glitch`
   already computes for a single roll, and matches p. 35's unscoped statement and p. 36's
   helper treatment. Rejected as the sole rule because it does not honor p. 44's own
   wording for critical glitch specifically, which the pinned printing (ADR 0003; no
   errata included) states as test-scoped every time it states the condition at all —
   "without a single hit on the test" — the one place this printing is not actually
   contested.
3. **Hybrid** — per-roll glitch trigger, test-level criticality. Chosen.

## Chosen design

Option 3. Precisely:

- A roll's own dice determine whether that roll glitches: more than half of that roll's own
  dice showing 1s.
- Whether a glitching roll is a *critical* glitch is judged against the test, not the roll:
  critical exactly when the test has accumulated zero hits across every roll made so far
  (including the glitching roll itself, since a roll's hits and its glitch are independent
  facts about that roll — a roll can glitch and also contribute hits).

## Reasoning

**p. 35 is a summary that defers to p. 44, not competing authority.** p. 35 states the
condition briefly and explicitly points elsewhere for detail ("see p. 44 for more info").
Where the two disagree on scoping, p. 44 is the fuller statement it directs the reader to,
and governs.

**p. 36's helper-glitch line is explained by a per-roll glitch trigger, not in tension with
it.** The book gives a glitch on a helper's own roll a distinct consequence from the
leader's. That is exactly what judging the glitch trigger against each roll's own dice
produces. A fully totaled denominator would have to treat this sentence as an unexplained
exception; the hybrid reading does not, because the glitch-trigger half of the rule was
never meant to total in the first place.

**Totaling degenerates over a long Extended test.** The book's own Extended test mechanic
shrinks the pool by one die every roll while accumulating hits toward the threshold. A
totaled ones-and-dice denominator grows with every roll even as the pool shrinks, making a
late roll in a long Extended test progressively less able to satisfy "more than half" no
matter how many 1s it produces. That is a degenerate consequence a straightforward reading
of the rule should not produce, and is itself evidence against the totaled reading.

**The criticality half of the rule is the one place the source is not actually contested.**
In the pinned printing, the critical-glitch condition is always stated as test-scoped —
"without a single hit on the test" — never on a single roll. Keeping criticality
test-scoped, while letting the glitch trigger itself stay per-roll (matching both p. 35's
plain statement and p. 36's worked application), honors the one part of the source that is
unambiguous rather than overriding it in either direction.

## Consequences

- A roll inside an Extended test can glitch on its own dice, independent of any other roll
  in the same test. Whether that glitch is critical depends on the test's hit total
  accumulated so far, not on that roll's own hits alone: once a test has banked any hit
  from any roll, a later glitching roll in the same test is an ordinary glitch, not a
  critical one. Extended's structure raises no further question here: every roll belongs
  to the same single actor and the same single pool, so "the test's accumulated hits" is
  unambiguous.
- In a Teamwork test, a helper's glitch is resolved on the helper's own roll, with its own
  consequence, consistent with the printed helper-glitch treatment: the per-roll glitch
  trigger above applies to that roll's own dice exactly as it does for any other roll. What
  a helper's roll contributes to the leader's pool is dice, not hits directly — printed
  p. 36 states that the helpers' *hits* become extra *dice* added to the leader's pool,
  which the leader then rolls as part of their own roll.
- **Not settled by this ADR: how a helper's roll feeds the test's accumulated hits.**
  Brandon decided that criticality is judged against hits accumulated across the test's
  rolls; he did not decide, and the book does not obviously settle, whether or when a
  helper's own roll counts toward that total before the leader rolls, or only through the
  larger pool it hands the leader. Concretely: two helpers, A rolls 3 hits and B glitches
  with 0 hits, before the leader has rolled at all — whether the test has already banked a
  hit at that point (an ordinary glitch for B) or not (a critical one) is exactly the
  question this ADR does not answer. It is left to whichever Issue implements Teamwork,
  with its own source packet and its own decision, not invented here.
- **Also not settled: a Teamwork test with no leader roll at all.** SR6 Core / Game
  Concepts / Tests / Teamwork Tests in Initiative / printed p. 36 / PDF p. 37 states that
  the leader performs their part of the test on a later combat round, and that if the
  leader defers or acts without doing so, the helpers' contributed dice are lost. A
  Teamwork test can therefore have helper rolls and no leader roll at all. Whether, or how,
  criticality applies to a test that never receives its leader's roll is likewise left
  unresolved here, for the same future Issue to decide against its own packet.
- This ADR does not govern a bare `OpposedTest` — one with no Teamwork assistance on
  either side. Each side of such a test makes exactly one roll (SR6 Core / Game Concepts /
  Tests / Opposed Tests / printed p. 35 / PDF p. 36), so each side's per-roll severity
  already is that side's test-level severity; this was never the ambiguity in question and
  needed a documentation correction, not an interpretation. Printed p. 36 states that a
  Teamwork test complements another test, so an Opposed test with a Teamwork-assisted side
  is not bare in this sense: that side's own test then spans more than one roll (the
  helpers' rolls plus its own), and this ADR's hybrid rule — including the open questions
  recorded above — would govern it. Combining Teamwork with an Opposed test is out of
  scope for both this ADR and Issue #61.
- `DicePoolRoll.Glitch`'s existing predicate for a single roll (`ones * 2 > dice.Count`,
  then `hits == 0 ? CriticalGlitch : Glitch`) is unaffected and remains exactly correct
  for any test that consists of one roll — Simple tests, and each side of a bare Opposed
  test. It also already computes the correct per-roll glitch trigger this ADR calls for in
  a multi-roll test. What it does *not* and cannot compute, because a single
  `DicePoolRoll` has no visibility into any other roll, is the multi-roll criticality this
  ADR defines: reading `DicePoolRoll.Glitch == CriticalGlitch` off one roll of a multi-roll
  test is wrong the moment the test has banked a hit on some other roll, and reading
  `GlitchSeverity.Glitch` off one roll while the test has zero hits everywhere else
  under-reports what should be critical.
- Whether `DicePoolRoll` should expose a differently-named member for the per-roll glitch
  trigger, or whether a future multi-roll type should withhold severity entirely until a
  test is formed and its accumulated hits are known, is a structuring choice left to
  whichever Issue implements Extended or Teamwork tests, per Issue #61's own scope. This
  ADR settles the rule it does settle, not the API shape that will express it, and not the
  Teamwork-specific questions recorded above as open.
- Supersedes no earlier ADR. ADR 0007 (Opposed test tie-break) is unaffected; this decision
  does not touch a bare Opposed test's resolution.

## Rejected alternatives

- **Totaled** (Options considered #1) — degenerates over a long Extended test and has no
  coherent application to Teamwork's actual dice-pool mechanics; see Reasoning.
- **Pure per-roll for both conditions** (Options considered #2) — does not honor p. 44's
  own test-scoped wording for critical glitch, the one part of the pinned printing that
  states this consistently; see Reasoning.
