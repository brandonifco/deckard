# Deckard

A deterministic rules engine implementing **Shadowrun, Sixth World** from one
hash-pinned core rulebook. Not a game. No UI, no client, no campaign layer. Future
clients (a game, a simulator, an encounter builder, character tooling) will consume
this as a library.

This file is the governing contract for every Claude task in this repository. It is
kept short on purpose: read the routed document when you need the detail, not before.

---

## Current phase

<!-- This is the single authority for current phase. README.md and docs/roadmap.md link
     here rather than restating it; repo-checks enforces that. -->

**Phase 0 — repository and source foundation.** Complete.
**Phase 1 — deterministic randomness and the dice/test kernel.** Next.

No Shadowrun mechanic is implemented yet. Do not add one outside its own Issue.
Phase order and exit criteria: `docs/roadmap.md`.

## Source authority

Ranked. Lower never overrides higher.

1. The hash-pinned core rulebook (`sr6-core` in `.github/source-manifest.json`)
2. Official errata Brandon has explicitly chosen to include — **currently none**
3. Accepted ADRs in `docs/decisions/`
4. Structured rules data in `Deckard.Data`
5. Code
6. Tests
7. Everything else — including your own memory of Shadowrun

Code and tests are not authoritative because they exist. **If a test contradicts the
book, the test may be wrong.** Wikis, forums, VTT implementations, character builders,
previous editions and supplements are never authority. Never implement a mechanic from
memory: get a source packet.

```bash
tools/source-slice.py --printed-pages 44-47 --expect "Edge Action"
```

## Global invariants

1. **Determinism.** Same rules version + same initial state + same seed + same ordered
   decisions ⇒ same outcomes and same ordered event history. No ambient randomness, no
   ambient clock, no order-dependent iteration, no floating point for discrete rules.
   Enforced by `tools/repo-checks.py`.
2. **Fail visibly.** An unsupported or unresolved mechanic must never do nothing, return
   a default, skip an effect, substitute a similar rule, or invent a value. It returns an
   explicit unresolved result. See `docs/decisions/0004-unresolved-rule-taxonomy.md`.
3. **Provenance.** Every rules change cites `SR6 Core / <section> / printed p. X / PDF p. Y`.
4. **The book never enters the repository.** Not the PDF, not chapters, not source packets,
   not substantial excerpts. Packets are ephemeral and gitignored.
5. **Layering.** `Core` ← `Data` ← `Rules`. Nothing points upward. Core touches no
   filesystem, UI, clock, network, or environment.

## GitHub work protocol

**GitHub Issues are the only live work queue.** Not this file, not the README, not the
roadmap, not chat history. If a future agent must know it, persist it in an Issue, an
ADR, a guide, code, or a test.

One concern → one Issue → one worktree → one branch → one PR → `Closes #NNN` → merge.
Found unrelated work? File another Issue. Do not enlarge the current one.

```bash
tools/dispatch-agent.sh <issue-number>     # isolated worktree, outside this repo
```

- Never push to `main`. Never `git add -A` or `git add .` — stage explicit paths.
- The primary checkout stays on `main`, clean. Implementation happens in a worktree.
- A PR must close exactly one Issue and contain no unrelated changes.

A hook blocks mutation of the primary checkout and bulk staging. Two environment
variables affect it, and nothing else does:

| Variable | Effect |
| --- | --- |
| `DECKARD_ALLOW_PRIMARY_MUTATION=1` | Escape hatch. Permits one sanctioned primary-checkout command — fast-forwarding after a merge, or a bootstrap-style commit. Say in the PR or report why it was needed. |
| `DECKARD_WORKTREE_ROOT` | Where worktrees are created. Defaults to `~/deckard-worktrees`. Must resolve outside this repository; `dispatch-agent.sh` refuses otherwise. |

## Canonical validation

One gate. Humans, Claude, subagents and CI all call the same command.

```bash
./scripts/validate.sh full
```

`fast` (Debug only) for the inner loop, `sdk-pin` to check the SDK alone.
`./scripts/doctor.sh` reports environment and source state.

## Stop and ask Brandon

Do not decide these yourself, and do not bury them in a PR:

- which rulebook or printing is authoritative, or whether errata are included
- a genuine source ambiguity with gameplay consequences
- a deliberate house rule or deviation from the book
- scope expansion beyond the core rulebook
- public distribution, licensing, or product naming
- a foundational architecture change not already settled here

Everything routine is yours to decide. Once Brandon decides, persist it immediately —
an ADR or an Issue, never conversation memory.

## Routing

| You need | Read |
| --- | --- |
| Layering, determinism, state transitions | `docs/architecture.md` |
| Source packets, provenance, page numbering | `docs/source-handling.md` |
| What is in and out of scope | `docs/scope.md` |
| Phase order and exit criteria | `docs/roadmap.md` |
| Agent roles and review packets | `docs/agent-team.md` |
| Past decisions and their reasoning | `docs/decisions/` |
| Subsystem specifics | `docs/guides/` |
| Copyright and distribution posture | `docs/licensing-notes.md` |
