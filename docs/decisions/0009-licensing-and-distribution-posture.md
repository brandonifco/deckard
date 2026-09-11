# 0009 — Licensing and distribution posture

## Status

Accepted — 2026-09-11. Decided by Brandon in the decision comment on Issue #5
("Decision recorded — Brandon, 2026-09-11"). This ADR transcribes that decision; it
does not extend or reinterpret it. **Nothing in this ADR is legal advice or a legal
conclusion** — see `docs/licensing-notes.md`.

## Decision

1. The repository is licensed under **Apache License, Version 2.0** (`LICENSE`),
   covering the Deckard engine. Brandon retains copyright and may dual-license or
   relicense future versions.
2. The engine code and the Shadowrun rules it implements are separate artifacts,
   licensed separately. The engine is original expression and is Brandon's to
   license. The rules the engine implements are not licensed by this repository —
   implementing a mechanic is not a claim over it.
3. **`Deckard.Data` (the structured Shadowrun rules data under `src/Deckard.Data`)
   is explicitly excluded from the Apache-2.0 grant.** `NOTICE` states that the
   grant does not extend to it. This exclusion is not an admission that the
   repository lacks a right to license that data — it is a refusal to grant rights
   that may not be Brandon's to grant, because whether they are is exactly the
   question `docs/licensing-notes.md` (open question 2 and 3, pre-decision)
   declined to answer.
4. No open Shadowrun licence — no SRD, OGL, or ORC equivalent — has been
   identified, and this project does not rely on one existing. The repository is
   non-commercial and unaffiliated. Any commercial distribution requires a
   separately negotiated licence from the rightsholders and review by counsel;
   neither is in progress, and neither is an agent's to initiate.
5. The repository **stays public**. The source boundary (no rulebook content
   committed) is already enforced mechanically by `tools/repo-checks.py --only
   source-boundary`, so visibility does not by itself expose rulebook content.

### Explicitly still undecided

- **Any public-facing product name.** "Deckard" is an internal codename, not a
  product name, and nothing promotes it to one by default.
- **Whether a commercial product happens at all**, and on what terms.

## Problem

The repository was public from bootstrap with no license file, which under default
copyright grants no rights to anyone. `docs/licensing-notes.md` recorded this as a
deliberate non-decision and listed five open questions (Issue #5, acceptance
criteria 1–5), each requiring a decision only Brandon could make: whether to
license at all and under what terms, how engine code relates to the rules it
implements, whether structured rules data should be treated differently from the
engine, whether an applicable publisher community-content policy exists, and
whether the repository should stay public while those questions were open. This
ADR records Brandon's answers to all five.

## Options considered

Per Brandon's decision comment on Issue #5:

1. **MIT license for the engine.** Simpler, but lacks an explicit patent grant and
   trademark clause.
2. **A copyleft license (e.g. GPL/AGPL family) for the engine.** Rejected: outside
   contribution is not currently expected, and a copyleft license would foreclose
   future commercial licensing choices that Brandon wants to keep open.
3. **Apache License, Version 2.0 for the engine.** Chosen, for its explicit patent
   grant and trademark clause, and because it preserves the widest set of future
   commercial choices.
4. **License `Deckard.Data` under the same Apache-2.0 grant as the engine.**
   Rejected: it would assert a right to license transcribed rules data that this
   project has not analysed and may not hold.
5. **Exclude `Deckard.Data` from the grant entirely**, via an explicit `NOTICE`
   carve-out. Chosen.
6. **Operate inside an identified publisher community-content policy** (an
   SRD/OGL/ORC-equivalent for Shadowrun). Rejected: no such policy has been
   identified, so the project does not rely on one existing.
7. **Take the repository private** while these questions were open. Rejected: the
   source boundary is already enforced mechanically, independent of repository
   visibility, so public visibility does not expose rulebook content.

## Chosen design

- `LICENSE`: the unmodified Apache License, Version 2.0 text, with the copyright
  line filled in for Brandon (2026).
- `NOTICE`: states that the Apache-2.0 grant does not extend to `Deckard.Data`,
  reproduces the existing rightsholder attributions (Topps, Catalyst Game Labs)
  from `docs/licensing-notes.md`, states that Deckard is an unaffiliated personal
  project claiming no association with the rightsholders, and states that no
  rulebook content is distributed.
- `docs/licensing-notes.md`: rewritten to state this settled posture in place of
  the now-answered open questions, pointing here for the decision record.
- No package or release is published as part of this decision. No CI step
  publishes anything. Commercial distribution remains gated on a separately
  negotiated licence and review by counsel, neither of which this repository
  initiates on its own.

## Reasoning

This ADR is a transcription of Brandon's decision, not an independent legal
analysis, per the Issue #5 non-goals ("No legal conclusions are written into the
repository by an agent"). The reasoning recorded here is exactly the reasoning
Brandon gave in the decision comment: Apache-2.0 over MIT for its explicit patent
grant and trademark clause; Apache-2.0 over a copyleft license because outside
contribution is not currently expected and Apache-2.0 preserves the widest set of
future commercial choices; excluding `Deckard.Data` because the repository does
not assert a right to license structured rules data it has not established it
holds, and excluding it is a refusal to grant rights rather than a claim about
who owns them; staying public because the source boundary is enforced mechanically
regardless of visibility; and not relying on a publisher community-content policy
because none has been identified.

## Consequences

- `LICENSE` and `NOTICE` land in this PR, alongside this ADR.
- Any future Issue touching packaging, publishing, or redistribution must respect
  the `Deckard.Data` carve-out in `NOTICE` — the engine's license does not carry
  that project forward automatically.
- Any future Issue proposing commercial distribution, a public-facing product
  name, or reliance on a publisher policy not identified here must go back to
  Brandon; this ADR does not authorize any of those. In particular, "Deckard"
  remains an internal codename only — see the "Naming" section of
  `docs/licensing-notes.md`.
- `docs/licensing-notes.md`'s "Open questions" and "Guidance until those are
  answered" sections are superseded by this ADR and have been rewritten to point
  here rather than restate the now-closed questions.
- This ADR does not decide whether a commercial product happens at all, or under
  what name. Both remain open and are not implicitly resolved by anything above.

## Rejected alternatives

- **MIT license.** No explicit patent grant or trademark clause; see Options
  considered #1.
- **A copyleft license.** Forecloses future commercial licensing choices Brandon
  wants to keep open, without a present need for it (no outside contribution is
  currently expected); see Options considered #2.
- **Licensing `Deckard.Data` under the same grant as the engine.** Would assert a
  right over transcribed rules data this project has not analysed and may not
  hold; see Options considered #4.
- **Operating inside an identified publisher community-content policy.** None has
  been identified; see Options considered #6.
- **Taking the repository private.** The source boundary is already enforced
  mechanically, independent of visibility; see Options considered #7.
