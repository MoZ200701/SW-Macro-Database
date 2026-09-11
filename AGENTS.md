# For agents

This repo answers one question: **has this SolidWorks automation problem
already been solved, and is the solution trustworthy?**

## Looking something up

1. Read [`manifest.json`](manifest.json). One file, every entry, with `answers`,
   `keywords`, `api` and `status`. That is usually enough to decide whether a
   solution exists.
2. If a candidate matches, read that entry in full. Entries are self-contained.
3. If you only need to know whether a specific API member is proven, grep
   [API-LEDGER.md](API-LEDGER.md).
4. Before writing any SolidWorks automation, read [GOTCHAS.md](GOTCHAS.md). It
   is short, and most of it is failure modes that are silent.

## How to read `status`

| Value | What you may do with it |
|---|---|
| `verified` | Use it. It ran against the named version. |
| `partly-verified` | Use it, and check the entry for which part is proven. |
| `unverified` | Good starting point. Test it before relying on it, and update the entry with what you find. |
| `superseded` | Do not use. Follow the link in the entry. |

Do not upgrade your confidence beyond what `status` says just because the code
reads well. That distinction is the repo's whole value.

## If the answer is not here

Say so plainly rather than improvising something that looks like an entry. Then,
if you go and solve it, **come back and write it up** — that is the point of the
collection.

Authorship rules are in [CONTRIBUTING.md](CONTRIBUTING.md). The short version:

- Never write an API call you cannot point to documentation or a run for.
- `status: verified` requires the version and what was observed, in the body.
- Extend an existing entry rather than adding a near-duplicate.
- Record failures and open questions, not just successes.
- Regenerate `manifest.json` and update the cross-references.

## Scope

**In scope:** anything about driving SolidWorks from outside itself, or about
the file formats it reads and writes.

**Out of scope:** general geometry, maths, UI frameworks, build tooling, and
application logic that happens to sit next to SolidWorks code. Those belong in
the project that needs them.

The test: would this entry still be useful to someone automating a completely
different kind of part? If not, it is application logic.

## Common starting points

| Task | Entry |
|---|---|
| Reach a running SolidWorks | [connect/01](entries/connect/01-attach-from-vbscript.md) (VBScript), [connect/02](entries/connect/02-attach-from-python.md) (Python), [connect/03](entries/connect/03-attach-from-csharp.md) (C#) |
| Find the part and the feature to work on | [connect/07](entries/connect/07-find-the-open-document.md), [connect/08](entries/connect/08-find-a-feature-by-name.md) |
| Get the units right | [sketches/05](entries/sketches/05-units-and-number-format.md) |
| Name what you created | [curves/07](entries/curves/07-rename-a-feature.md) |
| Make failures diagnosable | [reading/07](entries/reading/07-report-feature-type-on-failure.md) |

Those five are the scaffold around almost any new macro. If the feature you need
to drive is not covered, only the call in the middle is new.
