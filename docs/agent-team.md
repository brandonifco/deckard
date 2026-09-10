# Agent team

Deliberately small. Roles are added when a real failure mode demands one, not because a
role is imaginable.

The primary Claude session is the **orchestrator**: it holds project context, files and
grooms Issues, dispatches workers, reviews results, and merges. It does not implement
features in the primary checkout.

## Roles

### `engine-dev` — implement one settled Issue

Workhorse model, not the most expensive one available.

- **Requires an isolated worktree.** Must confirm it is in one before its first write.
- Reads: `CLAUDE.md`, the Issue, and explicitly routed guides and source packets. Not the
  whole repository.
- May edit. Must run `./scripts/validate.sh full` before opening a PR.
- **May not resolve a genuine design or rules ambiguity.** Hitting one means stopping and
  escalating, not choosing. An Issue at `state:needs-decision` is not implementable, and
  an implementation agent cannot promote it to `state:ready` by deciding the question.

### `repo-steward` — cheap structural review

Inexpensive capable model. Runs **before** expensive semantic verification, because
catching an out-of-scope file costs far less than a conformance review that then has to
be redone.

Checks: Issue scope adherence, unrelated changes, determinism invariants, documentation
synchronisation, source provenance present where required, forbidden primary-checkout
mutation, obvious policy violations.

### `rules-conformance` — adversarial verification against the source

High reasoning. **Read-only.** A reviewer that can edit what it reviews is not a reviewer.

Receives a bounded review packet and the raw source packet. Its job is to *falsify* the
implementation, not to confirm it. Where the book prints a finite table it checks every
applicable row, not a sample.

### Codex — independent cross-vendor conformance

See [`../AGENTS.md`](../AGENTS.md). Used where a plausible silent misreading would
propagate broadly: dice and test mechanics, Edge semantics, core formulas, initiative and
action ordering, damage resolution, high-impact table transcriptions.

Codex never sees another verifier's conclusions before producing its own. Showing a
second reviewer the first reviewer's findings destroys the independence that is the
entire reason for asking twice.

## Briefing agents

Never brief a verifier with "review this PR". Rediscovering context through dozens of
repository searches is expensive, slow, and produces worse reviews than simply being
told the facts.

Generate a bounded review packet containing:

- the Issue number and its acceptance criteria
- the changed file list
- a compact diff (`-U1` where the change is mechanical; wider where semantics matter)
- the required source locator, and the source packet itself
- the determinism risk of the change
- decisions already made and recorded
- exactly which gates are expected to pass

Tell the agent precisely which guide and which packet to read. Never tell an agent to
"search the repository for the documentation."

## Worktrees

Implementation happens in a worktree outside this repository:

```bash
tools/dispatch-agent.sh <issue-number>
tools/dispatch-agent.sh --cleanup <issue-number>
```

Worktrees never live under the repository — not in `tmp/`, not in `worktrees/`, not in
`.claude/worktrees/`. A worktree inside the repository eventually gets committed,
scanned, or deleted by something that did not know it was there.

Read-only review agents do not need a worktree.
