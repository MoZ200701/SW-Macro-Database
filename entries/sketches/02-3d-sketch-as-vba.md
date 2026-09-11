---
id: sketches-02-3d-sketch-as-vba
title: Generate a 3D sketch as a VBA macro
status: unverified
verified_on: null
language: [vba]
api: [ISketchManager.Insert3DSketch2, CreatePoint, CreateLine2, CreateSpline, AddConstraint]
keywords: [3d sketch, Insert3DSketch2, three coordinates, no plane, spline on surface, CreateArc plane]
answers: "How do I emit a VBA macro that draws a 3D sketch?"
---

# Generate a 3D sketch as a VBA macro

**Status: unverified**, on the same terms as [sketches/01](01-2d-sketch-as-vba.md).

## What changes from 2D

**No plane.** `CreatePoint`, `CreateLine2` and `CreateSpline` take three real
coordinates in space, so there is nothing to select before running the macro.
That removes the 2D version's standing hazard entirely.

```vb
Part.SketchManager.Insert3DSketch2 True

Set v_P_root = Part.SketchManager.CreatePoint(0.000000000, 0.000000000, 0.040000000)
Set v_P_tip  = Part.SketchManager.CreatePoint(0.260000000, 0.000000000, -0.010000000)

Set v_L_spar = Part.SketchManager.CreateLine2( _
        0.000000000, 0.000000000, 0.040000000, _
        0.260000000, 0.000000000, -0.010000000)
```

`Insert3DSketch2` is a toggle in the same way `InsertSketch` is: call it again
to close.

Everything else — welding, relations, dimensions, the select-then-constrain
shape — is identical to the 2D case.

## Splines are the same, with real Z

```vb
Dim pts_SP_edge(8) As Double
pts_SP_edge(0) = 0.000000000 : pts_SP_edge(1) = 0.000000000 : pts_SP_edge(2) = 0.040000000
pts_SP_edge(3) = 0.120000000 : pts_SP_edge(4) = 0.055000000 : pts_SP_edge(5) = 0.030000000
pts_SP_edge(6) = 0.260000000 : pts_SP_edge(7) = 0.000000000 : pts_SP_edge(8) = -0.010000000
Dim v_SP_edge As Object
Set v_SP_edge = Part.SketchManager.CreateSpline(pts_SP_edge)
```

## Arcs and circles: the open question

Whether `CreateArc` and `CreateCircleByRadius` honour the plane implied by
their own three coordinates, or resolve against whatever 3D-sketch plane
happens to be active, **is not settled**. Public sources do not answer it and
it has not been tested.

The safe response is to refuse to guess. Draw every arc and circle by hand, and
still create its centre and endpoints live so the manual instruction can say
"snap to the point already drawn" rather than asking someone to retype
coordinates:

```vb
' C_ring: circle, centre P_mid, r=18mm, normal [1, 0, 0]
' SW-MANUAL: C_ring (circle) - CreateCircleByRadius's plane behaviour inside a 3D
' sketch is UNVERIFIED - drawn by hand rather than guessed. By hand:
'   1. select the already-created point for P_mid (its centre, 120.00, 55.00, 30.00 mm)
'   2. Tools > Sketch Entities > Circle, radius 18.00 mm, normal [1.0000, 0.0000, 0.0000]
'   3. Esc/Enter to finish
```

**If you settle this question, update this entry and downgrade the manual step
to a live call.** That is exactly the kind of finding this repo exists to keep.

## Spline on surface

There is no documented COM call to create a spline constrained to lie on a
surface. Unlike a style spline, though, a plain spline *is* callable, so an
honest approximation is available: sample the true curve densely, pin it exactly
at each control point, and emit `CreateSpline` through the samples with a
comment naming the face it should lie on.

That is a live call and a labelled approximation rather than a manual step.
Between the pinned points, SolidWorks' own interpolation takes over from yours,
and the two agree less as the spacing widens.

## Relations in 3D

The constraint vocabulary is mostly shared with 2D and uses the same constants.
The exception is `sgHORIZONTAL2D` and `sgVERTICAL2D`, whose names give away
that they are plane concepts. In a 3D sketch, "horizontal" is only meaningful
relative to something, so prefer relations that are intrinsically
three-dimensional: coincident, parallel, perpendicular, tangent, equal, fix.

## Full generated example

[`code/vba/Sketch3D.bas`](../../code/vba/Sketch3D.bas) — points, a construction
line, a spline through them, a circle deferred to a manual step, two relations,
one dimension. Note the header's counted manual steps.

## See also

- [sketches/01 — A 2D sketch as VBA](01-2d-sketch-as-vba.md)
- [sketches/06 — What the sketch API cannot do](06-what-the-sketch-api-cannot-do.md)
