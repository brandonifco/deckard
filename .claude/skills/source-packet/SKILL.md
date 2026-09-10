---
name: source-packet
description: Generate a bounded, hash-verified excerpt from the Shadowrun core rulebook for implementing or reviewing a rule. Use whenever rules text is needed — never implement a mechanic from memory.
---

# Source packets

Never implement a Shadowrun mechanic from memory. Your recollection is not a source and
may be confidently wrong in ways that survive review.

```bash
tools/source-slice.py --printed-pages 44-47 --layout --expect "Edge Boost"
tools/source-slice.py --pages 45-48 --output /tmp/packet.txt
```

## Rules that matter

**`--printed-pages` over `--pages`.** Issues cite printed page numbers; the manifest holds
the offset (printed = PDF − 1). Converting by hand is where off-by-one errors come from.

**`--layout` for every printed table.** Without it, `pdftotext` interleaves table columns
into prose that reads plausibly and is wrong.

**`--expect REGEX` every time.** It asserts the slice actually covers what you think it
does. If it fails, your page range is wrong — find the right pages. Never weaken the
anchor to make it pass.

**Bounded.** 24 pages maximum. Wanting more usually means the Issue is too broad.

## Finding the right pages

This is a page-slice tool, not a search tool. Locate the section first — the book's table
of contents is near the front, and section headings are in the packet you already have.
If you cannot find a rule, say so; do not slice a wide range and hope.

## Absolute constraints

- **Never commit a packet.** They are ephemeral and gitignored. Keep them in `/tmp`.
- **Never paste rulebook prose** into an Issue, a PR, a commit message, a test, or a
  comment. Cite the location instead: `SR6 Core / Edge Actions / printed p. 46 / PDF p. 47`.
- **Never edit `.github/source-manifest.json` to make a hash check pass.** A hash mismatch
  means the wrong file, not a wrong manifest. Changing the baseline requires its own Issue,
  PR and ADR.
- **There is no `--file` argument.** If you find yourself wanting one, you are about to
  substitute a different book. Stop.

Short identifying phrases — a table's title, a mechanic's name — are necessary and fine.
Paragraphs are not.
