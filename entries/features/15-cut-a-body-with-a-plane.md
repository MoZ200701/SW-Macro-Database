---
id: features-15-cut-a-body-with-a-plane
title: Cut one solid body with a plane (Cut With Surface)
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertCutSurface, IFeature.Select2, IFeatureManager.InsertRefPlane]
keywords: [InsertCutSurface, Cut With Surface, SurfCut, cut by a plane, cut one body, feature scope, bodies to cut, VT_ARRAY VT_DISPATCH, which side is kept, flip, overhang, multibody]
answers: "How do I cut just one body of a multibody part by a reference plane from code, and which side does it keep?"
---

# Cut one solid body with a plane

## What this is for

A multibody part where one body runs past a plane it should stop at — a fin's
root extension hanging out behind the tail, say — and only that body is to be
cut, before the bodies are combined.

## The call

Select the plane (`IFeature.Select2(False, 0)` on the `RefPlane` feature), then
`IFeatureManager.InsertCutSurface(flip, -1, True, False, bodies, out err)` with
the body to cut in a typed array. From the Plane-Builder probe
`p12_junction.py` (`jk.body_of(feature)` is the body of the feature's first face,
`IFeature.GetFaces` → `IFace2.GetBody`; `harness.out_long()` is
`VARIANT(VT_BYREF | VT_I4, 0)`):

```python
call(doc.model, "ClearSelection2", True)
call(plane, "Select2", False, 0)
arr = harness.VARIANT(harness.pythoncom.VT_ARRAY | harness.pythoncom.VT_DISPATCH, [jk.body_of(doc.feature("PB_Fin_Ext"))])
before = doc.feature_count()
err = harness.out_long()
ok, ret = report.attempt(f"InsertCutSurface(flip={flip}, -1, True, False, [ext])",
                         lambda: call(doc.fm, "InsertCutSurface", flip, -1, True, False, arr, err))
report.fact(f"cut_error_out[{flip}]", getattr(err, "value", None))
call(doc.model, "ClearSelection2", True)
f = jk.last_made(report, doc, before, f"cut[{flip}]")
```

What the arguments are was taken from the help; only `flip` was varied, and the
first value tried, `False`, did what was wanted.

## What came out

The plane at Z = −560 mm, made by `InsertRefPlane(8 | 256, 0.56, 0, 0, 0, 0)`
from the Front plane (read back at −560.0); the body an extension below a fin
that ran 40 mm past it:

| | |
|---|---|
| Feature | `SurfCut`, `GetErrorCode2` 0, the error out 0 |
| Kept | with `flip` `False`, the side toward +Z: the cut body's lowest Z became −560.000002 mm |
| Volume of the cut body | 17,186.1 → 15,853.7 mm³ |
| The other bodies | the fuselage still 5,369,102.3 mm³, as before the cut |

Then Combine Add of the three bodies ([features/14](14-combine-solid-bodies.md))
gave one solid with **no tessellation vertex below the fin's foot aft of the
plane**, where the same part without the cut had 425; and the cut face, lying in
the plane of the fuselage's planar tail cap, **merged with that cap into one
planar face** (8,583.11 mm² against 8,517.46 without the cut).

## Why it is not obvious

- **The body list is a typed array.** `VARIANT(VT_ARRAY | VT_DISPATCH, [body])`.
- **Which side stays.** `flip` False kept the side the plane's sketch normal
  points to. A plane made from Front with the 256 flip still faces +Z
  ([features/08](08-offset-reference-plane.md)), so this kept the part forward
  of the plane, not "the side the plane was flipped to". Check a vertex or the
  volume afterwards.
- **Cut only what overhangs.** The body was its own loft so that the cut took
  nothing else. Cutting a body that also carries geometry you want behind the
  plane takes that too.

## What it does not do

- `flip` True and the other arguments were not run.
- A surface as the cutting tool (rather than a plane) was not tried.

## Evidence

SolidWorks 2026 (revision 34.0.0), Windows Python 3.13 with pywin32 from WSL,
Plane-Builder probe run `p12_junction-20261103-035715`, 2026-11-03: the
`SurfCut`, its error out 0, the lowest Z, the volumes, the vertex counts with and
without the cut, and the merged cap face.

## See also

- [features/14 — Join two solid bodies into one](14-combine-solid-bodies.md)
- [features/08 — Offset reference plane](08-offset-reference-plane.md) — the plane, and which way it faces
- [features/02 — Cut extrude](02-cut-extrude.md) — cutting by a sketch instead
- [surfacing/06 — Trim a surface from code](../surfacing/06-trim-a-surface-from-code.md) — the sheet-body counterpart, trimming by a plane
- [GOTCHAS §95](../../GOTCHAS.md)
