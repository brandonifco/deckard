# Decisions

Architecture and Rules Decision Records. A decision that a future agent must know is
recorded here, not remembered from a conversation.

## When an ADR is required

- the rulebook is genuinely ambiguous
- two authoritative passages conflict
- Brandon chooses a deliberate house rule or deviation
- a foundational determinism or replay decision changes
- a major architectural boundary changes
- the source baseline or errata policy changes

## When an ADR is not required

Routine implementation details. Naming a private helper. Test organisation. Formatting.
An ADR for every choice makes the important ones invisible.

## The rule that matters

**An implementation agent may not resolve a genuine rules ambiguity inside a PR.** The
decision gets recorded here, visibly, with its reasoning — or it gets escalated to
Brandon. A decision buried in a diff is a decision nobody can find, revisit, or trust.

## Format

Sequentially numbered, `NNNN-kebab-case-title.md`, containing: Status, Decision, Problem,
Authoritative source locations (for rules decisions), Options considered, Chosen design,
Reasoning, Consequences, Rejected alternatives.

Status is one of `Proposed`, `Accepted`, `Superseded by NNNN`. Accepted ADRs are not
edited to change their decision — they are superseded by a new one, so the reasoning
trail survives.

## Index

| # | Title | Status |
| --- | --- | --- |
| [0001](0001-architecture-boundaries.md) | Architecture boundaries and technical baseline | Accepted |
| [0002](0002-deterministic-randomness.md) | Deterministic randomness architecture | Accepted |
| [0003](0003-source-baseline.md) | Authoritative source baseline and errata policy | Accepted |
| [0004](0004-unresolved-rule-taxonomy.md) | Unresolved-rule taxonomy | Accepted |
| [0005](0005-replay-compatibility-identity.md) | Replay compatibility identity | Accepted |
| [0006](0006-deterministic-ordering-conventions.md) | Deterministic ordering conventions for observable sequences | Accepted |
| [0007](0007-opposed-test-tie-break-interpretation.md) | Opposed test tie-break: aggressor mapped to the acting side | Accepted |
