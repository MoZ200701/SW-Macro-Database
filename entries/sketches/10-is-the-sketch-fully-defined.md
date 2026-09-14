---
id: sketches-10-is-the-sketch-fully-defined
title: Check whether a sketch is fully defined
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [ISketch.GetConstrainedStatus, ISketchManager.ActiveSketch, IFeature.GetSpecificFeature2]
keywords: [GetConstrainedStatus, fully defined, under defined, over defined, swConstrainedStatus_e, swUnderConstrained, swFullyConstrained, 2, 3, sketch status, black sketch, blue sketch]
answers: "How do I check from code whether a sketch is fully defined?"
---

# Check whether a sketch is fully defined

## What this is for

A generated sketch that is not fully defined looks right until someone drags
it or a rebuild moves it, and then it solves to a different shape. A generator
should check its own output and say so. There is one call for it, and it works
on an open sketch or a closed one.

## The call

`GetConstrainedStatus` is on **ISketch**. On the sketch being edited, that is
`ISketchManager.ActiveSketch`; on a closed sketch, the sketch feature's
`IFeature.GetSpecificFeature2`.

From the calibration probe — a line that is free, then pinned, levelled and
dimensioned:

```python
manager = sc.open_sketch(px, doc, 1)
sketch = call(manager, "ActiveSketch")
line = call(manager, "CreateLine", 0.004, 0.003, 0.0, 0.030, 0.010, 0.0)
under = int(call(sketch, "GetConstrainedStatus"))
px.fact("status_free_line", under)
sc.relate(doc, "sgCOINCIDENT", call(line, "GetStartPoint2"), sc.origin_point(doc))
sc.relate(doc, "sgHORIZONTAL2D", line)
sc.select(doc, line)
call(doc, "AddDimension2", 0.015, 0.010, 0.0)
call(doc, "ClearSelection2", True)
full = int(call(sketch, "GetConstrainedStatus"))
px.fact("status_defined_line", full)
```

and after closing it, through the feature:

```python
def sketch_status(self, name: str) -> int:
    """``GetConstrainedStatus``: 2 under, 3 fully, 4 over (probe constrained_status)."""
    return int(call(call(self._by_name(name), "GetSpecificFeature2"), "GetConstrainedStatus"))
```

`sc.relate` selects the entities and calls `IModelDoc2.SketchAddConstraints`
([sketches/03](03-relation-constants.md)); `sc.origin_point` is the part
origin's sketch point, the first of `GetSketchPoints2` on the
`OriginProfileFeature`. `self._by_name` walks the top-level tree for a feature
of that name, and one level of sub-features under each feature too, so a sketch
is found whether or not a feature has been made from it.

| Value | `swConstrainedStatus_e` | Observed |
|---|---|---|
| 2 | `swUnderConstrained` | yes |
| 3 | `swFullyConstrained` | yes |
| 4 | `swOverConstrained` | **no** — value from `swconst.tlb`, never produced here |

## Why it is not obvious

**Returning without error says nothing about definition.** Every relation and
dimension call in the sketch above returned normally both times; only the
status tells them apart.

**An equation driven curve does not count as defined by itself.** A sketch
holding one Equation Driven Curve read 2, whether its last two creation
arguments (the probe's "lock") were `True` or `False`. `sgFIXED` on the curve
made it 3, and the curve still followed its globals on a rebuild. See
[sketches/07](07-equation-driven-curve.md).

**One free end anywhere is enough.** A fixed curve plus a line drawn from the
curve's start, whose other end was free, read 2.

**Check after the rebuild too.** In the gear build, all three sketches read 3
after building, and read 3 again after the teeth and module globals were
changed and the part rebuilt. A generated sketch that is only defined for the
numbers it was built with would show here.

## What it does not do

- It says *whether*, not *what*. Finding the free entity is still by eye.
- An over-defined sketch (4) was never produced, so how SolidWorks reports one
  that also has errors is not known from this.
- 3D sketches were not checked.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426.

- `constrained_status`: `status_free_line` 2, `status_defined_line` 3,
  `status_after_close` 3 through the feature.
- `curve_literal`: `status_one_locked_curve` 2, `status_one_fixed_curve` 3,
  `status_with_one_unlocked_curve` 2.
- `curve_fixed_rebuild`: `status_fixed_curve_over_globals` 3,
  `status_fixed_curve_after_rebuild` 3.
- `curve_end_points`: `status_curve_fixed_line_from_its_start` 2.
- `end_to_end`: `sketch_statuses = {'Blank Sketch': 3, 'Tooth Space Sketch': 3, 'Bore Sketch': 3}`
  and `sketch_statuses_after` the same. The tooth-space sketch holds two fixed
  involute curves, two lines, three arcs and a closing line, related by
  coincident, tangent and equal-length relations and two dimensions.

## See also

- [sketches/01 — A 2D sketch as VBA](01-2d-sketch-as-vba.md)
- [sketches/03 — Relation constants](03-relation-constants.md)
- [sketches/04 — Dimensions](04-dimensions.md)
- [sketches/06 — What the sketch API cannot do](06-what-the-sketch-api-cannot-do.md) — why some sketches cannot reach 3 from code
- [sketches/07 — Equation driven curve](07-equation-driven-curve.md)
