---
id: surfacing-03-how-a-loft-fills-between-profiles
title: How SolidWorks shapes a loft between two profiles, and how its accuracy scaled with the number of guides
status: verified
verified_on: SolidWorks 2026
language: [n/a]
api: []
keywords: [loft accuracy, loft deviation, guide curves, number of guides, accuracy versus guides, guide spacing, sag between guides, linear blend, chordwise stretch, section scaling, taper, sharp corner, guide near corner, loft fails, dome, STEP measurement, 0.05 mm]
answers: "If I loft two profiles along guide curves, how far does SolidWorks' surface stray from the shape I meant, and how does that change as I add guides?"
---

# How SolidWorks shapes a loft between two profiles

## What this is for

A loft through two profiles is SolidWorks' own interpolation. When the part has
to match a shape you computed (a wall of constant thickness, a tapered section
that must stay a known section), the question is how far that interpolation
strays and what extra curves pull it back. None of it can be worked out from
the API; it was measured, by writing each loft to STEP and comparing its
surface with the intended one.

This entry is about the geometry SolidWorks makes. The call that makes the loft
is [features/12](../features/12-guided-loft.md).

## What SolidWorks does between two profiles

Observed on SolidWorks 2026, lofting two closed profiles of different size
along a leading-edge and a trailing-edge guide:

- **It blends the two profiles linearly along the span, in millimetres.** A
  point part-way along is the matching points of the two profiles mixed in
  proportion.
- **It then stretches that blend chordwise to meet the guides.** Where the
  guides say the section is longer or shorter than the blend, the section is
  stretched along the chord to reach them.
- **It does not scale the section to the local chord.** A thickness that is a
  fixed fraction of the chord at both ends is not that fraction in between; the
  thickness follows the linear blend in mm while the length follows the guides.

So a loft between a root and a tip of different size, held only at its edges,
is not the family of scaled sections a designer usually means. The difference
is small but measurable, and it is what the surface guides below correct.

## Accuracy against the number of guides, in one case

Adding guide curves along the span on each surface, crossing both profiles,
holds the section in between, and the more there were, the closer the loft
came. These are the data points from **one case**, not a rule: a root-and-tip
loft of about 1000 mm span, an inner skin offset 2.5 mm inward from an outer
one, both lofted by SolidWorks 2026 with edge guides plus the surface guides
below, and the wall between the two lofts measured from their STEP files.

| Surface guides per surface | Chord stations | Wall (2.5 mm asked) | Skin within tolerance |
|---|---|---|---|
| about 5 | 5, 15, 30, 50, 75 % | 2.17–2.67 mm; up to 0.3 mm off ahead of the first guide and aft of the last | 93 % within ±0.1 mm |
| 7 | 2, 5, 15, 30, 50, 75, 90 % | 2.30–2.67 mm | 98.2 % within ±0.1 mm |
| 16 | the list below, closer together toward the nose | 2.38–2.57 mm | 99.4 % of the inner skin within ±0.05 mm |

**Between two guides SolidWorks sags**: by up to **0.15 mm** where neighbouring
guides were 10 to 25 % of the chord apart, in the same case. That sag is what
the closer spacing near the nose took out. The sixteen stations were:

```python
SURFACE_GUIDES = (
    0.01, 0.02, 0.035, 0.05, 0.075, 0.10, 0.15, 0.20, 0.25, 0.30,
    0.40, 0.50, 0.60, 0.75, 0.90, 0.95,
)
```

(fractions of the chord, on each surface; from the Airfoil Converter's
`wing.py`). How this carries over to another section, span or taper is not
known; treat the table as a starting point for your own measurement.

## Guides have to lie on the curve SolidWorks draws

A guide must cross each profile. The profile SolidWorks draws through a Curve
Through XYZ Points file is a natural cubic spline, not the polyline through the
file's points ([curves/01](../curves/01-sldcrv-file-format.md)). Guides whose
ends were computed on that spline, landing between the file's points as well as
on them, were accepted. Straight lines between the same points sat up to
0.18 mm inside the spline at a nose, more than three times the 0.05 mm the
lofts were held to.

## Guides near a sharp corner break the loft

On an inner (offset) profile that came to a sharp corner at its nose:

| First guide at | Result |
|---|---|
| 0.5 % of chord | the loft built, as a garbage dome |
| 1 to 1.5 % | the loft **failed** |
| 2 % | worked |

So the tool starts an offset profile's surface guides at 2 % and keeps the
outer profile's at 1 %. The threshold is for that shape; the lesson is to keep
guides a little way back from a corner and check the result, because one
failure mode is silent.

## Guide order

Changing only the order the guides were picked in moved the surface by up to
0.04 mm on one loft and not at all on another. Pick them in a fixed order when
comparing runs. See [features/12](../features/12-guided-loft.md).

## What it does not do

- **One case.** A single wing and its offset skin; the numbers above are data
  points for that shape, not a guide count to rely on elsewhere.
- **Only two-profile lofts were measured.** With more profiles, the span-wise
  blend is presumably no longer a straight line between two; not tested.
- Only guide influence "To next guide", Maintain tangency on, no end
  constraints. Other settings may shape it differently.
- The Boundary Surface feature was not measured this way; this is Loft only.
- The measurement script and the intended-shape maths are the Airfoil
  Converter's application logic and are not reproduced here; only what
  SolidWorks did is.
- Why guides at 1 to 1.5 % fail and 0.5 % does not is not known. Settling it
  would take the same loft at finer steps and a look at the corner's
  curvature.

## Evidence

SolidWorks 2026, lofts made by
[features/12](../features/12-guided-loft.md)'s code in the Airfoil Converter
and written one per file to STEP ([files/04](../files/04-export-a-body-to-step.md)),
then measured against the intended surfaces of a 1000 mm wing and the same wing
offset 2.5 mm inward. Recorded in that project's commits of 2026-09-16 ("Guide
the loft at 2% and 90% of the chord as well", "Hold a lofted wing to 0.05 mm,
and loft it from the Wing tab") and in its source comments:

- About five surface guides: wall 2.17–2.67 mm, up to 0.3 mm off ahead of the
  first guide and aft of the last, 93 % within ±0.1 mm. Seven: wall
  2.30–2.67 mm, 98.2 % within ±0.1 mm. Sixteen: wall 2.38–2.57 mm, 99.4 % of
  the inner skin within ±0.05 mm.
- Sag up to 0.15 mm between guides 10–25 % of the chord apart.
- Offset profile guides from 1–1.5 %: loft failed; 0.5 %: a dome; 2 %: built.
- Guides landing anywhere on the drawn profile spline were accepted.
- Guide order: up to 0.04 mm on one loft, none on another.

## See also

- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — the call and its settings
- [curves/01 — The .sldcrv format](../curves/01-sldcrv-file-format.md) — the spline SolidWorks draws through a file's points
- [curves/09 — Curves as loft profiles](../curves/09-curves-as-loft-profiles.md) — guides that meet profiles exactly
- [surfacing/01 — The boundary surface recipe](01-boundary-surface-recipe.md)
- [files/04 — Export a body to STEP](../files/04-export-a-body-to-step.md) — how the lofts were got out to be measured
- [GOTCHAS §50, §52](../../GOTCHAS.md)
