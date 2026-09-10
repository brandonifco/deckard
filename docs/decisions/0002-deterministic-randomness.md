# 0002 — Deterministic randomness architecture

## Status

Accepted — 2026-09-10, at repository bootstrap. Implemented in Phase 1.

## Decision

Randomness enters the engine only through an injected `IRandomSource` abstraction backed
by **PCG32** (`pcg_setseq_64_xsh_rr_32`), pinned against sequence fixtures derived from
the published reference implementation. `System.Random` is never the replay contract.

## Problem

Deckard's central claim is that the same seed and the same ordered decisions reproduce
the same outcomes and the same ordered history — across machines, across runtimes, and
across years. Everything downstream depends on it: replay, regression tests, simulation,
debugging a combat log, and any future save format.

`System.Random` cannot carry that claim. Its algorithm is an unspecified implementation
detail, and it has already changed once: .NET Core 3.0 and .NET 6 altered the sequence
produced for a given seed. A runtime upgrade would silently invalidate every stored replay
and every test that pinned an expected roll — with a green build and no error anywhere.

## Options considered

1. **`System.Random` with a fixed seed.** Zero work. Not a stable contract; already broken
   once by a runtime upgrade, and the breakage would be silent.
2. **A cryptographic RNG.** Unseedable by design; the opposite of what replay needs.
3. **xoshiro256\*\*.** Fast, well specified, good statistical quality — but it is also what
   .NET uses internally for `Random`, which invites exactly the confusion this decision
   exists to prevent.
4. **PCG32.** Small, precisely specified, published reference implementation, trivially
   portable, 64-bit state with a selectable stream.
5. **Mersenne Twister.** Well known, but 2.5 KB of state, poor seeding characteristics,
   and far more machinery than a dice engine needs.

## Chosen design

```
IRandomSource        uint NextUInt32()  -- stable, seedable, specified
      |
   dice roller       uint32 -> d6, with the rejection rule pinned by test
      |
   dice pool         N dice, ordered results preserved
      |
 test resolution     SR6 semantics: hits, glitches, thresholds, Edge
```

Layers are separate types with separate tests. Unit tests of higher layers inject a
fixed-sequence source rather than a real PRNG, so a test of glitch detection tests glitch
detection and not the generator.

Phase 1 must pin the PCG32 sequence against vectors **derived from the published reference
implementation** — obtained by running or faithfully porting the reference C code, not
recalled from model memory or copied from a blog. Vectors nobody verified are worse than
none, because they look like evidence.

## Reasoning

The layering matters as much as the algorithm. Collapsing "generate a number", "roll a
d6", and "resolve an SR6 test" into one class means a change to any of them can silently
alter the others, and it makes the uniform-d6 mapping — a genuine source of subtle bias —
untestable in isolation.

PCG32 over xoshiro256\*\* is mostly a legibility argument. Both are adequate. But sharing
an algorithm with `System.Random`'s internals makes it perpetually unclear, to humans and
agents alike, whether a given piece of code depends on the framework's behaviour or the
engine's. A distinct algorithm makes that question unaskable.

The d6 mapping must reject rather than modulo. `value % 6` biases low faces, subtly enough
to survive casual inspection and badly enough to distort dice-pool statistics — which is
precisely what a Shadowrun engine computes.

## Consequences

- Replacing the PRNG later is a **replay-compatibility event**, not a refactor. It
  invalidates stored replays and pinned expectations, and requires its own ADR.
- Every rules operation that consumes randomness must take an `IRandomSource` parameter.
  There is no ambient fallback to reach for, by design.
- `Random.Shared`, `new Random()` and `RandomNumberGenerator` are mechanically blocked in
  engine source by `tools/repo-checks.py --only determinism`.
- The number of random draws a mechanic makes becomes part of its observable contract:
  consuming an extra value shifts every subsequent result. Changes to draw counts must be
  called out in the PR's determinism section.

## Rejected alternatives

- **`System.Random` as the contract.** Silently unstable across runtimes.
- **Ambient/static RNG for convenience.** Removes the seam that makes tests deterministic.
- **Modulo mapping to d6.** Biased.
- **Deferring the choice to the implementing PR.** A foundational replay decision made
  inside a diff is a decision nobody can find later.
