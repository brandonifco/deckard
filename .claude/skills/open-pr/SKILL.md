---
name: open-pr
description: Open a Deckard pull request that satisfies the PR policy check. Use when implementation is complete and validated.
---

# Opening a pull request

## Before anything else

```bash
./scripts/validate.sh full
```

Green, with output you can quote. Opening a PR on a red gate wastes a review cycle.

## Staging

Stage explicit paths. **Never `git add -A` or `git add .`** — that is how build output,
source packets and another task's edits end up in a commit claiming to close one Issue.
A hook blocks it.

```bash
git add src/Deckard.Core/RandomSource.cs tests/Deckard.Core.Tests/RandomSourceTests.cs
git commit
git push -u origin "$(git branch --show-current)"
gh pr create --fill
```

## The template is checked mechanically

`.github/pull_request_template.md` is enforced by the `pr-policy` status check. A PR fails
policy if it does not close exactly one real Issue, or if **Exact behavioral claim**,
**Scope**, **Tests and evidence**, **Known limitations** or **Agent provenance** is empty —
or if rules files changed and **Rules conformance** is empty or `N/A`.

"Tests pass" is not evidence. Paste the command and its result.

## Writing it honestly

**Exact behavioral claim** — what is *true now* that was not true before. One or two
sentences. Not a list of files changed.

**Scope** — what changed, and what nearby behaviour deliberately did **not** change. The
second half is what tells a reviewer where to stop looking.

**Rules conformance** — the exact source locator and what you actually verified. "Checked
all 11 rows of the printed table" is a claim a reviewer can test. "Matches the book" is not.

**Determinism impact** — does this change random consumption, ordering, serialisation,
replay, or state transitions? Consuming one extra random value shifts every subsequent
result, so say so.

**Known limitations** — what remains unsupported. This is information, not an admission.
An engine honest about its gaps is the entire point.

**Agent provenance** — which model implemented, which independently verified. Leave it
blank and policy fails.

**Unrelated changes** — must be none.

## After merge

```bash
tools/dispatch-agent.sh --cleanup <issue-number>
git -C <primary> merge --ff-only origin/main
```
