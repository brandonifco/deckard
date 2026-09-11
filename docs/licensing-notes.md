# Licensing notes

Factual notes on the repository's licensing and distribution posture. **Nothing
here is legal advice or a legal conclusion.**

## Current state

- The repository is **public**.
- The repository is licensed under **Apache License, Version 2.0** (`LICENSE`).
- `NOTICE` states that the Apache-2.0 grant does not extend to `Deckard.Data`, and
  carries the rightsholder attributions and unaffiliated-project statement also
  reproduced below.
- This posture was decided by Brandon and is recorded in
  [`docs/decisions/0009-licensing-and-distribution-posture.md`](decisions/0009-licensing-and-distribution-posture.md).
  That ADR is the decision record; this file states the resulting facts.

## What is not in this repository

- The Shadowrun, Sixth World rulebook, in any form.
- Extracted chapters, pages, or generated source packets.
- Substantial verbatim rulebook text in code, tests, Issues, PRs or documentation.
- The local filesystem path to Brandon's copy.

The rulebook is © 2019 The Topps Company, Inc.; Shadowrun and Matrix are trademarks of
The Topps Company, Inc.; Catalyst Game Labs is a trademark of InMediaRes Productions, LLC.
Deckard is an unaffiliated personal project and claims no association with any of them.

`.gitignore` and `tools/repo-checks.py --only source-boundary` enforce the boundary
mechanically. `docs/source-handling.md` describes it in full.

## Naming

"Deckard" is a neutral internal codename chosen deliberately so the repository does not
carry a Shadowrun trademark in its name. Any public-facing product name is a separate
decision Brandon has not made, and is recorded as explicitly still open in
[ADR 0009](decisions/0009-licensing-and-distribution-posture.md).

## Settled posture

The five questions this file previously listed as open are answered in
[ADR 0009](decisions/0009-licensing-and-distribution-posture.md):

1. The repository has a license: Apache-2.0, covering the engine.
2. The engine and the rules it implements are separate artifacts, licensed
   separately; implementing a mechanic is not a claim over it.
3. Distributing structured rules data does differ from distributing the engine:
   `Deckard.Data` is explicitly excluded from the Apache-2.0 grant (`NOTICE`).
4. No applicable publisher community-content policy has been identified, and this
   project does not rely on one existing. Commercial distribution requires a
   separately negotiated licence and review by counsel.
5. The repository stays public; the source boundary above is enforced
   mechanically regardless of visibility.

Still explicitly open, per ADR 0009: any public-facing product name, and whether a
commercial product happens at all.

## Guidance going forward

- Do not treat `Deckard.Data` as covered by the Apache-2.0 grant in `LICENSE` — it
  is explicitly excluded; see `NOTICE` and ADR 0009.
- Do not commit rulebook text, and keep short identifying phrases short: a table
  title or a mechanic's name is necessary for provenance; a paragraph of rules
  prose is not.
- Do not publish packages or releases, initiate commercial distribution, or treat
  "Deckard" as a product name — these remain Brandon's decisions; see ADR 0009.
- Raise anything that looks like it changes the posture in ADR 0009 with Brandon
  rather than resolving it in a PR.
