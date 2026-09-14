---
id: sketches-04-dimensions
title: Add a dimension and set its value
status: partly-verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [vba, python]
api: [IModelDocExtension.AddDimension2, IModelDoc2.AddDimension2, IDisplayDimension.Dimension, IDisplayDimension.GetDimension2, IDimension.SystemValue, IDimension.Name, IDimension.FullName]
keywords: [AddDimension2, SystemValue, swDistanceDim, swAngularDim, dimension placement, horizontal vertical, circle diameter, arc radius, GetDimension2, FullName, swInputDimValOnCreate, dimension dialog]
answers: "How do I add a driving dimension from a macro and set its value?"
---

# Add a dimension and set its value

**Status: partly verified.** Adding, naming and setting a dimension, and what a
dimension on a circle, an arc and an angle measures, were run on SolidWorks 2026
(revision 34.0.0) from Python, through `IModelDoc2.AddDimension2`. The VBA
below, which calls it through `Part.Extension`, has not been run, and neither
has the placement-point rule, the type table, or the `Nothing` return. "What has
been run", below, has the details. This entry was `unverified` until that probe
run.

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
**Correction, from the probe run:** the member that ran on SolidWorks 2026 is
`AddDimension2` on **IModelDoc2** itself, called on the document. Whether the
`IModelDocExtension` form above also exists and behaves the same was not
tried; do not treat it as verified.

The form that ran, in Python:

```python
def dimension(self, name: str, refs: Sequence[str], at: Tuple[float, float]) -> str:
    """``AddDimension2`` at a point in metres, then named and read back (probe dimensions)."""
    doc = self._doc()
    self._select_entities(refs)
    display = call(doc, "AddDimension2", at[0] / MM_PER_METRE, at[1] / MM_PER_METRE, 0.0)
    call(doc, "ClearSelection2", True)
    if display is None:
        raise SolidWorksError(f"AddDimension2 would not dimension {' and '.join(refs)} as {name}.")
    dimension = call(display, "GetDimension2", 0)
    dimension.Name = name
    kept = str(call(dimension, "Name"))
    if self._open_sketch:
        self._handles[self._open_sketch]["dimensions"].append(kept)
    return kept
```

`_select_entities` is the clear-then-`Select4` sequence shown in
[sketches/03](03-relation-constants.md). The `IDimension` came from
`IDisplayDimension.GetDimension2(0)`, not from the `.Dimension` property the
VBA uses.

**Turn off the value dialog first.** The probe and the tool both switched off
`swInputDimValOnCreate` (`ISldWorks.SetUserPreferenceToggle(10, False)`) around
every `AddDimension2`, and restored it afterwards, because the API help for
`AddDimension2` says to: the dialog it opens is modal, and a modal dialog
blocks every COM call. With it off, no dialog appeared. Behaviour with it on was
not tried.

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

(Unverified: no point-to-point distance was dimensioned in the probe, so this
section is still as it was written.)

## What a single entity's dimension measures

Observed on SolidWorks 2026, selecting one entity and calling `AddDimension2`:

| Selected | The dimension measured | `SystemValue` |
|---|---|---|
| a circle, r 10 mm | its **diameter** | 0.02 |
| an arc, r 10 mm | its **radius** | 0.01 |
| two lines meeting at 20° | the angle | 0.34906585039886595 (20° in radians) |

So a circle dimension set to 0.030 made a circle of radius 15 mm, not 30.

## `SystemValue` is metres, or radians

```vb
disp_D.Dimension.SystemValue = 0.060000000   ' 60 mm
disp_D.Dimension.SystemValue = 0.087266463   ' 5 degrees, in radians
```

`SystemValue` is always SI regardless of document units. There is a separate
`Value` that follows document units; prefer `SystemValue` so your generated
macro cannot be changed by someone's unit setting.

Angles in radians. Convert once, at the boundary, and comment the original.

(Verified for reading and setting in a millimetre part: set 0.030 on a circle's
dimension and its radius became 0.015 m; an angle read in radians. `Value` was
not read, and an inch part was not tried.)

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

(Unverified: `IDimension.Type` was not read in the probe.)

## Check for `Nothing`

`AddDimension2` returns `Nothing` when the selection cannot carry the dimension
you are implying. In VBA that is not an error, so the next line fails on a null
reference instead, several lines from the cause. Check it, and print which
dimension and which type:

```vb
Debug.Print "FAIL  D_halfWidth (D1) - AddDimension2 returned Nothing; " & _
            "the selection above may not support a swDistanceDim"
```

(Unverified: every `AddDimension2` in the probe returned a display dimension.)

## Naming

`disp_D.Dimension.Name = "D1"` before setting the value. Names let you find the
dimension later, and let an equation reference it. The same collision caveat as
feature names applies: read it back if it matters. See
[curves/07](../curves/07-rename-a-feature.md).

Observed: names set while the sketch was open (`OD`, `R`, `HalfSpace`) read
back at once and were still there after the sketch closed. `IDimension.FullName`
read `'OD@Sketch1@Part223.Part'` while the sketch was open and
`'OD@Probe Dims@Part223.Part'` after it was closed and renamed. An equation
names it `"OD@Probe Dims"`; see
[equations/02](../equations/02-link-a-dimension-to-a-global.md).

## Driven dimensions

Everything above creates a driving dimension. A driven one is a separate flag on
the dimension, and if your source model distinguishes them, carry that through.
A driven dimension over-defining a sketch is a different failure from a driving
one, and much easier to diagnose.

## What has been run

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426, probes `dimensions`, `dimension_link` and `constrained_status`
in [`code/python/gear_generator/probe/p1_sketch.py`](../../code/python/gear_generator/probe/p1_sketch.py):

- `IModelDoc2.AddDimension2` on a circle (placed at (15, 15) mm), an arc (at
  (60, 20) mm) and a pair of lines (inside the angle) each returned a display
  dimension; `GetDimension2(0)` gave the `IDimension`; renamed and read back.
- `circle_dimension_measures = 'diameter'` (0.02 m on r 10 mm);
  `arc_dimension_measures = 'radius'`; `angle_dimension_system_value = 0.34906585039886595`.
- Setting `SystemValue = 0.030` on the circle's dimension made its radius 0.015 m.
- `dimension_names_after_close = ['OD', 'R', 'HalfSpace']`; `FullName` as above.
- A line-length dimension completed a fully defined sketch
  ([sketches/10](10-is-the-sketch-fully-defined.md)); a circle dimension linked
  to a global followed it ([equations/02](../equations/02-link-a-dimension-to-a-global.md)).

Not run: `IModelDocExtension.AddDimension2`, `IDisplayDimension.Dimension`, the
placement-orientation rule, `IDimension.Type`, a `Nothing` return, `Value`, and
driven dimensions.

## See also

- [sketches/05 — Units and number format](05-units-and-number-format.md)
- [sketches/03 — Relation constants](03-relation-constants.md)
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [features/04 — Read a feature's dimensions](../features/04-read-a-features-dimensions.md) — dimensions SolidWorks made and named itself
- [sketches/10 — Is the sketch fully defined](10-is-the-sketch-fully-defined.md)
