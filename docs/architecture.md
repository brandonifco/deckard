# Architecture

Deckard is a deterministic rules engine. Every decision below serves one goal: given the
same inputs, the engine produces the same outputs and the same ordered history, on any
machine, on any run, forever.

## Assemblies

```
Deckard.Core   deterministic primitives          depends on nothing
      ^
Deckard.Data   structured rule values            depends on Core
      ^
Deckard.Rules  Shadowrun Sixth World mechanics   depends on Core + Data
```

Nothing points upward.

`tools/repo-checks.py --only layering` enforces this today, by reading the declared
`ProjectReference` graph in the `.csproj` files. That check is exact and catches a
violation the moment it is written.

The per-assembly runtime tests are a second net that is **not yet load-bearing**: the C#
compiler omits assembly references a compilation does not actually use, so while no
Deckard assembly consumes another, `GetReferencedAssemblies()` returns nothing and those
assertions pass vacuously. They begin catching real violations as soon as Phase 1 code
crosses an assembly boundary. Until then, the declared-graph check is the enforcement.

### Test-support code

`tests/Deckard.Testing` carries fakes and test doubles (starting with
`FixedSequenceRandomSource`) shared across the test projects above. It is not a fourth
layer in the shipped graph: it is `IsPackable=false`, ships to nobody, and
`tools/repo-checks.py --only layering` forbids any `src/` project from referencing it.
`Deckard.Testing` itself may reference `Deckard.Core` only. See the amendment to
[`decisions/0001-architecture-boundaries.md`](decisions/0001-architecture-boundaries.md).

### Core

Deterministic and general. **Core must not touch** the filesystem, a UI or game engine,
ambient time, the network, environment variables, JSON files, or the rulebook.

Eventually: deterministic randomness primitives, dice primitives, value types, stable
IDs, result types, state-transition primitives, event records.

Shadowrun tables and rule policy do not belong here unless genuinely foundational.

### Data

Structured rule values that should not be hard-coded inside algorithms: skills, weapon
and armor properties, gear, qualities, spell definitions, printed tables, other finite
rule vocabulary.

Data is added **only when an Issue requires it**. There is no bulk transcription of the
rulebook, ever.

When structured data begins, its loaders must: reject unknown or malformed members,
enforce required fields, fail on duplicate stable IDs, fail on references to nonexistent
IDs, fail on invalid ranges, and order deterministically. A loader that silently drops
unknown data is a bug. Prefer closed vocabularies over stringly-typed rule concepts.

### Rules

The Shadowrun-specific mechanics: tests and opposed tests, Edge, attributes and skills,
initiative and the action economy, combat, damage, status effects, magic, the Matrix,
rigging and vehicles, character mechanics.

That list describes likely subsystem boundaries. It is not permission to implement them.

## Determinism

The invariant:

```
same rules version + same initial state + same random seed/state + same ordered decisions
    = same outcomes and the same ordered event/roll history
```

Forbidden in engine source, and mechanically blocked by `repo-checks.py --only determinism`:

| Forbidden | Why |
| --- | --- |
| `Random.Shared`, `new Random()` | process-global ambient entropy |
| `RandomNumberGenerator` | cryptographic RNG is not replayable |
| `Guid.NewGuid()` as game state | non-reproducible identity |
| `DateTime.Now` / `UtcNow`, `DateTimeOffset.Now` | ambient clock breaks replay |
| `Environment.TickCount`, `Stopwatch` | process timing is not a rules input |
| `Environment.GetEnvironmentVariable` | the engine must not read its environment |
| `Task.Run`, `.AsParallel()` | non-deterministic resolution order |

Also forbidden by policy, and not mechanically checkable: relying on hash iteration order
to determine anything observable. See
[`decisions/0006-deterministic-ordering-conventions.md`](decisions/0006-deterministic-ordering-conventions.md)
for what counts as observable and the approved and forbidden shapes.

Also forbidden by policy, and not mechanically checkable: using floating-point arithmetic
for discrete rules where exact integer or rational arithmetic is correct. Where Shadowrun
specifies rounding, encode the rounding rule explicitly and pin its boundary cases in
tests.

A genuine exception is opted into per line with `// deckard:allow-nondeterminism <reason>`
and must be justified in the PR. Diagnostics may need it. Rules resolution never does.

## Randomness layering

```
IRandomSource        stable, seedable, specified PRNG
      |
   dice roller       uint32 -> d6
      |
   dice pool         N dice, ordered results
      |
 test resolution     SR6 semantics: hits, glitches, thresholds, Edge
```

Each layer is separately testable, and each boundary is where a bug would otherwise hide.
`System.Random` is not the replay contract — its internal algorithm is an implementation
detail Microsoft may change. See
[`decisions/0002-deterministic-randomness.md`](decisions/0002-deterministic-randomness.md).

Replacing the PRNG later is a **replay-compatibility event**, not a refactor.

## Explainable resolution

The engine must be able to answer *why did this result occur?* Avoid APIs that return
only an opaque number.

A resolved mechanic should preserve, as structured data: the starting pool, each modifier
and its reason, the final pool, the dice actually rolled, any post-roll transformation,
the hit count, the opposition or threshold, and the final outcome.

Structured data first; presentation later. The rules engine does not format text. That
same structure is what makes deterministic replay, debugging, simulation, combat logs,
analytics and AI conformance review possible at all.

## State transitions

Game-affecting operations take the shape:

```
(current state, command, deterministic random source)
    -> resolution
    -> new state
    -> ordered events
```

Avoid systems where arbitrary objects mutate each other through hidden side effects. The
requirement is visibility and reproducibility — not a full event-sourcing framework. Do
not build one.

## Unresolved rules

An unsupported mechanic fails visibly. It never does nothing, returns a default, skips an
effect, substitutes a similar rule, or invents a value. See
[`decisions/0004-unresolved-rule-taxonomy.md`](decisions/0004-unresolved-rule-taxonomy.md).

Exceptions are for programmer errors. "The engine does not support this rule yet" is
normal flow and belongs in the result type.

## Deliberately deferred

Recorded so nobody rebuilds the reasoning: character creation, combat, Edge, magic, the
Matrix, rigging, vehicles, gear and spell corpora, NPCs, UI, Godot, campaign systems,
networking, save formats, AI opponents, procedural encounters, bulk PDF extraction,
rulebook search or RAG infrastructure, orchestration state machines, merge queues,
CODEOWNERS, dashboards and token telemetry.

These are not rejected. They are not day-one problems.
