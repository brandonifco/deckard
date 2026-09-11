# 0005 — Replay compatibility identity

## Status

Accepted — 2026-09-10. Implemented in Phase 1 (Issue #38).

## Decision

A replay is reproducible only when four independently-versioned components all match: the
random algorithm that produced its draws, the ruleset revision that consumed them, the
schema a recorded replay is shaped by, and the source baseline those rules encode. Deckard
represents this as `ReplayCompatibilityIdentity`, a value composed of `RandomAlgorithmId`,
`RulesetVersion`, `ReplaySchemaVersion`, and `SourceBaselineId`, all in
`Deckard.Core.Replay`. Two identities are compatible only when every component compares
equal.

## Problem

CLAUDE.md and `docs/architecture.md` state the engine's central claim as:

```
same rules version + same initial state + same random seed/state + same ordered decisions
    = same outcomes and the same ordered event/roll history
```

Three of those four terms already have a representation: initial state is engine state,
random seed/state is `Pcg32State`, and ordered decisions are whatever a caller feeds the
engine. **"Rules version" has none.** It is prose, and nothing in the engine can express
it, compare it, or refuse to replay across a mismatch.

PR #34 made the PCG32 sequence permanent and load-bearing. Issues #3 and #4 will start
producing pinned expectations derived from seeds. Left alone, the natural and wrong
assumption is that `seed + decisions` suffices to reproduce a result. It does not: the
rules that consumed those draws, and the source baseline those rules encode, are equally
part of the input. A generator swap, a rules revision, or a re-pinned source baseline can
each hold the seed and the decision sequence constant while changing what they mean —
and today, nothing in the codebase can say so.

## Authoritative source locations

N/A. This ADR defines engine identity, not a Shadowrun mechanic. It carries a value
derived from `.github/source-manifest.json` (`sourceId: sr6-core`), whose own authority is
settled by [ADR 0003](0003-source-baseline.md).

## Options considered

1. **A single opaque version integer**, bumped for any change that could affect replay.
   Cheapest to build. Rejected: it conflates axes that change for unrelated reasons — a
   pure schema revision (adding a diagnostic field to a recorded replay) would force the
   same bump as a mechanics change, and a mismatch could not say *what* changed, only
   *that* something did.
2. **A single hash** over the ruleset's compiled representation, the schema, and the PRNG
   identifier. Comparable, but not diagnosable, and it would couple this ADR to how the
   ruleset happens to be represented in code today — a refactor with no behavioural change
   could still flip the hash.
3. **Four independent, separately versioned components composing one identity value** —
   chosen. Each component changes for a genuinely different reason: the PRNG algorithm
   changing (rare, deliberate, already an ADR 0002 event), the ruleset revising as
   mechanics are implemented or corrected (routine engineering), the replay schema
   revising as recorded shape evolves (routine, unrelated to rules content), and the
   source baseline being re-pinned (rare, already an ADR 0003 event). Separating them
   makes each axis both comparable and diagnosable on its own.
4. **Fold the source baseline into the ruleset version** rather than keeping it separate.
   Rejected: a ruleset revision (a mechanics change Deckard makes) and a source baseline
   change (a different pinned book, per ADR 0003) are different events with different
   consequences. Conflating them would make a manifest hash update look identical to an
   ordinary code change, which defeats the point of ADR 0003 already treating the manifest
   hash as its own reviewable event.

## Chosen design

```
Deckard.Core.Replay
├── RandomAlgorithmId            names the PRNG variant a sequence of draws came from
├── RulesetVersion                which ruleset, and at what revision of its mechanics
├── ReplaySchemaVersion            the shape of a recorded replay
├── SourceBaselineId               the manifest's sourceId, passed in, never read
└── ReplayCompatibilityIdentity   the four above, compared as one value
```

| Type | Carries | Compared by |
| --- | --- | --- |
| `RandomAlgorithmId` | `Name` (e.g. `pcg_setseq_64_xsh_rr_32`) | value |
| `RulesetVersion` | `Id`, `Version` (int) | value |
| `ReplaySchemaVersion` | `Version` (int) | value |
| `SourceBaselineId` | `SourceId` (e.g. `sr6-core`) | value |
| `ReplayCompatibilityIdentity` | one of each above | value, all four |

All five are `readonly record struct` types, so equality is the compiler-derived,
field-by-field comparison rather than a hand-written `Equals` a future edit could silently
leave out of sync with the fields. `RandomAlgorithmId` additionally exposes
`Pcg32SetSeq64XshRr32`, the one value Deckard produces today, naming PCG32's
`pcg_setseq_64_xsh_rr_32` variant exactly as pinned by [ADR 0002](0002-deterministic-randomness.md)
and implemented by `Deckard.Core.Randomness.Pcg32`.

`SourceBaselineId` carries a `sourceId` string handed to it by a caller. It does not read
`.github/source-manifest.json`, parse JSON, or touch a file path — `Deckard.Core` may not
touch the filesystem (ADR 0001; `docs/architecture.md`), and `tools/repo-checks.py --only
determinism` / `--only layering` both enforce that boundary mechanically. Resolving the
manifest into a `SourceBaselineId` is a job for whatever layer is allowed to read it.

Nothing here serializes, persists, records, or replays anything. `ReplayCompatibilityIdentity`
exists to be constructed and compared, not stored.

## Reasoning

**Why four components and not one.** The Issue's own evidence is #34 permanently pinning
the PCG32 sequence: from that point on, "the algorithm" and "the seed" are separable
facts, and conflating them into a single version number would make ADR 0002's own rule —
"replacing the PRNG is a replay-compatibility event, not a refactor" — a matter of
tribal memory again, exactly what this ADR exists to prevent. The same separation applies
to the ruleset (what mechanics existed) and the schema (what shape a recording takes):
Issues #3 and #4 will revise rules content without touching how a replay is recorded, and
a future recording format could add a field without any mechanics changing. A single
counter cannot express "these two axes moved independently" — only four independent
values can.

**Why `RandomAlgorithmId` is a named value and not a free string used inline.** Nothing
stops a caller from constructing an arbitrary `RandomAlgorithmId`, but Deckard's own code
only ever produces and compares against
`RandomAlgorithmId.Pcg32SetSeq64XshRr32`. This satisfies the Issue's acceptance criterion
— exactly one value exists today — while remaining an ordinary constructible value rather
than a closed enum: the moment a second algorithm exists, it is added the same way this
one was, as a second named `static readonly` field, not by loosening a closed type.

**Why `SourceBaselineId` is "passed in, never read."** ADR 0001 already forbids Core from
touching the filesystem, JSON, or the rulebook. Reaching into
`.github/source-manifest.json` from `Deckard.Core` would violate that boundary for the sole
purpose of building a value that a caller already has cheaper access to. Keeping this a
plain wrapper around a string means Core's replay identity concept and the manifest's
concrete file format never couple.

**Why no serialization here.** ADR 0001 already establishes that Core takes no serializer
dependency. This Issue is explicitly about *detecting* a mismatch, not reconciling one, so
adding round-tripping now would build machinery ahead of the code that would consume it —
exactly the premature-generality failure ADR 0001's rejected alternatives already warn
against for assemblies, and the same reasoning applies at the type level.

**Why `record struct` rather than a hand-rolled class with a custom `Equals`.** A rules
engine's central claim is reproducibility; an identity type whose own equality is subtly
wrong is worse than no identity type, because it fails exactly where nobody is looking.
The compiler-generated, field-by-field equality on a `record struct` cannot drift from the
type's fields the way a hand-written override can after a field is added and the override
is not updated.

## Replay-compatibility events

[ADR 0002](0002-deterministic-randomness.md) already states one rule: "Replacing the PRNG
later is a replay-compatibility event, not a refactor... requires its own ADR." This ADR
extends that statement across all four components of the identity it defines, rather than
leaving the other three unaddressed:

- **`RandomAlgorithmId` changing** (the PRNG algorithm being replaced, or a second
  algorithm being added) is unconditionally a replay-compatibility event. This is ADR
  0002's existing rule; nothing about it changes here except that it is now a value
  comparison instead of a fact a reviewer has to remember.
- **`SourceBaselineId` changing** (re-pinning `.github/source-manifest.json` to a
  different hash, printing, or edition) is unconditionally a replay-compatibility event.
  This is [ADR 0003](0003-source-baseline.md)'s existing rule — "changing the pinned hash
  requires its own Issue, its own PR, and a superseding ADR" — restated here only to make
  clear that it also invalidates replay identity, not to add a new obligation.
- **`RulesetVersion` bumping** is routine engineering, expected every time an Issue adds,
  corrects, or reinterprets a mechanic, and does **not** by itself require a dedicated
  ADR. It becomes ADR-worthy only when the change meets
  [`docs/decisions/README.md`](README.md)'s existing general criteria (a genuine rules
  ambiguity, a deliberate house rule, a foundational determinism or replay decision) — the
  same standard as any other change, not a new one this ADR invents.
- **`ReplaySchemaVersion` bumping** is likewise routine, expected as recorded-replay shape
  evolves once something actually records one, and does **not** by itself require a
  dedicated ADR, for the same reason and under the same existing general criteria.

Put plainly: the two components already governed by their own standing rule (the PRNG
algorithm, the source baseline) stay governed by that rule, now checkable as a value
instead of aspirational. The two components that did not previously have a standing rule
(the ruleset revision, the replay schema) do not gain a new one here — they gain the
ability to be *named and compared*, which is this Issue's whole job.

## Consequences

- Any future replay-recording or replay-running code (explicitly out of scope here) must
  construct and compare a `ReplayCompatibilityIdentity` before treating a seed and a
  decision sequence as reproducible. It never assumes `seed + decisions` alone suffices.
- `Deckard.Core` continues to reference no other Deckard assembly and touch no filesystem;
  building `SourceBaselineId` from the manifest is the caller's job, not Core's.
- Adding a second `RandomAlgorithmId` value later is not a routine addition — it is the
  event ADR 0002 already named, now visible as a new `static readonly` field rather than a
  changed constant.
- `RulesetVersion` and `ReplaySchemaVersion` are expected to change often. Nothing about
  this ADR should be read as requiring an ADR for every bump; doing so would misapply the
  process docs/decisions/README.md sets out for genuinely foundational decisions to
  ordinary engineering.

## Rejected alternatives

- **A single opaque version integer.** Cannot express which axis moved; forces unrelated
  changes (a schema tweak, a mechanics revision) through the same number.
- **A single hash over compiled representation.** Not diagnosable, and couples the
  identity to an implementation detail of how the ruleset is represented in code.
- **Folding the source baseline into the ruleset version.** Conflates two events ADR 0003
  and this ADR both want to keep distinct and separately reviewable.
- **Requiring an ADR for every `RulesetVersion` or `ReplaySchemaVersion` bump.** Would
  make ordinary rules implementation work — exactly what Phases 2 onward consist of —
  procedurally indistinguishable from a foundational decision, contradicting
  `docs/decisions/README.md`'s own "when an ADR is not required" list.
