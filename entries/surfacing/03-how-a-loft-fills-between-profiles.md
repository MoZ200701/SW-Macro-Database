---
id: surfacing-03-how-a-loft-fills-between-profiles
title: How SolidWorks shapes a loft between two profiles, and how its accuracy scaled with the number of guides
status: verified
verified_on: SolidWorks 2026
language: [n/a]
api: []
keywords: [loft accuracy, loft deviation, guide curves, number of guides, accuracy versus guides, guide spacing, sag between guides, linear blend, chordwise stretch, section scaling, taper, sharp corner, guide near corner, loft fails, dome, STEP measurement, 0.05 mm, sliver face, extra face, one guide too many, solid loft refused, drop a guide, 2% guide, tip loop, end loop edge count]
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

The 16-guide row is not the guides alone. The same run also changed how the
intended shape was computed: the profiles were modelled as the spline
SolidWorks draws (below), an offset profile's sharp nose was cut into two
halves, and the section between root and tip was taken as the airfoil scaled
to the local chord. The first two rows differ only in their guides.

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

## One guide too many splits a sliver face, and that is what refuses the solid

A year later, on the same wing's inner skin, `InsertProtrusionBlend2` began
refusing the solid **silently** — `Nothing` back, no feature, no error, no
dialog — while `InsertLoftRefSurface2` made the surface through the same
curves every time. It refused at inward offsets of **1.30, 1.35 and 1.40 mm**
and not at 1.25, 1.45, 1.50 or 1.60.

Twelve rounds of experiments ruled out everything about the *section*: the
point spacing at the nose (six different edits, including resampling the nose
as a proper arc — identical outcomes), the nose corner angle, the
trailing-edge corners, planarity (both end profiles flat to 1e-6 mm), the
guides as such (it refused with **none**), keep-tangency, non-rational,
tessellation factor and merge. The tip loop on its own could not bound a solid
even as a **prism of itself** — lofted to a translated copy, no guides — and the
splines SolidWorks fitted at its nose crease were indistinguishable from a
passing offset's at 1 µm, sampled with `ICurve.Evaluate2`.

What settled it was the **surface loft's own topology**:

| Offset | Surface loft | Tip end loop |
|---|---|---|
| 1.45 mm (solid builds) | 3 faces | 3 edges: 120.03 mm spline, 0.500 mm line, 122.64 mm spline |
| 1.30, 1.35, 1.40 mm (solid refused) | **4 faces** | **4 edges**, one of them a straight chord of 2.18–2.24 mm |

That chord is in no curve file. It is where a **sliver face** — split off along
the nose, 5.8 mm wide at the root and 2.2 mm at the tip — degenerates at the
tip. A loop with it in cannot be closed by anything: a planar surface across
those four edges is refused outright, and a fill across them is too.

**One guide is responsible**: the lower-surface guide at **2 % of the chord**.
Leave it out and the loft is three faces at every offset in the band, both ends
cap, and knitting the three sheets gives one solid
([surfacing/04](04-cap-a-refused-loft-into-a-solid.md)):

| Offset | Volume, 32 guides, knitted |
|---|---|
| 1.30 mm | 2,551,504.3 mm³ |
| 1.35 mm | 2,530,077.9 mm³ |
| 1.40 mm | 2,508,086.5 mm³ |
| 1.45 mm | 2,486,810.8 mm³ |

Dropping the **upper** 2 % guide instead changes nothing — still four faces,
still a refused cap. Dropping the lower one also rescued the *solid loft
itself* at 1.40 mm, though not at 1.30 or 1.35.

So the 2 % rule above is not a floor that is always safe. On a tight inner nose
a guide that close can split a face rather than hold the section, and the
symptom is not a failed loft but a **silently refused solid**, or a fourth face
in a surface that looks right on screen. Two things to check, neither of which
is the return value:

- how many faces the surface loft has, against how many pieces its profiles are
  cut into;
- how many edges each end loop has, against the same number. One edge per
  profile piece is what an end of a two-profile loft is; any more and the loft
  has grown something the section does not have.

Why this guide, at this offset band, was not established. It would take the
same loft at finer offset steps with the nose curvature measured alongside.


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

The sliver-face findings are later and separate: SolidWorks 2026 SP0.0
(revision 34.0.0), 2026-09-22, on a save-as copy of a real 397-feature part,
experiments `e2`–`e4g` (the section edits), `e11`, `e14` (planarity), `e25`
(the refusal band), `e29`, `e4d` (the prism), `e30`, `e31` (the crease
splines), `e36b`, `e37`, `e39`, `e41`, `e42`, `e43` (the topology and the
guide), carried into the Airfoil Converter by commits `1bdec01`, `7caf687` and
`8555d5f`.

- `e25`: solid loft refused at 1.30, 1.35 and 1.40 mm; built at 1.25
  (2,576,169.1 mm³), 1.45 (2,487,078.9), 1.50 (2,464,421.2) and 1.60
  (2,421,781.6).
- `e14`: 3053 samples over each tip loop, out of plane by 4.3e-06 and 1.6e-06 mm.
- `e4d`: the failing tip profile lofted to a copy of itself 200 mm away,
  no guides — **refused**; the passing one — solid, 1 body.
- `e31`: at the crease, the two fitted splines' gap at 0.01 / 0.05 / 0.10 /
  0.20 / 0.30 mm along read 0.0207 / 0.0973 / 0.1868 / 0.3588 / 0.5244 mm on
  the failing offset against 0.0195 / 0.0965 / 0.1913 / 0.3712 / 0.5282 on the
  passing one.
- `e36b`, `e37`: the face and edge counts and the 2.185 / 2.210 / 2.240 mm
  chords in the table above; the tip cap refused at resolution 0, 1 and 3.
- `e39`: with all 33 guides the failing offset lofts to 4 faces and refuses the
  tip cap, with none or with only the three edge guides it lofts to 3 faces and
  caps; the passing offset is 3 faces and caps in all three.
- `e42`: dropping `_lower_02` gives 3 faces and one solid of 2,530,077.9 mm³;
  dropping `_upper_02` leaves 4 faces and a 2.185 mm chord.
- `e43`: the four volumes in the table, and the solid loft refused at 1.30 and
  1.35 but accepted at 1.40 (2,508,068.0 mm³) and 1.45 (2,486,953.2) once
  `_lower_02` was dropped.

## See also

- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — the call and its settings
- [curves/01 — The .sldcrv format](../curves/01-sldcrv-file-format.md) — the spline SolidWorks draws through a file's points
- [curves/09 — Curves as loft profiles](../curves/09-curves-as-loft-profiles.md) — guides that meet profiles exactly
- [surfacing/04 — Cap a refused loft into a solid](04-cap-a-refused-loft-into-a-solid.md) — what to do about a silently refused solid
- [surfacing/01 — The boundary surface recipe](01-boundary-surface-recipe.md)
- [files/04 — Export a body to STEP](../files/04-export-a-body-to-step.md) — how the lofts were got out to be measured
- [reading/13 — Measure a wall between two bodies](../reading/13-measure-a-wall-between-two-bodies.md) — the same wall measured without leaving SolidWorks
- [GOTCHAS §50, §52](../../GOTCHAS.md)
