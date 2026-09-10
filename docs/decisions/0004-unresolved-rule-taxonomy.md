# 0004 — Unresolved-rule taxonomy

## Status

Accepted — 2026-09-10, at repository bootstrap. Implemented in Phase 1 or 2, whichever
first needs to express an unresolved state.

## Decision

An engine operation that cannot resolve a mechanic returns an explicit unresolved
**domain result** carrying one of a closed set of reasons. It never approximates, and it
never throws for this case.

## Problem

An engine that implements 40% of a rulebook is useful if it is honest about which 40%, and
dangerous if it is not. The dangerous failure is silent: a mechanic that quietly does
nothing, returns zero, skips an effect, substitutes a similar rule, or invents a plausible
missing value. Downstream, that is indistinguishable from a correct answer. It corrupts
simulations, misleads playtesting, and produces bug reports nobody can reproduce.

This is also the failure mode an AI agent is most prone to. "Return a sensible default"
and "use the closest equivalent rule" both feel like helpfulness while destroying the
project's only real claim: that when Deckard answers, the answer came from the book.

## Options considered

1. **Throw an exception.** Visible, but conflates "the engine does not support this yet" —
   normal, expected flow — with "the programmer made a mistake". Callers end up catching
   exceptions as control flow, and a caught-and-logged exception is a silent failure again.
2. **Return `null` or a default.** The failure mode itself.
3. **A boolean success flag.** Visible but uninformative: a caller cannot distinguish "not
   implemented yet" from "outside scope by design" from "the book is ambiguous here".
4. **A result type carrying a closed reason vocabulary.** Explicit, exhaustively matchable,
   and self-documenting.

## Chosen design

Every resolution operation returns a result type that is either resolved or unresolved.
The unresolved case carries a reason from a closed vocabulary:

| Reason | Meaning |
| --- | --- |
| `UnsupportedRule` | The book defines this; Deckard has not implemented it yet |
| `RequiresInterpretation` | The source is genuinely ambiguous; an ADR is needed |
| `OutsideCurrentScope` | Defined in a supplement or another edition; deliberately not implemented |
| `UnsupportedInteraction` | Both mechanics exist, but their combination is not resolved |
| `MissingRulesData` | The algorithm exists; the structured data it needs is absent |

Each carries enough context to act on: what was attempted, and the source locator where
the rule lives.

Exceptions remain for programmer errors — a null argument, a corrupt state, an impossible
branch. "The engine does not yet support this rule" is normal flow and never an exception.

## Reasoning

The distinction between the five reasons is not bureaucratic; each implies a different
response. `UnsupportedRule` is a roadmap item. `RequiresInterpretation` is a question for
Brandon. `OutsideCurrentScope` is a deliberate boundary that will not change without a
scope decision. `UnsupportedInteraction` is usually the most interesting, because it marks
a combination that a naive implementation would have silently resolved wrongly.
`MissingRulesData` is a data-entry task, not an engineering one.

A closed vocabulary also makes the unimplemented surface **enumerable**. Phase 13's
conformance audit depends on being able to ask the engine what it cannot do, rather than
inferring it from what nobody has tested.

## Consequences

- Callers must handle the unresolved case. That is intended friction: a client that ignores
  it is a client that will silently misreport rules.
- Tests must cover refusal behaviour, not only success. An unsupported mechanic that fails
  to announce itself is a bug with a test to prove it.
- The vocabulary is closed. Adding a sixth reason means superseding this ADR — which forces
  the question of whether the new case is genuinely distinct or an existing one reworded.
- The exact type shape is deferred to the first Issue that needs it. This ADR fixes the
  semantics and the vocabulary, which are the parts that must not be decided inside a PR.

## Rejected alternatives

- **Exceptions for unsupported rules.** Turns expected flow into error handling; ends in
  catch-and-log, which is silence.
- **Defaults or nulls.** Precisely the behaviour this project exists to avoid.
- **An open-ended string reason.** Unmatchable, undocumentable, and it drifts into free
  text that no consumer can act on.
