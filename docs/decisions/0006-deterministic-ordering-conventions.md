# 0006 — Deterministic ordering conventions for observable sequences

## Status

Accepted — 2026-09-11. Governs all engine code from Phase 1 forward; becomes load-bearing
at Phase 2 (Issue #4), the first work that produces ordered dice results and ordered
resolution events.

## Decision

Anything the engine exposes as an observable order — event and history order, dice
results within a roll, initiative order, the order modifiers are applied, the order
random draws are consumed — must be produced by an already-ordered sequence or by an
explicit, total, stable sort. Enumerating a `Dictionary<,>` or `HashSet<>` may never
determine that order, even transitively (building a `List<T>` by copying one out, then
never re-sorting it, is still hash-order-derived). A dictionary used purely as a keyed
lookup, never enumerated into an observable result, is unaffected by this rule.

## Problem

`docs/architecture.md` has forbidden relying on hash iteration order since bootstrap, in
the same sentence that admits the rule is not mechanically checkable. Every sibling
hazard in that section — `Random.Shared`, `DateTime.UtcNow`, `Task.Run`, `.AsParallel()`
— is blocked by `tools/repo-checks.py --only determinism`. This one has never been more
than a sentence, and it is the more dangerous hazard for exactly that reason: it does not
look like a violation when written.

```csharp
foreach (var effect in modifiersByName.Values)   // compiles, reads fine, orders nothing
```

`Random.Shared` announces itself at the call site. Dictionary and hash-set enumeration
order is, by the BCL's own contract, unspecified — not guaranteed stable even between two
runs of the same process, and free to change with a runtime upgrade or a `Remove` call
earlier in the collection's life. Code built on it works today, on this machine, with
this input size, and the resulting non-determinism surfaces later as a replay that
reproduces on one run and not the next, long after the code that caused it looked correct
to everyone who read it.

[`decisions/0002-deterministic-randomness.md`](0002-deterministic-randomness.md) already
established that the *number* of random draws a mechanic makes is part of its observable
contract — consuming an extra value shifts every subsequent result, and a PR that changes
a draw count must say so. Draw *order* is the same property under a different name: two
mechanics drawing the same values in a different order produce a different replay just as
surely as drawing a different count does. Phase 2 (#4) is where this stops being
hypothetical, so the convention has to exist before the code it governs, not be
reconstructed afterward from a bug report.

## Options considered

1. **Rely on review discipline with no written convention.** What has existed since
   bootstrap. Costs nothing to maintain, but gives a reviewer nothing citable to hold a
   diff against — "that looks wrong" is not a standard, it is a mood, and it does not
   survive a reviewer who is in a hurry or an agent who was not told.
2. **A `repo-checks.py` regex flagging enumeration of a dictionary or hash set.** Cheap to
   write. Also indiscriminate: most `foreach`/`.Values`/`.Keys` use in any codebase is a
   pure lookup or a case where order genuinely does not matter, and a pattern that cannot
   tell those apart from the hazardous case fires constantly and gets disabled within a
   week — at which point it protects nothing and its presence lies about being enforced.
3. **A Roslyn analyzer that tracks whether a dictionary/hash-set enumeration's result
   flows into an observable output.** The right eventual shape *if* this becomes a
   recurring real defect — see "Why no analyzer yet" below — but building the dataflow
   analysis now means designing it against a hypothetical AST shape instead of an actual
   failure, with no corpus to validate the false-positive rate against.
4. **A written convention (this ADR), enforced by review, revisited if it fails in
   practice.** Costs one document. Gives reviewers — human or agent — a citable authority
   concrete enough to hold a diff against, which is the actual gap between options 1 and
   this one.

Chosen: 4.

## Chosen design

### What counts as observable ordering

Order is observable when a caller outside the producing code can see it and could notice
a difference — directly, or by it entering the event/replay history. Concretely, at
minimum:

- **Event and resolution history** — the sequence of events a resolved operation records.
- **Dice results within a roll** — the order individual die results appear in a pool's
  result, not only their multiset.
- **Initiative order** — the sequence combatants act in.
- **Modifier application order** — the order modifiers, bonuses, and penalties are applied
  to a pool or a value, wherever that order can affect the outcome (rounding boundaries,
  caps, mutually-exclusive stacking) or is itself recorded for explainability.
- **Random draw order** — the sequence in which values are pulled from an `IRandomSource`,
  per the Problem section above. This is the sharpest case: it is invisible in a single
  run's output and only surfaces as a replay mismatch.

This list is illustrative, not closed. The test is the one above: could two runs with
identical inputs and an identical seed produce a different observable answer because of
it. If yes, it is in scope of this ADR.

### The rule

Anything observable, per the above, is ordered by one of the approved shapes below. Never
by enumerating a `Dictionary<,>` or `HashSet<>` — not directly, and not by copying its
enumeration into a `List<T>`, array, or other sequence type and treating that copy as
already ordered. The type of the container the order ends up in is irrelevant; what
matters is whether hash-bucket order ever determined it, at any point in the pipeline.

### Approved shapes

- **`IReadOnlyList<T>` and arrays**, for sequences whose order is meaningful by
  construction: dice as rolled, an explicit turn order, an event log in the order events
  occurred. No sort is needed because the order was never lost.
- **`OrderBy`/`OrderByDescending`** (and `ThenBy`/`ThenByDescending`) with a key that is
  **total** — no two distinct elements the ordering must distinguish compare equal. Where
  the natural key has ties, break them with an explicit `ThenBy` on a further key (a
  stable ID, an ordinal name comparison) rather than relying on `OrderBy`'s stable-sort
  guarantee to preserve the *input* sequence's order for tied elements. That guarantee is
  only as good as the input order, and an input drawn from dictionary or hash-set
  enumeration has no order to preserve — stability across a boundary the code does not
  control is not determinism, it just moves the hazard one step upstream and hides it
  behind a LINQ call that looks correct.
- **`SortedDictionary<TKey,TValue>` / `SortedSet<T>` with an explicit `IComparer<TKey>`**,
  where a keyed collection is genuinely needed *and* is enumerated into an observable
  result more than once as it is mutated (for example, a live initiative table that is
  repeatedly re-queried in order as entries are added or removed) — as opposed to a
  one-shot collection that only needs sorting once, which an explicit sort produces more
  plainly.

### What stays fine

A `Dictionary<,>` or `HashSet<>` used purely as a lookup — `TryGetValue`, `Contains`,
keyed access — and never enumerated into a result the caller can observe, is unaffected.
The hazard is enumeration order leaking into an observable result, not the existence of a
hash-based map. Reflexively replacing every internal `Dictionary<,>` with a
`SortedDictionary<,>` "to be safe" is not this rule; it is cargo-culting a fix for a
problem that lookup-only usage does not have, at a real and pointless cost in comparer
overhead and code clarity. Getting this distinction backwards is the likeliest way this
ADR fails in practice — see the next section.

### Illustration

```csharp
// Forbidden: order is whatever this run's hash implementation happens to produce.
foreach (var effect in modifiersByName.Values)
    ApplyModifier(effect);

// Forbidden even though the result type looks ordered: the List already inherited the
// dictionary's unspecified enumeration order before anything sorted it.
List<Modifier> applied = modifiersByName.Values.ToList();

// Forbidden: ties on AppliedPhase silently fall back to the dictionary's enumeration
// order, because OrderBy's stability has nothing but that order to preserve.
var ordered = modifiersByName.Values.OrderBy(m => m.AppliedPhase);

// Approved: the key is made total by breaking ties on a stable, explicit second key.
IReadOnlyList<Modifier> ordered = modifiersByName.Values
    .OrderBy(m => m.AppliedPhase)
    .ThenBy(m => m.Id, StringComparer.Ordinal)
    .ToList();

// Approved: the sequence was already ordered at the source; nothing needs sorting.
IReadOnlyList<DieResult> rolled = pool.RollAll(randomSource);

// Fine: the dictionary is a lookup. Nothing observable is built by enumerating it.
if (modifiersByName.TryGetValue(name, out var modifier)) { /* ... */ }
```

### Why no analyzer yet

No engine code exists yet that produces an observable ordered result — Phase 0 was source
and repository scaffolding, and Phase 1 pins an `IRandomSource` and a d6 mapping, not
resolution logic with ordered output. Building a Roslyn analyzer today means designing
its dataflow rules — precisely the part that has to distinguish "enumerated into an
observable result" from "used as a lookup," which is the same distinction the Non-goals
section below rejects a `repo-checks.py` regex for being unable to make — against a
hypothetical shape instead of a real one, with no corpus of real instances to tune its
false-positive rate against. An external review of this repository already warned that
its process machinery (Issue templates, prose-restatement checks, agent rails) risks
outgrowing the engine it exists to protect, which has barely started. Committing to a
dataflow-sensitive analyzer now, before it has a single real defect to justify its
existence, would be exactly that pattern: governance built ahead of the problem it solves.

The condition that justifies building it: a real occurrence, in a merged PR or a
production replay bug report, where `Dictionary<,>`/`HashSet<>` enumeration order leaked
into an observable result — caught late, not in review. At that point there is an actual
AST shape to detect, an actual false-positive rate to test against the existing codebase,
and a real incident to weigh against the ongoing cost of building and maintaining the
check. File the analyzer as its own Issue then, citing the PR or bug that justified it,
rather than speculatively building detection for a pattern that review has caught every
time so far.

## Reasoning

The rule is stated in terms of what happens to be *observable*, not in terms of banning
`Dictionary<,>` outright, because a rules engine legitimately needs keyed lookups
everywhere — skills by name, modifiers by source, effects by target — and a convention
that cannot tell "iterated into a result" from "looked up by key" would either ban a
completely safe and idiomatic pattern or, more likely, get reinterpreted loosely until it
bans nothing. The Issue that opened this ADR names this risk directly: getting the
distinction wrong in the strict direction produces cargo-cult `SortedDictionary` calls
everywhere, which is its own form of the convention failing.

Requiring a *total* sort key, with ties broken explicitly, is the part most likely to be
missed by a reviewer skimming for "is there an `OrderBy`": an `OrderBy` call over data
whose input order is itself undefined is not made deterministic by the presence of the
`OrderBy` if its key has ties. The fix is cheap — one `ThenBy` on a stable identifier —
but it has to be a habit, not a hope.

Treating draw order as governed by this ADR, rather than leaving it implicit under "the
`IRandomSource` layering handles it," closes the specific gap ADR 0002 left open: 0002's
consequences section commits to draw *count* as observable contract but does not name
order. A mechanic that draws the right number of values in the wrong order relative to
another mechanic sharing the same source produces a different replay just as surely.

## Consequences

- Every future Issue that produces an observable sequence — Phase 2 test resolution
  (hits, glitches), Phase 4 Edge ordering, Phase 5 initiative — is reviewable against this
  ADR without re-deriving the rule from first principles.
- A PR introducing `Dictionary<,>`/`HashSet<>` enumeration that feeds an observable result
  is a defect against this ADR, citable in review by number, whether the reviewer is
  human or an agent.
- No mechanical gate exists for this yet. Enforcement is human/agent review until the
  condition in "Why no analyzer yet" is met; that is a deliberate and named gap, not an
  oversight.
- `docs/architecture.md` points at this ADR instead of restating its rule, so the two
  cannot drift the way the determinism ban list already did once (see
  `tools/repo-checks.py --only invariant-drift`, which does not check this ADR because
  there is no restatement left to check).

## Rejected alternatives

- **A `repo-checks.py` regex for dictionary/hash-set `foreach`.** Cannot distinguish a
  lookup from an enumeration that feeds an observable result; fires on the common,
  legitimate case and gets disabled within a week, which is worse than no check because
  it advertises coverage that no longer exists.
- **A Roslyn analyzer now.** No real instance exists yet to design or validate it against;
  see "Why no analyzer yet."
- **Mandating `SortedDictionary`/`SortedSet` for every keyed collection.** Solves a
  problem pure lookups do not have, at a real cost in comparer overhead and readability,
  and is the specific overcorrection this ADR calls out as its likeliest failure mode.
- **Leaving the existing one-sentence policy as-is.** Not mechanically checkable and, as
  written, not concrete enough for a reviewer to hold a diff against — the exact gap
  Issue #39 exists to close.
