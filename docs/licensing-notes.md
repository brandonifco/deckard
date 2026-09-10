# Licensing notes

Factual notes and open questions. **Nothing here is legal advice or a legal conclusion.**
Distribution and licensing are Brandon's decisions and have not been made.

## Current state

- The repository is **public**.
- **No license file has been added.** Under default copyright, a public repository
  without a license grants no rights to anyone to use, copy, modify or distribute its
  contents. "Publicly visible" and "openly licensed" are different things, and this
  repository is currently the first without being the second.
- No license was chosen automatically, and none should be added without Brandon
  explicitly deciding to.

## What is not in this repository

- The Shadowrun, Sixth World rulebook, in any form.
- Extracted chapters, pages, or generated source packets.
- Substantial verbatim rulebook text in code, tests, Issues, PRs or documentation.
- The local filesystem path to Brandon's copy.

The rulebook is © 2019 The Topps Company, Inc.; Shadowrun and Matrix are trademarks of
The Topps Company, Inc.; Catalyst Game Labs is a trademark of InMediaRes Productions, LLC.
Deckard is an unaffiliated personal project and claims no association with any of them.

`.gitignore` and `tools/repo-checks.py --only source-boundary` enforce the boundary
mechanically. `docs/source-handling.md` describes it in full.

## Naming

"Deckard" is a neutral internal codename chosen deliberately so the repository does not
carry a Shadowrun trademark in its name. Any public-facing product name is a separate
decision Brandon has not made.

## Open questions

These are unresolved, and being public makes them live rather than hypothetical:

1. **Should this repository have a license at all, and which one?** Until it does, it is
   visible but not usable by others.
2. **What is the intended relationship between the engine code and the rules it
   implements?** Game mechanics and the expression of them are treated differently under
   copyright law, and that distinction has been litigated. This project has not analysed
   where its structured rules data falls, and an agent must not decide it.
3. **Does distributing structured rules data differ from distributing the engine?** A
   corpus of transcribed tables is a different artifact from an algorithm, and may
   warrant a different answer.
4. **Is there an applicable community-content or fan-content policy** from the publisher,
   and does this project want to operate inside it?
5. **Does the repository want to stay public** during development, given that questions
   1–4 are open?

## Guidance until those are answered

- Do not add a license file.
- Do not publish packages or releases.
- Do not commit rulebook text, and keep short identifying phrases short: a table title
  or a mechanic's name is necessary for provenance; a paragraph of rules prose is not.
- Raise anything that looks like it changes the answers with Brandon rather than
  resolving it in a PR.
