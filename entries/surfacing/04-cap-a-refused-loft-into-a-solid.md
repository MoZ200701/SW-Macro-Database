---
id: surfacing-04-cap-a-refused-loft-into-a-solid
title: Get a solid out of a loft SolidWorks refuses, by capping the surface and knitting it
status: verified
verified_on: SolidWorks 2026 SP0.0 (revision 34.0.0)
language: [python]
api: [IModelDoc2.InsertLoftRefSurface2, IModelDoc2.InsertPlanarRefSurface, IFeatureManager.InsertSewRefSurface, IFeatureManager.InsertFillSurface2, IEntity.Select4, ISelectionMgr.CreateSelectData, IModelDocExtension.SelectByID2, IFeature.GetFaces, IFace2.GetBody, IFace2.GetArea, IBody2.GetEdges, IEdge.GetCurveParams2, IPartDoc.GetBodies2, IBody2.GetMassProperties]
keywords: [InsertProtrusionBlend2 returns Nothing, six decimals, end section not planar, replace capped loft, delete order, No feature called, loft refused, silent refusal, InsertPlanarRefSurface, planar surface cap, InsertSewRefSurface, knit, sew, SewRefSurface, PlanarSurface, TryToFormSolid, SURFACEBODY, Select4, CreateSelectData, GetEdges, GetCurveParams2, end loop, sliver face, cap area check, solid body count, InsertFillSurface2, VT_ARRAY, composite curve as boundary]
answers: "SolidWorks refuses my solid loft but makes the surface — how do I get a solid out of it from code?"
---

# Cap a refused loft into a solid

## What this is for

`IFeatureManager.InsertProtrusionBlend2` sometimes refuses a solid loft
**silently**: it returns `Nothing`, adds no feature, raises no error and shows
no dialog, while `IModelDoc2.InsertLoftRefSurface2` makes the surface through
exactly the same curves every time ([features/12](../features/12-guided-loft.md)).
A surface is enough to measure, but not to split, cut or export as a body.

This is the way round it: loft the **surface**, put a flat face across each
end, and knit the three sheets into one solid. It is the same geometry, built
from the other side, and it needs no sketch and no plane.

Why a solid is refused where the surface is not is in
[surfacing/03](03-how-a-loft-fills-between-profiles.md) — on the case measured
here, a single guide curve near the nose split a sliver face off the loft whose
end loop nothing can close. That entry is also why this recipe dropped guides a
rung at a time when a cap was refused.

> **Update, 2026-09-27: check the curve files' decimals first.** Most of the
> refusals this recipe was built for were not the sliver at all: the curve
> files were written to six decimals of a millimetre, and SolidWorks judged
> their end sections not flat and refused every solid loft ending on them.
> Written to ten decimals, the same lofts built as solids directly — through
> two profiles and three guides, and through 22 sections and 33 guides — and
> a Boss-Loft built that way survived seven later changes of its curves
> without error ([curves/01](../curves/01-sldcrv-file-format.md)). The recipe
> below still works and is still the fallback worth having, but the Airfoil
> Converter no longer drops guides to make it close: a guide ladder hid the
> cause instead of mending it. It tries the capped surface once, with every
> guide, and leaves the surface if that does not close.

## The three steps

Timings on the part below: surface loft 10–15 s, each planar cap 0.2 s, the
knit 0.2–8.4 s.

### 1. The surface loft

```python
loft = sw.insert_loft(plan.profiles, guides, name + "_surface", solid=False)
```

`insert_loft(..., solid=False)` is
`IModelDoc2.InsertLoftRefSurface2(False, keep_tangency, False, 1.0, 0, 0)` with
the profiles selected at mark 1 and the guides at mark 2
([features/12](../features/12-guided-loft.md)). Its type name is
`BlendRefSurface`.

### 2. A planar face across each end

The end edges are found geometrically, not by name: **an end loop's edges have
both of their ends in the plane of that end's profile**, while every edge
running from one end to the other has one end in each.

From the Airfoil Converter's `swcom.py`, `Session.cap_end`, copied as it ran
([`code/python/swcom.py`](../../code/python/swcom.py)):

```python
def cap_end(self, loft: str, profile: str, name: str) -> str:
    """Close one end of a surface loft with a planar surface, named ``name``."""
    doc = self._active()
    pieces = self.profile_pieces(profile)
    points = [point for piece in pieces for point in piece]
    plane = plane_through(points)
    wanted = area_in_plane(points, plane)
    edges = []
    for edge in list(call(self._feature_body(loft), "GetEdges") or []):
        # The curve has to be generated before its parameters can be read:
        # SolidWorks does not keep the underlying curve on the edge.
        call(edge, "GetCurve")
        params = call(edge, "GetCurveParams2")
        ends = (
            tuple(float(params[i]) * MM_PER_METRE for i in range(3)),
            tuple(float(params[i]) * MM_PER_METRE for i in range(3, 6)),
        )
        if all(_off_plane(end, plane) <= END_PLANE_TOL for end in ends):
            edges.append(edge)
    if not edges:
        raise SolidWorksError(
            f"No edge of {loft} lies in the plane of {profile}, so that end "
            "cannot be capped."
        )
    if len(edges) != len(pieces):
        # One edge along each piece of the profile is what an end of this
        # loft is. Any more and the loft has grown something there.
        raise SolidWorksError(
            f"The end of {loft} at {profile} has {len(edges)} edges where the "
            f"profile has {len(pieces)} pieces, so the loft has something on "
            "that end the section does not."
        )

    call(doc, "ClearSelection2", True)
    data = call(call(doc, "SelectionManager"), "CreateSelectData")
    for position, edge in enumerate(edges):
        if not call(edge, "Select4", position > 0, data):
            call(doc, "ClearSelection2", True)
            raise SolidWorksError(f"An edge of {loft} at {profile} would not select.")

    before = set(self.feature_names())
    made = call(doc, "InsertPlanarRefSurface")
    call(doc, "ClearSelection2", True)

    created = [n for n in self.feature_names() if n not in before]
    if not made or not created:
        raise SolidWorksError(
            f"SolidWorks would not put a planar surface across the {len(edges)} "
            f"edges of {loft} at {profile}."
        )
    if len(created) != 1:
        raise SolidWorksError(
            f"Capping {loft} at {profile} added {len(created)} features, "
            "so which one it is cannot be told."
        )

    # It made a face; the question is whether it made it across the end.
    # SolidWorks will happily put one over a sliver's own little loop and
    # answer True, and a knit of that sews a sheet and answers True too.
    made_area = self.face_area(created[0])
    if made_area < CAP_AREA_SHARE * wanted:
        self.delete_feature(created[0])
        raise SolidWorksError(
            f"The cap SolidWorks put across the end of {loft} at {profile} is "
            f"{made_area:.3f} mm² against the section's {wanted:.1f} mm², so it "
            "spans something else."
        )
    return self.rename_feature(created[0], name)
```

*Since 2026-09-23 the Airfoil Converter finds what this made by the feature count and `GetLastFeatureAdded` instead of listing the tree before and after — the same check, a hundredth of a second instead of seconds on a big part; the copy in [`code/python/swcom.py`](../../code/python/swcom.py) is that version. See [curves/02](../curves/02-insert-curve-from-file.md).*

Interfaces and units:

- `IFeature.GetFaces` gives a feature's faces; `IFace2.GetBody` gives the body
  behind one. **A feature does not offer its body — a face of it does.** That
  is `_feature_body` above.
- `IBody2.GetEdges` comes back through pywin32 as an **uncalled method object**,
  not a tuple; so does `IFace2.GetTessTriangles`. See
  [GOTCHAS §55](../../GOTCHAS.md) and
  [connect/02](../connect/02-attach-from-python.md).
- `IEdge.GetCurveParams2` gives the start point in items 0–2 and the end point
  in items 3–5, **in metres**, then the parameter range in 6 and 7. Calling
  `IEdge.GetCurve` first is in this code because SolidWorks does not keep the
  underlying curve on the edge until something asks for it.
- `IEntity.Select4(append, selectData)` selects an edge. The `selectData` comes
  from `ISelectionMgr.CreateSelectData` and needs no mark. An edge has no name,
  so `SelectByID2` — which is what the API help's fill-surface example uses —
  cannot be followed here.
- `IModelDoc2.InsertPlanarRefSurface` takes **no arguments**, reads the
  selection, and answers `True` or `False`. Its feature type name is
  `PlanarSurface`.
- `CAP_AREA_SHARE` is 0.5 and `END_PLANE_TOL` is 0.05 mm. Areas above are in
  square millimetres; `IFace2.GetArea` is in square metres, so `face_area`
  multiplies by 1e6, and `MM_PER_METRE` is 1000.0.
- The plain-arithmetic helpers are the tool's own, all in
  [`code/python/swcom.py`](../../code/python/swcom.py): `profile_pieces(name)`
  reads a composite's sources and each one's points
  ([curves/06](../curves/06-composite-curve.md)); `plane_through(points)` returns
  `(a point on the plane, its unit normal)`, the normal by Newell's area-weighted
  sum so that a nose where the points crowd together cannot tilt it;
  `area_in_plane` is the shoelace area of those points laid on two axes across
  that plane; `_off_plane` is a point's distance from it; `call` and `_null_dispatch` are
  [connect/02](../connect/02-attach-from-python.md)'s. Using the **profile's
  own** points and normal rather than a part axis is what keeps this true of a
  loft standing anywhere, at any angle — the experiment that found the recipe
  used the part's x and would not have.

### 3. Knit the three sheets

```python
def knit_to_solid(self, surfaces: Sequence[str], name: str, solid: bool = True) -> str:
    """Knit the bodies ``surfaces`` made into one, a solid if they close one."""
    if len(surfaces) < 2:
        raise SolidWorksError("A knit needs at least two surfaces to join.")
    doc = self._active()
    was_solid = self.solid_bodies() if solid else 0
    bodies = [self.body_name(surface) for surface in surfaces]
    self._select_all(
        [(body, KNIT_SELECT_MARK) for body in bodies], "to knit", SURFACE_BODY_TYPE,
    )

    before = set(self.feature_names())
    made = call(
        call(doc, "FeatureManager"), "InsertSewRefSurface",
        True,            # UseGapFilters
        solid,           # TryToFormSolid
        False,           # MergeEntities: keep the faces as they are
        KNIT_TOLERANCE,
        KNIT_GAP_RANGE,
    )
    call(doc, "ClearSelection2", True)

    created = [n for n in self.feature_names() if n not in before]
    if made is None or made is False or not created:
        what = "a solid" if solid else "one surface"
        raise SolidWorksError(
            f"SolidWorks would not knit {' and '.join(surfaces)} into {what}."
        )
    if len(created) != 1:
        raise SolidWorksError(
            f"Knitting added {len(created)} features, so which one it is cannot be told."
        )
    if solid and self.solid_bodies() != was_solid + 1:
        # It sewed them, and answered as if it had done what was asked. The
        # part has one sheet where it had three, and no more solid than it
        # started with.
        self.delete_feature(created[0])
        raise NotASolid(
            f"Knitting {' and '.join(surfaces)} sewed them into a sheet rather "
            "than a solid: the surfaces do not close a volume between them."
        )
    return self.rename_feature(created[0], name)
```

- `IFeatureManager.InsertSewRefSurface(useGapFilters, tryToFormSolid,
  mergeEntities, knitTolerance, maxGapRange)`. The two tolerances are **in
  metres**: `1e-4` is 0.1 mm, the upper limit the help gives.
- **The bodies are selected by name, not by themselves.** `IBody2` has no
  `Select4`, and `Select2` raised through pywin32 (`TypeError: The Python
  instance can not be converted to a COM object`) with a `0` and with a typed
  null as its second argument. A real `SelectData` from
  `ISelectionMgr.CreateSelectData` — which is what makes `IBody2.Select2` work
  elsewhere in this collection, on a **solid** body — was not retried here once
  the name route worked; that is an open question worth two minutes if you need
  it. What is proven is selecting each body **by its own name** with
  `IModelDocExtension.SelectByID2(body.Name, "SURFACEBODY", 0, 0, 0, append, 1,
  null, 0)` — mark 1, as the help's own knit example does. `IBody2.Name` is the
  name to use, reached through the body of the feature that made it.
- `KNIT_SELECT_MARK` is 1; `SURFACE_BODY_TYPE` is `"SURFACEBODY"`.
  `_select_all(picks, what, kind)` is the clear-select-all-or-rebuild-and-retry
  helper from [features/12](../features/12-guided-loft.md), given
  `"SURFACEBODY"` as the kind instead of `"REFERENCECURVES"`; `body_name` is
  `IBody2.Name` off `_feature_body`, and `NotASolid` is a subclass of the
  module's `SolidWorksError` so a caller can tell this failure from the others.
- The knit's `GetTypeName2` is **`SewRefSurface`**, not a loft type. Code that
  looks for "the bodies in this part" by feature type has to know that.
- `solid_bodies()` is `len(IPartDoc.GetBodies2(0, False))`; `swBodyType_e` is
  **0 solid, 1 sheet**.

## Why it is not obvious

**A boundary has to be edges.** Neither `InsertPlanarRefSurface` nor
`IFeatureManager.InsertFillSurface2` will take a composite curve as a boundary,
or the curves that composite joins. Both refused in **0.0 s** — no error, just
`False` or `None` — even over an end loop that was flat to a nanometre. Only
the loft body's own end edges were accepted.

*Open question (2026-09-27):* those curves were six-decimal files, "flat to a
nanometre" is that rounding, and SolidWorks is now known to reject such a loop
as not planar for a loft's end ([curves/01](../curves/01-sldcrv-file-format.md)).
Whether a planar surface would take a composite of ten-decimal curves was not
tried. Settling it is one call per decimals setting.

**A cap that spans the wrong thing still answers True.** On the failing case,
the planar surface across a 4-edge end loop came out as a face of
**0.078 mm²** where the section encloses **4,046.8 mm²**: a face across a
sliver's own little loop, not across the wing. `InsertPlanarRefSurface` returned
`True` and the feature appeared in the tree. That is the `made_area` check
above, and [GOTCHAS §60](../../GOTCHAS.md).

**A knit asked for a solid can sew a sheet and say yes.** With that 0.078 mm²
cap in it, `InsertSewRefSurface(..., tryToFormSolid=True, ...)` made a
`SewRefSurface` feature, turned three sheet bodies into one sheet body, and
added **no** solid body. Its `GetMassProperties` volume slot then read
439,803,896.6 mm³ against a real 2,551,504.3 — a meaningless number from an
unclosed shell. Count `GetBodies2(0, False)` before and after; that is
[GOTCHAS §61](../../GOTCHAS.md).

**Fill works too, and is fussier.** `InsertFillSurface2(3, 0, boundaries,
contacts, null, null)` on **IFeatureManager** capped the same ends in 0.2–0.3 s
once the right argument form was found, and finding it cost six attempts: it
only accepts its arrays as **typed VARIANTs**:
`VARIANT(VT_ARRAY | VT_DISPATCH, edges)` and `VARIANT(VT_ARRAY | VT_I4, [0] *
n)`. A Python tuple of edges, a `SelectData`, a typed null and "read the
selection" all returned `None`, each after about four seconds of SolidWorks
thinking about it. Where the ends are flat — and an offset wing's are, to about
a nanometre — the planar surface is the simpler call: no arguments, and exact.

**Deleting the knit does not delete what it knitted.** Take the
`SewRefSurface` out and the loft surface and both caps come back as three sheet
bodies, still in the tree. Whatever built them has to delete them itself; the
Airfoil Converter names all four (`<name>_surface`, `<name>_root_cap`,
`<name>_tip_cap`, `<name>`) so it can.

**Replacing a capped loft: delete the caps before the surface.** The caps are
built on the surface's end edges, so deleting the surface takes both caps with
it. Code that deleted the knit, then the surface, then asked for each cap by
name got `No feature called '..._root_cap'` and stopped — and the knit was
already gone, so the part was left with no loft at all. Delete in the reverse
of building: knit, tip cap, root cap, surface, each only if it is still there.
Observed on SolidWorks 2026 SP0.0, 2026-09-23 (`e55`, `e57`): with that
order, a capped loft whose section count had changed was replaced in 43 s and
came back a solid of the right volume.

**The knit is what carries the body.** For anything that walks "the lofts in
this part" — suppressing them one at a time to export, say
([files/04](../files/04-export-a-body-to-step.md)) — the capped loft's body
belongs to the knit, while the surface beside it still calls itself a
`BlendRefSurface` and must not be suppressed, because that would take the solid
with it.

## What it does not do

- **It was slow through the app**, 85–206 s for one capped loft on this part,
  because the surface was lofted more than once: when a cap was refused the
  guides came off a rung at a time and the surface was built again. The
  construction on its own — one surface, two caps, one knit — is 30–45 s, and
  36–73 s through the app once the ladder was taken out (2026-09-23).
- Only flat ends were capped. A loft whose end profile is not planar needs the
  fill surface rather than the planar one; that was made to work on this
  geometry but never on a non-planar end.
- `MergeEntities` was only ever `False` and the tolerance only ever 1e-4 m.
- `InsertSewRefSurface` was only ever given three sheets that meet along the
  very curves they were built from, so the gap filters had nothing to bridge.
- Whether the knitted solid is identical to the solid loft SolidWorks refuses
  cannot be answered directly, because in the refusing cases there is no solid
  loft to compare with. Where both exist they agree closely: at a 1.45 mm
  offset the solid loft weighed 2,487,078.9 mm³ and the knit of the same
  33 guides 2,487,141.5 mm³, a difference of **0.0025 %**.
- Nothing here sets the knit's or the cap's appearance, and the caps are
  separate faces in the result rather than merged into the loft.
- Whether `IBody2.Select2` would work on a sheet body given a proper
  `SelectData` was not settled, only that it raises with a `0` or a typed null.
  Settling it is one call.

## Evidence

SolidWorks 2026 SP0.0 (revision 34.0.0), Windows, Python 3.13 with pywin32,
against a save-as copy of a real 397-feature part, with the tree rolled back to
just after the wing's curves ([curves/12](../curves/12-roll-the-tree-back-before-reloading.md)).
Experiments `e33`, `e34`, `e35`, `e36b`, `e37`, `e38`, `e39`, `e40b`, `e42`,
`e43`, `e47`, `e48`, `e51` of 2026-09-22; `e40b.py` is the recipe written out
clean. Carried into the Airfoil Converter by commits `7caf687` ("Close a
refused solid loft the other way round: surface, flat caps, knit"), `2065563`
(v1.7) and `8555d5f` ("Check that a cap spans its end and a knit makes a solid,
and put a suppression back").

- `e33`, `e33b`, `e30`: a cap from the composite curve, and from the three
  curves it joins, refused in 0.0 s at both ends, every time.
- `e34`: of six argument forms for `InsertFillSurface2`, only
  `VT_ARRAY|VT_DISPATCH` + `VT_ARRAY|VT_I4` made a surface (`Surface-Fill2`,
  2.1 s); the other five returned `None` in 3.9–4.5 s, and
  `IModelDoc2.InsertFillSurface` raised `AttributeError`.
- `e36b`, `e37`, `e38`: at a 1.45 mm offset the surface loft has 3 faces, both
  end loops 3 edges, both caps made in 0.2 s, and the knit gave **1 solid body,
  2,487,141.5 mm³** in 0.6 s. At 1.30, 1.35 and 1.40 the loft has 4 faces, the
  tip loop 4 edges including a straight chord of 2.185–2.240 mm, the root cap
  is made and the **tip cap is refused** in 0.1 s, with resolution 0, 1 and 3
  and both options.
- `e39`, `e42`, `e43`: with the lower 2 % guide dropped (32 of 33), every
  offset lofts to 3 faces, both ends cap, and the knit gives one solid:
  2,551,504.3 / 2,530,077.9 / 2,508,086.5 / 2,486,810.8 mm³ at 1.30 / 1.35 /
  1.40 / 1.45. Knit 0.6–1.3 s. The knit feature's `GetTypeName2` is
  `SewRefSurface`; after deleting it, loft and both caps are still listed and
  the part holds 3 sheet bodies and 0 solid.
- `e48`: at 1.30 with all 33 guides, `cap_end(root)` made a `PlanarSurface` of
  **4,046.839 mm²** and `cap_end(tip)` a `PlanarSurface` of **0.078 mm²**; the
  knit then took 3 sheets to 1 sheet, gained **0** solid bodies, and its body's
  mass-properties volume slot read 439,803,896.6 mm³.
- `e47`, `e51`: through the app's own `swloft.loft_in_part`, one capped solid
  per offset, volume matching the by-hand knit to `+0.0000 %`; 85.2 s, 101.1 s,
  159.2 s, 174.1 s and 206.3 s across runs, against 19.7 s where the solid loft
  was accepted outright.
- `e40b`: the whole construction, 30.6–45.3 s per offset, one solid body each
  time.
- `e52`: the STEP file written from the knitted body holds
  `MANIFOLD_SOLID_BREP 1`, `CLOSED_SHELL 1`, `OPEN_SHELL 0`, `ADVANCED_FACE 5`.

## See also

- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — the solid loft this replaces, and the surface loft it starts from
- [surfacing/03 — How a loft fills between two profiles](03-how-a-loft-fills-between-profiles.md) — why the solid was refused, and which guide did it
- [curves/06 — Composite curves](../curves/06-composite-curve.md) — the profiles, and why their piece count is what an end loop is checked against
- [curves/12 — Roll the tree back before reloading](../curves/12-roll-the-tree-back-before-reloading.md)
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md) — the volume that told a solid from a sheet
- [reading/13 — Measure a wall between two bodies](../reading/13-measure-a-wall-between-two-bodies.md) — reading the body this makes
- [files/04 — Export a body to STEP](../files/04-export-a-body-to-step.md) — writing it out, and what its feature type means there
- [surfacing/01 — The boundary surface recipe](01-boundary-surface-recipe.md)
- [curves/01 — The .sldcrv format](../curves/01-sldcrv-file-format.md) — the decimals that caused most refusals
- [GOTCHAS §55, §60, §61, §63, §67](../../GOTCHAS.md)
