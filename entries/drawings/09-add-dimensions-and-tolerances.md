---
id: drawings-09-add-dimensions-and-tolerances
title: Add dimensions to a drawing view from code, with symmetric and fit tolerances
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IView.GetVisibleEntities2, IView.SelectEntity, IModelDoc2.AddDimension2, IDisplayDimension.GetDimension2, IDisplayDimension.SetPrecision3, IDisplayDimension.SetText, IDimension.Tolerance, IDimensionTolerance.Type, IDimensionTolerance.SetValues, IDimensionTolerance.SetFitValues, IDimensionTolerance.GetMaxValue, IDimensionTolerance.GetMinValue, IEdge.GetCurveParams2, ICurve.CircleParams, IDrawingDoc.CreateUnfoldedViewAt3]
keywords: [AddDimension2, add dimension from code, smart dimension, centre distance, diameter dimension, length dimension, face width, symmetric tolerance, plus minus, fit tolerance, H7, g6, p6, SetFitValues, swTolFITWITHTOL, Type 8, Type 4, SetValues, MOD-DIAM prefix, silhouette edge, swViewEntityType_SilhouetteEdge, GetVisibleEntities2 type 4, end view, step shaft, trailing zero]
answers: "How do I add dimensions to a drawing from code — centre distances, diameters, lengths — and give them a ± tolerance or an ISO fit?"
---

# Add dimensions and tolerances from code

## What this is for

Dimensioning a view from code rather than importing model dimensions: pick the
edges, place the dimension, then set its precision and tolerance. Which pair
of edges you select decides what kind of dimension you get, and one class of
edge — a cylinder's silhouette — cannot be picked through late binding at all.

Attach as in [drawings/01](01-new-drawing-and-place-views.md#attaching). Sheet
positions in the helpers are **millimetres**; the API takes **metres**, and
model coordinates from `CircleParams` and `GetCurveParams2` are model metres.

## Select, then `AddDimension2`

Select edges from `IView.GetVisibleEntities2(null, 1)` with
`IView.SelectEntity(edge, append)`, then `IModelDoc2.AddDimension2(x, y, 0)` on
the drawing places the dimension's text at that sheet point. It returned an
`IDisplayDimension`.

```python
def circle_edges(v):
    out = []
    for e in list(v.GetVisibleEntities2(nul, 1) or []):
        c = call(e, "GetCurve")
        if c is not None and call(c, "IsCircle"):
            out.append((e, round(list(call(c, "CircleParams"))[6] * 1000, 3)))
    return out
def dim_between(drw, v, ents, x, y):
    call(drw, "ClearSelection2", True)
    for i, e in enumerate(ents): v.SelectEntity(e, i > 0)
    d = drw.AddDimension2(x / 1000, y / 1000, 0); call(drw, "ClearSelection2", True)
    return d
def dim_value(d): return round(call(d.GetDimension2(0), "SystemValue") * 1000, 3)
```

What each selection produced:

| Selected | Dimension |
|---|---|
| Two circles seen face-on (two shafts' ends in a top view) | Centre-to-centre distance. Placed off the line between them, it came out **aligned** (23.3 on a diagonal pair), not as a horizontal or vertical component |
| One full circle seen face-on | Its diameter |
| One arc of a circle broken by other features | A **radius** (11.43 for a 22.86 circle) — dimension such features in a section instead |
| Two circle edges seen edge-on (the two end faces of a gear in a side view) | The distance between them: the face width |
| Two parallel lines | The distance between them (a spoke's two edges gave its width) |
| One straight edge | Its length |

`IEdge.GetCurveParams2` is property-style here: `call(e, "GetCurveParams2")`,
not `e.GetCurveParams2()`, which raised `'tuple' object is not callable`
([GOTCHAS §55](../../GOTCHAS.md)). Its items 0–2 and 3–5 are the start and end
points in model metres.

In a **section view**, the cut faces are planar, so every cut boundary is a
line. Lines whose two ends share a coordinate along the part's axis are faces
across the axis (face width, recess depth); lines whose ends share both other
coordinates are cylinders seen edge-on, at ±r from the axis. Pairing a +r line
with its −r partner and dimensioning them gave each internal diameter. On a cut
at an angle the sign of a line's offset is ambiguous; pairing by
`hypot` of the two off-axis coordinates and checking the two lines are about a
diameter apart worked.

## Precision, prefix and tolerance

```python
def decimals(v):
    for k in range(0, 3):
        if abs(round(v, k) - v) < 0.0051: return k
    return 2
def tidy(d, sym=None):
    v = dim_value(d); d.SetPrecision3(decimals(v), -1, -1, -1)
    if sym is not None:
        tol = call(d.GetDimension2(0), "Tolerance"); tol.Type = 4
        ok = tol.SetValues(-sym / 1000, sym / 1000)
        d.SetPrecision3(-1, -1, decimals(sym), -1)
        return v, ok, call(tol, "Type"), round(call(tol, "GetMaxValue") * 1000, 4), round(call(tol, "GetMinValue") * 1000, 4)
    return v
def fit(d, hole=None, shaft=None):
    tol = call(d.GetDimension2(0), "Tolerance"); tol.Type = 8
    ok = tol.SetFitValues(hole or "", shaft or "")
    d.SetPrecision3(decimals(dim_value(d)), -1, 3, -1)
    return ok, call(tol, "Type"), T(tol, "GetHoleFitValue"), T(tol, "GetShaftFitValue"), round(call(tol, "GetMaxValue")*1000, 4), round(call(tol, "GetMinValue")*1000, 4)
```

- **Symmetric:** `Tolerance.Type = 4` (`swTolSYMMETRIC`) then
  `SetValues(min, max)` in metres displayed `32.54 ±0.05`. `SetValues`
  returned `True`; the readback was max 0.05, min −0.05 mm.
- **Fit:** `Type = 8` (`swTolFITWITHTOL`) then `SetFitValues(hole, shaft)`,
  with an empty string for the side not used. SolidWorks filled in the limits
  from the ISO tables. Read back:

  | Nominal | Fit | Max / min (mm) |
  |---|---|---|
  | Ø3.3 hole | H7 | +0.012 / 0 |
  | Ø3.5 hole | H7 | +0.012 / 0 |
  | Ø2.3 hole | H7 | +0.010 / 0 |
  | Ø3.3 shaft | g6 | −0.004 / −0.012 |
  | Ø2.3 shaft | p6 | +0.012 / +0.006 |

  Displayed as `Ø3.3 H7 +0.012/0`, the fit and limits beside the value.
- `SetPrecision3(primary, dual, primaryTol, dualTol)`, `-1` to leave one alone
  ([drawings/05](05-read-and-tidy-dimensions.md)). Set the primary too, or a
  fit dimension on 3.3 showed `Ø3.30`.
- `IDisplayDimension.SetText(1, "<MOD-DIAM>")` put Ø in front of a linear
  distance between two silhouette or section lines that is really a diameter.
  For a repeated feature, `SetText(1, "4X ")` gave `4X 1.5`.

## Turned parts: the silhouette edge cannot be used

A shaft in a side view has no straight model edges along its length: its
outline is a **silhouette**. `GetVisibleEntities2(null, 1)` returned only the
end circles (seen edge-on). `GetVisibleEntities2(null, 4)`
(`swViewEntityType_SilhouetteEdge`) returned two objects per cylinder, but
through late binding **none of them was usable**: `GetCurve` returned `None`,
`GetStartPoint` and `GetEndPoint` raised "The server threw an exception"
(−2147417851), and `Select2(False, 0)` raised "Type mismatch".

What worked instead:

- **Lengths** from the end and step circles seen edge-on: group them by their
  centre's position along the axis (`CircleParams` items 0–2 and the axis 3–5)
  and dimension consecutive pairs.
- **Diameters** in an **end view** projected from the side view with
  `CreateUnfoldedViewAt3`, where each step is a full circle. On a stepped
  shaft, the end view from the large end showed only the largest diameter; the
  end view from the small end showed all three as nested circles.

## What it does not do

- **Ordinate, angular and chamfer dimensions** were not added.
- **Dimension placement** is by hand-picked points; overlapping text was moved
  by changing the point, not by a rule.
- **Open:** whether silhouette edges become usable through early binding or
  `ISilhouetteEdge` on the typed interface. Settle it by repeating the type-4
  calls from VBA or C#.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), part and assembly drawings on an
A3 template, checked in exported PDFs.

- Assembly top view: two shaft-end circles gave 32.54 and 23.3 (the second a
  diagonal pair, dimensioned aligned); a plate's two edges gave 87.5 and 70.
- Gear part drawings: bore circles gave Ø3.3, Ø3.5 and Ø2.3 with the fits in
  the table above; section lines gave face widths 5, 7, 5.5 and 4.318, a recess
  depth of 2.2 (measured from the face it was cut from, not 2.118 from the
  other), and internal diameters 10, 25.5, 27.5, 36.1 and 49.737 from ±r line
  pairs. On a spoked web, 32 parallel line pairs were all 1.5 mm apart; one pair
  dimensioned `4X 1.5`.
- Shafts: lengths 25 and 25; a stepped one 40.7 overall with steps 15.5, 14
  and 11.2; end-view diameters 3.3 g6, 3.5 g6, and 6.3, 6 and 2.3 p6.
- Silhouette edges: two per shaft returned by type 4, every member call failing
  as described.

## See also

- [drawings/05 — Read and tidy dimensions](05-read-and-tidy-dimensions.md) — the reading side, and `SetPrecision3`
- [drawings/08 — Section views from code](08-section-views-from-code.md) — where the cut-face lines come from
- [drawings/01 — New drawing and place views](01-new-drawing-and-place-views.md) — `CreateUnfoldedViewAt3`
- [drawings/10 — A general table](10-general-table.md) — data that is not a dimension
- [GOTCHAS §55, §89](../../GOTCHAS.md)
