---
id: sketches-03-relation-constants
title: Sketch relation constants for AddConstraint
status: partly-verified
verified_on: null
language: [vba]
api: [ISketchManager.AddConstraint]
keywords: [AddConstraint, sgCOINCIDENT, sgVERTICAL2D, sgSYMMETRIC, sgCOLINEAR, relation constants, swConstraintType]
answers: "What string do I pass to AddConstraint for a given sketch relation?"
---

# Sketch relation constants for `AddConstraint`

## The table

| Relation | Constant | Notes |
|---|---|---|
| Coincident | `sgCOINCIDENT` | |
| Point on entity | `sgCOINCIDENT` | Same constant; the selection decides which it is |
| Midpoint | `sgMIDPOINT` | |
| Horizontal | `sgHORIZONTAL2D` | 2D only, in effect |
| Vertical | `sgVERTICAL2D` | 2D only, in effect |
| Parallel | `sgPARALLEL` | |
| Perpendicular | `sgPERPENDICULAR` | |
| Tangent | `sgTANGENT` | |
| Equal | `sgEQUAL` | |
| Concentric | `sgCONCENTRIC` | |
| Collinear | `sgCOLINEAR` | **One L.** Historical API spelling; the UI says "Collinear" |
| Symmetric | `sgSYMMETRIC` | Two entities then the mirror line, in that order |
| Fix | `sgFIXED` | |

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

## What is not here

Relations whose target cannot be selected over COM at all — anything addressing
a spline's tangent handle, for instance — are not a constants problem. See
[sketches/06](06-what-the-sketch-api-cannot-do.md).

## See also

- [sketches/01 — A 2D sketch as VBA](01-2d-sketch-as-vba.md)
- [sketches/04 — Dimensions](04-dimensions.md)
