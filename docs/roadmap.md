# Roadmap

Phases, dependency order, and exit criteria. **This file does not track tasks** —
GitHub Issues are the only live work queue. If you want to know what to work on, ask
GitHub, not this document.

## Status

Current phase is recorded in [`../CLAUDE.md`](../CLAUDE.md), which every agent already
loads. It is not repeated here: a phase marker in three documents is three things to
update and two things to forget.

## A warning about this ordering

The order below is **provisional**. It is arranged by what later systems require rather
than by the book's chapter order, but it was drafted before reading the rulebook's
actual dependency structure.

Before expanding beyond the Phase 1–2 kernel, use the authoritative source to determine
the real dependencies and revise this file. Do not treat the list below as established
Shadowrun truth. Roadmap edits require stated reasons.

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
starting any of them early.
