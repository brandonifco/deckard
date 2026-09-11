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

Its verdict does not gate a merge until it is recorded — see "Recording a verdict" below.
A finding reported only in a chat transcript is not visible to GitHub.

### Codex — independent cross-vendor conformance

See [`../AGENTS.md`](../AGENTS.md). Used where a plausible silent misreading would
propagate broadly: dice and test mechanics, Edge semantics, core formulas, initiative and
action ordering, damage resolution, high-impact table transcriptions.

Codex never sees another verifier's conclusions before producing its own. Showing a
second reviewer the first reviewer's findings destroys the independence that is the
entire reason for asking twice.

For an Issue labelled `risk:rules-conformance`, Codex's verdict is mechanically
**required**, not merely recommended — see "Recording a verdict" below.

## Briefing agents

Never brief a verifier with "review this PR" — for any PR, not only a rules change.
Rediscovering context through dozens of repository searches is expensive, slow, and
produces worse reviews than simply being told the facts.

Generate the packet with `tools/review-packet.sh --issue <N> --branch <ref>`. Its exact
fields are defined once, in `.claude/skills/rules-review/SKILL.md` ("Generate the
packet") — that skill's packet-and-`repo-steward` step applies to every PR under review,
not only ones that touch a Shadowrun mechanic. Restating the field list here as well
would be a second copy of the same fact, free to drift the moment either one changes —
`tools/repo-checks.py --only invariant-drift` is not wired to this particular fact (it
tracks the determinism ban list, the manifest's page offsets, and the source-slice page
limit), so keeping exactly one copy is the only thing preventing that drift here.

Tell the agent precisely which guide and which packet file to read. Never tell an agent
to "search the repository for the documentation."

## Recording a verdict

A review that only exists in a chat transcript is a claim the PR makes about itself —
Issue #41 exists because that was, for a while, literally true: nothing on GitHub
distinguished "an adversarial review ran and passed" from "an agent typed a page number
into a text box." The `rules-conformance-gate` required check
(`.github/workflows/rules-conformance-gate.yml`, `tools/rules-conformance-gate.py`)
closes that gap for any PR touching a rules surface (`src/Deckard.Rules/`,
`src/Deckard.Data/`, their test projects, or `.github/source-manifest.json`): it passes
trivially otherwise, and requires a recorded verdict for the PR's exact head commit
otherwise.

Once a review concludes, record it — do not leave it in the transcript:

```bash
tools/record-verdict.sh --sha <head-sha> --reviewer rules-conformance \
  --verdict pass --packet-sha256 <source-slice.py bodySha256> --pages 44-47 \
  --pr <N> --rerun-gate
```

`--reviewer` is `rules-conformance` (the in-house agent) or `codex` (the independent
cross-vendor reviewer). Each posts a GitHub commit status tied to that exact SHA
(`deckard-verdict/rules-conformance` or `deckard-verdict/codex`) — tied to one commit
because that is what makes amending the PR invalidate a stale verdict: the new head SHA
simply has no status of its own until a fresh one is recorded against it. `--rerun-gate`
re-triggers the check so the merge gate reflects the verdict without waiting for another
push. For an Issue labelled `risk:rules-conformance`, the gate requires **both** contexts
before it passes — the in-house verdict alone is not enough for the mechanics that label
exists to flag.

**What this does and does not prove.** The gate proves a verdict was recorded against
this exact commit, naming a packet hash and page range. It cannot prove the verdict is
*true* — that is `rules-conformance` and Codex's job, not a CI script's, and the
rulebook never enters CI to check against (`CLAUDE.md`, "The book never enters the
repository"). It also cannot prove `--reviewer codex` was actually produced by Codex
rather than the same in-house model re-invoked under a different flag — that
cross-vendor independence is a process obligation this file and `AGENTS.md` state, not
one `tools/record-verdict.sh` enforces. Say so plainly rather than implying the mechanism
covers more than it does (Issue #12's standard).

Applying a rules-surface change without ever routing it through `rules-conformance` (and
Codex, where required) is still possible in principle — nothing stops an operator from
fabricating a verdict. What the gate removes is the *cheaper* failure: a review that
genuinely never happened, silently indistinguishable from one that did.

### Making the gate actually required

`rules-conformance-gate` runs as a GitHub Actions check on every PR (`on: pull_request`)
regardless of the ruleset below — that part needs no further action and is true the
moment this file's PR merges. What is *not* automatic is GitHub refusing to merge a PR
where it failed: that requires adding its context to the `main` branch ruleset's
`required_status_checks`, alongside `build-and-test` and `pr-policy`. A required status
check that is misconfigured blocks every future merge including its own fix, so this
project deliberately never applies this from inside an agent session (Issue #41) — a
human runs it once, deliberately:

`PUT` on a ruleset replaces the whole `rules` array, not just the one entry being
changed, so the request body below restates every existing rule (`deletion`,
`non_fast_forward`, `pull_request` with its approving-review count staying at `0`, and
the two existing required checks) alongside the one addition — omitting an existing rule
would silently delete it. This is the current ruleset (`gh api
repos/brandonifco/deckard/rulesets/22826088`) with exactly one line added.

```bash
cat > /tmp/protect-main-ruleset.json <<'JSON'
{
  "name": "protect-main",
  "target": "branch",
  "enforcement": "active",
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": false,
        "required_reviewers": [],
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": true,
        "require_extra_approval_for_unattributed_changes": false,
        "allowed_merge_methods": ["merge"]
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": true,
        "do_not_enforce_on_create": false,
        "required_status_checks": [
          { "context": "build-and-test" },
          { "context": "pr-policy" },
          { "context": "rules-conformance-gate" }
        ]
      }
    }
  ]
}
JSON
gh api --method PUT repos/brandonifco/deckard/rulesets/22826088 \
  --input /tmp/protect-main-ruleset.json
```

The `pull_request` rule's `required_approving_review_count` stays `0`: this project's
load-bearing artifact is the recorded verdict, not a human approving click.

Confirm afterward with `gh api repos/brandonifco/deckard/rulesets/22826088 --jq
'.rules[] | select(.type=="required_status_checks")'` — it should list all three
contexts.

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
