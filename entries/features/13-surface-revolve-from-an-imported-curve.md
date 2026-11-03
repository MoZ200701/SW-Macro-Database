---
id: features-13-surface-revolve-from-an-imported-curve
title: A surface revolve from an imported curve, that follows the curve
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [ISketchManager.SketchUseEdge3, ISketchManager.CreateSpline2, ISketchManager.CreateCenterLine, IFeatureManager.InsertRevolvedRefSurface, ISketch.ModelToSketchTransform, IModelDoc2.EditSketch, ISketchPoint.SetCoords, ISketchSpline.GetPoints2, IModelDoc2.EditDelete, ICurve.IsCircle, ICurve.CircleParams]
keywords: [InsertRevolvedRefSurface, revolved surface, RevolvRefSurf, surface revolve, nose, spinner, Convert Entities, SketchUseEdge3, CreateSpline2, SimulateNaturalEnds, sketch spline from points, revolve an imported curve, Curve Through XYZ Points in a sketch, follows reload, EditSketch, SetCoords, rewrite a spline, Right Plane, end curvature zero, G2 joint]
answers: "How do I revolve an imported curve into a surface from code, and keep the revolve following the curve when it changes?"
---

# A surface revolve from an imported curve

## What this is for

A body of revolution — a nose, a spinner — whose profile comes from your own
exported points. A revolve takes a sketch, not a reference curve, so the
imported curve has to get into a sketch first. There are two ways, and they
differ in what they can follow and in how the profile ends.

## The call

1. **A sketch on a plane through the axis.** Here the Right Plane, the third
   `RefPlane` in the tree, for an axis along model Z. Read its
   `ModelToSketchTransform` and put every model point through it: on the Right
   Plane sketch x is model **−Z** ([GOTCHAS §91](../../GOTCHAS.md)).
2. **The profile**, one of:
   - **Convert**: with the sketch open, select the imported curve
     (`SelectByID2(name, "REFERENCECURVES", 0, 0, 0, False, 0, null, 0)`) and
     call `ISketchManager.SketchUseEdge3(False, False)`. It returned `True` and
     made one spline segment (type 3).
   - **Write**: `ISketchManager.CreateSpline2(points, False)` with
     `VARIANT(VT_ARRAY | VT_R8, [x, y, 0, …])`, the model points put through the
     transform, in **metres**. The second argument is `SimulateNaturalEnds`.
3. **A centreline on the axis**, `ISketchManager.CreateCenterLine`, from model
   (0, 0, 0) to (0, 0, −0.1) m through the same transform. Close the sketch.
4. **The revolve**: select only the sketch feature and call
   `IFeatureManager.InsertRevolvedRefSurface(2π, False, 0, 0)`. With one
   centreline in the sketch no axis pick was needed (the same as
   [features/05](05-revolve.md) found for a solid revolve). Find the feature by
   `GetFeatureCount` and `GetLastFeatureAdded`; its type is **`RevolvRefSurf`**.

From the Plane-Builder probe `p11_nose.py` (`xform` puts a point through the
sixteen doubles by columns, as in [reading/05](../reading/05-sketch-to-model-transform.md);
`call` is [connect/02](../connect/02-attach-from-python.md)'s helper):

```python
def build_nose_sketch(report: harness.Report, doc: harness.Doc, route: str, fx: NoseFuselage) -> Tuple[str, Dict[str, Any]]:
    """The nose sketch on the Right plane: the profile (converted or written) and a centreline on the axis."""
    info: Dict[str, Any] = {"route": route}
    planes = plane_names(doc)
    info["planes"] = planes
    right = planes[2]
    before = doc.feature_count()
    sk = open_sketch_on_plane(report, doc, right)
    m2s = model_to_sketch(sk)
    s2m = sketch_to_model(sk)
    # ... the frame of the sketch recorded here ...
    skm = sketch_manager(doc)
    made = None
    try:
        if route == "convert":
            # select the imported curve while the sketch is open; no ClearSelection2 in a sketch (GOTCHAS §51)
            info["select_curve"] = select_by_id(doc, "PB_Nose", "REFERENCECURVES", False, 0)
            info["selected_count"] = doc.selection_count()
            for member, args in (("SketchUseEdge3", (False, False)), ("SketchUseEdge2", (False,))):
                ok, made = report.attempt(f"{member}{args}", lambda m=member, a=args: call(skm, m, *a))
                segs = segments(sk)
                info[f"segments_after_{member}"] = len(segs)
                if ok and segs:
                    info["convert_member"] = member
                    break
        else:
            pts: List[float] = []
            for p in fx.nose_profile():
                q = xform(m2s, tuple(c / 1000 for c in p))
                pts.extend([q[0], q[1], 0.0])
            info["spline_points"] = len(pts) // 3
            arr = harness.VARIANT(harness.pythoncom.VT_ARRAY | harness.pythoncom.VT_R8, pts)
            for member, args in (("CreateSpline2", (arr, False)), ("CreateSpline", (arr,))):
                ok, made = report.attempt(f"{member}(points, ...)", lambda m=member, a=args: call(skm, m, *a))
                segs = segments(sk)
                info[f"segments_after_{member}"] = len(segs)
                if ok and segs:
                    info["spline_member"] = member
                    break
        profile_segs = segments(sk)
        info["profile_segments"] = [int(call(s, "GetType")) for s in profile_segs]
        # the axis: model (0,0,0) to (0,0,-100) mm through the transform
        a = xform(m2s, (0.0, 0.0, 0.0))
        b = xform(m2s, (0.0, 0.0, -0.1))
        cl = call(skm, "CreateCenterLine", a[0], a[1], 0.0, b[0], b[1], 0.0)
        info["centreline_made"] = cl is not None
        if cl is not None:
            info["centreline_name"] = str(call(harness.as_dispatch(cl), "GetName"))
        info["segments_total"] = len(segments(sk))
    finally:
        close_sketch(doc)
    sketch = last_feature_of(doc, before, "the nose sketch")
    name = doc.rename(sketch, "PB_NoseSketch")
    info["sketch_type"] = str(call(sketch, "GetTypeName2"))
    info["sketch_error"] = doc.error_of(sketch)
    report.fact("nose_sketch", info)
    return name, info
```

The first member of each pair succeeded (`SketchUseEdge3`, `CreateSpline2`), so
the fallbacks never ran. The helpers it uses, from the same file:

```python
def open_sketch_on_plane(report: harness.Report, doc: harness.Doc, plane: str) -> Any:
    """Select the plane (outside any sketch, so ClearSelection2 is safe, GOTCHAS §51) and open a sketch on it."""
    if active_sketch(doc) is not None:
        raise SolidWorksError("A sketch is already open.")
    call(doc.model, "ClearSelection2", True)
    if not select_by_id(doc, plane, "PLANE"):
        raise SolidWorksError(f"{plane} would not select.")
    call(sketch_manager(doc), "InsertSketch", True)
    sk = active_sketch(doc)
    if sk is None:
        raise SolidWorksError(f"InsertSketch opened no sketch on {plane}.")
    return sk


def model_to_sketch(sk: Any) -> List[float]:
    return [float(v) for v in call(call(sk, "ModelToSketchTransform"), "ArrayData")]
```

and the revolve itself, with the selection that worked:

```python
call(doc.model, "ClearSelection2", True)
call(doc.feature(sketch), "Select2", False, 0)
before = doc.feature_count()
ok, made = report.attempt(f"InsertRevolvedRefSurface(2π, False, 0, 0) [{label}]",
                          lambda: call(doc.fm, "InsertRevolvedRefSurface", 2 * math.pi, False, 0.0, 0))
call(doc.model, "ClearSelection2", True)
added, last = doc.made_since(before)
```

Units: the angle in radians, sketch coordinates in metres.

## What came out

A 25-point elliptical nose profile from the axis to a joint circle of radius
30 mm at Z = −60:

| | Convert | Write (`CreateSpline2`) |
|---|---|---|
| Revolve | `RevolvRefSurf`, error 0, 1 sheet, **1 face** | the same |
| Area against the analytic nose (8,531.03 mm²) | +0.0004 % | −0.0083 % |
| Distance of 960 analytic points from the face | max 0.0032 mm | max 0.00003 mm |
| The open end | one edge, `ICurve.IsCircle` True, `CircleParams` centre (0, 0, −60) mm, axis −Z, radius 30.000000000 mm | the same |
| Meridian curvature at the joint end (analytic 0.003091 /mm) | **0.0000 /mm**, rising to 0.0009 at 1 mm from it | **0.003055 /mm** |

The circular edge's one vertex sat at (−30, 0, −60), on −X, although the
profile ended at (0, −30, −60). Nothing broke on it.

## It follows the curve: convert route

The converted sketch spline follows a reload of the curve it was converted
from. All eleven curves of the part were reloaded from a different shape in
place, rolled back first ([curves/04](../curves/04-reload-curve-in-place.md),
[curves/12](../curves/12-roll-the-tree-back-before-reloading.md)): 3.6 s, one
`EditRebuild3` → True. Sketch, revolve, the Boundary Surface on its edge, the
knit, the tail cap and the solid all read `GetErrorCode2` 0, and the revolve lay
on the new nose to 0.0027 mm, 0.70 mm from the old one.

## Rewriting it: write route

A written spline has no file to reload, so the sketch is edited instead. On a
finished part (Boundary Surface, knit, cap and solid built on the revolve's
edge), both of these rebuilt every downstream feature with error 0 and put the
revolve on the new profile to 0.00003 mm:

- **Move the fit points**: select the sketch feature, `IModelDoc2.EditSketch`
  (returns `None`, opens it), `ISketchSpline.GetPoints2` (25 points), then
  `ISketchPoint.SetCoords(x, y, 0)` on each — all 25 `True`, read back within
  1.4e-14 mm — and close. 1.0 s with the rebuild. The point count must stay
  the same.
- **Delete and draw again**: `ISketchSegment.Select4(False, SelectData)`,
  `IModelDoc2.EditDelete` (returned `None`; the spline was gone and the
  centreline left), `CreateSpline2` again, close. 0.6 s. The Boundary Surface's
  reference to the revolve's end edge survived it.

```python
if mode == "setcoords":
    pts = [harness.as_dispatch(p) for p in (call(spl, "GetPoints2") or ())]
    results[f"{mode}.fit_points"] = len(pts)
    oks = []
    # interior points first, ends last; the end on the joint does not move (same circle)
    for i, (sp, q) in enumerate(zip(pts, prof)):
        oks.append(bool(call(sp, "SetCoords", q[0], q[1], 0.0)))
    results[f"{mode}.setcoords_true"] = sum(oks)
    back = [(float(call(p, "X")), float(call(p, "Y"))) for p in pts]
    results[f"{mode}.points_vs_target_max_mm"] = max(math.hypot(b[0] - q[0], b[1] - q[1]) for b, q in zip(back, prof)) * 1000
else:
    data = call(call(doc.model, "SelectionManager"), "CreateSelectData")
    sel = bool(call(spl, "Select4", False, data))
    results[f"{mode}.selected"] = sel
    results[f"{mode}.EditDelete"] = repr(call(doc.model, "EditDelete"))
    results[f"{mode}.segments_after_delete"] = [int(call(s, "GetType")) for s in segments(sk)]
    flat: List[float] = []
    for q in prof:
        flat.extend([q[0], q[1], 0.0])
    arr = harness.VARIANT(harness.pythoncom.VT_ARRAY | harness.pythoncom.VT_R8, flat)
    made = call(sketch_manager(doc), "CreateSpline2", arr, False)
```

(`spl` is the sketch's one spline segment, `prof` the new profile already in
sketch coordinates; the sketch was opened with `EditSketch` and is closed
afterwards.)

## Why it is not obvious

- **Convert gives the profile a natural end.** The imported curve is an open
  Curve Through XYZ Points, so it ends with zero curvature
  ([GOTCHAS §65](../../GOTCHAS.md)), and so does everything converted from it.
  Measured at the joint: 0.0000 /mm against 0.003091. **A converted profile
  cannot meet a curved neighbour with matching curvature (G2).** The written
  spline with `SimulateNaturalEnds` False kept the end curvature to 1.2 %.
- **The two routes push differently.** Convert follows the curve reload you
  already do; write needs its own edit step, and its profile is not a curve
  file you can reload.
- **The axis is the sketch's centreline.** With one centreline nothing else
  had to be selected.

## Joining it to the rest

The revolve's circular end edge served as the first profile of a Boundary
Surface ([surfacing/05](../surfacing/05-boundary-surface-from-code.md)); the two
sheets knitted into one, were capped and knitted into a solid
([surfacing/04](../surfacing/04-cap-a-refused-loft-into-a-solid.md#the-same-knit-on-a-revolve-and-a-boundary-surface)).
The face condition across that joint (tangency or curvature to face) was stored
and did not change the shape ([GOTCHAS §88](../../GOTCHAS.md)).

The exported joint circle, a closed 48-point curve file, lay 0.023 µm from the
revolve's exact circle at worst ([curves/01](../curves/01-sldcrv-file-format.md#closed-curves)).

## What it does not do

- Only a full turn, one centreline, and a profile meeting the axis were made.
  The other arguments of `InsertRevolvedRefSurface` (`False, 0, 0`) were not
  varied. Selecting the centreline at a mark was in the code as a fallback and
  never needed.
- `ISketchManager.CreateSpline` (the older member) was not run.
- `SetCoords` with a different number of points is not possible; delete and
  redraw for that.
- Only the Right Plane was used.

## Evidence

SolidWorks 2026 (revision 34.0.0), Windows Python 3.13 with pywin32 from WSL,
Plane-Builder probes, 2026-11-03. Result records:

- `p11_nose-20261103-035854`: the convert route, `SketchUseEdge3` → True,
  `ProfileFeature`, revolve by the sketch alone, area, deviation, the circle
  edge, zero end curvature; the reload of eleven curves and every feature after
  it at error 0; the Right Plane's frame.
- `p11_nose-20261103-041026`: the write route, `CreateSpline2`, the kept end
  curvature, and both rewrites with their times and errors.

## See also

- [features/05 — Revolve](05-revolve.md) — the solid revolve, `FeatureRevolve2`
- [surfacing/05 — Build a Boundary Surface from code](../surfacing/05-boundary-surface-from-code.md) — the surface that started from this revolve's edge
- [surfacing/04 — Cap a refused loft into a solid](../surfacing/04-cap-a-refused-loft-into-a-solid.md) — knit, cap, solid
- [curves/04 — Reload a curve in place](../curves/04-reload-curve-in-place.md)
- [reading/05 — Sketch to model transform](../reading/05-sketch-to-model-transform.md)
- [surfacing/02 — Axis conventions](../surfacing/02-axis-conventions.md)
- [sketches/01 — A 2D sketch as VBA](../sketches/01-2d-sketch-as-vba.md) — splines from a flat array
- [GOTCHAS §51, §65, §88, §91](../../GOTCHAS.md)
