---
id: features-11-mirror-body
title: Mirror a part's body about a plane and merge it into one
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertMirrorFeature2, ISelectionMgr.CreateSelectData, IBody2.Select2, IPartDoc.GetBodies2, IFeature.Select2]
keywords: [InsertMirrorFeature2, mirror, mirror body, MirrorSolid, merge, one body, selection mark 256, mark 2, CreateSelectData, IBody2.Select2, herringbone, double helical, symmetric part, nothing mirrored, returned None]
answers: "How do I mirror a part's solid body about a reference plane from code so the result is one merged body?"
---

# Mirror a part's body about a plane and merge it into one

## What this is for

A part that is two mirrored halves: a herringbone gear, whose second half is
the first half's helix reflected; any symmetric part built as half and
mirrored. From code this is `InsertMirrorFeature2` with the body and the plane
selected at two different marks. The marks are the whole difficulty: two of
the three combinations tried returned `None` and mirrored nothing.

## The call

The tool's form, written from the probe:

```python
def mirror_body(self, handle: str, name: str, plane: str) -> str:
    """The part's body mirrored about a plane this build made, merged into one.

    Probe mirror_body_merge, SolidWorks 2026: the plane at mark 2 and the
    body at mark 256, ``InsertMirrorFeature2(True, False, True, False, 0)``,
    made one body of twice the volume; the body at mark 1 mirrored nothing.
    """
    doc = self._doc()
    plane_name = self._handles.get(plane)
    if not isinstance(plane_name, str):
        raise SolidWorksError(f"The mirror {name} needs the plane {plane} first.")
    bodies = call(doc, "GetBodies2", 0, True)
    if not bodies or len(bodies) != 1:
        raise SolidWorksError(f"The mirror {name} needs exactly one solid body; the part has {len(bodies or ())}.")
    self._select_feature(self._by_name(plane_name), mark=findings.MIRROR_PLANE_MARK)
    data = call(call(doc, "SelectionManager"), "CreateSelectData")
    data.Mark = findings.MIRROR_BODY_MARK
    before_count = self._selected()
    if not call(bodies[0], "Select2", True, data) or self._selected() <= before_count:
        raise SolidWorksError(f"The body could not be selected to mirror {self._selection_context()}.")
    before = self._top_names()
    made = call(call(doc, "FeatureManager"), "InsertMirrorFeature2", True, False, True, False, 0)
    call(doc, "ClearSelection2", True)
    if made is None:
        raise SolidWorksError(f"InsertMirrorFeature2 would not mirror the body about {plane_name}.")
    kept = self._rename(self._made_since(before, "MirrorSolid", "InsertMirrorFeature2"), name)
    self._handles[handle] = {"feature": kept}
    return kept
```

`findings.MIRROR_PLANE_MARK = 2`, `findings.MIRROR_BODY_MARK = 256`.

Interfaces:

- The plane: **IFeature** `Select2(append, mark)` at mark **2**, judged by the
  selection count ([features/01](01-boss-extrude.md)).
- The body: **IPartDoc** `GetBodies2(0, True)` for the solid bodies (0 is
  `swSolidBody`), then **ISelectionMgr** `CreateSelectData`, its `Mark` set to
  **256** by attribute assignment, and **IBody2** `Select2(True, data)`,
  appended.
- `InsertMirrorFeature2(True, False, True, False, 0)` on **IFeatureManager**.
  The probe passes its `mirror_body` flag, `True`, as the first argument; the
  other four were passed as shown and never varied. Returns the feature,
  `GetTypeName2` `"MirrorSolid"`, or `None`.

## The probe

From [`code/python/gear_generator/probe/p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py),
probe `mirror_body_merge`: a half blank r 20 × 5 mm extruded from the Front
plane, a plane offset 5 mm onto its end face
([features/08](08-offset-reference-plane.md)), and three selection
combinations, each on a fresh part.

```python
for label, body_mark, plane_mark, mirror_body in (
    ("body mark 1, plane mark 2", 1, 2, True),
    ("body mark 256, plane mark 2", 256, 2, True),
    ("body mark 1, plane mark 1", 1, 1, True),
):
    with sc.quiet_dimensions(px), sc.scratch(px) as doc:
        from .p2_solid import _extrude_args  # noqa: PLC0415
        front = sc.planes(doc)[0]
        sketch = circle_sketch(px, doc, front, "Blank Sketch", (0.0, 0.0, 0.0), BLANK_R)
        sc.select_feature(doc, sc.feature_by_name(doc, sketch))
        call(sc.feature_manager(doc), "FeatureExtrusion3", *_extrude_args(half / sc.MM))
        call(doc, "ClearSelection2", True)
        half_volume = _volume(doc)
        plane = offset_plane(px, doc, half, flip_for_side(px, ez))
        bodies = call(doc, "GetBodies2", SOLID_BODY, True)
        px.require(bool(bodies), "the half blank has a body")
        call(doc, "ClearSelection2", True)
        sc.select_feature(doc, plane, append=False, mark=plane_mark)
        data = call(call(doc, "SelectionManager"), "CreateSelectData")
        data.Mark = body_mark
        call(bodies[0], "Select2", True, data)
        before = sc.feature_names(doc)
        got, result = px.attempt(f"{label}: InsertMirrorFeature2(True, False, True, False, 0)",
                                 lambda: call(sc.feature_manager(doc), "InsertMirrorFeature2",
                                              mirror_body, False, True, False, 0))
        call(doc, "ClearSelection2", True)
        feature = made_feature(doc, before, result) if got else None
        if feature is None:
            px.note(f"{label}: nothing was mirrored")
            continue
        volume = _volume(doc)
        centre = centre_of_mass_mm(doc)
        count = solid_bodies(doc)
        px.fact(f"mirror {label}", {"volume_mm3": round(volume, 4), "bodies": count, "centre_mm": centre,
                                    "type": sc.type_name(feature)})
        if near(volume, 2 * half_volume, 1e-6 * half_volume) and count == 1 and near(centre[2], half * ez, 1e-6):
            working.append(label)
```

The oracle: one body, twice the half's volume, with its centre of mass on the
mirror plane.

| Body mark | Plane mark | Returned | Result |
|---|---|---|---|
| 1 | 2 | `None` | nothing mirrored |
| **256** | **2** | the feature | **one body**, 12566.3706 mm³ (2 × 6283.185), centre at z 5.0 mm, `MirrorSolid` |
| 1 | 1 | `None` | nothing mirrored |

## Why it is not obvious

**The body goes at mark 256.** Mark 1, the mark most features read their
first selection from, returned `None`, with no error and no feature. Neither
mark nor argument order is guessable from the call's name. See
[GOTCHAS §47](../../GOTCHAS.md).

**A body is selected through selection data, not by name.** `IBody2.Select2`
takes an `ISelectData` whose `Mark` is set before the call; there is no mark
argument. The selection count going up is the check that it took.

**Merged is the default here.** With these arguments the mirrored half and the
original came out as one solid body. Whether another argument would keep two
bodies was not tried.

## What it does not do

- Mirroring features or faces (rather than a body), a face as the mirror
  plane, and a part with more than one body were not tried.
- The four trailing arguments were never varied, so nothing is claimed about
  what they do.
- Body mark 256 with the plane at mark 1 was not tried.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe
`mirror_body_merge` in
[`p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py), with
the same results in run 20260915-002812 (43 of 43 passed) and run
20260915-023929 (54 of 54);
[`probe-results-20260915-023929.txt`](../../code/python/gear_generator/probe/results/probe-results-20260915-023929.txt).

- `body mark 1, plane mark 2: InsertMirrorFeature2(True, False, True, False, 0): returned None`;
  "nothing was mirrored".
- `body mark 256, plane mark 2: InsertMirrorFeature2(True, False, True, False, 0): returned 'CDispatch'`;
  `mirror body mark 256, plane mark 2 = {'volume_mm3': 12566.3706, 'bodies': 1, 'centre_mm': (-1.0638645858380303e-15, 0.0, 4.999999999999999), 'type': 'MirrorSolid'}`.
- `body mark 1, plane mark 1: InsertMirrorFeature2(True, False, True, False, 0): returned None`;
  "nothing was mirrored".
- `mirror_body_routes = ['body mark 256, plane mark 2']`.
- The herringbone half of probe `helical_end_to_end` in
  [`p5_helical.py`](../../code/python/gear_generator/probe/p5_helical.py), same
  runs (figures below from 20260915-023929), built a module 2, 13-tooth, 30° herringbone gear as a helical half
  mirrored about its `Mid Plane` through `Session.mirror_body`: one solid body,
  every sketch fully defined, and it weighed what its straight twin did,
  `herringbone_volumes_mm3 = {'part': 6005.962761822714, 'twin': 6005.622503453732}`.
  After `"Helix Angle"` and `"Face Width"` changed through the tool it still
  did: `herringbone_volumes_after_mm3 = {'part': 8188.880950334994, 'twin': 8188.956589516912}`.

## See also

- [features/08 — Offset reference plane](08-offset-reference-plane.md) — the mirror plane
- [features/10 — Swept cut](10-swept-cut.md) — the helical half that gets mirrored
- [features/09 — Twisted sweep](09-twisted-sweep.md)
- [reading/04 — Read the selection](../reading/04-read-the-selection.md) — marks and counts
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md)
- [GOTCHAS §47](../../GOTCHAS.md)
