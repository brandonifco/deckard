# Source handling

Deckard implements a commercial, copyrighted rulebook that Brandon owns a copy of. The
whole point of this document is that the engine can be *provably* faithful to that book
without the repository ever containing it.

## The authoritative baseline

One source, pinned by hash, declared in [`.github/source-manifest.json`](../.github/source-manifest.json):

| | |
| --- | --- |
| `sourceId` | `sr6-core` |
| Book | *Shadowrun, Sixth World* core rulebook |
| Printing | **First Printing** by Catalyst Game Labs, © 2019 The Topps Company |
| PDF pages | 322 |
| Page numbering | **printed page = PDF page − 1** |

This is **not** Seattle City Edition, Berlin City Edition, or Hong Kong City Edition. A
newer printing does not supersede this baseline. Changing the pinned hash is a
deliberate source-baseline change requiring its own Issue, its own PR, and an ADR — see
[`decisions/0003-source-baseline.md`](decisions/0003-source-baseline.md).

Official errata are **not** part of the baseline as of bootstrap. Including them is
Brandon's decision, not an agent's.

## What is never committed

- the rulebook, in any format
- extracted chapters, pages, or source packets
- substantial verbatim excerpts, in code, tests, Issues, PRs or documentation
- the local path to Brandon's copy

`.gitignore` blocks `*.pdf`, `source.local.json` and packet-shaped files.
`tools/repo-checks.py --only source-boundary` fails the build if a rulebook-shaped
binary, a packet header, or a local source path is ever tracked. Both are backstops,
not permission to be careless.

Short identifying phrases — a table's title, a section heading, the name of a mechanic —
are fine and necessary. Paragraphs of rules prose are not.

## Configuring your local copy

The tool reads the path from outside version control, in this order:

1. the environment variable named in the manifest, `SR6_CORE_PDF`
2. `source.local.json` in the repo root (gitignored)

```bash
export SR6_CORE_PDF=/absolute/path/to/your/own/copy.pdf
```

```json
{ "sr6-core": "/absolute/path/to/your/own/copy.pdf" }
```

Confirm with `./scripts/doctor.sh`, which reports the filename and hash status but
deliberately never prints the full path, because doctor output gets pasted into Issues.

## Source packets

A **source packet** is a small, hash-verified, self-describing extract. Every agent that
needs rulebook text gets one, so that every agent sees identical bytes and no agent ever
reconstructs the book from memory.

```bash
tools/source-slice.py --printed-pages 44-47
tools/source-slice.py --pages 45-48 --layout --expect "Edge Boost"
tools/source-slice.py --printed-pages 43 --output /tmp/packet.txt
```

- `--printed-pages` takes the numbers printed in the book and converts them using the
  manifest offset. Prefer it: Issues cite printed pages, and the conversion is where
  off-by-one errors come from.
- `--layout` preserves column structure. Use it for **every printed table**; plain
  extraction interleaves table columns into unusable prose.
- `--expect REGEX` (repeatable) asserts the slice actually covers what it claims to. If
  the anchor is missing the tool extracts nothing and exits non-zero. Fix the page
  range; never weaken the anchor.

### Deliberate limitations

**There is no `--file` argument, and there never will be.** If an agent could point the
tool at a different printing, a previous edition, a supplement, or a pirated scan, then
every provenance claim in the repository would become unverifiable. This is covered by a
test that fails if the flag ever appears.

**The hash is verified before any extraction.** A mismatch extracts nothing and prints
the expected and actual digests. Do not "fix" a mismatch by editing the manifest.

**Packets are bounded** to 24 pages. Wanting 40 pages at once almost always means the
Issue is too broad, not that the limit is wrong.

**This is not a search tool.** It slices pages you already located. Do not build a
searchable copy of the book, a full transcription, or a RAG index in this repository.

## Citing rules

Every mechanics Issue, PR and rules test cites its source location:

```
SR6 Core / Edge Actions / printed p. 46 / PDF p. 47
```

Record both numbers when practical. Cite the *location*, do not paste the *prose*.

## When the book is wrong or unclear

Never quietly correct an apparent typo, and never quietly pick an interpretation.

If the source is genuinely ambiguous or internally inconsistent, stop and open an ADR
recording three things separately:

1. what the source literally prints
2. what the project decided to execute
3. why

The Issue gets `state:needs-decision`. An implementation agent may not resolve a genuine
rules ambiguity by deciding it inside a PR.

## Testing against the source

Where the book prints a finite table, verify **the entire table**, not three convenient
rows. Where a formula is derived from printed examples, test it against the whole
printed domain and report what was covered.

For high-risk rules, derive expected values independently from the source rather than
from the implementation. A test that restates the implementation's algorithm proves
only that the code agrees with itself.
