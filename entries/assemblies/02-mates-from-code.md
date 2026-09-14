---
id: assemblies-02-mates-from-code
title: Add coincident, distance and angle mates
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IAssemblyDoc.AddMate5, IComponent2.FeatureByName, IFeature.Select2, IModelDocExtension.SelectByID2, IFeature.GetFirstSubFeature, IFeature.GetNextSubFeature, IEquationMgr.Add2]
keywords: [AddMate5, mate, coincident mate, distance mate, angle mate, swMateType_e, swMateCOINCIDENT, swMateDISTANCE, swMateANGLE, swMateAlign_e, MateGroup, MateCoincident, MateDistanceDim, MatePlanarAngleDim, FeatureByName, selection mark 1, drive mate from global]
answers: "How do I add coincident, distance and angle mates between assembly components from code?"
---

# Add coincident, distance and angle mates

## What this is for

Components inserted into an assembly
([assemblies/01](01-new-assembly-and-insert-components.md)) float. Mates put
them where they belong, and a distance or angle mate carries a dimension that a
global can drive, so a centre distance becomes an equation. This entry is how
to select what to mate, the `AddMate5` call that ran, how to find the mate it
made, and how to link its dimension.

## Selecting a component's plane or axis

Select the two entities at selection mark **1**. The probe's route: ask the
component for the feature by name, select it, and fall back to
`SelectByID2` with the assembly-qualified name.

```python
def _select_in_component(px: Px, asm: Any, component: Any, feature_name: str, kind: str,
                         append: bool, mark: int = 1) -> bool:
    """A component's plane or axis: FeatureByName then Select2, else SelectByID2 by full name."""
    before = sc.selected_count(asm)
    if not append:
        call(asm, "ClearSelection2", True)
        before = 0
    feature = call(component, "FeatureByName", feature_name)
    if feature is not None and sc._retrying(asm, lambda: call(feature, "Select2", append, mark), before,
                                            "component FeatureByName.Select2"):
        return True
    asm_name = os.path.splitext(str(call(asm, "GetTitle")))[0]
    full = f"{feature_name}@{call(component, 'Name2')}@{asm_name}"
    if call(sc.extension(asm), "SelectByID2", full, kind, 0.0, 0.0, 0.0, append, mark, sc.null(), 0) and \
            sc.selected_count(asm) > before:
        sc._count(f"SelectByID2 {kind} in component")
        return True
    return False
```

`FeatureByName` is on **IComponent2**; `Select2(append, mark)` on **IFeature**;
`SelectByID2` on **IModelDocExtension**. `sc._retrying` repeats the select up to
four times and counts it only if `GetSelectedObjectCount2` went up. The plane
names passed in were read from the part's tree in order (Front, Top, Right),
not typed. **Which of the two routes made each selection was not recorded**;
every selection succeeded.

## The call

```python
# swMateType_e, swMateAlign_e, swAddMateError_e, from swconst.tlb on SolidWorks 2026.
MATE_COINCIDENT = 0
MATE_DISTANCE = 5
MATE_ANGLE = 6
ALIGN_CLOSEST = 2

def _mate(px: Px, asm: Any, label: str, kind: int, distance: float = 0.0, angle: float = 0.0) -> Optional[str]:
    before = [n for n, _ in _mate_features(asm)]
    got, result = px.attempt(
        f"AddMate5 {label}",
        lambda: call(asm, "AddMate5", kind, ALIGN_CLOSEST, False, distance, distance, distance, 1, 1,
                     angle, angle, angle, False, False, 0, sc.out_long()),
    )
    call(asm, "ClearSelection2", True)
    mate, status = (result[0], result[1]) if got and isinstance(result, tuple) else (result, None)
    px.fact(f"mate {label}", {"returned": type(mate).__name__ if got else "raised", "status": status})
    made = [n for n, _ in _mate_features(asm) if n not in before]
    if not px.check(got and mate is not None and len(made) == 1, f"{label}: one mate appears under Mates"):
        return None
    return made[0]
```

`AddMate5` is on **IAssemblyDoc**. The fifteen arguments as they ran: type,
alignment `2`, `False`, the distance three times in **metres**, `1`, `1`, the
angle three times in **radians**, `False`, `False`, `0`, and a by-reference
long. Nothing else was varied.

**Finding the mate it made** — mates live under the `MateGroup` feature as
sub-features:

```python
def _mate_features(asm: Any) -> List[Tuple[str, str]]:
    out: List[Tuple[str, str]] = []
    for feature in sc.top_features(asm):
        if sc.type_name(feature) == "MateGroup":
            child = call(feature, "GetFirstSubFeature")
            while child is not None:
                out.append((str(call(child, "Name")), sc.type_name(child)))
                child = call(child, "GetNextSubFeature")
    return out
```

The four mates of a gear pair, in the order they were added:

```python
px.require(_select_in_component(px, asm, second, front, "PLANE", False) and
           _select_in_component(px, asm, first, front, "PLANE", True), "the two front planes select")
_mate(px, asm, "coincident fronts", MATE_COINCIDENT)
px.require(_select_in_component(px, asm, second, "Gear Axis", "AXIS", False) and
           _select_in_component(px, asm, first, top, "PLANE", True), "B's axis and A's top plane select")
_mate(px, asm, "axis on top plane", MATE_COINCIDENT)
px.require(_select_in_component(px, asm, second, "Gear Axis", "AXIS", False) and
           _select_in_component(px, asm, first, right, "PLANE", True), "B's axis and A's right plane select")
distance = _mate(px, asm, "distance axis to right plane", MATE_DISTANCE, distance=CENTRE / sc.MM)
px.require(_select_in_component(px, asm, second, top, "PLANE", False) and
           _select_in_component(px, asm, first, top, "PLANE", True), "the two top planes select")
angle = _mate(px, asm, "angle between top planes", MATE_ANGLE, angle=math.radians(3.0))
```

**Driving the distance from a global** — rename the mate, find its dimension by
value ([features/04](../features/04-read-a-features-dimensions.md)), link it
([equations/02](../equations/02-link-a-dimension-to-a-global.md)):

```python
feature.Name = "Centre Distance"
px.check(str(call(feature, "Name")) == "Centre Distance", "the distance mate renames and reads back")
dims = sc.dimensions_of(feature)
values = [(str(call(d, "Name")), float(call(d, "SystemValue"))) for d in dims]
px.fact("distance_mate_dimensions", values)
if with_link:
    match = [d for d in dims if abs(float(call(d, "SystemValue")) - CENTRE / sc.MM) < 1e-9]
    px.require(len(match) == 1, "the distance mate's dimension is found by value")
    dim_name = str(call(match[0], "Name"))
    eqm = call(asm, "GetEquationMgr")
    add = adder(px, eqm)
    index = add('"Centre Distance"= 60')
    px.require(index >= 0, "an assembly global is added")
    link = add(f'"{dim_name}@Centre Distance"= "Centre Distance"')
    px.require(link >= 0, f'the link "{dim_name}@Centre Distance"= "Centre Distance" is accepted')
    sc.put_indexed(eqm, "Equation", index, '"Centre Distance"= 70')
    call(asm, "ForceRebuild3", False)
```

## The tool's form, and an angle mate linked to a global

After the probe, the same call went into the tool that builds a gear pair's
assembly:

```python
def add_mate(self, handle: str, kind: str, a: str, b: str, value: float = 0.0, name: str = "") -> Tuple[str, str]:
    """``AddMate5`` on two component references; returns ``(mate name, its dimension)``.

    Probe assembly_components_and_mates: coincident, distance and angle
    mates were made this way and the component moved to where they said.
    """
    types = {"coincident": findings.MATE_COINCIDENT, "distance": findings.MATE_DISTANCE,
             "angle": findings.MATE_ANGLE}
    if kind not in types:
        raise SolidWorksError(f"There is no verified mate type for {kind!r}.")
    before = self._mate_names()
    self._component_ref(a, append=False)
    self._component_ref(b, append=True)
    distance = value / MM_PER_METRE if kind == "distance" else 0.0
    angle = math.radians(value) if kind == "angle" else 0.0
    mate = call(self._doc(), "AddMate5", types[kind], findings.MATE_ALIGN_CLOSEST, False, distance, distance,
                distance, 1, 1, angle, angle, angle, False, False, 0, _out_long())
    call(self._doc(), "ClearSelection2", True)
    made = [n for n in self._mate_names() if n not in before]
    if mate is None or len(made) != 1:
        raise SolidWorksError(f"AddMate5 would not make a {kind} mate between {a} and {b}.")
    feature = self._mate_feature(made[0])
    kept = self._rename(feature, name) if name else made[0]
    dim = ""
    if kind in ("distance", "angle"):
        wanted = distance if kind == "distance" else angle
        matches = [d for d in self._dimensions(feature) if abs(float(call(d, "SystemValue")) - wanted) < 1e-9]
        dim = str(call(matches[0], "Name")) if len(matches) == 1 else ""
    self._handles[handle] = {"mate": kept}
    return kept, dim
```

`_component_ref` makes the two selections at mark 1 as above; `_mate_names`
lists the `MateGroup` children; `_dimensions` is the display-dimension walk in
[features/04](../features/04-read-a-features-dimensions.md). The value is in
millimetres or degrees, converted to metres or radians at the call.

A pair built through the tool's window on SolidWorks 2026 (revision 34.0.0) made
four mates this way, two of them named and linked to assembly globals:

```
ok      mate coincident gear2:plane1 ~ gear1:plane1
ok      mate coincident gear2:Gear Axis ~ gear1:plane2
ok      mate distance gear2:Gear Axis ~ gear1:plane3 as Centre Distance
ok      link "D1@Centre Distance"
ok      mate angle gear2:plane2 ~ gear1:plane2 as Mesh Angle
ok      link "D1@Mesh Angle"
ok      3 globals read back and match the maths
ok      rebuilt once
```

Read back from the assembly afterwards:

- Equations, in order: `"Centre Distance"= 60`, `"Ratio"= 3`,
  `"Mesh Angle"= 3`, `"D1@Centre Distance"= "Centre Distance"`,
  `"D1@Mesh Angle"= "Mesh Angle"`.
- **The angle mate's dimension** was `[('D1', 0.05235987755983)]`, 3° in
  radians, and its link to the global was accepted and read back. That is all
  that was observed: the angle global was not changed afterwards, so whether the
  angle follows it is not known.
- The distance mate's dimension was `[('D1', 0.06)]`. The components sat at
  (-0.0012, -0.0001, -5.0001) mm and (59.9988, -0.0001, -5.0001) mm (translations
  rounded to 0.0001 mm). With `"Centre Distance"` changed to 61 by an indexed put
  of `Equation(i)` and one `ForceRebuild3` (which returned `True`), the dimension
  read 0.061 and the second component moved to x = 60.9988 mm while the first
  stayed put: 61 mm apart, the same small offset on both.

## Why it is not obvious

**It returned the mate, not a tuple.** With a by-reference long for the error
code, pywin32 might be expected to return `(mate, error)`. Each of the four
calls returned the mate object alone, so the error out value was never seen.
Check the result for `None` and diff the `MateGroup` children: each call added
exactly one.

**Type names in the tree differ from the enum names.** The four mates read
`Coincident1` and `Coincident2` (`MateCoincident`), the distance
(`MateDistanceDim`), and `Angle1` (`MatePlanarAngleDim`).

**A mate's dimension is `D1`, and it is live.** The distance mate's only
dimension was `('D1', 0.06)`. Linked as `"D1@Centre Distance"` to an assembly
global, changing the global from 60 to 70 and rebuilding moved component B
from 60 mm to 70 mm.

## What it does not do

- Alignment values other than `2`, and every argument other than type,
  distance and angle, were not varied.
- The effect of the 3° angle mate on B's orientation was not measured; only
  that the mate was created.
- No failing mate was produced, so what `AddMate5` returns for an impossible
  mate, and what the error out holds, are unknown.
- Which selection route (`FeatureByName` plus `Select2`, or `SelectByID2`)
  succeeded is not recorded.
- An angle mate's dimension linked to a global was accepted and read back, but
  the global was never changed afterwards, so the angle following it is not
  observed.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426, probes `assembly_components_and_mates` and
`assembly_distance_link` in
[`code/python/gear_generator/probe/p4_assembly.py`](../../code/python/gear_generator/probe/p4_assembly.py).

- Each of the four `AddMate5` calls: `{'returned': 'CDispatch', 'status': None}`,
  and one new mate under `MateGroup`.
- `mates = [('Coincident1', 'MateCoincident'), ('Coincident2', 'MateCoincident'), ('Centre Distance', 'MateDistanceDim'), ('Angle1', 'MatePlanarAngleDim')]`.
- After `ForceRebuild3`, `component_b_translation_mm_mated = (60.0, 0.0, -5.0)`,
  60 mm from A's axis.
- `distance_mate_dimensions = [('D1', 0.06)]`.
- `assembly_distance_link`: the link `"D1@Centre Distance"= "Centre Distance"`
  accepted; `component_b_translation_mm_after_global_70 = (70.0, 0.0, -5.0)`.
- A later gear pair built by the tool on the same session: the angle-mate link
  accepted, and the centre distance moved 60 → 61 mm, as described above. That
  run's output is quoted in this entry; its log is not kept in this repo.

## See also

- [assemblies/01 — New assembly and insert components](01-new-assembly-and-insert-components.md)
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [features/04 — Read a feature's dimensions](../features/04-read-a-features-dimensions.md)
- [equations/01 — Global variables from code](../equations/01-global-variables-from-code.md) — the indexed put that changed the global
- [reading/04 — Read the selection](../reading/04-read-the-selection.md)
- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md) — sub-features
