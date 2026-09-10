# AGENTS.md

**[`CLAUDE.md`](CLAUDE.md) is the governing contract for this repository.** Read it
first. It applies to every agent regardless of vendor — Claude, Codex, or anything else.

This file is not a mirror of it. Mirrored governing documents drift, and a drifted
contract is worse than no contract. Everything below is genuinely agent-specific;
everything else lives in `CLAUDE.md` and is not repeated here.

---

## Codex: independent rules conformance

Codex's value in Deckard is **cross-vendor independence**, not extra implementation
throughput. A plausible-looking misreading of a rulebook table propagates silently
through every system built on top of it, and a second reviewer trained the same way as
the implementer tends to make the same misreading.

Use Codex to adversarially verify:

- dice, test resolution and Edge semantics
- core formulas and derived attributes
- initiative and action ordering
- damage resolution
- transcriptions of printed tables

Do not use Codex for bulk transcription, routine documentation, or as a second generic
implementation worker.

**Codex receives:** the bounded review packet, the raw source packet, and the Issue's
acceptance criteria.

**Codex does not receive:** any other reviewer's conclusions, before producing its own.
Showing a verifier the first verifier's findings destroys the independence that is the
entire reason for asking twice.

**Codex is read-only.** A reviewer that can edit the thing it reviews is not a reviewer.

## Any non-Claude agent

- You are almost certainly running in an isolated worktree. Confirm before your first
  write: `git rev-parse --git-common-dir` differing from `--git-dir` means worktree.
- The canonical gate is `./scripts/validate.sh full`. Do not invent a substitute.
- Never implement a Shadowrun mechanic from memory. Request a source packet.
- Never edit `.github/source-manifest.json` to make a hash check pass.
