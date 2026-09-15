---
id: features-03-circular-pattern
title: Pattern a feature around an axis
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IModelDoc2.InsertAxis2, IFeatureManager.FeatureCircularPattern5, IFeature.Select2]
keywords: [FeatureCircularPattern5, circular pattern, InsertAxis2, reference axis, RefAxis, axis from sketch line, GetRefAxisParams, CirPattern, selection mark, mark 1, mark 4, DName NULL, instance count, equal spacing, teeth]
answers: "How do I make a circular pattern of a feature about an axis from code?"
---

# Pattern a feature around an axis

## What this is for

Gear teeth, bolt circles, spokes: one feature repeated equally around an axis.
From code that is a reference axis, a selection with two different marks, and
`FeatureCircularPattern5` with fourteen arguments, two of them strings that
must be `"NULL"`.

## The axis it needs

The part's own normal to the first plane is the intersection of the other two.
Select reference planes 2 and 3 (tree order: Top and Right) and call
**IModelDoc2** `InsertAxis2(True)`:

```python
def insert_axis(self, handle: str, name: str, planes: Tuple[int, int]) -> str:
    """Planes 2 and 3 selected, ``InsertAxis2(True)``, the new RefAxis found and named (probe axis)."""
    doc = self._doc()
    before = self._top_names()
    self.select_plane_by_order(planes[0])
    self.select_plane_by_order(planes[1], append=True)
    made = call(doc, "InsertAxis2", True)
    call(doc, "ClearSelection2", True)
    if not made:
        raise SolidWorksError(f"InsertAxis2 would not make {name} from planes {planes[0]} and {planes[1]}.")
    kept = self._rename(self._made_since(before, "RefAxis", "InsertAxis2"), name)
    self._handles[handle] = kept
    return kept
```

`InsertAxis2` returns `True`, not the axis, so the new feature is found by
diffing the top-level names before and after and taking the one new `RefAxis`
(`_made_since`). `select_plane_by_order` selects the Nth `RefPlane` in tree
order ([documents/01](../documents/01-new-part-from-template.md)).

### From a sketch line

A reference axis along a sketch line, which follows the line when the line's
angle is driven. Select the line of the closed sketch by name at mark 0
(`"Line2@<sketch>"`, `"EXTSKETCHSEGMENT"`, as in [features/05](05-revolve.md))
and call the same `InsertAxis2(True)`. From probe `axis_from_sketch_line` in
[`p2_bevel.py`](../../code/python/gear_generator/probe/p2_bevel.py):

```python
px.require(_select_segment(doc, sketch, line_name, False, 0), f"{line_name}@{sketch} selects")
before = sc.feature_names(doc)
made = call(doc, "InsertAxis2", True)
call(doc, "ClearSelection2", True)
new = [n for n, t in sc.new_features(doc, before) if t == "RefAxis"]
px.require(bool(made) and len(new) == 1, "InsertAxis2 makes one axis from the line")
axis = sc.feature_by_name(doc, new[0])

def direction() -> Vec:
    params = [float(v) for v in call(call(axis, "GetSpecificFeature2"), "GetRefAxisParams")]
    d = (params[3] - params[0], params[4] - params[1], params[5] - params[2])
    n = math.sqrt(sum(v * v for v in d))
    return (d[0] / n, d[1] / n, d[2] / n)
```

`GetRefAxisParams` is on **IRefAxis**, reached by `GetSpecificFeature2`: six
doubles, two points on the axis. On SolidWorks 2026 (revision 34.0.0), run
20260915-015506, a line on the Top plane at 30° from +Z toward −X gave
`axis_direction = (-0.49999999999999994, 0.0, 0.8660254037844387)`; with the
line's angle dimension linked to a global of 50 and one rebuild,
`axis_direction_linked = (-0.766044443118979, 0.0, 0.6427876096865381)`.
The angle dimension had to be placed in model space ([GOTCHAS §43](../../GOTCHAS.md)).

## The call

```python
def circular_pattern(self, handle: str, name: str, axis: str, feature: str, count: int,
                     count_dim: str) -> Tuple[str, str]:
    """Axis at mark 1, the feature at mark 4, 360 degrees equal (probe pattern)."""
    doc = self._doc()
    axis_name = self._handles.get(axis)
    seed = (self._handles.get(feature) or {}).get("feature")
    if not isinstance(axis_name, str) or not seed:
        raise SolidWorksError(f"The pattern {name} needs the axis {axis} and the feature {feature} first.")
    self._select_feature(self._by_name(axis_name), mark=1)
    self._select_feature(self._by_name(seed), append=True, mark=4)
    made = call(call(doc, "FeatureManager"), "FeatureCircularPattern5", count, 2 * math.pi, False, "NULL", False,
                True, False, False, False, False, 1, 0.0, "NULL", False)
    call(doc, "ClearSelection2", True)
    if made is None:
        raise SolidWorksError(f"FeatureCircularPattern5 would not pattern {seed} {count} times about {axis_name}.")
    kept = self._rename(made, name)
    dim = self._name_dimension_by_value(made, float(count), count_dim, "count")
    self._handles[handle] = {"feature": kept}
    return kept, dim
```

- **Selection marks:** the axis at mark **1**, the feature to pattern at mark
  **4**, through `IFeature.Select2(append, mark)` (with the count-checked retry
  and `SelectByID2` fallback in [features/01](01-boss-extrude.md)).
- **Arguments** on **IFeatureManager**, exactly as they ran:
  `(count, 2π, False, "NULL", False, True, False, False, False, False, 1, 0.0, "NULL", False)`.
  The first is the instance count including the seed, the second the total
  angle in **radians**. The two `"NULL"` strings are the probe's "DName"
  arguments. The rest were not varied here. The fifth, passed `False`, was
  varied on patterns of twisted cuts in [features/10](10-swept-cut.md): `True`
  made the same solid and rebuilt in about half the time.
- It returns the new feature, `GetTypeName2` `"CirPattern"`, or `None`.

## Why it is not obvious

**Two marks, not one.** The axis and the seed feature are told apart by their
selection marks. Both selected at mark 0 was not tried; the 1-and-4 form was
the first tried and worked.

**`"NULL"` is a string.** Those two arguments were passed the four-letter text
`"NULL"`. The probe had a fallback with `""` in their place, ready if the first
form failed; it was never reached, so `""` is untested.

**The count is a dimension, and it can be linked.** The pattern's dimensions
came back `[('D3', 6.283185307179586), ('D1', 6.0)]`: the angle first, in
radians, then the count. Renamed `Count`, `"Count@Teeth"= "Probe N"` with 8
made eight holes. Find the count by value rather than by position or name
([features/04](04-read-a-features-dimensions.md)).

## What it does not do

- Unequal spacing, a partial angle, patterning a body or a face, and patterning
  more than one feature were not tried.
- `IFeatureManager.FeatureCircularPattern4` was listed in the probe but never
  called.
- Only cuts were patterned: extruded cuts here, twisted swept cuts in
  [features/10](10-swept-cut.md).

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426, probes `axis`, `pattern` and `pattern_count_link` in
[`code/python/gear_generator/probe/p2_solid.py`](../../code/python/gear_generator/probe/p2_solid.py).

- `axis`: `insert_axis2_returns = True`; `axis_new_features = [('Axis1', 'RefAxis')]`;
  renamed `Gear Axis` and read back; selected again.
- `pattern`: `pattern_call = 'FeatureCircularPattern5 DName "NULL"'`;
  `pattern_type_name = 'CirPattern'`; six r 3 mm holes left 10869.911 mm³
  against 12566.371 − 6 × 282.743 = 10869.911;
  `pattern_dimensions = [('D3', 6.283185307179586), ('D1', 6.0)]`,
  count at index 1, default name `D1`, renamed `Count`.
- `pattern_count_link`: `"Count@Teeth"= "Probe N"` with 8 accepted; 10304.424 mm³,
  which is 12566.371 − 8 × 282.743.
- `end_to_end`: 12 tooth spaces patterned about `Gear Axis`, count linked to
  `"No. of Teeth"`; after that global changed to 13 and one rebuild,
  `Count@Teeth` read 13.

## See also

- [features/02 — Cut extrude](02-cut-extrude.md) — the seed feature
- [features/04 — Read a feature's dimensions](04-read-a-features-dimensions.md)
- [features/10 — Swept cut](10-swept-cut.md) — a pattern of twisted cuts, and the fifth argument
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md)
- [features/05 — Revolve](05-revolve.md) — selecting a sketch line by name
- [features/06 — Reference plane normal to a line](06-reference-plane-normal-to-a-line.md) — a plane, rather than an axis, from a line
- [reading/04 — Read the selection](../reading/04-read-the-selection.md) — selection marks and counts
- [GOTCHAS §33](../../GOTCHAS.md)
