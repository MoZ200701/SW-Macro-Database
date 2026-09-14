---
id: sketches-01-2d-sketch-as-vba
title: Generate a 2D sketch as a VBA macro
status: partly-verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [vba]
api: [ISketchManager.InsertSketch, CreatePoint, CreateLine2, CreateArc, CreateCircleByRadius, CreateSpline, AddConstraint, Select4, GetStartPoint2, GetEndPoint2, GetCenterPoint2]
keywords: [2d sketch, InsertSketch, CreateLine2, CreateSpline, sketch macro, generate vba, weld, construction geometry, shared end point, CreateArc direction]
answers: "How do I emit a VBA macro that draws a fully-defined 2D sketch?"
---

# Generate a 2D sketch as a VBA macro

**Status: partly verified.** The VBA below is well-formed against the documented
API surface, deterministic and complete, and **the macro itself has still not
been run**. Most of the members it uses have now been run on SolidWorks 2026
(revision 34.0.0) from Python over pywin32, and behaved as described here; a
few have not, and two were run in a different form. "What has been run", below,
says which. This entry was `unverified` until that probe run.

## The skeleton

```vb
Option Explicit

Sub CreateSketch()
    Dim swApp As Object, Part As Object
    Set swApp = Application.SldWorks
    Set Part = swApp.ActiveDoc
    If Part Is Nothing Then
        MsgBox "Open the target part and select the sketch plane first."
        Exit Sub
    End If

    ' Uses whatever plane or face is currently selected.
    Part.SketchManager.InsertSketch True

    ' ... entities, relations, dimensions ...

    ' Close the sketch and give it its final name.
    Dim swSketch As Object
    Set swSketch = Part.SketchManager.ActiveSketch
    Part.SketchManager.InsertSketch True
    If Not swSketch Is Nothing Then
        On Error Resume Next
        swSketch.GetFeature().Name = "S4_section"
        On Error GoTo 0
    End If
End Sub
```

**`InsertSketch` is a toggle.** The first call opens a sketch, the second
closes it. Capture `ActiveSketch` *before* the closing call, because afterwards
there is no active sketch to ask.

**It uses the current selection.** This macro never creates or selects a plane,
which is a deliberate choice: wiring geometry into a model is a decision, and a
macro that picks a plane for you picks the wrong one eventually. Say in the
header comment which plane to select.

## Entities

Coordinates are **metres**, and the third coordinate is always 0 in a 2D sketch.

```vb
' A point
Set v_P_crown = Part.SketchManager.CreatePoint(0.000000000, 0.045000000, 0)

' A line
Set v_L_centreline = Part.SketchManager.CreateLine2( _
        0.000000000, -0.040000000, 0, 0.000000000, 0.045000000, 0)
v_L_centreline.ConstructionGeometry = True

' A circle
Set v_C = Part.SketchManager.CreateCircleByRadius(0.0, 0.0, 0, 0.018000000)

' An arc: centre, start, end, direction
Set v_A = Part.SketchManager.CreateArc(cx, cy, 0, sx, sy, 0, ex, ey, 0, 1)
```

Hold every created entity in its own `Dim`ed variable. You need them later to
select for relations and dimensions, and there is no way to look one up by name.

**Arc direction `1` is counter-clockwise from the start point, `-1`
clockwise** (observed on SolidWorks 2026: centre at the origin, start (30, 0)
mm, end (0, 30) mm, direction `1` made the 90° arc, 47.12 mm long; `-1` made
the 270° arc, 141.37 mm long).

## Splines take a flat array

```vb
Dim pts_SP(26) As Double
pts_SP(0) = 0.060000000 : pts_SP(1) = 0.000000000 : pts_SP(2) = 0
pts_SP(3) = 0.059390179 : pts_SP(4) = 0.010758158 : pts_SP(5) = 0
' ... three per point ...
Dim v_SP As Object
Set v_SP = Part.SketchManager.CreateSpline(pts_SP)
```

The array bound is `3n - 1` for n points. Declare it as `Double`, not `Variant`.

## Welding: the pattern that makes a sketch coherent

`CreateLine2` mints its own endpoints. If you also created a standalone point
at the same location, they are two different things sitting on top of each
other, and the sketch is not connected. Weld them with a coincident relation:

```vb
Part.ClearSelection2 True
v_P_keel.Select4 False, Nothing
v_L_centreline.GetStartPoint2().Select4 True, Nothing
Part.SketchManager.AddConstraint "sgCOINCIDENT"  ' weld: L_centreline start == P_keel
```

The shape is always the same and is worth internalising:

1. `ClearSelection2 True`
2. Select the first entity with `Select4 False, Nothing` — `False` means replace
3. Select each subsequent entity with `Select4 True, Nothing` — `True` means append
4. `AddConstraint "<constant>"`

`GetStartPoint2`, `GetEndPoint2` and `GetCenterPoint2` return selectable point
objects, so they can be the target of a relation directly.

### Ends drawn exactly on ends are already one point

Observed on SolidWorks 2026 (revision 34.0.0): an entity **created starting
exactly at another entity's end point does not get a point of its own there.**
A line started on a previous line's end, on an arc's start, and on an Equation
Driven Curve's start each left exactly one sketch point at that spot, and the
new line's `GetStartPoint2` compared equal to the other entity's point. A line
started 0.1 mm away got its own point.

Two consequences for a generator. There is nothing to weld between two such
ends: the coincident relation already holds. And trying to add it anyway
selects one object twice: selecting that shared point and then "the other" end
left the selection count at 1, so a two-entity relation has only one entity to
work on. Skip the weld when both ends were drawn at the same coordinates. The
standalone-point case above — a `CreatePoint` and a line end at the same place
— was not tested, so the weld for it stands as written.

## Relations

```vb
' vertical on one line
Part.ClearSelection2 True
v_L_centreline.Select4 False, Nothing
Part.SketchManager.AddConstraint "sgVERTICAL2D"

' symmetric: two entities plus the mirror line, in that order
Part.ClearSelection2 True
v_P_stbd.Select4 False, Nothing
v_P_port.Select4 True, Nothing
v_L_centreline.Select4 True, Nothing
Part.SketchManager.AddConstraint "sgSYMMETRIC"
```

Constant names are in [sketches/03](03-relation-constants.md).

## Dimensions

```vb
Part.ClearSelection2 True
v_P_origin.Select4 False, Nothing
v_P_stbd.Select4 True, Nothing
Dim disp_D As Object
Set disp_D = Part.Extension.AddDimension2(0.030000000, 0.012000000, 0)
If Not disp_D Is Nothing Then
    disp_D.Dimension.Name = "D1"
    disp_D.Dimension.SystemValue = 0.060000000  ' 60 mm
Else
    Debug.Print "FAIL  D1 - AddDimension2 returned Nothing; the selection may not support a swDistanceDim"
End If
```

See [sketches/04](04-dimensions.md) for why the placement point matters more
than it looks.

## Construction geometry

```vb
v_L_centreline.ConstructionGeometry = True
```

Works on segments. **On a bare sketch point its availability varies by
version**, so if you set it there, be prepared for the line to error and have
the macro carry on.

## What has been run

On SolidWorks 2026 (revision 34.0.0), from Python over pywin32, in the probe
run 20260914-180426 (probes `close_document`, `sketch_open_close`,
`sketch_entities`, `relations`, `dimensions`, `curve_end_points`):

| In this entry | Run? | What was observed |
|---|---|---|
| `InsertSketch True` twice | Yes | a plane selected, the first call opened a sketch (`ActiveSketch` not `Nothing`), the second closed it (`ActiveSketch` `Nothing`) |
| `CreatePoint` | Yes | a point at (70, 80) mm read back there |
| `CreateLine2` | **No** | `ISketchManager.CreateLine` with the same six coordinates ran instead; its ends read back where asked and `GetLength` was in metres |
| `CreateCircleByRadius` | Yes | centre and `GetRadius` read back, in metres |
| `CreateArc(..., 1)` | Yes | direction as stated above |
| `CreateSpline` | **No** | |
| `ConstructionGeometry = True` | On a line, yes | set on a line and read back `True`; not tried on a bare point |
| `Select4 append, data` | Yes | from Python, with `data` a typed null dispatch rather than `Nothing`; 18 selections, each on the first try |
| `GetStartPoint2` / `GetEndPoint2` / `GetCenterPoint2` as relation targets | Yes | relations on them moved the geometry as expected |
| `SketchManager.AddConstraint` | **No** | `IModelDoc2.SketchAddConstraints` with the same constant strings ran instead; see [sketches/03](03-relation-constants.md) |
| `Extension.AddDimension2` and `.Dimension` | **No** | `IModelDoc2.AddDimension2` and `IDisplayDimension.GetDimension2(0)` ran instead; see [sketches/04](04-dimensions.md) |
| `swSketch.GetFeature().Name` | **No** | the sketch feature was found by diffing the tree and renamed through `IFeature.Name` |
| Ends drawn on ends | Yes | as in the section above |

A complete generated sketch in the same style — curves, lines and arcs,
coincident, tangent and equal-length relations, two dimensions — read
`GetConstrainedStatus` 3, fully defined; see
[sketches/10](10-is-the-sketch-fully-defined.md). To move this entry to
`verified`, run [`code/vba/SectionSketch.bas`](../../code/vba/SectionSketch.bas)
in SolidWorks and record what it drew.

## Full generated example

[`code/vba/SectionSketch.bas`](../../code/vba/SectionSketch.bas) — a real
generated fuselage section. 35 entities, 12 relations, 12 dimensions, four
splines, welded throughout, with a counted header of the steps that cannot be
automated.

## The header is part of the output

The generated macro leads with a comment block stating the units, which plane
to select, the entity counts, and **how many manual steps remain and why**.
Counting them in the header rather than leaving them to be discovered in the
body is the difference between a macro someone finishes and one they abandon.

See [sketches/06](06-what-the-sketch-api-cannot-do.md) for what those steps are.

## See also

- [sketches/02 — A 3D sketch as VBA](02-3d-sketch-as-vba.md)
- [sketches/05 — Units and number format](05-units-and-number-format.md)
- [connect/04 — Attach from VBA](../connect/04-attach-from-vba.md)
- [sketches/07 — Equation driven curve](07-equation-driven-curve.md) — curve ends shared with lines
- [sketches/10 — Is the sketch fully defined](10-is-the-sketch-fully-defined.md) — checking the result
- [GOTCHAS §35](../../GOTCHAS.md)
