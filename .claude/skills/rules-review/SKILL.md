---
name: rules-review
description: Build a bounded review packet and dispatch verification of a rules change. Use when a PR implements or modifies a Shadowrun mechanic.
---

# Reviewing a rules change

## Never say "review this PR"

A verifier told only that rediscovers context through dozens of searches, costs far more,
and reviews worse. Hand it the facts.

## The review packet

Assemble and pass directly:

1. the **Issue number** and its acceptance criteria
2. the **changed file list**
3. a **compact diff** — `git diff -U1` where the change is mechanical, wider where the
   semantics need surrounding context
4. the **source locator** and the **raw source packet** itself
5. the **determinism risk** — does this touch random consumption, ordering, or replay?
6. **decisions already recorded** — relevant ADRs, so the reviewer does not relitigate them
7. **which gates** are expected to pass

Name the exact guide and packet to read. Never tell an agent to search the repository for
documentation.

## Order the reviews cheapest first

1. **`repo-steward`** — scope, unrelated changes, determinism, provenance, docs sync.
   Cheap. Catching an out-of-scope file here saves redoing an expensive review later.
2. **`rules-conformance`** — adversarial verification against the source packet.
3. **Codex** — independent cross-vendor verification, for high-impact rules only: dice and
   test mechanics, Edge semantics, core formulas, initiative and action ordering, damage
   resolution, table transcriptions.

## The independence rule

**Codex does not see the first verifier's conclusions before producing its own.** Showing
a second reviewer the first reviewer's findings destroys the independence that is the
entire reason for asking twice. Collect both, then compare.

Where they disagree, the source packet decides — not seniority, not the model, not the
implementer's explanation.

## Reviewers are read-only

A reviewer that edits what it reviews is not a reviewer. Findings come back as reports;
fixes go to the implementing agent in its worktree.

## What a good verdict looks like

Exhaustive where the book is finite: "verified all 11 rows; rows 4 and 9 disagree" beats
"looks correct". A reviewer that always approves and a reviewer that always finds
something are equally useless.
