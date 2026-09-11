---
id: sketches-06-sketch-api-limits
title: What the sketch API cannot do, and how to handle it
status: verified
verified_on: documented API surface as of SW 2026
language: [vba]
api: []
keywords: [spline handle, tangent handle, style spline, interior fit point, manual step, SW-MANUAL, limits, cannot select]
answers: "Why can't my macro fully define this sketch, and what do I do about it?"
---

# What the sketch API cannot do, and how to handle it

## The principle first

When the documented API has no way to do something, **emit a counted manual
instruction, not a guessed call.** A macro that silently does the wrong thing is
worse than one that stops and says what it needs.

The pattern used throughout this collection is a `SW-MANUAL:` comment naming the
exact step, plus a tally in the file header so the person running it knows what
they are in for before they start rather than discovering it halfway.

```
' ENTITIES: 35   RELATIONS: 12   DIMENSIONS: 12
' MANUAL STEPS: this sketch cannot be fully automated over COM. Marked
' "SW-MANUAL:" below, and counted here rather than left to be discovered:
'   - 8 relation(s) targeting a spline tangent handle
'   - 8 dimension(s) targeting a spline tangent handle
'   - 1 relation(s) naming a spline's own interior fit point
```

## Limit 1: spline tangent handles cannot be selected

A spline carries tangent handles: one available at any point, with an interior
point carrying two independent arms either side of one shared direction. They
are where a section's fullness and end tangency actually live.

There is no confidently-documented COM call that selects a handle as its own
object. `GetStartPoint2` and `GetEndPoint2` select the spline's **endpoint**,
not the tangent handle sitting off it, and there is no per-index selector for
an interior point's handle either.

**This is not redundant work.** A cubic through two fixed points has a whole
family of tangent directions consistent with it, so a handle's relation and its
length dimension are not implied by the through-points the macro already emits.
Between them they carry the crown tangency and the section fullness, which
nothing else states.

So it becomes an instruction:

```vb
' SW-MANUAL: R_SU_start (vertical) cannot be a live COM call - see the file header.
' By hand:
'   1. select the forward-reaching arrow of the handle on point 1 of SP_stbd_upper
'   2. Tools > Relations > sgVERTICAL2D (vertical)
```

and for the dimension, with the value spelled out in both units:

```vb
' SW-MANUAL: D_PL_end (D12, distance) cannot be a live COM call. By hand:
'   1. select the back-reaching arrow of the handle on point 7 of SP_port_lower
'   2. Smart Dimension, targeting swDistanceDim (distance)
'   value: 0.006308006 m (6.308006174731335 mm)
```

Naming the arm as "forward-reaching" or "back-reaching" rather than "the handle"
is what makes the instruction followable.

## Limit 2: a spline's interior fit points get no standalone object

`CreateSpline` mints its own through-point at each coordinate you gave it.
Those points exist in the sketch, but there is no call to select the Nth
interior point of a spline and weld it to a point you created separately.

If nothing else in the sketch needs an independently selectable point there,
this costs nothing and the generator simply does not create one. If something
does, the two coincident points cannot be welded from code:

```vb
' SW-MANUAL: P_mid is both an interior point of SP_edge and a standalone point
' this file did create. CreateSpline above still minted its own coincident point
' at the same location; there is no documented call to select a spline's Nth
' interior point by index and weld it. By hand: select that interior point on
' SP_edge in the graphics area and add a Coincident relation to P_mid.
```

## Limit 3: style splines have no creation call

There is no `CreateStyleSpline`. A style spline is drawn by hand or not at all.
Anything referencing one inherits that.

## Limit 4: 3D arcs and circles are an open question

Whether `CreateArc` and `CreateCircleByRadius` inside a 3D sketch honour their
own coordinates' plane or the active sketch plane is untested. The choice made
here is to draw them by hand rather than risk them landing on the wrong plane.
See [sketches/02](02-3d-sketch-as-vba.md).

**This one is answerable.** Anyone with SolidWorks and ten minutes could settle
it. If you do, update that entry.

## Limit 5: no spline on surface

No documented call. The approximation that works is a plain spline through
densely sampled points, pinned at the control points, labelled honestly as an
approximation.

## Counting the steps

Count by **cause**, not by occurrence, and count a relation once even if several
of its references are unreachable. The header tally should answer "how much work
is left" in a form a person can act on.

The tally is also a regression test on the generator: if a change makes the
number go up, something that used to be automated no longer is.

## The general rule for a new macro

Before emitting a call, ask whether you can point at documentation for it. If
not, emit the instruction instead and count it. The cost is one manual step. The
cost of the alternative is a sketch that solves to the wrong shape and looks
right.

## See also

- [sketches/01 — A 2D sketch as VBA](01-2d-sketch-as-vba.md)
- [sketches/02 — A 3D sketch as VBA](02-3d-sketch-as-vba.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md) — the same rule, as an authorship rule
