---
id: reading-16-read-a-sketch-as-one-ordered-loop
title: Read a drawn sketch back as points and tangents in model space, ready to chain into one loop
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [ISketch.GetSketchSegments, ISketchSegment.ConstructionGeometry, ISketchSegment.GetType, ISketchSegment.GetCurve, ISketchSegment.GetStartPoint2, ISketchArc.GetRotationDir, ISketchSpline.GetPoints2, ICurve.GetEndParams, ICurve.ReverseEvaluate, ICurve.Evaluate2, ICurve.GetTessPts, ISketch.ModelToSketchTransform, ISketchManager.AddToDB]
keywords: [read a sketch, sketch to points, ordered loop, chain segments, GetSketchSegments, ConstructionGeometry, construction lines, centreline, GetCurve on a sketch segment, GetEndParams, unbounded line, -10000 10000, arc full circle, ReverseEvaluate, GetTessPts with ends, Evaluate2 tangent, corner angle, tangent join, sketch inference, automatic relation, AddToDB, import a section sketch]
answers: "How do I read a sketch the user drew back as ordered points in model space, with its corners, without editing it?"
---

# Read a drawn sketch back as points and tangents in model space

## What this is for

A user draws a section — lines, arcs, a spline, some construction lines — and
your program needs it as one ordered outline in model coordinates, with each
corner's angle. Everything needed is on the closed sketch feature: no sketch
edit and no selection.

## The calls

All in **sketch space and metres** until put through the inverse of
`ISketch.ModelToSketchTransform`, read by columns
([reading/05](05-sketch-to-model-transform.md), [GOTCHAS §10](../../GOTCHAS.md)).

1. `IFeature.GetSpecificFeature2` on the sketch feature → `ISketch`.
2. `ISketch.GetSketchSegments`. **Drop every segment whose
   `ISketchSegment.ConstructionGeometry` reads True**: it did for a
   `CreateCenterLine` line and for a line made construction.
3. Per kept segment, `GetType` (0 line, 1 arc, 3 spline) and its ends:
   `GetStartPoint2` / `GetEndPoint2` for lines and arcs, the first and last of
   `ISketchSpline.GetPoints2` for splines, each `ISketchPoint.X/Y/Z`. Arcs also
   `GetCenterPoint2`, `GetRadius` and `ISketchArc.GetRotationDir`.
4. `ISketchSegment.GetCurve` → `ICurve`, and sample it **between the
   segment's own ends**, because the curve is not bounded by the segment:

| Segment | `ICurve.GetEndParams` | How to sample it |
|---|---|---|
| Line | **−10000 … 10000**, not closed: the unbounded line | `ReverseEvaluate` of the ends gave 0 … 0.0278 (its length in metres); `GetTessPts` with the ends gave exactly the two end points |
| Arc | **0 … 2π, closed, periodic**: the whole circle | `ReverseEvaluate` of the ends gives the arc's two parameters, but which way round is the arc's span is not given. `GetTessPts(chord, length, start, end)` with the ends **followed the arc as drawn**, for both rotation directions |
| Spline | 0 … 1 | `Evaluate2` and `GetTessPts` directly |

5. **Corner angles from derivatives, not chords**: `ICurve.Evaluate2(t, 1)` at
   each end gives the tangent (items 3–5), which, put through the rotation,
   gives the turn at each join exactly. (In an offline test of the probe's
   chaining code, not a SolidWorks run, chords of 40 points per arc gave a
   15.63° corner as 16.88°.)

`GetEndParams` has four by-reference outs; through pywin32 it came back as a
5-tuple `(ok, t0, t1, closed, periodic)`. The probe accepts both forms:

```python
def get_end_params(curve: Any) -> Optional[Tuple[float, float, bool, bool]]:
    outs = [harness.VARIANT(harness.pythoncom.VT_BYREF | harness.pythoncom.VT_R8, 0.0),
            harness.VARIANT(harness.pythoncom.VT_BYREF | harness.pythoncom.VT_R8, 0.0),
            harness.out_bool(), harness.out_bool()]
    res = call(curve, "GetEndParams", *outs)
    if isinstance(res, tuple) and len(res) == 5:
        return float(res[1]), float(res[2]), bool(res[3]), bool(res[4])
    return float(outs[0].value), float(outs[1].value), bool(outs[2].value), bool(outs[3].value)
```

The tessellation of one segment between its ends, sketch space in and out:

```python
tess = call(curve, "GetTessPts", 1e-7, 1e-8,
            harness.VARIANT(harness.pythoncom.VT_ARRAY | harness.pythoncom.VT_R8, list(sa)),
            harness.VARIANT(harness.pythoncom.VT_ARRAY | harness.pythoncom.VT_R8, list(sb)))
```

(`sa`, `sb` the segment's start and end as sketch-space triples in metres.)
The whole reader, about 140 lines, is
[`code/python/sketch_loop/read_sketch.py`](../../code/python/sketch_loop/read_sketch.py),
copied from the probe that ran it.

Chaining the kept segments into one loop is plain geometry: start from a known
end, take the next segment whose start or end meets the current end
(reversing it if needed), and negate both of a reversed segment's tangents and
swap them. The probe did that with a 1e-4 mm join tolerance; it is not in the
code file, being the application's rule about where a loop starts.

## What it read

Two sections drawn from code on planes offset from the Front plane
(`InsertRefPlane(8 | 256, d, 0, 0, 0, 0)`, sketch frame the identity with a
translation), **out of order and partly backwards**, with a centreline and a
construction line among them:

| | closed (Z = −200) | half (Z = −300) |
|---|---|---|
| Segments / construction dropped / kept | 7 / 2 / 5 | 4 / 1 / 3 |
| Ordered loop matches the drawing, every segment and direction | yes | yes |
| Closure gap, largest gap at a join | 7.9e-15 mm, 1.6e-14 mm | open; both ends on X = 0.000000000 |
| Error against what was drawn (lines, arcs, spline fit points) | < 5e-10 mm, both samplings | < 5e-10 mm |
| Corners from `Evaluate2` tangents | 68.7345° and 15.6327° (drawn: the same), arc→spline 24.933° | 15.6327°, 21.142° |

A third sketch with a line drawn **tangent** to an arc read 0.0000° at that
join (not flagged as a corner) and 47.763° at a real corner, as drawn.

The arc→spline angles have no drawn value: a sketch spline's free end points
wherever its natural end puts it, so a drawn section's arc–spline joins are
corners unless the user adds a tangent relation.

## Why it is not obvious

- **The curve is bigger than the segment.** A line's curve is infinite and an
  arc's is the whole circle; sampling over `GetEndParams` draws the wrong
  thing. Bound everything by the segment's own ends.
- **Construction geometry is in the segment list.** Centrelines included.
- **Drawing from code can be moved by sketch inference.** The first try of the
  tangent sketch came back 0.395 mm off what was drawn: a line drawn 2.2° off
  horizontal had been given a Horizontal relation automatically. Draw with
  `ISketchManager.AddToDB = True` (and restore it after)
  ([GOTCHAS §94](../../GOTCHAS.md)). The reader reported the moved geometry
  faithfully; it was the drawing that was wrong.
- **A Front-parallel plane cannot catch a transposed transform.** Reading the
  rotation by rows gave the same points here to 0.0 mm (GOTCHAS §10). The code
  reads by columns anyway; a sketch on the Right Plane is where it would show
  ([GOTCHAS §91](../../GOTCHAS.md)).

## What it does not do

- Ellipses, slots, sketch text, points and 3D sketches were not drawn.
- Only planes parallel to Front were read.
- Sketches the user drew by hand were not read; these were drawn from code, so
  relations a user adds (and inference while they draw) were not exercised.

## Evidence

SolidWorks 2026 (revision 34.0.0), Windows Python 3.13 with pywin32 from WSL,
Plane-Builder probes, 2026-11-03:

- `p1_sketch_read-20261103-041058`: the closed and half sketches, the counts,
  order, closure, errors, corner angles and the `GetEndParams` values above.
- `p1_sketch_read-20261103-043300`: the tangent sketch drawn without
  `AddToDB`, moved 0.395 mm by inference; `-043327`: with `AddToDB` True, as
  drawn, tangent join 0.0000°.

## See also

- [reading/05 — Sketch to model transform](05-sketch-to-model-transform.md)
- [reading/06 — Live sketch monitor](06-live-sketch-monitor.md) — watching a sketch while the user draws
- [sketches/01 — A 2D sketch as VBA](../sketches/01-2d-sketch-as-vba.md) — drawing the same entities
- [sketches/06 — What the sketch API cannot do](../sketches/06-what-the-sketch-api-cannot-do.md)
- [features/13 — A surface revolve from an imported curve](../features/13-surface-revolve-from-an-imported-curve.md) — a sketch spline written from points
- [GOTCHAS §10, §91, §94](../../GOTCHAS.md)
