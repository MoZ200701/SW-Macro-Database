---
id: features-06-reference-plane-normal-to-a-line
title: A reference plane square to a sketch line, through its end
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertRefPlane, IModelDocExtension.SelectByID2, ISketch.ModelToSketchTransform, IMathTransform.Inverse, IMathTransform.ArrayData]
keywords: [InsertRefPlane, reference plane, perpendicular plane, normal to curve, plane normal to line, plane through point, swRefPlaneReferenceConstraint_Perpendicular, swRefPlaneReferenceConstraint_Coincident, 2, 4, EXTSKETCHPOINT, EXTSKETCHSEGMENT, sketch frame, plane orientation, cone generatrix, bevel heel plane, back cone]
answers: "How do I make a reference plane perpendicular to a sketch line at its end, and which way does a sketch on that plane face?"
---

# A reference plane square to a sketch line, through its end

## What this is for

A section that must stay square to a line that moves: the heel and toe sections
of a bevel gear, square to its pitch cone's generatrix; a profile at the end of
a path. The plane has to follow the line when the line's angle or length is
driven by a global, and anything sketched on it has to land the same way round
every time. This entry is the `InsertRefPlane` form that did that, the
selections it needs, and where the plane's sketch axes turned out to be.

## The call

The tool's form, written from the probe:

```python
def ref_plane_normal(self, handle: str, name: str, line: str, point: str) -> str:
    """A plane square to a closed sketch's line, through a point on it (probe ref_plane_normal).

    The line at mark 0, the point by its model location at mark 1, and
    InsertRefPlane(perpendicular 2, 0, coincident 4, 0, 0, 0). On SolidWorks
    2026 the plane's sketch had its origin at the point, x along the cone's
    outward radial, y along +Y, and kept that frame as the line moved.
    """
    doc = self._doc()
    self._select_segment(line, append=False, mark=0)
    self._select_sketch_point(point, append=True, mark=1)
    before = self._top_names()
    made = call(call(doc, "FeatureManager"), "InsertRefPlane", findings.REF_PLANE_PERPENDICULAR, 0.0,
                findings.REF_PLANE_COINCIDENT, 0.0, 0, 0.0)
    call(doc, "ClearSelection2", True)
    if made is None:
        raise SolidWorksError(f"InsertRefPlane would not make {name} square to {line} through {point}.")
    kept = self._rename(self._made_since(before, "RefPlane", "InsertRefPlane"), name)
    self._handles[handle] = kept
    return kept
```

`findings.REF_PLANE_PERPENDICULAR = 2` and `findings.REF_PLANE_COINCIDENT = 4`
(`swRefPlaneReferenceConstraints_e` perpendicular and coincident, read from the
type library). `_select_segment` selects `"Line2@<sketch>"` as
`"EXTSKETCHSEGMENT"`, as in [features/05](05-revolve.md).

**Selecting the line's end by where it is.** The sketch is closed by now, so
the point is found by its model-space location: its sketch `X`, `Y` put
through the sketch's inverse transform, then `SelectByID2` with an empty name:

```python
def _select_sketch_point(self, ref: str, append: bool, mark: int) -> None:
    """An end of a closed sketch's line, by its location in model space (probe ref_plane_normal)."""
    handle = ref.partition(".")[0]
    record = self._handles.get(handle) or {}
    sketch = (self._handles.get(record.get("sketch") or "") or {}).get("sketch")
    if sketch is None:
        raise SolidWorksError(f"{ref!r} is not a point of a sketch this build made.")
    point = self._entity(ref)
    x, y = float(call(point, "X")), float(call(point, "Y"))
    data = [float(v) for v in call(call(call(sketch, "ModelToSketchTransform"), "Inverse"), "ArrayData")]
    scale = data[12] if len(data) > 12 and data[12] else 1.0
    model = (scale * (data[0] * x + data[3] * y) + data[9], scale * (data[1] * x + data[4] * y) + data[10],
             scale * (data[2] * x + data[5] * y) + data[11])
    doc = self._doc()
    before = self._selected()
    if not (call(call(doc, "Extension"), "SelectByID2", "", "EXTSKETCHPOINT", model[0], model[1], model[2], append,
                 mark, _null_dispatch(), 0) and self._selected() > before):
        raise SolidWorksError(f"The point {ref} could not be selected at its place {self._selection_context()}.")
```

Interfaces and units:

- `InsertRefPlane(first constraint, first value, second constraint, second
  value, third constraint, third value)` on **IFeatureManager**. The first
  reference is the selection at mark **0**, the second the one at mark **1**.
  Both values `0.0`; the third reference unused (`0, 0.0`). Returns the
  `RefPlane` feature, or `None`.
- `SelectByID2(name, type, x, y, z, append, mark, callout, option)` on
  **IModelDocExtension**; `x, y, z` in **metres**, model space.
- `ModelToSketchTransform` on **ISketch**; `Inverse` and `ArrayData` on
  **IMathTransform**, rotation by columns
  ([reading/05](../reading/05-sketch-to-model-transform.md)).

## Where its sketch faces

This is the part a caller needs and cannot guess. In the probe the line ran
from the origin, on the Top plane, at 30° from +Z toward +X (a cone's generatrix
about Z), 40 mm long. The plane's sketch, read back in model space:

| | Model space | What it is |
|---|---|---|
| origin | (20, 0, 34.641) mm | the line's end |
| sketch x | (0.866, 0, −0.5) | the cone's **outward** radial, square to the line in the XZ plane, pointing away from the Z axis |
| sketch y | (0, 1, 0) | model **+Y** |
| normal | (0.5, 0, 0.866) | **along** the line, away from the origin |

With the line's angle linked to a global of 50 and its length to 45, and one
rebuild, the plane moved to the new end (34.472, 0, 28.925) mm, and its sketch x
became (0.643, 0, −0.766), y (0, 1, 0), normal (0.766, 0, 0.643): the same
three signs. **It kept the frame** as the line moved, so a profile drawn on it
lands the same way round at any cone angle.

How the frame was read: a sketch opened and closed on the plane, its
`ModelToSketchTransform.Inverse.ArrayData`, and the points (0,0,0), (1,0,0),
(0,1,0), (0,0,1) put through it.

```python
def plane_frame(px: Px, doc: Any, plane: Any) -> Tuple[Vec, Vec, Vec, Vec]:
    """Where a plane's sketch is in model space: its origin in mm, and its x, y and normal as unit vectors."""
    from .p2_helical import sketch_to_model  # noqa: PLC0415

    before = sc.feature_names(doc)
    manager = open_on(px, doc, plane)
    data = sketch_to_model(call(manager, "ActiveSketch"))
    sc.close_sketch(px, doc, "Probe Frame", before, allow_empty=True)
    origin = transform_point(data, (0.0, 0.0, 0.0))

    def axis(v: Vec) -> Vec:
        tip = transform_point(data, v)
        d = (tip[0] - origin[0], tip[1] - origin[1], tip[2] - origin[2])
        n = math.sqrt(sum(c * c for c in d))
        return (d[0] / n, d[1] / n, d[2] / n)

    return (origin[0] * sc.MM, origin[1] * sc.MM, origin[2] * sc.MM), axis((1.0, 0.0, 0.0)), axis((0.0, 1.0, 0.0)), \
        axis((0.0, 0.0, 1.0))
```

## The probe's selections and call

From [`code/python/gear_generator/probe/p2_bevel.py`](../../code/python/gear_generator/probe/p2_bevel.py),
probe `ref_plane_normal`, after drawing a vertical centreline and the line from
the origin, relating both to the origin, and dimensioning the angle (`Cone`) and
the length (`Heel`):

```python
px.require(_select_segment(doc, sketch, line_name, False, 0), f"{line_name}@{sketch} selects at mark 0")
count = sc.selected_count(doc)
picked = call(sc.extension(doc), "SelectByID2", "", "EXTSKETCHPOINT", end_model[0] / sc.MM, 0.0,
              end_model[2] / sc.MM, True, 1, sc.null(), 0)
px.require(bool(picked) and sc.selected_count(doc) > count, "the line's end selects at mark 1 by its location")
before = sc.feature_names(doc)
made = call(sc.feature_manager(doc), "InsertRefPlane", 2, 0.0, 4, 0.0, 0, 0.0)
call(doc, "ClearSelection2", True)
new = [n for n, t in sc.new_features(doc, before) if t == "RefPlane"]
px.require(made is not None and len(new) == 1, "InsertRefPlane(perpendicular 2, coincident 4) makes one plane")
```

`sc.MM` is 1000, so `end_model[0] / sc.MM` is metres.

## Why it is not obvious

**Two references, two marks.** The line is mark 0 and the point mark 1, and the
constraint pairs with them in that order: perpendicular to the first,
coincident with the second.

**The point is selected by location, not by name.** The line's end has no name
a caller can build, so it is picked by its model coordinates with type
`"EXTSKETCHPOINT"` and an empty name. The line's own name comes from
`ISketchSegment.GetName` read while the sketch was open.

**The plane carries no dimension.** `ref_plane_normal_dimensions = []`. Nothing
about the plane can be linked to a global; it follows the line because the line
is driven.

## What it does not do

- Other orderings of the constraints or the marks, a point that is not a line's
  end, and a curve rather than a line were not tried.
- The sketch frame was measured only for a line in the Top plane's XZ plane,
  leaning toward +X. For a line in another plane, or leaning the other way, the
  frame's signs are not known; measure them the same way.
- Selecting the point by name or through `ISketchPoint.Select4` was not tried
  once the sketch was closed.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe `ref_plane_normal`
in development runs 20260915-015506 and 20260915-022314 (the second passing all
41 probes it ran), with
the same facts in both. Excerpts:
[`probe-log-excerpts-20260915.txt`](../../code/python/gear_generator/probe/results/probe-log-excerpts-20260915.txt).

- `cone_dimensions_deg_mm = (29.999999999999996, 40.0)`.
- "Line2@Cone Sketch selects at mark 0"; "the line's end selects at mark 1 by
  its location"; "InsertRefPlane(perpendicular 2, coincident 4) makes one plane".
- `ref_plane_normal_dimensions = []`.
- `ref_plane_normal_frame_30 = {'origin_mm': (19.999999999999996, 0.0, 34.641016151377556), 'x': (0.8660254037844386, 0.0, -0.5000000000000001), 'y': (0.0, 1.0, 0.0), 'normal': (0.5000000000000001, 0.0, 0.8660254037844386), 'x_along_outward': 1.0, 'y_along_plus_y': 1.0, 'normal_along_line': 1.0}`.
- After linking to 50° and 45 mm and rebuilding:
  `ref_plane_normal_frame_50 = {'origin_mm': (34.47199994035407, 0.0, 28.9254424358942), 'x': (0.6427876096865379, 0.0, -0.7660444431189793), 'y': (0.0, 1.0, 0.0), 'normal': (0.7660444431189793, 0.0, 0.6427876096865379), …}`,
  "the plane's sketch keeps the same frame when the line moves".
- In run 20260915-022314 the tool made each bevel part's heel and toe planes
  through `Session.ref_plane_normal` (`ok plane Heel Plane` in the build logs),
  and probe `bevel_end_to_end` passed with every sketch fully defined.

## See also

- [features/05 — Revolve](05-revolve.md) — selecting a sketch line by `LineN@Sketch`
- [features/07 — Loft cut](07-loft-cut.md) — sections on planes like this
- [reading/05 — Sketch to model transform](../reading/05-sketch-to-model-transform.md) — the frame read-back
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md) — driving the line
- [sketches/04 — Dimensions](../sketches/04-dimensions.md) — placing the angle dimension
- [reading/04 — Read the selection](../reading/04-read-the-selection.md) — selection marks
- [GOTCHAS §43](../../GOTCHAS.md)
