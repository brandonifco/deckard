---
name: rules-review
description: Build a bounded review packet and dispatch review of a Deckard PR. Use for any PR under review, not only one that touches a Shadowrun mechanic -- the packet and the repo-steward step apply to every PR; rules-conformance and Codex are the additional rules-only steps inside it.
---

# Reviewing a Deckard PR

## Never say "review this PR"

A verifier told only that rediscovers context through dozens of searches, costs far
more, and reviews worse. Hand it the facts — for every PR, not only a rules change.

## Generate the packet

```bash
tools/review-packet.sh --issue <N> --branch <branch-or-ref> [--base origin/main] \
  [--context 3] [--output /tmp/packet-<N>.md] [--pr <N>]
```

This is the one place the packet is assembled, so every reviewer sees the same bytes
built the same way — a hand-assembled packet is how the two fields drift. It prints to
stdout unless `--output` is given, and refuses to write inside the repository unless the
path is already gitignored: packets are ephemeral and must never be committed, exactly
like the source packets `tools/source-slice.py` produces.

The generated packet contains:

1. the **Issue number**, **title** and **acceptance criteria**
2. the **associated PR**, if one exists yet — title, URL and body, so the "Closes
   #NNN" and "Tests and evidence" checks in `repo-steward`'s charter have something to
   check against. Packets in this project are routinely built *before* a PR exists;
   when the lookup (by `--pr`, or by branch name otherwise) finds nothing, the packet
   says so explicitly and states that those two checks do not apply yet, rather than
   silently omitting the section
3. the **changed file list**
4. a **diff** at `--context` lines of surrounding code (default 3; narrow it for a
   mechanical change, widen it where semantics need more context)
5. the Issue's **source locator**, carried verbatim (`N/A` for non-rules work; a
   citation for rules work — fetch the actual excerpt separately with
   `tools/source-slice.py`, since this packet never carries the excerpt itself). This
   depends on the Issue's `## Source` section actually being a locator rather than
   pasted rulebook prose — the generator does not check that shape
6. a **determinism risk prompt**
7. every recorded **ADR**, so the reviewer does not relitigate a decision
8. **which gates** are expected to pass, including whether the changed files touch
   `src/Deckard.Rules`, `src/Deckard.Data`, their test projects, or the source
   manifest — the signal for whether rules-conformance and Codex apply on top of
   repo-steward

This list is the packet's one definition. Do not re-enumerate its fields elsewhere —
point here instead (see `docs/agent-team.md`, "Briefing agents", and
`.claude/agents/repo-steward.md`, "Your inputs").

Name the exact packet file to the reviewer. Never tell an agent to search the repository
for documentation.

## Order the reviews cheapest first

1. **`repo-steward`** — scope, unrelated changes, determinism, provenance, docs sync.
   Cheap, read-only, and runs on **every** PR. Catching an out-of-scope file here saves
   redoing an expensive review later.
2. **`rules-conformance`** — adversarial verification against the source packet.
   **Rules-only**: dispatch it only when the PR implements or modifies a Shadowrun
   mechanic — the packet's own "touches Rules/Data" line says so.
3. **Codex** — independent cross-vendor verification. **Rules-only**, and only for
   high-impact rules: dice and test mechanics, Edge semantics, core formulas, initiative
   and action ordering, damage resolution, table transcriptions.

A PR that touches no rules file stops after step 1. That is a complete review for that
PR, not a shortened one.

## Record the verdict (rules-only)

For a PR touching a rules surface, a review is not finished when the agent reports its
conclusion in chat — that conclusion has to become something `rules-conformance-gate`
(the required check; see `docs/agent-team.md`, "Recording a verdict") can actually see.
Once `rules-conformance` (and Codex, when required) conclude, record each verdict:

```bash
tools/record-verdict.sh --sha <head-sha> --reviewer rules-conformance \
  --verdict pass --packet-sha256 <bodySha256> --pages <range> --pr <N> --rerun-gate
```

Do not skip this because the review "obviously passed" — an unrecorded verdict is
mechanically indistinguishable from a review that never happened.

## The independence rule (rules-only)

**Codex does not see the first verifier's conclusions before producing its own.** Showing
a second reviewer the first reviewer's findings destroys the independence that is the
entire reason for asking twice. Collect both, then compare.

Where they disagree, the source packet decides — not seniority, not the model, not the
implementer's explanation.

## Reviewers are read-only

A reviewer that edits what it reviews is not a reviewer. Findings come back as reports;
fixes go to the implementing agent in its worktree. This applies to `repo-steward` on
every PR, not only a rules change.

## What a good verdict looks like

For a structural review, specificity is what makes a finding actionable: file, line, what
is wrong, why it matters — or a plain statement that nothing was found. Never an invented
finding to look thorough.

For a rules review, exhaustive where the book is finite: "verified all 11 rows; rows 4
and 9 disagree" beats "looks correct". A reviewer that always approves and a reviewer
that always finds something are equally useless.
