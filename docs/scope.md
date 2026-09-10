# Scope

## In scope

The **Shadowrun, Sixth World core rulebook, First Printing (2019)** — and nothing else.

That single book is the entire rules surface. Its mechanics, its tables, its finite
vocabularies. Deckard aims to implement them faithfully and deterministically, and to
be honest about the parts it has not implemented yet.

## Out of scope

**Supplements.** No additional combat, magic, Matrix, rigger, companion or other Sixth
World book. Not even to clarify something. Not even when a supplement is obviously
better. Brandon expands scope; agents do not.

**Other editions.** No previous Shadowrun edition is authoritative for anything, at any
time, for any reason.

**Other printings.** City Editions are different books, not newer versions of this one.

**Unofficial sources.** Wikis, forums, Reddit, VTT implementations, character builders,
homebrew, and general web summaries are not authority. They may reveal that a question
exists. They never answer it.

**Model memory.** Your recollection of how Shadowrun works is not a source. Get a packet.

## When the core book points elsewhere

The core rulebook sometimes references material in other books. When that happens:

1. record the dependency in the Issue,
2. return an explicit unresolved result rather than approximating the missing rule,
3. file an Issue if the gap is worth tracking,
4. do **not** silently implement the supplement's version.

An engine that honestly reports "this rule lives in a book I do not implement" is more
useful than one that quietly invents a plausible substitute, because the first can be
trusted everywhere it does not complain.

## Not a game

Deckard is a library. It contains no UI, no Godot client, no console game, no campaign
or story systems, no networking, no save format, no AI opponents, and no procedural
encounter generation.

Clients come later and consume a stable engine API. Keeping the engine free of client
concerns is what makes it reusable by a game, a simulator, an encounter system and
character tooling simultaneously.
