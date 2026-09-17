---
id: surfacing-01-boundary-surface-recipe
title: The boundary surface recipe, and generating it
status: verified
verified_on: SolidWorks 2026
language: [n/a]
api: []
keywords: [boundary surface, loft, knit, trim, planar, fill, manual steps, recipe, surface]
answers: "Which parts of building a surface from curves are automatable, and what stays manual?"
---

# The boundary surface recipe, and generating it

## What is automatable and what is not

| Step | Automatable |
|---|---|
| Write the curve files | Yes |
| Import them as features | Yes — [curves/02](../curves/02-insert-curve-from-file.md) |
| Name them predictably | Yes — [curves/07](../curves/07-rename-a-feature.md) |
| **Build the boundary surface over them** | **No** |
| Build a loft over them, with guides | Yes, since 2026-09-16 — [features/12](../features/12-guided-loft.md) |
| Refresh the curves afterwards | Yes — [curves/04](../curves/04-reload-curve-in-place.md) |
| Knit, trim, thicken | No |

There is no automation here for creating a boundary surface from a set of
curves. (A loft is different: see [features/12](../features/12-guided-loft.md),
and [surfacing/03](03-how-a-loft-fills-between-profiles.md) for how close it
comes.) The practical answer is to **build it once by hand and then never touch
it again**: once the surface exists, refreshing the curves under it updates the
shape in place with no reference re-picked.

So the manual pass happens exactly once per model. That makes it worth
generating a precise checklist for, rather than leaving it to memory.

## The recipe, as generated

Regenerate it with the actual filenames of the current export, so it is never
describing a previous version:

```markdown
## 1. Import the curves

**Insert → Curve → Curve Through XYZ Points → Browse**, pick the file, OK, then
rename the feature in the tree.

| Feature name | File |
|---|---|
| S1 | trainer40_S1.sldcrv |
| S2 | trainer40_S2.sldcrv |
| keel | trainer40_G_keel.sldcrv |

## 2. Build the surface

**Boundary Surface**, preferred over Loft for its continuity control in both
directions.

- Direction 1: S1, S2, S3, S4, S5, S6, S7, S8 — in order, nose to tail
- Direction 2: keel, starboard, crown, port

**Pick the curves themselves.** No 3D sketches, no Convert Entities.

Loft is a known-good fallback on identical input if Boundary Surface ever
struggles.

## 3. Verify

Zebra stripes should flow unbroken across each station and along the keel.

## 4. Forever after

Edit the model, then run the refresh. The surface rebuilds in place.
```

## Why generating it beats writing it once

The filenames change, the station list changes, and the order matters. A stale
recipe is worse than none, because it is followed.

Generating it also means the direction ordering comes from the same data the
exporter used, so the two cannot disagree.

## Later surface operations, for reference

If your model needs more than one surface:

- **Insert → Surface → Knit** — select every surface, **Merge entities** on.
  A fill patch is just another surface to this step; knit does not care which
  command built which face.
- **Insert → Surface → Planar** — closes a flat rim.
- **Insert → Surface → Fill** — closes a curved opening. A tangent fill closes
  it invisibly where a planar face would read as a step.
- **Insert → Surface → Trim**, **Mutual** — where two surfaces cross, then knit
  the results so the shell is one surface.

All manual.

## Boundary Surface or Loft

Both build cleanly on identical input. Neither errored on the probe geometry
tested. The default here is Boundary Surface for its continuity control; Loft is
recorded as a known-good fallback. The test geometry was too simple to separate
them, so this is a preference, not a measured result.

## See also

- [curves/09 — Curves as loft profiles](../curves/09-curves-as-loft-profiles.md)
- [curves/01 — The .sldcrv format](../curves/01-sldcrv-file-format.md)
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — the loft, automated
- [surfacing/03 — How a loft fills between two profiles](03-how-a-loft-fills-between-profiles.md)
