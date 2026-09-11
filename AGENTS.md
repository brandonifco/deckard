# AGENTS.md

**[`CLAUDE.md`](CLAUDE.md) is the governing contract for this repository.** Read it
first. It applies to every agent regardless of vendor — Claude, Codex, or anything else.

This file is not a mirror of it. Mirrored governing documents drift, and a drifted
contract is worse than no contract. Everything below is genuinely agent-specific;
everything else lives in `CLAUDE.md` and is not repeated here.

---

## Independent rules conformance: Codex, then Gemini, then in-house

The independent verdict's value in Deckard is **cross-vendor independence**, not extra
implementation throughput. A plausible-looking misreading of a rulebook table propagates
silently through every system built on top of it, and a second reviewer trained the same
way as the implementer tends to make the same misreading — that is the specific failure
mode asking a second vendor exists to catch.

For an Issue labelled `risk:rules-conformance`, the independent verdict is produced by an
**ordered fallback chain**, decided by Brandon on Issue #83 and recorded in
[ADR 0010](docs/decisions/0010-independent-verdict-fallback-chain.md):

1. **Codex** — the default. Try it first, always.
2. **Gemini** — used only when Codex is unavailable.
3. **In-house** (a second, independent `rules-conformance` pass) — used only when
   neither Codex nor Gemini is available.

**This is a fallback chain, not three equivalent options, and the third link costs
something real.** Codex and Gemini are genuinely different vendors from the in-house
implementer; falling back to a second in-house pass is not. A reviewer from the same
model family as the implementer tends to reproduce the implementer's misreading, which
is the entire stated reason a second verdict exists in the first place — so the in-house
fallback measurably weakens the guarantee the independent verdict is supposed to
provide. Brandon accepted that weakening deliberately, to avoid every rules merge being
blocked by one vendor's rate limit or outage, and only on the condition that the trade is
visible: see "name the vendor" below.

**Name the vendor. Do not collapse the three into a generic label.** Whichever link in
the chain actually ran, record its verdict under its own context —
`deckard-verdict/codex`, `deckard-verdict/gemini`, or `deckard-verdict/in-house-independent`
(`tools/record-verdict.sh --reviewer codex|gemini|in-house-independent`,
`tools/rules-conformance-gate.py`'s `INDEPENDENT_CONTEXTS`). A reader of a merged commit's
statuses must be able to tell a genuine cross-vendor verdict from a same-vendor fallback
without opening a transcript. This script cannot verify which model actually produced a
verdict, or that the chain was tried in order before falling back — that remains a
process obligation on whoever invokes it, stated here rather than implied to be enforced
(`docs/agent-team.md`, "Recording a verdict").

Use the independent reviewer — Codex or Gemini — to adversarially verify:

- dice, test resolution and Edge semantics
- core formulas and derived attributes
- initiative and action ordering
- damage resolution
- transcriptions of printed tables

Do not use Codex or Gemini for bulk transcription, routine documentation, or as a second
generic implementation worker.

**The independent reviewer receives:** the bounded review packet, the raw source packet,
and the Issue's acceptance criteria.

**The independent reviewer does not receive:** any other reviewer's conclusions, before
producing its own. Showing a verifier the first verifier's findings destroys the
independence that is the entire reason for asking twice — this applies to the in-house
fallback pass too, not only to Codex or Gemini.

**The independent reviewer is read-only.** A reviewer that can edit the thing it reviews
is not a reviewer.

## Any non-Claude agent

- You are almost certainly running in an isolated worktree. Confirm before your first
  write: `git rev-parse --git-common-dir` differing from `--git-dir` means worktree.
- The canonical gate is `./scripts/validate.sh full`. Do not invent a substitute.
- Never implement a Shadowrun mechanic from memory. Request a source packet.
- Never edit `.github/source-manifest.json` to make a hash check pass.
