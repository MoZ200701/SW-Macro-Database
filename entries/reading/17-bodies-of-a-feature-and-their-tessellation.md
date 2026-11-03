---
id: reading-17-bodies-of-a-feature-and-their-tessellation
title: Find the bodies a feature made, the feature a body came from, and their triangles
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeature.GetFaces, IFace2.GetBody, IBody2.Name, IBody2.GetFeatures, IPartDoc.GetBodies2, IFace2.GetTessTriangles, IBody2.GetMassProperties, IFeatureManager.FeatureExtrusion3]
keywords: [bodies of a feature, which body, which feature made this body, GetFeatures, GetBody, body name, feature[1], feature[2], multibody, keep-in, tessellation, GetTessTriangles, triangles, mesh, chord error, containment, Merge False, separate bodies, ICE]
answers: "Which bodies did this feature make, which feature made this body, and how do I get their triangles?"
---

# The bodies a feature made, and their triangles

## What this is for

A part holds several solid bodies — keep-out volumes for components, say —
and you want them by the feature that made them, or the reverse, and then as
triangles to test against something else. A feature has no "get my bodies"
member; a face does.

## The calls

- **Feature → bodies**: `IFeature.GetFaces`, each face's `IFace2.GetBody`,
  unique by `IBody2.Name`. A feature that made two bodies gives both.
- **Body → features**: `IPartDoc.GetBodies2(0, False)` for the solid bodies,
  then `IBody2.GetFeatures` on each: the features that made it.
- **Triangles**: per face, `IFace2.GetTessTriangles(True)`, a flat array of
  doubles, three per vertex, nine per triangle, in **metres**. Through pywin32
  it comes back as an uncalled method and has to be called
  ([GOTCHAS §55](../../GOTCHAS.md)); `call` below does that.

From the Plane-Builder probe `p6_bodies.py` (`call` and `as_dispatch` as in
[connect/02](../connect/02-attach-from-python.md); `MM` is 1000.0;
`doc.feature_faces(f)` is `[as_dispatch(x) for x in (call(f, "GetFaces") or ())]`):

```python
def bodies_of_feature(doc: harness.Doc, name: str) -> List[Any]:
    """Keep-in by feature name: the feature's faces → their bodies, one each (by body name)."""
    seen: Dict[str, Any] = {}
    for f in doc.feature_faces(doc.feature(name)):
        b = harness.as_dispatch(call(f, "GetBody"))
        seen.setdefault(str(call(b, "Name")), b)
    return list(seen.values())


def tessellate(body: Any) -> Dict[str, Any]:
    tris = 0
    verts: List[Tuple[float, float, float]] = []
    faces = [harness.as_dispatch(f) for f in (call(body, "GetFaces") or ())]
    for f in faces:
        t = call(f, "GetTessTriangles", True)
        vals = [float(v) * MM for v in (t or ())]
        tris += len(vals) // 9
        verts.extend(tuple(vals[i:i + 3]) for i in range(0, len(vals) - 2, 3))
    uniq = sorted(set((round(v[0], 6), round(v[1], 6), round(v[2], 6)) for v in verts))
    zs = [v[2] for v in uniq]
    return {"faces": len(faces), "triangles": tris, "vertices_raw": len(verts), "vertices_unique": len(uniq),
            "z_range_mm": (min(zs), max(zs)) if zs else None, "verts": uniq}
```

and the reverse map:

```python
all_solids = doc.bodies(harness.SOLID_BODY)
b2f = {}
for b in all_solids:
    try:
        fs = call(b, "GetFeatures")
        b2f[str(call(b, "Name"))] = [str(call(harness.as_dispatch(f), "Name")) for f in (fs or ())]
    except Exception as exc:  # noqa: BLE001
        b2f[str(call(b, "Name"))] = harness.describe_error(exc)
```

## What it read

Three extrusions, each made with **Merge False** so it stayed its own body
([features/01](../features/01-boss-extrude.md)): a 30 × 40 × 200 mm box, a
Ø30 × 100 mm cylinder, and one sketch of two 10 mm squares extruded 60 mm.

| | |
|---|---|
| Solid bodies in the part | 4: `PB_KeepIn_Box`, `PB_KeepIn_Servo`, `PB_KeepIn_Pair[1]`, `PB_KeepIn_Pair[2]` |
| Feature → bodies | Box 1, Servo 1, **Pair 2** |
| `IBody2.GetFeatures` | each body → exactly its own extrusion; both `Pair[n]` → `PB_KeepIn_Pair` |
| Body names | the feature's name, and `<feature>[1]`, `<feature>[2]` for a feature that made two |
| Feature types | Box `Extrusion`; Servo and Pair **`ICE`** ([GOTCHAS §96](../../GOTCHAS.md)) |
| Triangles | box 6 faces, **12 triangles**, 8 vertices; cylinder 3 faces, **168 triangles**, 86 vertices; each square prism 12 |
| Volumes, `IBody2.GetMassProperties(1.0)` item 3 | 240,000.000 mm³ (30·40·200); 70,685.835 (π·15²·100 = 70,685.835); 6,000.000 each |

**The tessellation is inside a curved face, not on it.** The cylinder's
triangles reached a half-height of 14.958 mm for its 15 mm radius: up to
0.042 mm inside. A clearance measured on triangles is optimistic by up to the
chord error on curved faces. The default resolution of
`GetTessTriangles(True)` was the only one used.

The probe sliced each body's vertices at a plane (segment crossings between
vertices either side, then a 2D hull) and tested the slice for containment in
a section; that geometry is the application's. Testing against the section
curve as SolidWorks drew it (read with `ICurve.GetTessPts`) and against the
application's own section gave the same verdicts to 0.0014 mm.

## Why it is not obvious

- **There is no feature-to-body call.** Go through a face. And take every body
  of the feature, not the first: one extrusion of two regions is two bodies.
- **Separate bodies need Merge off**, and are then typed `ICE`, the type a cut
  extrude has ([GOTCHAS §96](../../GOTCHAS.md)): filter by what the feature
  made, not by its type name.
- **Mind which way the extrusion went** on an offset plane
  ([GOTCHAS §95](../../GOTCHAS.md)): the first run's box went through the nose.

## What it does not do

- Only solid bodies from extrusions. Sheet bodies, and bodies changed by later
  features (combined, split), were not read. `IBody2.GetFeatures` on a body
  more than one feature built was not tried.
- Tessellation resolution was not set.

## Evidence

SolidWorks 2026 (revision 34.0.0), Windows Python 3.13 with pywin32 from WSL,
Plane-Builder probe run `p6_bodies-20261103-041508`, 2026-11-03: the bodies,
names, `GetFeatures` map, type names, triangle counts, volumes and the
cylinder's 14.958 mm half-height above. The earlier run
`p6_bodies-20261103-041327` built the same features in the wrong place
(GOTCHAS §95) and read the same counts.

## See also

- [features/01 — Boss extrude](../features/01-boss-extrude.md) — the extrusion, with Merge off
- [reading/13 — Measure a wall between two bodies](13-measure-a-wall-between-two-bodies.md) — one body's volume, and distances between bodies
- [reading/10 — Mass properties as an oracle](10-mass-properties-as-an-oracle.md)
- [surfacing/04 — Cap a refused loft into a solid](../surfacing/04-cap-a-refused-loft-into-a-solid.md) — a feature's body through its face
- [GOTCHAS §55, §95, §96](../../GOTCHAS.md)
