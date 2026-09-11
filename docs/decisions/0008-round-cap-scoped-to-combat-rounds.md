# 0008 — Edge's bonus-gain cap applies only inside a combat round

## Status

Accepted — 2026-09-11. Governs `Deckard.Rules.Edge.EdgePool.GainBonusPoint` (Issue #68).
Decided by Brandon after independent conformance review flagged the cap being applied
unconditionally.

## Decision

`EdgePool`'s cap of two bonus Edge points is enforced only while a combat round is
currently in progress (tracked as `EdgePool.BonusGainedThisRound` being non-null, set by
`BeginCombatRound` and cleared by `EndCombatRound`). While no combat round is in
progress — including during a purely social or hacking confrontation, or between two
combats — `GainBonusPoint` enforces no round-scoped limit at all; only the hold cap of 7
total Edge can refuse a gain in that state.

## Problem

SR6 Core / Game Concepts / Edge / printed p. 45 / PDF p. 46 states the cap with an exact
textual scope: no more than two bonus points of Edge may be gained in a combat round. The
same page separately states that bonus Edge can also accumulate outside combat entirely —
a confrontation's Edge resets when it ends, and the confrontations named include hacking
and social persuasion alongside combat — without ever attaching a numeric limit to
gaining Edge in those non-combat cases.

An earlier revision of `EdgePool.GainBonusPoint` enforced the two-point cap
unconditionally, regardless of whether any combat round was in progress. That silently
extended a limit the book states only for combat rounds onto every other kind of
confrontation the book names, capping a purely social scene at two bonus points for its
entire duration with no textual basis.

## Authoritative source locations

SR6 Core / Game Concepts / Edge / printed p. 45 / PDF p. 46 — both the round-cap sentence
and the confrontation-reset sentence naming hacking and social persuasion as contexts
where bonus Edge accumulates.

## Options considered

1. **Apply the round cap unconditionally, regardless of whether a combat round is in
   progress** (the prior, now-superseded behavior). Simplest to implement, but invents a
   restriction on non-combat gain the book never states — the cap is written specifically
   "in a combat round", not "per confrontation" or "per scene".
2. **Apply the round cap only while a combat round is in progress** (chosen). Matches the
   printed rule's own scoping exactly: outside a combat round, nothing here refuses a
   gain except the hold cap, because the book states no other limit there.
3. **Invent an equivalent cap for non-combat confrontations** (e.g., two per hacking
   exchange or per social scene) by analogy to the combat-round cap. Rejected: no textual
   basis at all. The book distinguishes "a combat round" from the broader confrontation
   concept in the very paragraph that lists hacking and social persuasion as also able to
   accumulate bonus Edge, and never assigns either a numeric limit of its own.
4. **Return an unresolved result (ADR 0004) for any bonus-Edge gain attempted outside a
   combat round.** Rejected: gaining Edge outside combat is not an unimplemented rule —
   there is nothing ambiguous or unbuilt about it. The book's silence on a non-combat cap
   is simply the absence of one, not a gap this engine failed to resolve.

## Chosen design

`EdgePool.BonusGainedThisRound` is `int?`: `null` means no combat round is currently in
progress, and a value 0-2 means one is, counting bonus points gained since
`BeginCombatRound`. `GainBonusPoint`'s round-cap check
(`BonusGainedThisRound is int n && n >= RoundGainCap`) is trivially false whenever the
value is `null`, so outside a combat round only the hold cap of 7 remains in force.
`BeginCombatRound()` sets the counter to 0; `EndCombatRound()` clears it back to `null`.
Both are opaque, caller-driven transitions — this type still has no opinion on what makes
a combat round begin or end (initiative, turns), the same posture already established for
`BeginCombatRound` before this ADR.

## Reasoning

The cap's own printed wording is the whole argument: "in a combat round" is a specific
scope, not a stand-in for "per confrontation" or "while bonus Edge is accumulating" —
the same paragraph already has broader language ("any ongoing confrontation") available
and uses it for a different rule (the reset), which is evidence the narrower phrase for
the cap is deliberate rather than incidental. An engine that cannot tell "no combat round
in progress" from "a combat round in progress with zero gained so far" cannot honor that
distinction, which is exactly what the prior unconditional implementation could not do.

## Consequences

- A future Combat-phase caller must call `BeginCombatRound` and `EndCombatRound` around
  each round for the cap to apply at all; omitting either call is a caller-discipline
  gap this type cannot detect on its own, the same kind of obligation determinism
  discipline already places on callers elsewhere in this engine.
- Bonus Edge gained outside a combat round is, as of this ADR, bounded only by the hold
  cap of 7 — correct per what is printed, but worth flagging since it may read as
  asymmetric with the combat case to a future reader expecting one uniform limit.
- Should a future packet reveal a stated non-combat limit this pp. 45-48 excerpt does not
  contain, this ADR is superseded rather than quietly reinterpreted.

## Rejected alternatives

- **Applying the cap unconditionally.** The superseded behavior; see Problem and Options
  considered #1.
- **Inventing an equivalent non-combat cap.** No textual support; see Options considered #3.
- **An unresolved result outside combat.** Non-combat gain is not an unimplemented rule;
  see Options considered #4.
