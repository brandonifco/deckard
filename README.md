# Deckard

A deterministic rules engine implementing **Shadowrun, Sixth World** from a single
hash-pinned core rulebook.

Deckard is not a game. It is the layer a game would sit on: given a state, a command and
a seed, it resolves a rule and explains how. A future client — a game, a simulator, an
encounter builder, character tooling — consumes it as a library.

> **Status:** no Shadowrun mechanic is implemented yet — this is the foundation the
> mechanics will stand on. Current phase is recorded in [`CLAUDE.md`](CLAUDE.md).

## What "deterministic" means here

```
same rules version + same initial state + same seed + same ordered decisions
    = same outcomes and the same ordered event history
```

No ambient randomness, no ambient clock, no order-dependent iteration, no floating-point
arithmetic for discrete rules. These are mechanically enforced, not merely documented:
`tools/repo-checks.py` fails the build on `Random.Shared`, `DateTime.UtcNow`,
`Guid.NewGuid()`, `Task.Run`, `.AsParallel()` and their relatives in engine source.

The second half of the contract is that the engine **fails visibly**. An unsupported rule
never quietly returns a default or substitutes something similar — it returns an explicit
unresolved result saying which rule and why. An engine honest about its 40% is useful; one
that guesses at the other 60% is not.

## The rulebook is not in this repository

Deckard implements a commercial copyrighted book. The book is never committed — not the
file, not chapters, not extracts. Contributors configure a path to their own copy, and a
tool serves small hash-verified excerpts to whoever is implementing a rule.

```bash
export SR6_CORE_PDF=/absolute/path/to/your/own/copy.pdf
tools/source-slice.py --printed-pages 44-47 --expect "Edge Action"
```

The source is pinned by SHA-256. A wrong printing, a different scan or a corrupt file
fails loudly and extracts nothing. There is deliberately no way to point the tool at a
different document. See [`docs/source-handling.md`](docs/source-handling.md).

**Licensing:** this repository is public but carries **no license**, which means default
copyright and no grant to anyone. That is an open question, not a settled position — see
[`docs/licensing-notes.md`](docs/licensing-notes.md).

## Layout

```
src/Deckard.Core     deterministic primitives        depends on nothing
src/Deckard.Data     structured rule values          depends on Core
src/Deckard.Rules    Shadowrun mechanics             depends on Core + Data
tests/               one test project per assembly
tools/               source packets, repo checks, agent dispatch
scripts/             validate.sh, doctor.sh
docs/                architecture, scope, roadmap, decisions
```

## Getting started

```bash
./scripts/doctor.sh
```

Reports what is present and what is missing, and never changes anything. The build, the
tests and CI do not need the rulebook; only rules work does.

```bash
./scripts/bootstrap-dotnet.sh
```

Installs the exact SDK patch `global.json` pins into a repo-local `.dotnet/` in the
primary checkout — gitignored, and something `git clean -xfd` would delete — rather than
`$HOME` or system-wide, so no global `PATH` change is ever needed and the SDK on `PATH`
stays whatever it already was for every other project on the machine. `validate.sh` and
`doctor.sh` both prefer it automatically once it exists, including from a worktree. A
previously hand-installed SDK outside the repository is superseded by this and can be
removed.

```bash
./scripts/validate.sh full
```

The canonical gate. Humans, agents and CI all run this exact command — there is no second
definition of "acceptable" living in a workflow file. `fast` for the inner loop,
`sdk-pin` to check the SDK alone.

Requires the exact .NET SDK patch pinned in `global.json`, Python 3, and
`poppler-utils` for source extraction.

## Working on it

GitHub Issues are the only work queue. One concern, one Issue, one branch, one PR,
`Closes #NNN`.

Implementation happens in a worktree outside the repository; the primary checkout stays on
`main` and clean.

```bash
tools/dispatch-agent.sh <issue-number>
```

[`CLAUDE.md`](CLAUDE.md) is the governing contract for agents and the fastest orientation
for humans.

## Disclaimer

Deckard is an unaffiliated personal project. *Shadowrun* and *Matrix* are trademarks of
The Topps Company, Inc.; the rulebook is © 2019 The Topps Company, Inc.; Catalyst Game Labs
is a trademark of InMediaRes Productions, LLC. This project is not endorsed by or
associated with any of them.
