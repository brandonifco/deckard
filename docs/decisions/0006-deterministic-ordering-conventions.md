# 0006 — Deterministic ordering conventions for observable sequences

## Status

Accepted — 2026-09-11. Governs engine code from Phase 1 forward; load-bearing at Phase 2
(Issue #4), the first work producing ordered dice results and resolution events.

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
the same sentence admitting the rule is not mechanically checkable — unlike every sibling
hazard in that section (`Random.Shared`, `DateTime.UtcNow`, `Task.Run`, `.AsParallel()`),
which `tools/repo-checks.py --only determinism` blocks outright. It is the more dangerous
hazard for exactly that reason: it does not look like a violation when written.

```csharp
foreach (var effect in modifiersByName.Values)   // compiles, reads fine, orders nothing
```

`Random.Shared` announces itself at the call site. Dictionary and hash-set enumeration
order is, by the BCL's own contract, unspecified: code depending on it depends on
unspecified behaviour regardless of what is observed in practice. Concretely, .NET
randomizes the string hash seed per process, so a `Dictionary<string,_>` enumerates
differently run to run, while a `Dictionary<int,_>` built the same way typically does
not — precisely what lets this bug hide: an integer-keyed version can reproduce for
months and only break once something is keyed by name, the runtime rehashes, or an
earlier `Remove` reshapes the bucket layout.

Draw order extends [ADR 0002](0002-deterministic-randomness.md)'s commitment that draw
*count* is part of a mechanic's observable contract: two mechanics drawing the same
values in a different order replay differently just as surely as a different count does.
Phase 2 (#4) is where this stops being hypothetical, so the convention must exist first.

## Options considered

1. **Leave the existing one-sentence policy as-is, enforced only by review.** The status
   quo; gives a reviewer nothing citable — "that looks wrong" is a mood, not a standard.
2. **A `repo-checks.py` regex flagging dictionary/hash-set enumeration.** Cannot tell an
   enumeration feeding an observable result from a lookup — most `foreach`/`.Values` use
   here is the latter — so it fires on the legitimate case and gets disabled within a
   week, protecting nothing while still claiming to.
3. **A Roslyn analyzer with real dataflow tracking.** Right once this recurs as a real
   defect, but no engine code yet produces an observable ordered result — building it now
   means designing against a hypothetical AST shape with no corpus to tune against.
4. **A written convention (this ADR), enforced by review.** Costs one document; gives
   reviewers — human or agent — an authority concrete enough to hold a diff against.

Chosen: 4.

## Chosen design

### What counts as observable ordering

Order is observable when a caller outside the producing code can see it, or it enters the
event/replay history, and could differ between two runs with identical inputs and seed.
At minimum: event and resolution history; dice results within a roll (not only their
multiset); initiative order; modifier application order (wherever it affects rounding,
caps, or stacking, or is itself recorded for explainability); and random draw order (the
sharpest case — invisible in a single run, surfacing only as a replay mismatch). This
list is illustrative, not closed; the test above decides new cases.

Anything observable, per the above, is ordered by one of the approved shapes below —
never by enumerating a `Dictionary<,>` or `HashSet<>`, directly or via a copy into a
`List<T>`/array treated as already ordered. The container's type is irrelevant; what
matters is whether hash-bucket order determined it anywhere in the pipeline.

### Approved shapes

- **`IReadOnlyList<T>` and arrays**, for sequences ordered by construction — dice as
  rolled, an explicit turn order, an event log in occurrence order — no sort needed.
- **`OrderBy`/`OrderByDescending`/`ThenBy`**, with a key that is **total** — no two
  distinct elements the ordering must distinguish compare equal. Where the natural key
  has ties, break them with an explicit `ThenBy` on a further key (a stable ID, an
  ordinal name comparison) rather than leaning on `OrderBy`'s stable-sort guarantee alone
  — see Reasoning for why that guarantee does not save you here.
- **`SortedDictionary<TKey,TValue>` / `SortedSet<T>` with an explicit `IComparer<TKey>`**,
  when a keyed collection is genuinely needed and re-enumerated into an observable result
  repeatedly as it mutates (a live initiative table re-queried as entries are added or
  removed) — not for a one-shot case, which an explicit sort produces more plainly.

### What stays fine

A `Dictionary<,>`/`HashSet<>` used purely as a lookup — `TryGetValue`, `Contains`, keyed
access, never enumerated into an observable result — is unaffected (see Reasoning).

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

No engine code yet produces an observable ordered result (Options considered #3): there
is no real instance to design a detector against or tune a false-positive rate on.

The condition that justifies one: a real occurrence caught late, not in review — a merged
PR or replay bug where `Dictionary<,>`/`HashSet<>` enumeration order leaked into an
observable result. File it as its own Issue then, citing the incident.

## Reasoning

Requiring a *total* sort key, with ties broken explicitly, is not optional decoration:
`OrderBy`'s stable-sort guarantee only preserves the *input* sequence's order for tied
elements, and input drawn from dictionary or hash-set enumeration has no order to
preserve. Leaning on that guarantee alone just moves the hazard upstream and hides it
behind a call that looks correct. An explicit `ThenBy` on a stable key removes the tie
outright rather than hoping the input's incidental order happens to be sound.

The rule is framed around what is *observable*, not around banning `Dictionary<,>`
outright, because keyed lookups are a routine, safe pattern this engine needs everywhere
— skills by name, modifiers by source, effects by target. A convention that could not
tell a lookup from an enumeration feeding an observable result would either forbid an
idiomatic pattern or get reinterpreted loosely until it protects nothing. Reflexively
replacing every lookup with `SortedDictionary<,>` "to be safe" fails for the same reason
in the opposite direction: it treats the map's existence as the hazard, not what happens
when it is enumerated.

## Consequences

- Every future Issue producing an observable sequence — Phase 2 test resolution, Phase 4
  Edge ordering, Phase 5 initiative — is reviewable against this ADR by number.
- No mechanical gate exists for this yet; enforcement is human/agent review until the
  condition above is met — a deliberate, named gap, not an oversight.
- `docs/architecture.md` points at this ADR instead of restating its rule, removing the
  restatement `--only invariant-drift` would otherwise need to guard against drifting.

## Rejected alternatives

- **A `repo-checks.py` regex.** Cannot distinguish a lookup from an observable
  enumeration — see Options considered #2.
- **A Roslyn analyzer now.** No real instance exists yet to design or validate it against
  — see Options considered #3 and "Why no analyzer yet".
- **Mandating `SortedDictionary`/`SortedSet` for every keyed collection.** Solves a
  problem pure lookups do not have, at a real cost in comparer overhead and readability —
  see Reasoning.
- **Leaving the existing one-sentence policy as-is.** Not concrete enough for a reviewer
  to hold a diff against — see Options considered #1.
