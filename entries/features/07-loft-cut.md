---
id: features-07-loft-cut
title: Cut a loft between two sketched sections
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertCutBlend, IFeature.Select2]
keywords: [InsertCutBlend, lofted cut, loft cut, cut loft, blend, BlendCut, loft profiles, selection mark 1, section order, frustum, tapered cut, bevel tooth space, 12 arguments]
answers: "How do I make a lofted cut between two closed sketches from code?"
---

# Cut a loft between two sketched sections

## What this is for

A cut whose section changes along its length: a bevel gear's tooth space, which
shrinks from the heel toward the cone's apex; a tapered slot. From code that is
two closed sketches on two planes, both selected at the same mark in order, and
`InsertCutBlend` with twelve arguments.

## The call

The tool's form, written from the probe:

```python
def loft_cut(self, handle: str, name: str, profiles: Sequence[str]) -> str:
    """A cut lofted through closed sketches, each at mark 1 in order (probe loft_cut_sections)."""
    doc = self._doc()
    for position, sketch in enumerate(profiles):
        self._select_feature(self._sketch_feature(sketch), append=position > 0, mark=findings.LOFT_PROFILE_MARK)
    before = self._top_names()
    made = call(call(doc, "FeatureManager"), "InsertCutBlend", False, False, False, 1.0, 0, 0, False, 0.0, 0.0,
                0, True, True)
    call(doc, "ClearSelection2", True)
    if made is None:
        raise SolidWorksError(f"InsertCutBlend would not loft a cut through {', '.join(profiles)} as {name}.")
    kept = self._rename(self._made_since(before, findings.LOFT_CUT_TYPE, "InsertCutBlend"), name)
    self._handles[handle] = {"feature": kept}
    return kept
```

`findings.LOFT_PROFILE_MARK = 1`, `findings.LOFT_CUT_TYPE = "BlendCut"`.
`_select_feature` is **IFeature** `Select2(append, mark)` judged by the
selection count, with a `SelectByID2` fallback
([features/01](01-boss-extrude.md)).

Interfaces and arguments:

- `InsertCutBlend` is on **IFeatureManager**. The twelve arguments as they ran:
  `False, False, False, 1.0, 0, 0, False, 0.0, 0.0, 0, True, True`. The probe's
  own gloss is "open, no tangency, rational, tolerance 1, no matching, not
  thin, feature scope auto". None was varied, so nothing more is claimed about
  any one of them. The arity, 12, was read from `sldworks.tlb`.
- Both sketches at mark **1**, the first with `append` false and the second
  appended.
- It returns the feature, `GetTypeName2` `"BlendCut"`, or `None`.

## The probe

From [`code/python/gear_generator/probe/p2_bevel.py`](../../code/python/gear_generator/probe/p2_bevel.py),
probe `loft_cut_sections`. A blank r 30 × 40 mm, two planes offset from the
Front plane to z = 35 mm (heel) and z = 5 mm (toe), and on each a rectangle
scaled about an apex at z = −15 mm: 6 × 4 mm centred at x = 12 mm on the heel,
the same shape at scale `k = 0.4` on the toe.

```python
def cut_blend_args() -> Tuple[Any, ...]:
    """InsertCutBlend: open, no tangency, rational, tolerance 1, no matching, not thin, feature scope auto."""
    return (False, False, False, 1.0, 0, 0, False, 0.0, 0.0, 0, True, True)
```

```python
for label, order in (("heel then toe, mark 1", (heel, toe)), ("toe then heel, mark 1", (toe, heel))):
    call(doc, "ClearSelection2", True)
    px.require(sc.select_feature(doc, sc.feature_by_name(doc, order[0]), mark=1) and
               sc.select_feature(doc, sc.feature_by_name(doc, order[1]), append=True, mark=1),
               f"{label}: both sections select")
    got, made = px.attempt(f"InsertCutBlend [{label}]",
                           lambda: call(sc.feature_manager(doc), "InsertCutBlend", *cut_blend_args()))
    call(doc, "ClearSelection2", True)
    if got and made is not None:
        px.fact("loft_cut_route", label)
        break
px.require(made is not None, "a lofted cut is made")
px.fact("loft_cut_type", sc.type_name(made))
removed = blank - _volume(doc)
k = (TOE_Z - APEX_Z) / (HEEL_Z - APEX_Z)
expected = (HEEL_Z - TOE_Z) * RECT_W * RECT_H * (1.0 + k + k * k) / 3.0
```

**The oracle.** Two similar sections scaled about a common apex bound a
frustum, `L A (1 + k + k²) / 3`, with `L` the distance between the planes, `A`
the larger section's area and `k` the linear scale. Here 30 × 24 × 1.56 / 3 =
374.4 mm³. A loft that twisted, or paired the wrong corners, would remove
something else ([reading/10](../reading/10-mass-properties-as-an-oracle.md)).

## Why it is not obvious

**The sections go at mark 1, in order.** Heel then toe at mark 1 was the first
form tried, and it cut exactly the frustum. The reverse order was listed as a
fallback and never reached, so whether order matters here is not known.

**Its dimensions are the planes'.** `loft_cut_dimensions` came back as two
dimensions both named `D1`, 0.005 and 0.035: the two planes' offsets (5 and
35 mm) in metres. Which feature each belongs to was not recorded. Two `D1`s in
one list is another reason to find a dimension by value
([features/04](04-read-a-features-dimensions.md)).

## What it does not do

- Guide curves, tangency at the ends, more than two sections, a boss loft and a
  thin loft were not tried here. A boss loft through curves with guide curves
  is [features/12](12-guided-loft.md).
- Sections on non-parallel planes were not probed on a scratch part. The tool's
  bevel parts do loft between planes built square to a cone's generatrix
  ([features/06](06-reference-plane-normal-to-a-line.md)), but there only "the
  part builds, is one body and weighs what a fresh build weighs" was checked,
  not a volume formula.
- Which corners the loft pairs when the sketches were drawn in different orders
  was not examined; both rectangles here were drawn corner for corner in the
  same order.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe
`loft_cut_sections` in development runs 20260915-015506 and 20260915-022314 (the
second passing all 41 probes it ran), same facts in both. Excerpts:
[`probe-log-excerpts-20260915.txt`](../../code/python/gear_generator/probe/results/probe-log-excerpts-20260915.txt)
(20260915-022314's).

- "heel then toe, mark 1: both sections select";
  `InsertCutBlend [heel then toe, mark 1]: returned 'CDispatch'`.
- `loft_cut_type = 'BlendCut'`.
- `loft_cut_removed_mm3 = {'removed': 374.3999999999942, 'frustum': 374.40000000000003}`.
- "one solid body".
- `loft_cut_dimensions = [('D1', 0.005), ('D1', 0.035)]`.
- In the same run the tool cut each bevel part's tooth space through
  `Session.loft_cut` (`ok loft Tooth Space`), and `bevel_end_to_end` passed: the
  pinion one solid body, and after its teeth were changed 20 → 21 it weighed what
  a fresh 21-tooth build weighed (`bevel_volumes_after_mm3 = {'updated': 43638.71982950241, 'fresh': 43638.719829502304}`).

## See also

- [features/02 — Cut extrude](02-cut-extrude.md) — the straight cut
- [features/06 — Reference plane normal to a line](06-reference-plane-normal-to-a-line.md) — planes to put the sections on
- [features/05 — Revolve](05-revolve.md) — the blank it cut in the tool
- [features/04 — Read a feature's dimensions](04-read-a-features-dimensions.md) — two `D1`s
- [curves/09 — Curves as loft profiles](../curves/09-curves-as-loft-profiles.md) — lofting through imported curves instead of sketches
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md)
- [features/09 — Twisted sweep](09-twisted-sweep.md) — a section that turns rather than shrinks
- [features/12 — Insert a guided loft](12-guided-loft.md) — a boss loft through curves, with guides at mark 2
