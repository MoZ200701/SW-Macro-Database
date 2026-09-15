---
id: features-01-boss-extrude
title: Extrude a closed sketch into a boss
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.FeatureExtrusion3, IFeature.Select2, IModelDocExtension.SelectByID2, IFeature.Name, IMassProperty.CenterOfMass]
keywords: [FeatureExtrusion3, extrude, boss, blind, swEndCondBlind, 23 arguments, depth, solid from sketch, FeatureManager, select sketch, extrude direction, which way, +Z, annulus, ring, two concentric circles, ring gear blank]
answers: "How do I extrude a closed sketch into a solid boss from code?"
---

# Extrude a closed sketch into a boss

## What this is for

The first solid in most parts: a closed sketch pushed out to a depth. From code
it is one call with 23 arguments on the feature manager, after selecting the
sketch. This entry gives the arguments that ran, how to be sure the sketch is
selected, and what the call hands back.

## The call

Select the closed sketch's feature, then call **IFeatureManager**
(`IModelDoc2.FeatureManager`) `FeatureExtrusion3`. From the tool whose gear
build ran it:

```python
def extrude(self, handle: str, sketch: str, name: str, depth: float, depth_dim: str) -> Tuple[str, str]:
    """A blind boss (probe extrude: volume pi r^2 w, depth dimension D1)."""
    doc = self._doc()
    self._select_feature(self._sketch_feature(sketch))
    m = depth / MM_PER_METRE
    feature = call(call(doc, "FeatureManager"), "FeatureExtrusion3", True, False, False, 0, 0, m, 0.0, False,
                   False, False, False, 0.0, 0.0, False, False, False, False, True, True, True, 0, 0.0, False)
    call(doc, "ClearSelection2", True)
    if feature is None:
        raise SolidWorksError(f"FeatureExtrusion3 would not extrude {sketch} into {name}.")
    kept = self._rename(feature, name)
    dim = self._name_dimension_by_value(feature, m, depth_dim, "depth",
                                        ignore=self._handles[sketch].get("dimensions", ()))
    self._handles[handle] = {"feature": kept}
    return kept, dim
```

The probe's version of the same arguments, with its own comment:

```python
# swEndConditions_e, from swconst.tlb on SolidWorks 2026.
BLIND = 0
THROUGH_ALL = 1
THROUGH_ALL_BOTH = 9

def _extrude_args(depth_m: float) -> Tuple[Any, ...]:
    """FeatureExtrusion3's 23 arguments for a single-ended blind boss, in the help's order."""
    return (True, False, False, BLIND, BLIND, depth_m, 0.0, False, False, False, False, 0.0, 0.0,
            False, False, False, False, True, True, True, 0, 0.0, False)
```

What this entry claims about the arguments is only what was varied or read:
the fourth and fifth are end conditions (`0`, blind, for both), the sixth is
the depth in **metres**, and the rest were passed as shown. The seventh, the
second direction's depth, was `0.0`.

It returns the new feature (`IFeature`, `GetTypeName2` `"Extrusion"`), or
`None`. Rename it and read the name back
([curves/07](../curves/07-rename-a-feature.md)).

### Selecting the sketch

```python
def _select_feature(self, feature: Any, append: bool = False, mark: int = 0) -> None:
    doc = self._doc()
    if not append:
        call(doc, "ClearSelection2", True)
    before = self._selected()
    if self._retry_select(lambda: call(feature, "Select2", append, mark), before):
        return
    kind = {"RefPlane": "PLANE", "RefAxis": "AXIS", "ProfileFeature": "SKETCH"}.get(
        _type_name(feature), "BODYFEATURE")
    name = str(call(feature, "Name"))
    if call(call(doc, "Extension"), "SelectByID2", name, kind, 0.0, 0.0, 0.0, append, mark,
            _null_dispatch(), 0) and self._selected() > before:
        return
    raise SolidWorksError(f"{name} ({_type_name(feature)}) could not be selected by Select2 or SelectByID2 "
                          f"{self._selection_context()}.")
```

`_selected()` is `ISelectionMgr.GetSelectedObjectCount2(-1)`; `_retry_select`
tries the select up to four times, 0.25 s apart, and counts it only if the
selection count went up. Success is judged by the count, not by `Select2`'s
return value. Which of the two routes selected the sketch in the recorded run
was not logged; one of them did every time. The same count-judged
selection failed once elsewhere, on a new sketch point, with no known cause; see
[GOTCHAS §36](../../GOTCHAS.md).

### Which way it goes

From the Front plane, with the arguments above, the boss grows toward **+Z**.
Probe `center_of_mass` in
[`p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py)
extruded a circle r 3 mm centred at (12, 5) by 10 mm and read where its
centre of mass was:

```python
front = sc.planes(doc)[0]
sketch = circle_sketch(px, doc, front, "Probe Boss Sketch", (PROFILE_X, 5.0, 0.0))
px.require(sc.select_feature(doc, sc.feature_by_name(doc, sketch)), "the boss sketch selects")
from .p2_solid import _extrude_args  # noqa: PLC0415
boss = call(sc.feature_manager(doc), "FeatureExtrusion3", *_extrude_args(BLANK_W / sc.MM))
call(doc, "ClearSelection2", True)
px.require(boss is not None, "the boss is extruded")
centre = centre_of_mass_mm(doc)
px.fact("boss_centre_of_mass_mm", centre)
px.check(near(centre[0], PROFILE_X, 1e-6) and near(centre[1], 5.0, 1e-6),
         f"x and y of the centre are the sketch's, in metres: {centre}")
px.require(near(abs(centre[2]), BLANK_W / 2.0, 1e-6), f"the centre is half the depth off the plane: {centre[2]}")
sign = 1 if centre[2] > 0 else -1
px.fact("extrude_z_sign", sign, f"a blank from the Front plane extrudes toward {'+' if sign > 0 else '-'}Z")
```

The centre came back (12.0, 5.0, 5.0) mm: z at **+half the depth**.
`centre_of_mass_mm` is **IMassProperty** `CenterOfMass` after a rebuild, three
doubles in metres, scaled to mm. An unflipped offset plane from the Front plane
lands on the same side ([features/08](08-offset-reference-plane.md)).

### Two circles make a ring

A sketch of two concentric circles extrudes, with the same call, into one
annular body: the outer circle is the rim and the inner one the hole. Probe
`annulus_extrude` in
[`p2_internal.py`](../../code/python/gear_generator/probe/p2_internal.py):

```python
before = sc.feature_names(doc)
manager = sc.open_sketch(px, doc, 1)
rim = call(manager, "CreateCircleByRadius", 0.0, 0.0, 0.0, RIM_D / 2.0 / sc.MM)
tip = call(manager, "CreateCircleByRadius", 0.0, 0.0, 0.0, TIP_D / 2.0 / sc.MM)
px.require(rim is not None and tip is not None, "both circles are made")
origin = sc.origin_point(doc)
sc.relate(doc, "sgCOINCIDENT", call(rim, "GetCenterPoint2"), origin)
sc.relate(doc, "sgCOINCIDENT", call(tip, "GetCenterPoint2"), origin)
px.check(_dimension(doc, rim, 28.0, "Rim") == "Rim", "the rim's dimension renames to Rim")
px.check(_dimension(doc, tip, 5.0, "Tip") == "Tip", "the tip circle's dimension renames to Tip")
sketch = sc.close_sketch(px, doc, "Ring Sketch", before)
```

then `FeatureExtrusion3` with `_extrude_args` as above. With D 40 and d 24 mm
and 10 mm deep the part weighed π/4 × (40² − 24²) × 10 = 8042.4772 mm³ in one
solid body, not a disc. The inner circle's dimension, linked by
`"Tip@Ring Sketch"= "Probe Tip"` to 20, opened the hole to match: 9424.7780 mm³.
That is how the Gear Generator makes a ring gear's blank.

## Why it is not obvious

**23 positional arguments and no names.** From late-bound Python there are no
keyword arguments; a wrong count raises and a wrong value in the wrong place
makes a different feature. Copy the tuple that ran and change only what you
have verified.

**The call returning an object does not prove the solid is right.** The probe
checked the part's volume afterwards
([reading/10](../reading/10-mass-properties-as-an-oracle.md)): a 20 mm radius
circle extruded 10 mm gave 12566.371 mm³, which is π × 20² × 10.

**The depth dimension is `D1`, but find it by value.** The extrusion's
dimensions came back as `[('D1', 0.01), ('OD', 0.04)]`: its own depth, and the
sketch's diameter dimension as well. The depth is the one whose `SystemValue`
equals the depth asked for, ignoring the sketch's own dimensions. See
[features/04](04-read-a-features-dimensions.md).

**Rebuild suppression did not get in the way.** With
`ISldWorks.CommandInProgress` set `True`, a blank and a cut through it were
both made, the flag restored, and one rebuild gave the right volume
(12283.627 mm³). See [curves/08](../curves/08-rebuild-once-at-the-end.md).

## What it does not do

- Only single-ended blind bosses on the first reference plane were made, from
  one circle or two concentric circles. Mid-plane, two-direction, draft, thin features and
  merge-result options were not varied.
- The first three boolean arguments were passed `True, False, False` and never
  changed, so which direction `True` means is not established. With them the
  boss grew toward +Z from the Front plane.
- Extruding a sketch that is not closed was not tried.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426, probe `extrude` in
[`code/python/gear_generator/probe/p2_solid.py`](../../code/python/gear_generator/probe/p2_solid.py).

- `extrude_type_name = 'Extrusion'`; renamed to `Blank` and read back.
- `blank_volume_mm3 = 12566.370614359175` against π × 20² × 10 = 12566.370614359173.
- `extrude_dimensions = [('D1', 0.01), ('OD', 0.04)]`;
  `depth_dimension_default_name = 'D1'`; renamed `Width`, `FullName`
  `'Width@Blank@Part233.Part'`.
- Linked `"Width@Blank"= "Probe W"` with 12: 15079.645 mm³ (π × 20² × 12).
- `batched_under_command_in_progress`: 12283.627 mm³ after one rebuild.
- `end_to_end`: the gear blank was made through `extrude` above and its width
  linked to `"Face Width"`.
- `center_of_mass`, in [`p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py),
  runs 20260915-002812 and 20260915-023929:
  `boss_centre_of_mass_mm = (12.0, 5.000000000000001, 5.000000000000001)`;
  "a blank from the Front plane extrudes toward +Z".
- `annulus_extrude`, in [`p2_internal.py`](../../code/python/gear_generator/probe/p2_internal.py),
  run 20260915-023929: `annulus_volume_mm3 = {'volume': 8042.477193189872, 'expected': 8042.47719318987}`;
  `annulus_solid_bodies = 1`; "the link "Tip@Ring Sketch"= "Probe Tip" is accepted";
  `annulus_volume_linked_mm3 = {'volume': 9424.777960769381, 'expected': 9424.77796076938}`.
  Probe `internal_end_to_end` in the same run built spur and helical rings on
  such blanks, each one solid body with every sketch fully defined.

## See also

- [features/02 — Cut extrude](02-cut-extrude.md)
- [features/04 — Read a feature's dimensions](04-read-a-features-dimensions.md)
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md)
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [sketches/10 — Is the sketch fully defined](../sketches/10-is-the-sketch-fully-defined.md) — check the profile first
- [curves/11 — Feature-tree folders](../curves/11-feature-tree-folders.md) — another use of `IFeature.Select2`
- [features/05 — Revolve](05-revolve.md) — the other way to make a blank
- [features/08 — Offset reference plane](08-offset-reference-plane.md) — planes on the side the boss grows
