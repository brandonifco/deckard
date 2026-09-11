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
engine. **"Rules version" has none.** It is prose, and nothing in the engine can express,
compare, or refuse to replay across a mismatch in it.

PR #34 made the PCG32 sequence permanent and load-bearing, and Issues #3 and #4 will start
producing pinned expectations derived from seeds. Left alone, the natural and wrong
assumption is that `seed + decisions` suffices to reproduce a result. It does not: the
rules that consumed those draws, and the source baseline those rules encode, are equally
part of the input. A generator swap, a rules revision, or a re-pinned source baseline can
each hold the seed and the decisions constant while changing what they mean — and today,
nothing in the codebase can say so.

## Authoritative source locations

N/A — engine identity, not a Shadowrun mechanic. It carries a value derived from
`.github/source-manifest.json` (`sourceId: sr6-core`), whose authority is settled by
[ADR 0003](0003-source-baseline.md).

## Options considered

1. **A single opaque version integer**, bumped for any change that could affect replay.
   Cheapest, but cannot say *what* changed, and forces unrelated axes (a schema tweak, a
   mechanics revision) through the same number.
2. **A single hash** over the ruleset's compiled representation, the schema, and the PRNG
   identifier. Comparable but not diagnosable, and it couples the identity to an
   implementation detail of how the ruleset happens to be represented in code.
3. **Four independent, separately versioned components composing one identity value** —
   chosen. Each changes for a genuinely different reason (see Reasoning).
4. **Fold the source baseline into the ruleset version.** Conflates a mechanics revision
   Deckard makes with a different pinned book (ADR 0003) — different events with different
   consequences.

## Chosen design

```
Deckard.Core.Replay
├── RandomAlgorithmId            names the PRNG variant a sequence of draws came from
├── RulesetVersion                which ruleset, and at what revision of its mechanics
├── ReplaySchemaVersion            the shape of a recorded replay
├── SourceBaselineId               the manifest's sourceId + sha256, passed in, never read
└── ReplayCompatibilityIdentity   the four above, compared as one value
```

| Type | Carries | Compared by |
| --- | --- | --- |
| `RandomAlgorithmId` | `Name` (e.g. `pcg_setseq_64_xsh_rr_32`) | value |
| `RulesetVersion` | `Id`, `Version` (int) | value |
| `ReplaySchemaVersion` | `Version` (int) | value |
| `SourceBaselineId` | `SourceId` (e.g. `sr6-core`), `Sha256` | value |
| `ReplayCompatibilityIdentity` | one of each above | value, all four |

All five are `readonly record struct` types, so equality is the compiler-derived,
field-by-field comparison rather than a hand-written `Equals` a future edit could leave
out of sync with the fields. `RandomAlgorithmId` additionally exposes
`Pcg32SetSeq64XshRr32`, the one value Deckard produces today, naming PCG32's
`pcg_setseq_64_xsh_rr_32` variant exactly as pinned by [ADR 0002](0002-deterministic-randomness.md).

`SourceBaselineId` carries both `sourceId` and `sha256` handed to it by a caller; it does
not read `.github/source-manifest.json` itself. `Deckard.Core` may not touch the
filesystem (ADR 0001), and `tools/repo-checks.py --only core-filesystem` enforces that
boundary mechanically. Resolving the manifest into a `SourceBaselineId` is a job for
whatever layer is allowed to read it. Nothing here serializes, persists, records, or
replays anything — `ReplayCompatibilityIdentity` exists to be constructed and compared,
not stored.

## Reasoning

**Four components, not one.** #34 permanently pinning the PCG32 sequence is what makes
"the algorithm" and "the seed" separable facts in the first place; collapsing them back
into a single version number would make ADR 0002's rule — replacing the PRNG is a
replay-compatibility event, not a refactor — tribal memory again. The same separation
applies to the ruleset (what mechanics existed) and the schema (what shape a recording
takes): Issues #3 and #4 will revise rules content without touching how a replay is
recorded, and a future recording format could add a field with no mechanics change. A
single counter cannot express that two axes moved independently; four values can.

**`RandomAlgorithmId` is a named value, not a closed enum.** Nothing stops a caller from
constructing an arbitrary one, but Deckard's own code only ever produces and compares
against `Pcg32SetSeq64XshRr32` — satisfying "exactly one value exists today" without a
closed type. A second algorithm is added as a second named `static readonly` field, not
by loosening a closed type.

**`SourceBaselineId` carries `sourceId` *and* `sha256`, both passed in, never read.**
A source-baseline re-pin edits the manifest's `sha256` field of the existing entry in
place and leaves `sourceId` unchanged (ADR 0003; the manifest's own comment: "Changing a
sha256 here ... requires its own Issue, its own PR, and an ADR"). An identifier carrying
only `sourceId` would compare equal across exactly the event it exists to detect — the
error Issue #38 was originally filed with and corrected before implementation; see its
"Known ambiguity" section. Separately, ADR 0001 already forbids Core from touching the
filesystem or JSON, so reaching into the manifest from `Deckard.Core` to obtain either
value would violate that boundary for values the caller already has cheaper access to.

**No serialization here.** ADR 0001 already establishes that Core takes no serializer
dependency, and this Issue is about *detecting* a mismatch, not reconciling one. Building
round-tripping now would be machinery ahead of the code that would consume it.

**`record struct`, not a hand-rolled `Equals`.** An identity type whose own equality is
subtly wrong fails exactly where nobody is looking. Compiler-generated, field-by-field
equality cannot drift from the type's fields the way a hand-written override can after a
field is added and the override is not updated.

## Replay-compatibility events

[ADR 0002](0002-deterministic-randomness.md) already states one rule: replacing the PRNG
is a replay-compatibility event, not a refactor, and requires its own ADR. This ADR
extends that statement across all four identity components rather than leaving the other
three unaddressed:

- **`RandomAlgorithmId` changing** (the algorithm being replaced, or a second one added) is
  unconditionally a replay-compatibility event. This is ADR 0002's existing rule, now a
  value comparison instead of a fact a reviewer has to remember.
- **`SourceBaselineId` changing** (re-pinning the manifest to a different hash, printing,
  or edition) is unconditionally a replay-compatibility event. This is
  [ADR 0003](0003-source-baseline.md)'s existing rule — restated here only to note that it
  also invalidates replay identity, not to add a new obligation.
- **`RulesetVersion` and `ReplaySchemaVersion` bumping** are routine engineering —
  expected every time an Issue adds, corrects, or reinterprets a mechanic, or the
  recorded-replay shape evolves — and do **not** by themselves require a dedicated ADR.
  Either becomes ADR-worthy only when the change meets
  [`docs/decisions/README.md`](README.md)'s existing general criteria (a genuine rules
  ambiguity, a deliberate house rule, a foundational determinism or replay decision) — the
  same standard as any other change, not a new one this ADR invents.

## Consequences

- Any future replay-recording or replay-running code (explicitly out of scope here) must
  construct and compare a `ReplayCompatibilityIdentity` before treating a seed and a
  decision sequence as reproducible.
- `Deckard.Core` continues to reference no other Deckard assembly and touch no filesystem;
  building `SourceBaselineId` from the manifest is the caller's job, not Core's.
- Adding a second `RandomAlgorithmId` value later is the event ADR 0002 already named, now
  visible as a new `static readonly` field rather than a changed constant.
- `RulesetVersion` and `ReplaySchemaVersion` are expected to change often. Nothing here
  requires an ADR for every bump; doing so would misapply the process
  `docs/decisions/README.md` sets out for foundational decisions to ordinary engineering.
- **Known limitation, deliberately left open:** all five types are `readonly record
  struct`s, so a struct's implicit parameterless constructor (`default(T)`, `new T()`, or
  a deserializer setting fields directly) bypasses every constructor above entirely — the
  same hole PR #34 documented for `Pcg32State`. `default(RandomAlgorithmId)`,
  `default(RulesetVersion)`, and `default(SourceBaselineId)` each yield a value whose
  string field(s) are `null` rather than throwing; `default(ReplaySchemaVersion)` is not
  actually a hole, since its only field is an `int` and `0` passes validation anyway.
  `Pcg32.FromState` closes the equivalent hole for `Pcg32State` by re-validating at its
  one consuming entry point; no such entry point exists yet here, so there is nowhere to
  put a second gate without inventing a consumer this Issue does not need. The first code
  that accepts one of these values from outside its own constructor — a future replay
  loader, most likely — must treat a `null` string field as invalid input before trusting
  it. Pinned by test in `tests/Deckard.Core.Tests/Replay/`.

## Rejected alternatives

- **A single opaque version integer** and **a single hash over compiled representation** —
  see Options considered 1–2.
- **Folding the source baseline into the ruleset version** — see Options considered 4.
- **Requiring an ADR for every `RulesetVersion` or `ReplaySchemaVersion` bump.** Would make
  ordinary rules implementation work procedurally indistinguishable from a foundational
  decision, contradicting `docs/decisions/README.md`'s own "when an ADR is not required"
  list.
