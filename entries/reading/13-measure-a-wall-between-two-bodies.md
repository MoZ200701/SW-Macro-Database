---
id: reading-13-measure-a-wall-between-two-bodies
title: Measure the wall between two bodies without leaving SolidWorks
status: verified
verified_on: SolidWorks 2026 SP0.0 (revision 34.0.0)
language: [python]
api: [IFace2.GetTessTriangles, IFace2.GetClosestPointOn, IFeature.GetFaces, IBody2.GetMassProperties, IFace2.GetArea]
keywords: [GetTessTriangles, GetClosestPointOn, wall thickness, skin thickness, offset check, distance between surfaces, tessellation, measure in SolidWorks, no STEP, GetMassProperties, volume slot, validate a measurement, closest point zero]
answers: "How do I check from code that the skin between two lofted surfaces really is the thickness I asked for?"
---

# Measure the wall between two bodies

## What this is for

An inner surface offset from an outer one is only as good as the wall between
them, and the loft SolidWorks draws through the curves is not the offset you
computed ([surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md)).
That entry measured the wall by writing each loft to STEP and reading the files
back. This does it **inside SolidWorks**, in one script, with no export: points
off one surface's tessellation, distance to the other surface's faces.

It is coarser than a STEP comparison — a few dozen points, not a mesh — and it
is fast enough to run after every change.

## The two calls

```python
def sample(fs, per_face=16):
    """Points spread over the faces, in mm, away from the ends and the trailing edge."""
    pts = []
    for f in fs:
        tess = z(f, "GetTessTriangles", True)
        verts = [tuple(v * 1000 for v in tess[i:i + 3]) for i in range(0, len(tess), 3)]
        keep = [p for p in verts if 200.0 <= abs(p[0]) <= 800.0]
        if not keep:
            continue
        step = max(1, len(keep) // per_face)
        pts += keep[::step][:per_face]
    return pts


def wall(tag):
    outer, inner = faces(OUTER), faces(INNER)
    pts = sample(inner)
    ds = []
    for p in pts:
        best = 1e9
        for f in outer:
            got = z(f, "GetClosestPointOn", p[0] / 1000.0, p[1] / 1000.0, p[2] / 1000.0)
            if got is None:
                continue
            best = min(best, math.dist(p, tuple(v * 1000 for v in got[:3])))
        if best < 1e8:
            ds.append(best)
    ds.sort()
```

From experiment `e27_wall.py` of 2026-09-22, as it ran. `faces(name)` is
`IFeature.GetFaces` on the loft feature; `z` is the late-bound helper that
calls a member which came back as an uncalled method object
([GOTCHAS §55](../../GOTCHAS.md)).

Interfaces and units:

- `IFace2.GetTessTriangles(noConversion)` returns a **flat array of doubles**,
  three per vertex, nine per triangle. With `True` they are in **metres**; the
  code above multiplies by 1000. It comes back through pywin32 as an **uncalled
  method object**, so it has to be invoked explicitly.
- `IFace2.GetClosestPointOn(x, y, z)` takes a point **in metres** and returns
  the nearest point on that face, again in metres, in its first three items. It
  is per **face**, not per body, so the wall is the minimum over every face of
  the other body.
- `IFeature.GetFaces` gives a feature's faces. For a body's faces, go through
  `IFace2.GetBody` and the body, as in
  [surfacing/04](../surfacing/04-cap-a-refused-loft-into-a-solid.md).

## Validate the measurement before you believe it

The first thing the script does is measure a point **back to its own face**:

```python
tess = z(fs[0], "GetTessTriangles", True)
p = tuple(v * 1000 for v in tess[0:3])
got = z(fs[0], "GetClosestPointOn", p[0] / 1000.0, p[1] / 1000.0, p[2] / 1000.0)
```

That must read zero, and it did: **0.000000 mm**. Without it, a units mistake
between the two calls — one in metres, one in millimetres — reads as a
plausible-looking wall and nothing complains.

## What it read

Inner skin against outer skin on a real wing, 48 to 64 sample points over 3 or
4 inner faces against 20 outer faces, at four offsets:

| Offset asked | Points | Min | Median | Max |
|---|---|---|---|---|
| 1.25 mm | 48 | 0.948 | **1.248** | 1.276 |
| 1.35 mm | 64 | 1.158 | **1.345** | 1.426 |
| 1.45 mm | 48 | 1.227 | **1.447** | 1.464 |
| 1.60 mm | 48 | 1.296 | **1.598** | 1.624 |

The median is within **0.002–0.005 mm** of the offset asked for every one. The
minima are 0.2–0.3 mm thin, which is the loft sagging between guides and near
the ends, exactly the effect
[surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md) measured
from STEP — the sample deliberately keeps to 200–800 mm of the span and still
sees it.

## Volume as the second reading

`IBody2.GetMassProperties(accuracy)` returns twelve doubles; **item 3 is the
volume, in cubic metres**. Item 4 was read once as an area in square
metres (2.2802351, on a six-face sheet) and never checked against anything, so
treat it and the rest of the array as unidentified:

```python
volume_mm3 = float(call(body, "GetMassProperties", 1.0)[3]) * 1e9
```

It was cross-checked against the wall: over about 435,000 mm² of inner surface,
the enclosed volume fell about **21,400 mm³ per 0.05 mm** of extra offset
(2,487,078.9 → 2,464,421.2 → 2,421,781.6 mm³ at 1.45, 1.50 and 1.60 mm), which
is the surface area (from `IFace2.GetArea`, 433,971.8 mm²) times the step, as
it should be.

Note that `IMassProperty` — the document-level object that
[reading/10](10-mass-properties-as-an-oracle.md) uses — is not the route to
**one body's** volume from Python: `IMassProperty.AddBodies` reached by
attribute access answered a `bool` and then could not be called with its
bodies (`'bool' object is not callable`). `IBody2.GetMassProperties` is what
worked ([GOTCHAS §55](../../GOTCHAS.md)).

## What it does not do

- **It is a sample, not a survey.** 48–64 points over a wing. It catches a wall
  that is wrong everywhere and a wall that is wrong in a sampled place; it can
  miss a local thin spot between samples.
- The tessellation's vertices are wherever SolidWorks put them, so the sample
  is not evenly spread — the code takes every *n*th vertex, which is a stride
  through SolidWorks' ordering, not through space.
- `GetTessTriangles`' second argument was only ever `True` (no conversion).
  What `False` returns was not checked.
- `GetClosestPointOn` was only used on faces of a surface body. Whether it
  behaves the same on a solid body's faces was not tested.
- The accuracy argument to `GetMassProperties` was only ever `1.0`.
- It measures the distance from a point on one surface to the nearest point of
  the other, which is the wall only where the two are roughly parallel. Near
  the trailing edge they are not, which is one reason the sample excludes it.

## Evidence

SolidWorks 2026 SP0.0 (revision 34.0.0), Windows, Python 3.13 with pywin32, on
a save-as copy of a real 397-feature part, the inner skin reloaded to each
offset in turn ([curves/12](../curves/12-roll-the-tree-back-before-reloading.md)).
Experiments `e26`, `e27` and `e25` of 2026-09-22.

- `e26`: `wing_loft` (`Blend`) 20 faces, 423 tessellation doubles;
  `wing_inner_1.4mm_loft` (`BlendRefSurface`) 4 faces, 11,277 doubles;
  `GetClosestPointOn` on a point of its own face, **0.000000 mm**.
- `e27`: the table above, and the same validation reading 0.000000 mm.
- `e25`: `IMassProperty` reached through `AddBodies` failed with `'bool' object
  is not callable` on all four passing offsets, while
  `IBody2.GetMassProperties(1.0)[3]` gave 2,576,169.1 / 2,487,078.9 /
  2,464,421.2 / 2,421,781.6 mm³ at 1.25 / 1.45 / 1.50 / 1.60 mm.

## See also

- [reading/10 — Mass properties as an oracle](10-mass-properties-as-an-oracle.md) — the document's volume, and when to use it instead
- [reading/09 — Bounding box as a check](09-bounding-box.md)
- [surfacing/03 — How a loft fills between two profiles](../surfacing/03-how-a-loft-fills-between-profiles.md) — the same wall measured from STEP files, in more detail
- [surfacing/04 — Cap a refused loft into a solid](../surfacing/04-cap-a-refused-loft-into-a-solid.md) — getting a body worth measuring
- [files/04 — Export a body to STEP](../files/04-export-a-body-to-step.md) — the route this avoids
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — why `GetTessTriangles` has to be called
- [GOTCHAS §55](../../GOTCHAS.md)
