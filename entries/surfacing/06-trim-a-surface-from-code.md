---
id: surfacing-06-trim-a-surface-from-code
title: Trim a surface from code, by a curve, a plane, another surface, or mutually
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.PreTrimSurface, IFeatureManager.PostTrimSurface, IModelDocExtension.SelectByRay, IModelDocExtension.SelectByID2, IEntity.Select4, IFeatureManager.InsertSewRefSurface]
keywords: [trim surface, PreTrimSurface, PostTrimSurface, TrimRefSurface, mutual trim, standard trim, trim tool, trim by curve, trim by plane, trim by edge refused, pieces to keep, SelectByRay, swSelSURFACEBODIES, 75, SelectByID2 SURFACEBODY point returns False, selection cleared, GetFaces empty, junction, fairing foot]
answers: "How do I trim one surface by a curve, a plane or another surface from code, and choose which piece to keep?"
---

# Trim a surface from code

## What this is for

Cutting a sheet body back to a boundary — a fuselage skin to the foot of a
fairing, a skin and a fin against each other — before knitting the pieces into
one body. The Trim Surface command is two calls with a selection in between,
and the selection is where it goes wrong.

## The sequence

1. **Select the tool(s) first.** For a *standard* trim, the trim tool: a curve
   feature lying on the face (`"REFERENCECURVES"`), a reference plane
   (`"PLANE"`), or another sheet body (`"SURFACEBODY"` by name). For a *mutual*
   trim, both sheet bodies (`"SURFACEBODY"` by name).
2. **`IFeatureManager.PreTrimSurface(mutual, splitSys, linear, remove)`**. It
   takes the selection and **clears it**: the selection count went from 2 to 0.
3. **Pick the pieces to keep** (or, with `remove` True, to remove) with
   **`IModelDocExtension.SelectByRay`**, type 75 (`swSelSURFACEBODIES`),
   aimed at a point on each piece.
4. **`IFeatureManager.PostTrimSurface(True)`**. It returns the feature, a
   **`TrimRefSurface`**.

From the Plane-Builder probes (`p12_junction.py`, `junction_kit.py`):

```python
# The surfaces selected first: PreTrimSurface takes them (and clears the selection); the picks after it are
# the pieces. With nothing selected before it, PostTrimSurface made no feature.
jk.select_bodies(doc, [a_body, b_body], "SURFACEBODY", 0)
ok, ret = report.attempt(f"PreTrimSurface(True, {split_sys}, False, {remove})[{label}]",
                         lambda: call(doc.fm, "PreTrimSurface", True, split_sys, False, remove))
picked = [jk.pick_point(report, doc, p, d, f"{label}#{i}")["how"] for i, (p, d) in enumerate(pts)]
before = doc.feature_count()
sa0 = jk.sheet_area(doc)
ok, ret = report.attempt(f"PostTrimSurface(True)[{label}]", lambda: call(doc.fm, "PostTrimSurface", True))
call(doc.model, "ClearSelection2", True)
f = jk.last_made(report, doc, before, f"trim[{label}]")
```

`select_bodies` is `SelectByID2(name, kind, 0, 0, 0, append, mark, null, 0)`
for each name, the first not appended. The pick, a ray from 3 mm before the
point along a direction (unit vector), in metres:

```python
s = [point[i] - 3.0 * direction[i] for i in range(3)]
ok = call(doc.ext, "SelectByRay", s[0] / MM, s[1] / MM, s[2] / MM, direction[0], direction[1], direction[2],
          0.0005, 75 if how == "ray75" else 2, True, mark, 0)
```

The arguments after the direction are the ray's radius (0.0005 m), the
selection type, append, mark and an option. Judge each pick by the selection
count going up. The probe projected its keep point onto the face with
`IFace2.GetClosestPointOn` first.

`MM` is 1000.0; `call` is [connect/02](../connect/02-attach-from-python.md)'s
helper. `sheet_area` sums `IFace2.GetArea` over `IPartDoc.GetBodies2(1, False)`.

## What worked

| Trim | Tool, selected before `PreTrimSurface` | `PreTrimSurface` | Result |
|---|---|---|---|
| Mutual: a fuselage sheet and a fin's sheet through it | both bodies, `SURFACEBODY`, mark 0 | `(True, True, False, False)` | `TrimRefSurface`, error 0; 5,058.8 mm² of sheet removed (the skin under the fin and the fin's part inside the skin); knitted with caps into one solid ([features/14](../features/14-combine-solid-bodies.md)) |
| Standard, by a curve | a `CurveInFile` lying on the skin, `REFERENCECURVES` | `(False, False, False, False)` | `TrimRefSurface`, error 0, 4,148.4 mm² removed; the hole a **single edge** |
| Standard, by a plane | `SelectByID2(name, "PLANE", …)` | the same | each of two sheets cut at the plane, the forward piece kept |
| Standard, by another sheet | `SURFACEBODY` by name (a loft through the trim line) | the same | 2,272.6 mm² removed |

## What did not

- **Picking a piece by `SelectByID2("", "SURFACEBODY", x, y, z, …)`** at a
  point lying exactly on the surface (projected onto it with
  `GetClosestPointOn`) returned **False** every time; `SelectByRay` at the same
  point selected it every time ([GOTCHAS §98](../../GOTCHAS.md)).
- **Nothing selected before `PreTrimSurface`**, the surfaces and the pieces all
  picked after it: `PostTrimSurface` returned `None` and made no feature, three
  ways.
- **Another sheet's edge as the trim tool was refused.** A Boundary Surface's
  foot edge, lying on the skin, selected before `PreTrimSurface` (selection
  count 1) by `IEntity.Select4` at mark 0, 1 or 2, or by
  `SelectByID2("", "EDGE", point on it, …)`, made **no feature** each time. The
  curve that surface's edge was built on, as the tool, worked. Trim by the
  curve, not by the edge.

## Why it is not obvious

- **`PreTrimSurface` clears the selection.** What you select before it is the
  tool (or the mutual pair); what you select after it is the pieces. The help's
  example reads either way.
- **`TrimRefSurface.GetFaces` is empty** — 0 faces — even when the trim
  worked. An earlier run deleted good trims for reporting no faces. Judge a trim
  by the sheet bodies' area before and after ([GOTCHAS §99](../../GOTCHAS.md)).
- **A knit after it closes the joins.** The trimmed skin, a fairing whose
  foot lay on the trim line and a fin wall knitted with
  `InsertSewRefSurface(True, False, False, 1e-4, 1e-4)` into **one sheet body**,
  3 faces, the foot and top joins closed.

## What it does not do

- `splitSys`, `linear` and `remove` were not separated: mutual with `splitSys`
  True and `remove` False is the combination that built, and is all this entry
  claims.
- Trimming solids, and trimming by a sketch, were not tried.

## Evidence

SolidWorks 2026 (revision 34.0.0), Windows Python 3.13 with pywin32 from WSL,
Plane-Builder probes, 2026-11-03:

- `p12_junction-20261103-043842`: the mutual trim (2 selected before
  `PreTrimSurface`, 0 after it; 5,058.8 mm² removed; `TrimRefSurface` with
  0 faces); `SelectByID2` picks False, `SelectByRay` picks good.
- `p12_junction-20261103-040255`: nothing selected before `PreTrimSurface`, no
  feature, three ways. `p12_junction-20261103-040027`: `PostTrimSurface`
  returned a feature that the probe deleted for having no faces.
- `p13_fairing-20261103-043914`: the edge as tool refused four ways, the curve
  as tool working; the trims by plane and by sheet; the knit of three sheets
  into one.
- `p13_fairing-20261103-042019`: the trim by the curve, 4,148.4 mm², the hole a
  single edge.

## See also

- [features/14 — Join two solid bodies into one](../features/14-combine-solid-bodies.md) — the solid route to the same junction
- [features/15 — Cut a body with a plane](../features/15-cut-a-body-with-a-plane.md)
- [surfacing/04 — Cap a refused loft into a solid](04-cap-a-refused-loft-into-a-solid.md) — the knit and the caps after a trim
- [surfacing/05 — Build a Boundary Surface from code](05-boundary-surface-from-code.md)
- [surfacing/01 — The boundary surface recipe](01-boundary-surface-recipe.md) — where Trim was still listed as manual
- [GOTCHAS §98, §99](../../GOTCHAS.md)
