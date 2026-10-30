---
id: reading-15-measure-a-part-that-has-no-dimensions
title: Measure a part's sizes from its faces when it has no dimensions to read
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IPartDoc.GetBodies2, IBody2.GetBodyBox, IBody2.GetFaces, IFace2.GetSurface, IFace2.GetBox, ISurface.IsPlane, ISurface.PlaneParams, ISurface.IsCylinder, ISurface.CylinderParams, ISurface.Identity, IFeature.GetTypeName2, IFeature.GetFirstDisplayDimension, IFeature.GetNextDisplayDimension]
keywords: [derived part, SavedExtBody, inserted part, imported body, no dimensions, dumb solid, measure part, face positions, planar faces, cylinder radius, hole diameter, slot width, wall thickness, PlaneParams, CylinderParams, GetBodyBox, reverse engineer dimensions, dimension table from geometry]
answers: "My part was derived or imported and has no dimensions of its own — how do I get its real sizes (lengths, slot widths, hole diameters, positions) from code?"
---

# Measure a part's sizes from its faces when it has no dimensions to read

## What this is for

A part split off a master part, inserted from another part, or imported carries
no sketch or feature dimensions: its tree is a `SavedExtBody` and perhaps a
fillet. To dimension it on a drawing, or to check its fit against a mating part,
you need its sizes anyway. They are all in the faces: every flat face's position
along its normal, every cylinder's radius and axis.

Attach as in [assemblies/06](../assemblies/06-read-every-mate.md#attaching).

## First, what dimensions there are

Walk the features and list each one's dimensions; for a derived part this shows
how little there is:

```python
f = call(d, "FirstFeature")
while f is not None:
    tn = call(f, "GetTypeName2"); name = call(f, "Name")
    dims = []; dd = call(f, "GetFirstDisplayDimension")
    while dd is not None:
        dim = dd.GetDimension2(0)
        dims.append(f"{call(dim, 'Name')}={round(call(dim, 'SystemValue') * (1000 if call(dd, 'GetType') != 1 else 57.29578), 3)}")
        dd = f.GetNextDisplayDimension(dd)
    f = call(f, "GetNextFeature")
```

Three derived parts read `SavedExtBody` with no dimensions, then fillets (one
`D1` each) and on one a cut (`D1`, `D2`).

## Then the faces

Group the planar faces whose normal is along an axis by their offset on that axis,
and list every cylinder:

```python
r3 = lambda a: [round(x * 1000, 3) for x in a]
for b in list(d.GetBodies2(0, True) or []):
    box = r3(call(b, "GetBodyBox")); print("  box min", box[:3], "max", box[3:])
    planes = {0: set(), 1: set(), 2: set()}; other = []
    for f in list(call(b, "GetFaces") or []):
        s = call(f, "GetSurface"); fb = r3(call(f, "GetBox"))
        if call(s, "IsPlane"):
            p = list(call(s, "PlaneParams")); n = [round(x, 4) for x in p[:3]]
            ax = [i for i in range(3) if abs(abs(n[i]) - 1) < 1e-4]
            if ax: planes[ax[0]].add(round(p[3 + ax[0]] * 1000, 3))
            else: other.append(("plane n", n, "box", fb))
        elif call(s, "IsCylinder"):
            p = list(call(s, "CylinderParams"))
            other.append(("cyl r", round(p[6] * 1000, 3), "axis", [round(x, 3) for x in p[3:6]], "pt", r3(p[:3]), "box", fb))
        else: other.append((call(s, "Identity"), "box", fb))
    for i, a in enumerate("XYZ"): print(f"  {a}-planes:", sorted(planes[i]))
    for o in other:
        if not (o[0] == "cyl r" and o[1] <= 0.5): print("  ", *o)
    print("   (+", sum(1 for o in other if o[0] == "cyl r" and o[1] <= 0.5), "fillet cylinders r<=0.5)")
```

- Everything is in **metres** in the part's own frame; the code converts to mm.
- `PlaneParams` items 0–2 are the normal and 3–5 a point on the plane, so for an
  axis-aligned face, item `3 + axis` is its offset.
- `CylinderParams` items 0–2 are a point on the axis, 3–5 the axis direction,
  6 the radius.
- `IFace2.GetBox` tells you **where** on the part a face is; it is what separates
  a slot's two walls from the outer faces at the same offset in another axis.
- Fillet faces come back as small cylinders and as other surface types
  (`Identity` 4004 and 4005 were seen on corner blends); filter them by size.

## Reading the result

The sorted offsets are a dimension chain. Four X-plane offsets `[a, b, c, d]` on
a block are its length `d − a`, a window `c − b` wide, and the window's distance
from each end, `b − a` and `d − c`; two Y-planes symmetric about 0 are the
window's height; a cylinder about X whose radius exceeds the flats is the block's
rounded sides. Six Z-plane offsets symmetric about 0, with a pair of equal
cylinders on the Z axis, were a block, a window, and a peg at each end — the
outer pair of offsets minus the next pair is the peg length.

Two parts that fit together can be compared directly: a window and the bar that
slides through it came out the same size in the model, line-to-line, which is
the cue that the drawing needs a fit or tolerance there.

## What it does not do

- **Inclined faces** (normal not on an axis) are listed with their normal but not
  turned into dimensions.
- **Splines, cones and tori** are only reported by `Identity`.
- **A part in an assembly**: the numbers are in the part's frame. For assembly
  coordinates, carry them through the component's `Transform2`
  ([GOTCHAS §10](../../GOTCHAS.md)).
- **Whether the model's sizes are the real part's** is not something code can
  tell; record which were measured and which estimated.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-30, three derived parts,
one other machined part and four simple cylindrical parts, all open in an
assembly session. The feature walk
gave the dimensions quoted above. The face walk gave the plane offsets and
cylinders quoted, which reproduced each block's `GetBodyBox` extents and, on the one part that had
a dimensioned cut, the cut's two dimensions exactly. A tube read two cylinders
(outside and bore) and its length from two X-planes. The sizes drove two finished drawings;
their exports were checked against these numbers. `IFace2.GetBox` reached by
`f.GetBox()` raised `'tuple' object is not callable` — it came back already
called ([GOTCHAS §5](../../GOTCHAS.md)); `call(f, "GetBox")` worked.

## See also

- [reading/09 — Bounding box as a check](09-bounding-box.md)
- [reading/13 — Measure a wall between two bodies](13-measure-a-wall-between-two-bodies.md)
- [features/04 — Read a feature's dimensions](../features/04-read-a-features-dimensions.md)
- [reading/08 — Dimensions and equations](08-dimensions-and-equations.md)
- [drawings/04 — Centre marks and centre lines](../drawings/04-centre-marks-and-centre-lines.md) — the same curve and surface parameters in a view
- [drawings/05 — Read and tidy dimensions](../drawings/05-read-and-tidy-dimensions.md)
- [GOTCHAS §2, §5, §10](../../GOTCHAS.md)
