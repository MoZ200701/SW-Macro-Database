# SolidWorks Macro & Automation Database

A reference for driving SolidWorks from outside itself: VBA macros, VBScript,
Python and C#. Every entry is something that has already been built, and each
one says plainly whether it has been run against a real SolidWorks or only
written against the documented API.

The point is that the next time something needs automating, you check here
first instead of rediscovering it.

**This repo is standalone.** Every entry carries its own explanation and its own
code. You never need to open the projects the entries came from.

## Start here

| If you are… | Read |
|---|---|
| An AI agent looking something up | [AGENTS.md](AGENTS.md), then [manifest.json](manifest.json) |
| An AI agent adding an entry | [CONTRIBUTING.md](CONTRIBUTING.md) |
| A person browsing by task | [INDEX.md](INDEX.md) |
| Checking whether one API call is proven | [API-LEDGER.md](API-LEDGER.md) |
| About to write any automation at all | [GOTCHAS.md](GOTCHAS.md) |
| After working code to copy | [`code/`](code/) |

## What's in here

55 entries, grouped ten ways:

- **[entries/connect/](entries/connect/)** — reaching a running SolidWorks from
  VBScript, Python, C# and VBA, keeping the connection alive safely, and
  probing an API call on a live session before depending on it.
- **[entries/documents/](entries/documents/)** — making a new part or assembly
  in the right units, saving it somewhere, and closing it.
- **[entries/equations/](entries/equations/)** — global variables in the
  Equation Manager, and dimensions linked to them: what makes a part
  parametric.
- **[entries/curves/](entries/curves/)** — the Curve Through XYZ Points
  workflow end to end: the file format, inserting, refreshing in place, reading
  points back, joining curves, naming features, and gathering them into
  folders.
- **[entries/sketches/](entries/sketches/)** — generating 2D and 3D sketches as
  VBA, with the full relation and dimension constant tables, equation driven
  curves, checking a sketch is fully defined, and an honest list of what the
  sketch API cannot be made to do.
- **[entries/features/](entries/features/)** — boss extrude, cut extrude and
  circular pattern, and finding the dimensions they make.
- **[entries/assemblies/](entries/assemblies/)** — inserting components and
  mating them, with a mate driven by a global.
- **[entries/reading/](entries/reading/)** — interrogating a model: a closed
  file's references, reference trees, what the user has selected, sketch to
  model coordinates, and reading dimensions, equations and sizes back out.
  It also covers the part's volume as a check on what a feature did.
- **[entries/surfacing/](entries/surfacing/)** — the parts that stay manual, and
  the axis conventions that decide which way geometry lands.
- **[entries/files/](entries/files/)** — whole-library operations: importing
  neutral formats such as STEP in bulk, writing one file per configuration, and
  moving a tree onto a newer file version.

## The status field is the whole point

Every entry declares one:

| Value | Means |
|---|---|
| `verified` | Run against SolidWorks and observed. The entry names the version and what was seen. |
| `partly-verified` | Some of it was run. The entry says which part. |
| `unverified` | Well-formed against the documented API. Nobody has watched it run. |
| `superseded` | Do not use. The entry links to what replaced it. |

Currently: **45 verified, 7 partly verified, 3 unverified.**

This distinction is the most valuable thing here, so it is never blurred. An
entry that guesses says so.

## Ground rules the collection follows

1. **Never guess an API call.** Where the documented surface has no way to do
   something, the output says so and asks for a manual step, counted in the
   header. A macro that silently does the wrong thing is worse than one that
   stops.
2. **The API is in metres.** Every entry that crosses that boundary states which
   side it is on.
3. **Read a name back after you set it.** SolidWorks quietly renames on
   collision.
4. **All COM calls on one apartment thread.** Three of the four languages here
   have a helper for it, in [`code/`](code/).

## Maintaining it

```
python3 tools/build_manifest.py
```

Rebuilds [`manifest.json`](manifest.json) from each entry's frontmatter, and
refuses to write if an entry is missing a field, duplicates an `id`, or claims
`verified` without naming a version. Run it after any change and commit the
result.

## Where this came from

| Project | What it does | Language |
|---|---|---|
| **Fuselage-Builder** | Parametric fuselage exported as curve files, pushed live into an open part | TypeScript emitting VBScript and VBA |
| **Airfoil-Converter** | Airfoil sections placed on a picked plane and inserted into a part | Python with pywin32 |
| **SW-File-Manager** | Reference trees, broken-reference detection, safe rename | C# on .NET 8 |
| **Gear-Generator** | Parametric spur gears and gear pairs built straight into SolidWorks, and the API probe that verified every call first | Python with pywin32 |

The code in `code/` is lifted from those projects verbatim or generated by them.
The Gear-Generator probe, its results and the COM module written from them are
in [`code/python/gear_generator/`](code/python/gear_generator/probe/harness.py);
its gear maths and interface are application logic and are not copied.
Where an entry reproduces code, that code is the real thing, not a paraphrase.
