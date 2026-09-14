---
id: sketches-03-relation-constants
title: Sketch relation constants for AddConstraint
status: partly-verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [vba, python]
api: [ISketchManager.AddConstraint, IModelDoc2.SketchAddConstraints]
keywords: [AddConstraint, SketchAddConstraints, sgCOINCIDENT, sgVERTICAL2D, sgSYMMETRIC, sgCOLINEAR, sgEQUAL, sgSAMELENGTH, sgTANGENT, sgFIXED, sgHORIZONTAL2D, equal radius, relation constants, swConstraintType]
answers: "What string do I pass to AddConstraint for a given sketch relation?"
---

# Sketch relation constants for `AddConstraint`

**Status: partly verified.** Six of these constants were run on SolidWorks
2026 (revision 34.0.0) and each was checked by where the geometry went; one of
them, `sgEQUAL`, did not do what this table implied for circles. The rest are
unverified. "What has been run", below, has the details.

## The table

| Relation | Constant | Notes |
|---|---|---|
| Coincident | `sgCOINCIDENT` | **Verified.** |
| Point on entity | `sgCOINCIDENT` | Same constant; the selection decides which it is. **Verified** (point on a circle, origin on a line) |
| Midpoint | `sgMIDPOINT` | |
| Horizontal | `sgHORIZONTAL2D` | 2D only, in effect. **Verified** on a line |
| Vertical | `sgVERTICAL2D` | 2D only, in effect |
| Parallel | `sgPARALLEL` | |
| Perpendicular | `sgPERPENDICULAR` | |
| Tangent | `sgTANGENT` | **Verified** line to arc; also arc to arc in a fully defined build |
| Equal | `sgEQUAL` | **On two circles it did nothing** on SW 2026: radii 4 and 7 mm stayed. Use `sgSAMELENGTH` there |
| Equal (radius, length) | `sgSAMELENGTH` | **Verified**: made two circles' radii equal, and two arcs equal in a fully defined build. Added after the probe |
| Concentric | `sgCONCENTRIC` | |
| Collinear | `sgCOLINEAR` | **One L.** Historical API spelling; the UI says "Collinear" |
| Symmetric | `sgSYMMETRIC` | Two entities then the mirror line, in that order. **Verified** with two points and a centreline |
| Fix | `sgFIXED` | **Verified** on an Equation Driven Curve: the sketch became fully defined |

## `sgCOLINEAR` is the one to check

The API spelling has one L, against the UI's two. It is worth a second glance
rather than trusting a table, including this one.

## How to call it

```vb
Part.ClearSelection2 True
v_first.Select4 False, Nothing
v_second.Select4 True, Nothing
Part.SketchManager.AddConstraint "sgSYMMETRIC"
```

`Select4(append, data)`. `False` on the first replaces the selection, `True` on
the rest appends. The **order of selection matters** for asymmetric relations:
symmetric wants the two mirrored entities first and the mirror line last.

The form that actually ran, in Python, puts the constant through
**IModelDoc2** `SketchAddConstraints` rather than `ISketchManager.AddConstraint`:

```python
def select(doc: Any, *entities: Any) -> None:
    """Clear the selection, then select each sketch entity in order.

    ``Select4`` on the entity first, retried; then, for a sketch point,
    ``SelectByID2`` at its location, which is in model coordinates and so only
    right on the first plane, where the sketch and the model share them.
    """
    call(doc, "ClearSelection2", True)
    for position, entity in enumerate(entities):
        before = selected_count(doc)
        append = position > 0
        if _retrying(doc, lambda: call(entity, "Select4", append, null()), before, "Select4"):
            continue
        try:
            x, y = float(call(entity, "X")), float(call(entity, "Y"))
        except Exception:  # noqa: BLE001 - a segment has no X, and no location route
            x = y = None
        if x is not None and call(extension(doc), "SelectByID2", "", "SKETCHPOINT", x, y, 0.0, append, 0,
                                  null(), 0) and selected_count(doc) > before:
            _count("SelectByID2 SKETCHPOINT at location")
            continue
        raise Require(
            f"Entity {position + 1} of {len(entities)} could not be selected by any route "
            f"(in {call(doc, 'GetTitle')}, with {_active_title()} active and {selected_count(doc)} selected)."
        )


def relate(doc: Any, constraint: str, *entities: Any) -> None:
    select(doc, *entities)
    call(doc, "SketchAddConstraints", constraint)
    call(doc, "ClearSelection2", True)
```

`null()` is `VARIANT(VT_DISPATCH, None)` and `call` the late-bound helper, both
from [connect/02](../connect/02-attach-from-python.md); `_retrying` repeats the
select up to four times and counts it only if
`ISelectionMgr.GetSelectedObjectCount2(-1)` went up. In the recorded run every
`Select4` succeeded first time, so the `SelectByID2` fallback was not used. Outside the probe
the same sequence once failed on a new circle's centre point, and 14 controlled
trials could not make it fail again; see [GOTCHAS §36](../../GOTCHAS.md).

## Keep the mapping in one place

If you are generating macros, define the mapping as a total map from your own
vocabulary to these strings, so a new relation kind cannot be added without
someone choosing its constant:

```typescript
export const RELATION_CONSTRAINT: Record<RelationKind, string> = {
  coincident: "sgCOINCIDENT",
  pointOnEntity: "sgCOINCIDENT",
  midpoint: "sgMIDPOINT",
  horizontal: "sgHORIZONTAL2D",
  vertical: "sgVERTICAL2D",
  parallel: "sgPARALLEL",
  perpendicular: "sgPERPENDICULAR",
  tangent: "sgTANGENT",
  equal: "sgEQUAL",
  concentric: "sgCONCENTRIC",
  collinear: "sgCOLINEAR",
  symmetric: "sgSYMMETRIC",
  fix: "sgFIXED",
};
```

In a typed language, `Record<Kind, string>` means the compiler catches a missing
entry. That is worth more than it sounds: a silently missing relation produces a
sketch that solves to a different shape and looks plausible.

The map above predates the probe and still says `equal: "sgEQUAL"`. The tool
built from the probe keeps only the verified constants, and maps equal to
`sgSAMELENGTH`:

```python
# relations: every constant below was checked by where the geometry went.
# sgEQUAL left two radii at 4 and 7 mm; sgSAMELENGTH made them equal.
RELATIONS = {
    "coincident": "sgCOINCIDENT",
    "tangent": "sgTANGENT",
    "symmetric": "sgSYMMETRIC",
    "horizontal": "sgHORIZONTAL2D",
    "fixed": "sgFIXED",
    "equal": "sgSAMELENGTH",
}
```

## What has been run

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe `relations`
(run 20260914-180426) in
[`code/python/gear_generator/probe/p1_sketch.py`](../../code/python/gear_generator/probe/p1_sketch.py),
each relation on free geometry in one sketch on the first plane, checked by
coordinates afterwards:

| Constant | Selection | What the geometry did |
|---|---|---|
| `sgCOINCIDENT` | a line's start point, then the part origin's sketch point | the start moved to (0.0, 0.0) |
| `sgHORIZONTAL2D` | that line | its ends went to (0.0, 0.0) and (26.9258, 0.0) mm |
| `sgCOINCIDENT` | the origin, then a free line | the origin lay on the line's extension: ends (25, 25), (45, 45) mm |
| `sgCOINCIDENT` | a free point, then a circle r 10 mm | the point went onto the circle, 10.0 mm from its centre |
| `sgTANGENT` | a line, then an arc r 10 mm | the line's distance from the arc centre became 10.0 mm |
| `sgSYMMETRIC` | two points, then a vertical centreline at x = 150 mm | points at (140, 20) and (160, 20) mm |
| `sgEQUAL` | a circle r 4 mm, then a circle r 7 mm | **nothing**: radii still 4.0 and 7.0 mm |
| `sgSAMELENGTH` | the same two circles | radii both 4.0 mm |
| `sgCOINCIDENT` | an arc's centre point, then a circle | the centre went onto the circle |

Also:

- `sgFIXED` on an Equation Driven Curve took its sketch from
  `GetConstrainedStatus` 2 to 3, and the curve still followed its globals on a
  rebuild (probes `curve_literal`, `curve_fixed_rebuild`).
- The tool's gear build used `sgCOINCIDENT`, `sgTANGENT` (line to arc, arc to
  arc), `sgSAMELENGTH` (two fillet arcs) and `sgFIXED` in one sketch, which read
  fully defined before and after its globals changed (probe `end_to_end`).

Not run: `sgMIDPOINT`, `sgVERTICAL2D`, `sgPARALLEL`, `sgPERPENDICULAR`,
`sgCONCENTRIC`, `sgCOLINEAR`, and `ISketchManager.AddConstraint` itself. Whether
`sgEQUAL` works on two lines was not tried. Why it did nothing on circles is
not known.

## What is not here

Relations whose target cannot be selected over COM at all — anything addressing
a spline's tangent handle, for instance — are not a constants problem. See
[sketches/06](06-what-the-sketch-api-cannot-do.md).

## See also

- [sketches/01 — A 2D sketch as VBA](01-2d-sketch-as-vba.md)
- [sketches/04 — Dimensions](04-dimensions.md)
- [sketches/07 — Equation driven curve](07-equation-driven-curve.md) — `sgFIXED` on a curve
- [sketches/10 — Is the sketch fully defined](10-is-the-sketch-fully-defined.md)
- [GOTCHAS §30](../../GOTCHAS.md)
