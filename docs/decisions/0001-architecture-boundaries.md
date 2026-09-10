# 0001 — Architecture boundaries and technical baseline

## Status

Accepted — 2026-09-10, at repository bootstrap.

## Decision

Deckard is a C# / .NET 10 library split into three assemblies — `Deckard.Core`,
`Deckard.Data`, `Deckard.Rules` — with a strictly downward dependency graph, built
against an exactly pinned SDK patch with warnings as errors.

## Problem

A rules engine intended to outlive its first client needs boundaries decided before code
exists. Retrofitting a layering rule onto a codebase that has already violated it is
expensive, and the violation usually arrives disguised as convenience: a table constant
placed in an algorithm, a rules concept placed in a primitive, a data loader reaching
into rules logic.

Equally, "deterministic" is not a property that survives a floating SDK. Compiler
versions change code generation, and analyzer versions change what compiles at all.

## Options considered

1. **One assembly.** Simplest. Nothing prevents rules policy leaking into primitives, or
   a future client from depending on internals that were never meant to be public.
2. **Two assemblies (engine, data).** Better, but conflates deterministic primitives with
   Shadowrun-specific policy — exactly the boundary most likely to blur.
3. **Three assemblies (Core, Data, Rules).** Separates general determinism machinery from
   the finite rule vocabulary from the Shadowrun mechanics.
4. **Many fine-grained assemblies per subsystem.** Premature. Subsystem boundaries are
   not yet known from the source, and guessing them now would encode a guess as structure.

## Chosen design

```
Deckard.Core    deterministic primitives           depends on nothing
Deckard.Data    structured rule values             depends on Core
Deckard.Rules   Shadowrun Sixth World mechanics    depends on Core + Data
```

`Core` may not touch the filesystem, a UI, ambient time, the network, environment
variables, JSON, or the rulebook.

Technical baseline: `net10.0`; SDK pinned to `10.0.111` with `rollForward: disable`;
`Nullable`, `ImplicitUsings`, `Deterministic`, `TreatWarningsAsErrors`, `AnalysisLevel
latest`; UTF-8 without BOM, LF endings, one trailing newline. Central package management
so every external dependency has a single version definition.

## Reasoning

The layering is enforced twice, deliberately, because the two mechanisms fail differently.
`tools/repo-checks.py --only layering` reads the declared `ProjectReference` graph and
catches intent the moment it is written. The per-assembly runtime tests read the compiled
output and catch what actually shipped.

Neither alone is sufficient: the C# compiler omits assembly references a compilation does
not use, so the runtime check cannot assert that a declared dependency exists — only that
no forbidden one does. That is why the runtime assertions are one-directional and the
declared graph is checked separately and exactly.

`rollForward: disable` rather than `latestFeature` because a feature-band roll-forward is
still a different compiler. For a project whose central claim is byte-identical
reproducibility, "close enough SDK" is the wrong default.

## Consequences

- CI must install exactly `10.0.111`. An SDK upgrade is a deliberate, reviewable change to
  `global.json`, not something that happens because a runner image was rebuilt.
- Adding a `ProjectReference` that violates the graph fails the canonical gate locally,
  before review.
- Warnings cannot be suppressed globally to obtain a green build. A specific, justified
  suppression is fine; a blanket `NoWarn` is not.

## Rejected alternatives

- **Floating `10.0.x`.** Silently admits a different compiler.
- **Single assembly.** No mechanical way to state or enforce the boundaries.
- **Subsystem assemblies now.** Encodes a guess about Shadowrun's structure before reading
  the book's actual dependencies.
