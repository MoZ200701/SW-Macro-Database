---
id: features-08-offset-reference-plane
title: A reference plane at a distance from another, on the side you choose
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertRefPlane, IFeature.Name, IDimension.Name, IEquationMgr.Add2, ISketch.ModelToSketchTransform]
keywords: [InsertRefPlane, offset plane, reference plane, distance, parallel plane, swRefPlaneReferenceConstraint_Distance, swRefPlaneReferenceConstraint_OptionFlip, 8, 256, flip, which side, RefPlane, D1, Offset, linked plane, plane follows a global, profile plane, mid plane]
answers: "How do I make a reference plane offset from the Front plane from code, choose which side it lands on, and drive the offset from a global?"
---

# A reference plane at a distance from another, on the side you choose

## What this is for

A sketch that has to sit some distance in front of or behind a part's plane: a
sweep's profile placed ahead of the face it cuts into, the middle plane of a
herringbone gear, a mirror plane at an end face. From code that is one
`InsertRefPlane` call with a distance constraint, plus a flip bit that decides
the side. This entry gives the call that ran, which side each flip gave, and
how the offset was named and linked to a global.

## The call

The tool's form, written from the probe:

```python
def ref_plane(self, handle: str, name: str, plane: int, offset: float, flip: bool,
              offset_dim: str) -> Tuple[str, str]:
    """``InsertRefPlane(8 | flip 256, d, 0, 0, 0, 0)`` on the Nth plane; returns ``(name, offset dimension)``.

    Probe ref_plane_offset, SolidWorks 2026: from the Front plane the
    unflipped plane was at +d on Z and the flipped one at -d, both facing +Z;
    the offset was the plane's only dimension, D1, and followed a global.
    """
    doc = self._doc()
    before = self._top_names()
    self.select_plane_by_order(plane)
    m = offset / MM_PER_METRE
    constraint = findings.REF_PLANE_DISTANCE | (findings.REF_PLANE_FLIP if flip else 0)
    made = call(call(doc, "FeatureManager"), "InsertRefPlane", constraint, m, 0, 0.0, 0, 0.0)
    call(doc, "ClearSelection2", True)
    if made is None:
        raise SolidWorksError(f"InsertRefPlane would not make {name} {offset:g} mm from plane {plane}.")
    feature = self._made_since(before, "RefPlane", "InsertRefPlane")
    kept = self._rename(feature, name)
    dim = self._name_dimension_by_value(feature, m, offset_dim, "offset")
    self._handles[handle] = kept
    return kept, dim
```

`findings.REF_PLANE_DISTANCE = 8` and `findings.REF_PLANE_FLIP = 256`
(`swRefPlaneReferenceConstraints_e` distance and its flip option, read from
`swconst.tlb`). `select_plane_by_order(1)` selects the first `RefPlane` in the
tree, the Front plane ([documents/01](../documents/01-new-part-from-template.md)).
`_made_since` finds the one new `RefPlane` by diffing the top-level feature
names, and `_name_dimension_by_value` renames the dimension whose value is the
offset ([features/04](04-read-a-features-dimensions.md)).

Interfaces and units:

- `InsertRefPlane(first constraint, first value, second constraint, second
  value, third constraint, third value)` on **IFeatureManager**, with the base
  plane selected. The distance is in **metres**. The second and third
  references are unused (`0, 0.0`). Returns the `RefPlane` feature, or `None`.
- The flip is not a separate argument: it is `256` OR-ed into the first
  constraint, `8 | 256 = 264`.

## The probe

From [`code/python/gear_generator/probe/p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py),
probe `ref_plane_offset`. `sc.m(mm)` divides by 1000.

```python
def offset_plane(px: Px, doc: Any, distance_mm: float, flip: bool, base: int = 1) -> Any:
    """InsertRefPlane at a distance from the Nth plane; returns the new RefPlane feature."""
    if not sc.select_plane(doc, base):
        raise Require(f"Reference plane {base} could not be selected.")
    before = sc.feature_names(doc)
    constraint = PLANE_DISTANCE | (PLANE_FLIP if flip else 0)
    made = call(sc.feature_manager(doc), "InsertRefPlane", constraint, sc.m(distance_mm), 0, 0.0, 0, 0.0)
    call(doc, "ClearSelection2", True)
    new = [name for name, kind in sc.new_features(doc, before) if kind == "RefPlane"]
    if made is None or len(new) != 1:
        raise Require(f"InsertRefPlane({constraint}, {sc.m(distance_mm)}) made {new}.")
    return sc.feature_by_name(doc, new[0])
```

Where each plane landed was measured, not assumed: a sketch opened on it and
closed empty, and its origin and normal put through the sketch's inverse
transform ([reading/05](../reading/05-sketch-to-model-transform.md)):

```python
def plane_origin_z(px: Px, doc: Any, plane: Any) -> Tuple[float, Vec]:
    """Where a plane is, as its sketch origin's model z in mm, and its sketch normal in model space."""
    before = sc.feature_names(doc)
    manager = open_on(px, doc, plane)
    data = sketch_to_model(call(manager, "ActiveSketch"))
    sc.close_sketch(px, doc, "Probe Empty", before, allow_empty=True)
    origin = transform_point(data, (0.0, 0.0, 0.0))
    tip = transform_point(data, (0.0, 0.0, 1.0))
    return origin[2] * sc.MM, (tip[0] - origin[0], tip[1] - origin[1], tip[2] - origin[2])
```

Then the unflipped plane was renamed, its one dimension found by value and
renamed `Offset`, and linked:

```python
plane = planes[False]
px.fact("ref_plane_type_name", sc.type_name(plane))
plane.Name = "Probe Plane"
px.check(str(call(plane, "Name")) == "Probe Plane", "the plane renames and reads back")
dims = sc.dimensions_of(plane)
px.fact("ref_plane_dimensions", [(str(call(d, "Name")), float(call(d, "SystemValue"))) for d in dims])
offset = [d for d in dims if near(float(call(d, "SystemValue")), 0.005, 1e-9)]
px.require(len(offset) == 1, "the offset dimension is found by its value")
px.fact("ref_plane_offset_dim_default_name", str(call(offset[0], "Name")))
offset[0].Name = "Offset"
px.check(str(call(offset[0], "Name")) == "Offset", "the offset dimension renames to Offset")
eqm = call(doc, "GetEquationMgr")
add = adder(px, eqm)
px.require(add('"Probe Off"= 7') >= 0, "a global is added")
px.require(add('"Offset@Probe Plane"= "Probe Off"') >= 0, 'the link "Offset@Probe Plane"= "Probe Off" is accepted')
call(doc, "ForceRebuild3", False)
z, _ = plane_origin_z(px, doc, plane)
px.check(near(z, 7.0 if sides["False"] > 0 else -7.0, 1e-6), f"linked to 7 the plane moves to z {z}")
```

## Which side

From the Front plane, 5 mm:

| First constraint | Plane origin, model z | Sketch normal |
|---|---|---|
| `8` (no flip) | **+5.0 mm** | (0, 0, 1) |
| `8 \| 256` (flip) | **−5.0 mm** | (0, 0, 1) |

The unflipped plane is on the **+Z** side, which is also the side a boss
extruded from the Front plane grows toward
([features/01](01-boss-extrude.md)). Both planes' sketches face +Z like the
Front plane's own, so the flip moves the plane without turning its sketch
over: a profile drawn on either lands the same way round.

## Why it is not obvious

**The flip is a bit in the constraint, not an argument.** There is no
"reverse" parameter to find; `256` goes into the same integer as `8`.

**Measure the side once.** Nothing in the call's name says which way "not
flipped" goes. The probe records it as a fact
(`ref_plane_on_extrude_side_flip = False`) and the tool reads that finding
rather than assuming.

**The offset is the plane's only dimension, and it links like any other.**
`ref_plane_dimensions = [('D1', 0.005)]`. Renamed `Offset`, it took the link
`"Offset@Probe Plane"= "Probe Off"`, and the plane followed the global
([equations/02](../equations/02-link-a-dimension-to-a-global.md)). A plane square
to a line has no dimension at all, by contrast
([features/06](06-reference-plane-normal-to-a-line.md)).

## What it does not do

- Only the Front plane was used as the base, at 5 mm, and a linked 7 mm. Other
  base planes, a face as the base, negative distances and angle constraints
  were not tried.
- Whether the flipped plane's dimension reads +0.005 or −0.005 was not
  recorded; only the unflipped plane's dimensions were read.
- A global driven through zero, which would move the plane to the other side
  or be refused, was not tried.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe `ref_plane_offset`
in [`p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py).
It passed with the same facts in run 20260915-002812 (43 of 43 probes) and run
20260915-023929 (54 of 54);
[`probe-results-20260915-023929.txt`](../../code/python/gear_generator/probe/results/probe-results-20260915-023929.txt).

- `InsertRefPlane distance 5 mm, flip False: returned 'CDispatch'`, and the same
  for flip True.
- `plane_flip_False = {'origin_z_mm': 5.0, 'normal': [0.0, 0.0, 1.0]}`;
  `plane_flip_True = {'origin_z_mm': -5.0, 'normal': [0.0, 0.0, 1.0]}`;
  "flip False puts the plane on the +Z side".
- `ref_plane_type_name = 'RefPlane'`; "the plane renames and reads back".
- `ref_plane_dimensions = [('D1', 0.005)]`; `ref_plane_offset_dim_default_name = 'D1'`;
  "the offset dimension renames to Offset".
- "the link "Offset@Probe Plane"= "Probe Off" is accepted"; "linked to 7 the
  plane moves to z 7.0".
- Probe `helical_end_to_end`, same runs: the tool made a helical gear's
  `Profile Plane` and a herringbone gear's `Mid Plane` through
  `Session.ref_plane`, and both parts built with every feature the tool names
  in the tree and every sketch fully defined.

## See also

- [features/06 — Reference plane normal to a line](06-reference-plane-normal-to-a-line.md) — the other `InsertRefPlane` form
- [features/09 — Twisted sweep](09-twisted-sweep.md) — a profile on an offset plane
- [features/10 — Swept cut](10-swept-cut.md) — starting a cut ahead of the face
- [features/11 — Mirror a body](11-mirror-body.md) — mirroring about an offset plane
- [features/01 — Boss extrude](01-boss-extrude.md) — which way +Z is from the Front plane
- [reading/05 — Sketch to model transform](../reading/05-sketch-to-model-transform.md) — measuring where the plane is
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [features/04 — Read a feature's dimensions](04-read-a-features-dimensions.md)
