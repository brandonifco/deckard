---
name: start-issue
description: File a well-formed Deckard Issue, or begin work on an existing one. Use when starting any new piece of work, or when unrelated work is discovered that needs its own Issue.
---

# Starting work

GitHub Issues are the only live work queue. Work starts from an Issue, always.

## Filing one

`gh issue create` bypasses GitHub's web Issue Forms, so the structure has to come from
here. Use `tools/new-issue.sh`, which supplies the template and refuses to file a
mechanics Issue with no source locator:

```bash
tools/new-issue.sh --title "Implement PCG32 deterministic random source" \
                   --label area:core --label phase:1-kernel --label type:implementation
```

Every Issue carries these sections:

```markdown
## Purpose
## Source            # SR6 Core / <section> / printed p. X / PDF p. Y  -- or N/A for non-rules work
## Exact scope
## Non-goals
## Acceptance criteria
## Required tests/evidence
## Dependencies
## Known ambiguity    # None, or an explicit description
## Risk
```

**One concern per Issue.** If it needs "and", it is probably two Issues.

## Labels that gate work

- `state:ready` — implementable now
- `state:blocked` — a dependency is unmet
- `state:needs-decision` — a genuine ambiguity Brandon must resolve

A mechanics Issue must not be `state:ready` until its source location is identified
precisely enough to implement from. **An implementation agent may not promote a
`state:needs-decision` Issue to `state:ready` by deciding the question itself.**

## Beginning work

```bash
tools/dispatch-agent.sh <issue-number>
```

This verifies the Issue exists, is open, and is not blocked or awaiting a decision; then
creates a branch and a worktree **outside** the repository and prints the path. Work
there. The primary checkout stays on `main`, clean.

## Discovering unrelated work mid-task

File another Issue. Do not enlarge the current one, and do not fix it in the current
branch — a PR that closes one Issue while quietly changing three other things cannot be
reviewed against its acceptance criteria.
