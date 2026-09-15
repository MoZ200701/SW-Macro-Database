---
id: assemblies-01-new-assembly-and-insert-components
title: Make an assembly and insert saved parts as components
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [ISldWorks.NewDocument, IAssemblyDoc.AddComponent5, IComponent2.Name2, IComponent2.Transform2, IComponent2.IsFixed, IComponent2.Select4, IAssemblyDoc.FixComponent]
keywords: [assembly, AddComponent5, insert component, component name -1, Name2, Transform2, ArrayData, IsFixed, FixComponent, fixed on insert, first component fixed, asmdot, assembly template, component position]
answers: "How do I make a new assembly and insert saved parts into it as components from code?"
---

# Make an assembly and insert saved parts as components

## What this is for

Putting parts together: a gear pair, a fixture, anything with more than one
body that moves. From code that is a new assembly document, a saved file for
each part, and one `AddComponent5` per instance. This entry covers those, how
to read where a component landed, and what state the first one is in.

## The call

The assembly probe, which ran on SolidWorks 2026. `sc.*` helpers are in
[`scaffold.py`](../../code/python/gear_generator/probe/scaffold.py): `new_document`
is `ISldWorks.NewDocument(template, 0, 0, 0)`, `null()` a typed null dispatch
and `out_long()` a by-reference integer
([connect/02](../connect/02-attach-from-python.md)).

**Save each part first.** `AddComponent5` takes a path.

```python
def _part_with_axis(px: Px, folder: str, stem: str) -> Tuple[Any, str]:
    """A saved r 20 x 10 mm cylinder with a Gear Axis, left open."""
    path = os.path.join(folder, f"{stem}.SLDPRT")
    if os.path.exists(path):
        raise Require(f"{path} already exists.")
    doc = sc.new_document(px)
    _blank(px, doc)
    if not (sc.select_plane(doc, 2) and sc.select_plane(doc, 3, append=True)):
        raise Require("Planes 2 and 3 could not be selected for the axis.")
    before = sc.feature_names(doc)
    call(doc, "InsertAxis2", True)
    call(doc, "ClearSelection2", True)
    sc.feature_by_name(doc, sc.new_features(doc, before)[0][0]).Name = "Gear Axis"
    result = call(sc.extension(doc), "SaveAs3", path, 0, SAVE_AS_SILENT, sc.null(), sc.null(), sc.out_long(),
                  sc.out_long())
    px.shared.setdefault("created", []).append(str(call(doc, "GetTitle")))
    if not os.path.isfile(path):
        raise Require(f"{stem} did not save: {result!r}")
    return doc, path
```

**Then the assembly, its units, and the components:**

```python
asm = sc.new_document(px, sc.SW_DOC_ASSEMBLY)
try:
    ext = sc.extension(asm)
    linear = int(call(ext, "GetUserPreferenceInteger", sc.SW_UNITS_LINEAR, 0))
    if linear != sc.SW_MM:
        call(ext, "SetUserPreferenceInteger", sc.SW_UNIT_SYSTEM, 0, sc.SW_UNIT_SYSTEM_MMGS)
    px.fact("assembly_units_before_setting", linear)
    px.check(int(call(asm, "GetType")) == sc.SW_DOC_ASSEMBLY, "NewDocument from the assembly template is an assembly")

    got, first = px.attempt("AddComponent5(part A at the origin)",
                            lambda: call(asm, "AddComponent5", path_a, 0, "", False, "", 0.0, 0.0, 0.0))
    got2, second = px.attempt("AddComponent5(part B at 60 mm)",
                              lambda: call(asm, "AddComponent5", path_b, 0, "", False, "", CENTRE / sc.MM, 0.0, 0.0))
    px.require(got and got2 and first is not None and second is not None, "both components are inserted")
    names = (str(call(first, "Name2")), str(call(second, "Name2")))
    px.fact("component_names", names)
    px.fact("component_b_translation_mm_as_inserted", _translation_mm(second))

    got, fixed = px.attempt("IsFixed of the first component", lambda: call(first, "IsFixed"))
    px.fact("first_component_fixed_on_insert", fixed if got else "raised")
    call(asm, "ClearSelection2", True)
    px.require(bool(call(first, "Select4", False, sc.null(), False)), "the first component selects")
    # FixComponent takes no arguments, so call() would only fetch it: an earlier
    # version of this probe got back a bound method and never ran it.
    got, _ = px.attempt("FixComponent() on the already fixed first component", lambda: asm.FixComponent())
    call(asm, "ClearSelection2", True)
    px.check(bool(call(first, "IsFixed")), "the first component is fixed")
```

**Fixing a component that floats** — at the end of the same probe, after the
mates ([assemblies/02](02-mates-from-code.md)), on the second component:

```python
    if not with_link:
        # Last, because a fixed second component would fight its mates.
        px.fact("second_component_fixed_before_fix_component", bool(call(second, "IsFixed")))
        call(asm, "ClearSelection2", True)
        px.require(bool(call(second, "Select4", False, sc.null(), False)), "the second component selects")
        got, _ = px.attempt("FixComponent() on the free second component", lambda: asm.FixComponent())
        call(asm, "ClearSelection2", True)
        fixed = bool(call(second, "IsFixed"))
        px.fact("second_component_fixed_after_fix_component", fixed)
        px.check(got and fixed, "FixComponent() fixes the selected component")
```

The tool's own form, which skips a component that is already fixed and reads
the result back:

```python
def fix_component(self, handle: str) -> None:
    """Fix a component, and read back that it is fixed.

    The first component inserted is already fixed (probe
    assembly_components_and_mates). ``FixComponent`` takes no arguments,
    so it is invoked explicitly: :func:`call` would only fetch it, which is
    how an earlier version never ran it at all.
    """
    component = self._handles[handle]["component"]
    if bool(call(component, "IsFixed")):
        return
    doc = self._doc()
    call(doc, "ClearSelection2", True)
    if not call(component, "Select4", False, _null_dispatch(), False):
        raise SolidWorksError(f"{call(component, 'Name2')} could not be selected to fix it.")
    doc.FixComponent()
    call(doc, "ClearSelection2", True)
    if not bool(call(component, "IsFixed")):
        raise SolidWorksError(f"{call(component, 'Name2')} is still floating after FixComponent.")
```

(The `try` closes with the parts and the assembly being closed; the mates in
between are [assemblies/02](02-mates-from-code.md).) `SW_UNITS_LINEAR` is 47,
`SW_UNIT_SYSTEM` 263, `SW_UNIT_SYSTEM_MMGS` 5, `SW_MM` 0, `SW_DOC_ASSEMBLY` 2,
`CENTRE` 60.0 mm.

**Where a component is:**

```python
def _translation_mm(component: Any) -> Tuple[float, float, float]:
    data = call(call(component, "Transform2"), "ArrayData")
    return (float(data[9]) * sc.MM, float(data[10]) * sc.MM, float(data[11]) * sc.MM)
```

Interfaces and units:

- `AddComponent5(path, 0, "", False, "", x, y, z)` on **IAssemblyDoc** (the
  assembly's `IModelDoc2`, late-bound), position in **metres**. It returns the
  component (`IComponent2`). Only these values of the middle four arguments
  were tried.
- `Name2`, `IsFixed`, `Select4(append, data, ...)` and `Transform2` on
  **IComponent2**. `Transform2.ArrayData` is sixteen doubles; items 9, 10 and 11
  are the translation in metres ([reading/05](../reading/05-sketch-to-model-transform.md)
  for the layout).
- `FixComponent()` on **IAssemblyDoc** fixes the selected component and returns
  `None`. Judge it by `IComponent2.IsFixed` afterwards, and call it with
  parentheses; see below.

## Why it is not obvious

**The assembly template was inches too.** The default assembly template was the
2025 folder's `Assembly.asmdot` and the new assembly read `swUnitsLinear` 3.
Set MMGS on it as for a part ([documents/01](../documents/01-new-part-from-template.md));
an assembly global that drives a mate distance is in document units.

**Components are named `<file stem>-1`.** Inserting `Probe Add A.SLDPRT` gave
`Probe Add A-1`. Read `Name2` rather than building the name yourself.

**The first component is already fixed.** `IsFixed` read `True` on the first
component straight after `AddComponent5`, before anything was done to it.

**`FixComponent` works, when it is actually called.** Select the component
with `Select4(False, null, False)` and call `asm.FixComponent()`: on a free
second component `IsFixed` read `False` before and `True` after, and the call
returned `None`. It has to be called with parentheses. The first probe run
reached it through the late-bound helper, `call(asm, "FixComponent")`, which
with no arguments only fetches the attribute: it came back as a Python `method`
object and never ran, and its check passed only because the first component
was already fixed. See [connect/02](../connect/02-attach-from-python.md) and
[GOTCHAS §5, §31](../../GOTCHAS.md).

**Where it lands is not quite where you asked.** Part B was inserted at
(60, 0, 0) mm and its transform read (60.0, 0.0, -5.0) mm. The part is a
cylinder 10 mm deep from its front plane; that the 5 mm is half its depth is a
likely explanation, not an established one. Mate it into place rather than
relying on the insertion point.

It was seen again in development run 20260915-015506: a component left where
`AddComponent5` put it at (0, 0, 0), used as the reference for an
origin-to-origin mate, put the mated component's origin 5.0 mm from the
assembly origin. Setting that first component's frame with `Transform2` too
made the error 0 mm. The same 10 mm part both times, so the rule is still not
established; setting every frame is the fix. See
[assemblies/04](04-mates-between-non-parallel-axes.md) and [GOTCHAS §41](../../GOTCHAS.md).

## What it does not do

- The parts were saved **and still open** when inserted, because the API help
  says the file must be loaded. Inserting a part that is not open was not tried.
- Configurations, sub-assemblies, and `AddComponent5`'s other options were not
  explored.
- Only fixing was tried; unfixing a component was not.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426, probes `assembly_components_and_mates` and
`assembly_distance_link` in
[`code/python/gear_generator/probe/p4_assembly.py`](../../code/python/gear_generator/probe/p4_assembly.py).
Both passed, each on its own new pair of saved parts and assembly.

- `assembly_units_before_setting = 3`; `GetType` 2.
- `AddComponent5` returned a component both times;
  `component_names = ('Probe Add A-1', 'Probe Add B-1')` and
  `('Probe Link A-1', 'Probe Link B-1')`.
- `component_b_translation_mm_as_inserted = (60.0, 0.0, -5.0)`.
- `IsFixed of the first component: returned True`;
  `first_component_fixed_on_insert = True`.
- `Select4(False, null, False)` on the first component selected it.
- In that run, `FixComponent: returned 'method'`: fetched, not called.
- The probe run closed all three documents afterwards and left nothing open.

Rerun 20260914-183429 on the same SolidWorks 2026 (revision 34.0.0), all 33
probes passing, with the probe calling `asm.FixComponent()`
([`probe-results-20260914-183429.txt`](../../code/python/gear_generator/probe/results/probe-results-20260914-183429.txt)),
in `assembly_components_and_mates`:

- `first_component_fixed_on_insert = True`; `FixComponent() on the already
  fixed first component: returned None`.
- `second_component_fixed_before_fix_component = False`; the second component
  selected; `FixComponent() on the free second component: returned None`;
  `second_component_fixed_after_fix_component = True`; check
  "FixComponent() fixes the selected component" ok.
- Every other assembly fact above was the same in the rerun.

## See also

- [assemblies/02 — Mates from code](02-mates-from-code.md)
- [documents/01 — New part from a template](../documents/01-new-part-from-template.md)
- [documents/02 — Save as, and close](../documents/02-save-as-and-close.md)
- [features/03 — Circular pattern](../features/03-circular-pattern.md) — the `InsertAxis2` call used on each part
- [reading/05 — Sketch to model transform](../reading/05-sketch-to-model-transform.md) — `ArrayData` layout
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md)
- [assemblies/03 — Interference detection](03-interference-detection.md) — checking inserted parts for overlap
- [assemblies/04 — Mates between non-parallel axes](04-mates-between-non-parallel-axes.md) — setting a component's frame
