---
id: sketches-04-dimensions
title: Add a dimension and set its value
status: unverified
verified_on: null
language: [vba]
api: [IModelDocExtension.AddDimension2, IDisplayDimension.Dimension, IDimension.SystemValue, IDimension.Name]
keywords: [AddDimension2, SystemValue, swDistanceDim, swAngularDim, dimension placement, horizontal vertical]
answers: "How do I add a driving dimension from a macro and set its value?"
---

# Add a dimension and set its value

## The call

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

Three steps: select what is being measured, add the dimension at a placement
point, then set its name and value.

`AddDimension2` is on `IModelDocExtension`, not on `ISketchManager`.

## The placement point is the only orientation control there is

`AddDimension2(x, y, z)` takes a point and nothing else. That point is where
the dimension text goes, **and it is what decides whether a distance between
two points reads as horizontal or vertical.**

This is exactly what happens when a person drags a Smart Dimension: drag the
text beside a pair of points and you get the vertical reading, drag it above and
you get the horizontal one. The type enum does not carry orientation. Only the
placement does.

**Consequence:** derive the placement point from the geometry and the kind of
dimension you want, deterministically. Do not carry through a label position a
human chose in some other tool. A number dragged clear of a tall narrow
dimension, to stop it colliding with its own extension lines, is exactly a
number that has crossed into the half-plane SolidWorks reads as the other
orientation. The macro then measures a different distance from the one on
screen, silently.

Placing the text is something SolidWorks is good at, and something the person
is about to redo by hand anyway. Measuring the wrong axis is not recoverable by
dragging.

## `SystemValue` is metres, or radians

```vb
disp_D.Dimension.SystemValue = 0.060000000   ' 60 mm
disp_D.Dimension.SystemValue = 0.087266463   ' 5 degrees, in radians
```

`SystemValue` is always SI regardless of document units. There is a separate
`Value` that follows document units; prefer `SystemValue` so your generated
macro cannot be changed by someone's unit setting.

Angles in radians. Convert once, at the boundary, and comment the original.

## Dimension types

`IDimension.Type` reports a `swDimensionType_e` member:

| Kind | Constant |
|---|---|
| Distance | `swDistanceDim` |
| Horizontal | `swDistanceDim` |
| Vertical | `swDistanceDim` |
| Radius | `swRadiusDim` |
| Diameter | `swDiameterDim` |
| Angle | `swAngularDim` |

Note the first three collapse. The type enum genuinely does not distinguish
orientation, which is the same fact as the placement rule above, seen from the
other side.

You do not pass this constant to `AddDimension2`. It is what you read back, and
what to name in an error message so a failure says which kind of dimension the
selection would not support.

## Check for `Nothing`

`AddDimension2` returns `Nothing` when the selection cannot carry the dimension
you are implying. In VBA that is not an error, so the next line fails on a null
reference instead, several lines from the cause. Check it, and print which
dimension and which type:

```vb
Debug.Print "FAIL  D_halfWidth (D1) - AddDimension2 returned Nothing; " & _
            "the selection above may not support a swDistanceDim"
```

## Naming

`disp_D.Dimension.Name = "D1"` before setting the value. Names let you find the
dimension later, and let an equation reference it. The same collision caveat as
feature names applies: read it back if it matters. See
[curves/07](../curves/07-rename-a-feature.md).

## Driven dimensions

Everything above creates a driving dimension. A driven one is a separate flag on
the dimension, and if your source model distinguishes them, carry that through.
A driven dimension over-defining a sketch is a different failure from a driving
one, and much easier to diagnose.

## See also

- [sketches/05 — Units and number format](05-units-and-number-format.md)
- [sketches/03 — Relation constants](03-relation-constants.md)
