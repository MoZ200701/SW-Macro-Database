---
id: features-01-boss-extrude
title: Extrude a closed sketch into a boss
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.FeatureExtrusion3, IFeature.Select2, IModelDocExtension.SelectByID2, IFeature.Name]
keywords: [FeatureExtrusion3, extrude, boss, blind, swEndCondBlind, 23 arguments, depth, solid from sketch, FeatureManager, select sketch]
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

- Only a single-ended blind boss from a closed circle on the first reference
  plane was made. Mid-plane, two-direction, draft, thin features and
  merge-result options were not varied.
- The first three boolean arguments were passed `True, False, False` and never
  changed, so which direction `True` means is not established.
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

## See also

- [features/02 — Cut extrude](02-cut-extrude.md)
- [features/04 — Read a feature's dimensions](04-read-a-features-dimensions.md)
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md)
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [sketches/10 — Is the sketch fully defined](../sketches/10-is-the-sketch-fully-defined.md) — check the profile first
- [curves/11 — Feature-tree folders](../curves/11-feature-tree-folders.md) — another use of `IFeature.Select2`
- [features/05 — Revolve](05-revolve.md) — the other way to make a blank
