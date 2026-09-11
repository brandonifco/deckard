# Roadmap

Phases, dependency order, and exit criteria. **This file does not track tasks** —
GitHub Issues are the only live work queue. If you want to know what to work on, ask
GitHub, not this document.

## Status

Current phase is recorded in [`../CLAUDE.md`](../CLAUDE.md), which every agent already
loads. It is not repeated here: a phase marker in three documents is three things to
update and two things to forget.

## The dependency pass — done 2026-09-11

This file previously said its order was provisional and instructed a reader to derive the
real dependencies from the source before expanding past the Phase 1–2 kernel. **That pass
has now been done.** It was done late: work had already reached Phases 3 and 4, and the
delay caused a concrete defect, recorded below.

Roadmap edits still require stated reasons.

### What the book's own structure shows

Within Game Concepts, printed p. 35 (Tests) cross-references p. 37 (attributes), p. 39
(skills) and p. 44 (glitches). Attributes and skills reference nothing in-chapter. They are
leaves; test resolution sits above them.

At chapter scale, counting cross-references of three or more:

```
CHARACTER CREATION  -> GAME CONCEPTS       7     COMBAT      -> GAME CONCEPTS       7
COMBAT              -> CHARACTER CREATION  6     MAGIC       -> GAME CONCEPTS       7
SHOW YOUR TALENTS   -> GAME CONCEPTS       6     MAGIC       -> COMBAT              7
CHARACTER TRAITS    -> GAME CONCEPTS       4     POWERS      -> MAGIC               5
CHARACTER METATYPES -> CHARACTER CREATION  3     INITIATION  -> MAGIC               4
```

Game Concepts is the base chapter every other chapter references into. Magic's subsystems
(Powers, Initiation, Enchanting) depend on Magic. Combat depends on Game Concepts and
Character Creation.

**The coarse phase order below survives this evidence.** It was not the problem.

### The rule that was missing

The defect was not the order of the phases. It was treating a phase as a unit that could be
built in any internal order.

> **Within a phase, build what a rule consumes before the rule.** A mechanic that takes a
> value another rule computes is downstream of whoever computes it, regardless of which
> phase each sits in.

What happened without that rule: Phase 2 shipped test resolution (#4) while the rule that
*produces* its input — dice-pool assembly, printed on the same page as the tests that consume
it — was scheduled nowhere. `SimpleTest.Resolve` took a dice pool as a bare integer across four
merged PRs until #69 supplied one. The same shape appeared three more times: `ConditionMonitor`,
`EdgePool` and pool assembly each took loose `int` ranks because no phase owned character
state, which #70 finally introduced as `CharacterRanks`.

A phase's exit criteria describe what must be *true* at the end of it. They do not describe
what order to build it in. Use the rule above for that.

## Phases

| # | Phase | Exit criteria |
| --- | --- | --- |
| 0 | Repository and source foundation | Solution builds warning-free; canonical gate passes; source boundary hash-verified and tested; `main` protected; agent rails in place |
| 1 | Deterministic randomness and dice kernel | `IRandomSource` pinned against published test vectors; d6 generation layered above it; replay proven by test |
| 2 | Core test-resolution primitives | SR6 hits, glitches, thresholds and opposed tests implemented from a source packet, with structured explainable results |
| 3 | Character attributes, skills, derived mechanics | Attribute and skill vocabulary from the book; derived values verified across their whole printed domain |
| 4 | Edge foundation | Edge gain, loss, caps and Edge Actions, with ordering pinned |
| 5 | Initiative and action economy | Initiative determination, ordering, action allocation |
| 6 | Physical combat | Attack resolution built on phases 2–5 |
| 7 | Damage, recovery, status | Damage application, condition monitors, healing, status effects |
| 8 | Core equipment data and mechanics | Weapon and armor properties as structured data with validating loaders |
| 9 | Remaining core character mechanics | Qualities and the rest of character construction |
| 10 | Magic | Spellcasting, drain, spirits, adepts — core book only |
| 11 | Matrix | Matrix actions, overwatch, cybercombat — core book only |
| 12 | Rigging and vehicles | Vehicle mechanics — core book only |
| 13 | Completeness and conformance audit | Systematic pass against the book; every unimplemented rule visible as an explicit unresolved result rather than silently absent |
| 14 | Stable engine API | Versioned public surface a client can depend on |

## Why this order

Phases 1 and 2 are the deterministic nucleus. Everything above them consumes test
resolution, so a defect there propagates into every later system and is expensive to
discover late. They are built as narrow, separately proven layers rather than one large
first change.

Edge (4) follows test resolution (2) because Edge modifies tests. Combat (6) follows
initiative (5) because it consumes the action economy. Damage (7) follows combat because
it consumes attack outcomes.

Magic, the Matrix and rigging are deliberately late: each is a large subsystem that
mostly reuses the kernel, so building the kernel correctly first is worth more than
starting any of them early. The cross-reference evidence above supports this — Magic's own
subsystems point back at Magic, and Magic points back at Game Concepts and Combat.

Where the evidence is silent, the existing order stands. It says nothing about the relative
order of equipment (8), qualities (9), or rigging (12) beyond their dependence on the kernel,
so those remain a judgement call rather than a derived one.
