---
id: curves-09-curves-as-loft-profiles
title: Imported curves are selectable as loft and boundary profiles
status: verified
verified_on: SolidWorks 2026
language: [n/a]
api: []
keywords: [loft profile, boundary surface, convert entities, 3d sketch, reference curve, guide curve, pierce]
answers: "Do I need to convert imported curves into sketches before lofting them?"
---

# Imported curves are selectable as loft and boundary profiles

## The answer is no, and it deletes a whole step

**Imported Curve Through XYZ Points features can be picked directly as loft or
boundary profiles.** No 3D sketch wrapping them, no Convert Entities, no
intermediate geometry of any kind.

This is worth stating plainly because the assumption that you need a sketch is
extremely common, and it is expensive: it doubles the feature count, and it
means a refresh has to update the sketch as well as the curve.

The consequence for automation is that the entire loop is **one import per
curve and nothing else.**

## Guides pierce at machine precision, if you make them

Four guide curves were accepted across three sections on a probe cylinder with
no "guide curves must intersect all the profiles" error, and the same on a real
fuselage with 32 crossings.

That is not luck. It works because every station-times-guide crossing is
computed **once** and written into both files as the same characters. See
[curves/01](01-sldcrv-file-format.md).

If you generate sections and guides independently and hope they meet, you will
get the intersection error, and no tolerance setting will save you.

## Boundary Surface or Loft

Both build cleanly on identical input. Neither errored on the probe geometry.

**Default to Boundary Surface**, for its continuity control in both directions.
Keep Loft as a known-good fallback on the same curves if a particular shape
gives Boundary Surface trouble.

The probe geometry was too easy to separate them, so this is a preference
rather than a measured result.

## Closed sections versus split halves

A closed section — first point repeated as the last — works, and shows no
visible seam. See [curves/01](01-sldcrv-file-format.md).

If a sharper shape ever does show a seam, the fallback is to export each
section as two open halves split at the poles, and pick both halves as profiles,
either as one direction entry per side or as two separate boundary features.

## What is still manual

Building a **boundary surface** itself. There is no automation here for
creating a boundary surface from a set of curves, and inserting a curve does
not add it to an existing one.

**Correction, 2026-09-16:** this section used to say the same of a loft. A loft
through imported curves, with guide curves, is now made from code with
`IFeatureManager.InsertProtrusionBlend2`, profiles at selection mark 1 and
guides at mark 2, and it reproduced a hand-made loft of the same curves on
SolidWorks 2026: [features/12](../features/12-guided-loft.md). How close such a
loft comes to the shape you meant, against the number of guides, is
[surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md). Adding a
curve to a loft that already exists is still not automated.

Composite curves are selectable as profiles too, with conditions:
[curves/06](06-composite-curve.md).

The practical approach is to build the surface **once**, by hand, and then
refresh the curves under it forever after. That is the loop in
[curves/04](04-reload-curve-in-place.md), and the surface never has to be
touched again.

Generating a checklist for that one manual pass is worth doing. See
[surfacing/01](../surfacing/01-boundary-surface-recipe.md).

## See also

- [curves/01 — The .sldcrv format](01-sldcrv-file-format.md)
- [surfacing/01 — The boundary surface recipe](../surfacing/01-boundary-surface-recipe.md)
- [features/07 — Loft cut](../features/07-loft-cut.md) — a lofted cut between sketched sections
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — the loft from code
- [surfacing/03 — How a loft fills between two profiles](../surfacing/03-how-a-loft-fills-between-profiles.md)
- [curves/06 — Composite curves](06-composite-curve.md) — composite profiles
