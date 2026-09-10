# 0003 — Authoritative source baseline and errata policy

## Status

Accepted — 2026-09-10. Selected by Brandon at bootstrap.

## Decision

The single authoritative source is the **Shadowrun, Sixth World core rulebook, First
Printing (© 2019 The Topps Company, Catalyst Game Labs)**, pinned by SHA-256 in
`.github/source-manifest.json`. Official errata are **not** included in the baseline.

## Problem

"Shadowrun 6E" does not name one document. The original 2019 core rulebook, Seattle City
Edition, Berlin City Edition and Hong Kong City Edition differ in rules text. Catalyst has
also published errata. Without an explicit, verifiable baseline, an engine claiming
fidelity cannot say fidelity *to what* — and two agents can implement contradictory rules
while both believing they followed the book.

## Authoritative source identification

| | |
| --- | --- |
| Title | *Shadowrun, Sixth World* |
| Printing | First Printing by Catalyst Game Labs, an imprint of InMediaRes Productions, LLC |
| Copyright | © 2019 The Topps Company, Inc. |
| PDF pages | 322 |
| Page numbering | printed page = PDF page − 1, verified at PDF pp. 30, 50, 100, 200, 300 |
| SHA-256 | `7db88be98dbc2a4d1f777b7ff10034af3ea35e20bb3899b2aad95b7232bbac41` |

Identified from the book's own credits page, which carries the 2019 Topps copyright
notice and the words "First Printing by Catalyst Game Labs". The running header reads
*Shadowrun: Sixth World*, with no City Edition designation.

## Options considered

1. **Original 2019 core rulebook.** The book Brandon owns and selected.
2. **A City Edition.** A different book, not a newer version of this one. Not owned, not
   selected, and adopting one would silently change rules text.
3. **Core plus official errata now.** Higher fidelity to designer intent, but requires a
   second document Brandon has not designated, and doubles the source surface before a
   single mechanic exists.
4. **Whatever an agent finds convenient.** The failure mode this ADR exists to prevent.

## Chosen design

One pinned source, verified before every extraction. `tools/source-slice.py` has no
`--file` argument — there is no path by which an agent can substitute another document,
and a test fails if that flag ever appears.

Errata are excluded for now. This is a **deliberate, revisable choice**, not an oversight,
and not a judgement that errata are wrong.

## Reasoning

Pinning by hash rather than by title makes the baseline mechanically checkable. A title is
a claim; a hash is a fact. A wrong printing, a different scan, or a corrupted download all
fail loudly and extract nothing, rather than producing plausible text from the wrong book.

Excluding errata keeps bootstrap honest. Adding a second authority level before any
mechanic exists means every future rule carries a question — *core or errata?* — that
nobody has needed to answer yet. Adding errata later is a clean, reviewable change to a
manifest with an ADR attached. Removing them once mechanics depend on them is not.

The printed-to-PDF page offset is recorded in the manifest rather than left to each agent
to rediscover, because off-by-one page errors are how an agent ends up confidently
implementing the wrong table.

## Consequences

- The rulebook is never committed. Not the file, not chapters, not packets, not
  substantial excerpts. Enforced by `.gitignore` and by
  `tools/repo-checks.py --only source-boundary`.
- Each developer configures their own copy through `SR6_CORE_PDF` or the gitignored
  `source.local.json`.
- CI never possesses the book. The tooling tests are hermetic against synthetic PDFs, so
  the source boundary is proven without the source.
- **Changing the pinned hash requires its own Issue, its own PR, and a superseding ADR.**
  Editing the manifest to make a failing hash check pass is prohibited.
- Where the core book refers to a supplement, the engine records the dependency and
  returns an explicit unresolved result. It does not implement the supplement.

## Rejected alternatives

- **Naming the edition without pinning a hash.** Unverifiable; two people can hold
  different files both called "SR6 core".
- **Allowing `--file`.** Destroys every provenance claim in the repository at once.
- **Silently treating a newer printing as authoritative.** A newer book is a different
  baseline, and the change must be visible and reviewable.
