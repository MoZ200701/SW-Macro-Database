---
id: features-05-revolve
title: Revolve a sketch about a centreline
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.FeatureRevolve2, ISketchManager.CreateCenterLine, IModelDocExtension.SelectByID2, ISketchSegment.GetName, IMassProperty.CenterOfMass]
keywords: [FeatureRevolve2, revolve, revolved boss, Revolution, centreline, centerline, CreateCenterLine, axis of revolution, selection mark 16, mark 16, EXTSKETCHSEGMENT, LineN@Sketch, two centrelines, Pappus, bevel blank, 20 arguments]
answers: "How do I revolve a sketch about a centreline from code, and pick which line is the axis when the sketch has more than one?"
---

# Revolve a sketch about a centreline

## What this is for

A turned part (a pulley, a bevel gear's blank, a shaft with steps) is one
closed profile revolved about an axis. From code that is `FeatureRevolve2` with
twenty arguments. Which line the profile turns about is decided by the sketch
when it has one centreline, and has to be selected when it has more than one.
This entry is the call that ran, both selections, and the volume check that
showed the ring was the one meant.

## The call

The tool's form, written from the probe. The sketch is selected at mark 0, then
the axis line at mark **16**:

```python
def revolve(self, handle: str, sketch: str, name: str, axis: str) -> str:
    """A full turn about a line of the sketch; the sketch, then the line at mark 16, selected.

    Probe revolve, SolidWorks 2026: FeatureRevolve2 (20 arguments) made a
    Revolution of 2 pi r A about the centreline, and with a second
    centreline in the sketch the one meant had to be selected at mark 16.
    """
    doc = self._doc()
    self._select_feature(self._sketch_feature(sketch))
    self._select_segment(axis, append=True, mark=findings.REVOLVE_AXIS_MARK)
    before = self._top_names()
    made = call(call(doc, "FeatureManager"), "FeatureRevolve2", True, True, False, False, False, False, 0, 0,
                2.0 * math.pi, 0.0, False, False, 0.0, 0.0, 0, 0.0, 0.0, True, True, True)
    call(doc, "ClearSelection2", True)
    if made is None:
        raise SolidWorksError(f"FeatureRevolve2 would not revolve {sketch} about {axis} into {name}.")
    kept = self._rename(self._made_since(before, findings.REVOLVE_TYPE, "FeatureRevolve2"), name)
    self._handles[handle] = {"feature": kept}
    return kept
```

`findings.REVOLVE_AXIS_MARK` is `16` and `findings.REVOLVE_TYPE` is
`"Revolution"`. `_select_feature` is `IFeature.Select2(append, mark)` judged by
the selection count, with a `SelectByID2` fallback
([features/01](01-boss-extrude.md)); `_made_since` diffs the top-level feature
names and insists on exactly one new feature of the type.

**Selecting the axis line** by its sketch name:

```python
def _select_segment(self, ref: str, append: bool, mark: int) -> None:
    """A line of a closed sketch, by ``LineN@Sketch`` (probes revolve, ref_plane_normal)."""
    record = self._handles.get(ref) or {}
    sketch = (self._handles.get(record.get("sketch") or "") or {}).get("name")
    if not record.get("segment") or not sketch:
        raise SolidWorksError(f"The line {ref!r} is not a line of a closed sketch this build made.")
    doc = self._doc()
    before = self._selected() if append else 0
    if not append:
        call(doc, "ClearSelection2", True)
    full = f"{record['segment']}@{sketch}"
    if not (call(call(doc, "Extension"), "SelectByID2", full, "EXTSKETCHSEGMENT", 0.0, 0.0, 0.0, append, mark,
                 _null_dispatch(), 0) and self._selected() > before):
        raise SolidWorksError(f"{full} could not be selected {self._selection_context()}.")
```

The segment name was read while the sketch was open with
**ISketchSegment** `GetName` (it read `'Line2'` for the second line drawn in the
probe that recorded it) and stored. After the sketch closes, the line is
selected on **IModelDocExtension** `SelectByID2` as `"Line2@<sketch name>"`
with type `"EXTSKETCHSEGMENT"`.

Interfaces and units:

- `FeatureRevolve2` is on **IFeatureManager**. The ninth argument is the angle
  in **radians** (a full turn, `2π`). The arguments as they ran, in order:
  `True, True, False, False, False, False, 0, 0, 2π, 0.0, False, False, 0.0,
  0.0, 0, 0.0, 0.0, True, True, True`. The probe's docstring for them is "one
  direction, solid, blind through the angle, merged". Only the angle was
  varied (it was always a full turn), so nothing more is claimed about the
  others. The arity, 20, was read from `sldworks.tlb`.
- It returns the new feature, `GetTypeName2` `"Revolution"`, or `None`.
- `CreateCenterLine(x1, y1, z1, x2, y2, z2)` on **ISketchManager**, in
  **metres**, sketch coordinates.

## Why it is not obvious

**One centreline: the sketch alone is enough. Two: select the axis at mark 16.**
With a single centreline in the sketch, selecting only the sketch made the
revolve, about that line, along Z and tilted alike. With a second centreline in
the sketch, as a bevel blank's sketch has, the probe went straight to selecting
the sketch and then the meant line at mark 16, and that worked first time. So
what a sketch with two centrelines does when only the sketch is selected was not
observed: the probe listed it as a last resort and never reached it.

**Mark 16, not 1 or 4.** The probe had mark 4 as the next route; it was never
reached, so mark 4 is untested, not refuted.

**Check it by weight, not by return.** A revolve about the wrong line still
returns a feature. A square of side `a` whose centre is `r` from the axis
encloses `2π r a²` (Pappus), and the centre of mass sits on the axis. Both were
checked ([reading/10](../reading/10-mass-properties-as-an-oracle.md)).

## The probe

From [`code/python/gear_generator/probe/p2_bevel.py`](../../code/python/gear_generator/probe/p2_bevel.py).
A 4 mm square 15 mm from an axis through the origin and 20 mm along it, drawn on
the Top plane through the sketch's own transform (`open_on`, `model_to_sketch`
and `_sketch_point` put model millimetres through `ModelToSketchTransform`,
[reading/05](../reading/05-sketch-to-model-transform.md)):

```python
def revolve_args(angle: float = 2.0 * math.pi) -> Tuple[Any, ...]:
    """FeatureRevolve2: one direction, solid, blind through ``angle``, merged."""
    return (True, True, False, False, False, False, 0, 0, angle, 0.0, False, False, 0.0, 0.0, 0, 0.0, 0.0,
            True, True, True)
```

```python
for route in order:
    call(doc, "ClearSelection2", True)
    px.require(sc.select_feature(doc, sc.feature_by_name(doc, sketch)), f"{label}: the sketch selects")
    if route != "sketch only":
        mark = 16 if "16" in route else 4
        if not _select_segment(doc, sketch, _segment_name(centreline), True, mark):
            px.note(f"{label}: the centreline did not select for {route}")
            continue
    got, made = px.attempt(f"{label}: FeatureRevolve2 [{route}]",
                           lambda: call(sc.feature_manager(doc), "FeatureRevolve2", *revolve_args()))
    call(doc, "ClearSelection2", True)
    if got and made is not None:
        routes.append(route)
        break
```

with the probe's own segment select:

```python
def _select_segment(doc: Any, sketch_name: str, segment_name: str, append: bool, mark: int) -> bool:
    before = sc.selected_count(doc) if append else 0
    if not append:
        call(doc, "ClearSelection2", True)
    return bool(call(sc.extension(doc), "SelectByID2", f"{segment_name}@{sketch_name}", "EXTSKETCHSEGMENT",
                     0.0, 0.0, 0.0, append, mark, sc.null(), 0)) and sc.selected_count(doc) > before
```

The centre of mass is **IMassProperty** `CenterOfMass` (metres, converted to mm)
from `IModelDocExtension.CreateMassProperty`, after `ForceRebuild3`.

## What it does not do

- Only a full turn, one direction, a solid, merged. A partial angle, two
  directions, a thin feature and a revolved cut were not tried.
- A sketch with two centrelines and only the sketch selected was not tried, nor
  mark 4, nor selecting the axis at mark 0.
- The revolve's angle is dimension `D1` in radians. Linking it to a global was
  not tried.
- The axis was always a sketch centreline; a reference axis or a model edge as
  the axis was not tried.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe `revolve`, in
development runs 20260915-015506 and 20260915-022314 (the second passing all 41
probes it ran),
with the same facts in both. Excerpts:
[`probe-log-excerpts-20260915.txt`](../../code/python/gear_generator/probe/results/probe-log-excerpts-20260915.txt).

- `Revolve Z` (axis along +Z): `FeatureRevolve2 [sketch only]: returned 'CDispatch'`;
  `revolve_z_type = 'Revolution'`; "the ring weighs 1507.9645 mm³ (Pappus
  1507.9645)", which is 2π × 15 × 4²; centre of mass
  `(-1.2056305644163638e-16, -1.3619874161106644e-31, 20.000000000000014)` mm,
  on the axis 20 mm along it; `revolve_z_dimensions = [('D1', 6.283185307179586)]`.
- `Revolve Tilted` (axis 30° from +Z toward −X): sketch only, `'Revolution'`,
  1507.9645 mm³ again, centre `(-10.000000000000009, …, 17.32050807568879)` mm,
  which is 20 mm along the tilted axis.
- `Revolve Selected` (a second centreline at 30° in the sketch):
  `FeatureRevolve2 [sketch and its centreline at mark 16]: returned 'CDispatch'`,
  `'Revolution'`, 1507.9645 mm³, centre on the meant axis.
- `revolve_routes = ['sketch only', 'sketch only', 'sketch and its centreline at mark 16']`.
- In run 20260915-022314 the tool built a straight bevel pinion and gear
  through `Session.revolve` (`ok revolve Blank` in both parts' build logs), in
  probe `bevel_end_to_end`, which passed.

## See also

- [features/01 — Boss extrude](01-boss-extrude.md) — selecting a sketch, and the other way to make a blank
- [features/06 — Reference plane normal to a line](06-reference-plane-normal-to-a-line.md) — the other feature that selects a sketch line by name
- [features/07 — Loft cut](07-loft-cut.md) — what was cut from the revolved blank
- [features/04 — Read a feature's dimensions](04-read-a-features-dimensions.md)
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md) — the Pappus check
- [reading/05 — Sketch to model transform](../reading/05-sketch-to-model-transform.md) — drawing at model points
- [GOTCHAS §38](../../GOTCHAS.md)
