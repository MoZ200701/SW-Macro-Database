---
id: assemblies-03-interference-detection
title: Check an assembly for interference and read the volumes
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IAssemblyDoc.InterferenceDetectionManager, IInterferenceDetectionMgr.GetInterferenceCount, IInterferenceDetectionMgr.GetInterferences, IInterferenceDetectionMgr.Done, IInterference.Volume]
keywords: [InterferenceDetectionManager, interference detection, clash detection, collision, GetInterferenceCount, GetInterferences, IInterference, Volume, Done, TreatCoincidenceAsInterference, IgnoreHiddenBodies, UseTransform, property or method, late binding, negative control, gear mesh check]
answers: "How do I check an assembly for interfering components from code and get the interference volume?"
---

# Check an assembly for interference and read the volumes

## What this is for

Whether two components run into each other: gear teeth that should mesh, a
fixture that should clear. SolidWorks' Interference Detection answers it, and
from code it is a manager object on the assembly, a handful of options, a count
and a list of interferences each with a volume. This entry is what ran, which
members are property gets and which a method under late binding, and the
overlap-and-apart test that showed the check could tell the two apart.

## The call

The tool's form, written from the probe:

```python
def interference_count(self) -> Tuple[int, float]:
    """How many interferences the build's assembly has, and their volume in mm³.

    Probe interference, SolidWorks 2026: two cylinders overlapping by a lens
    counted one interference of exactly the lens's volume, and none apart.
    ``GetInterferenceCount``, ``GetInterferences`` and ``Volume`` are
    property gets through late binding; ``Done`` is a method, and is called
    whatever the count came to.
    """
    doc = self._doc()
    manager = call(doc, "InterferenceDetectionManager")
    if manager is None:
        raise SolidWorksError(f"{call(doc, 'GetTitle')} has no interference detection manager.")
    for name, value in findings.INTERFERENCE_OPTIONS:
        setattr(manager, name, value)
    try:
        count = int(call(manager, "GetInterferenceCount"))
        items = list(call(manager, "GetInterferences") or ())
        volume = sum(float(call(item, "Volume")) for item in items) * 1e9
    finally:
        manager.Done()
    return count, volume
```

with the options set before counting, each a property put:

```python
INTERFERENCE_OPTIONS = (
    ("TreatCoincidenceAsInterference", False),
    ("TreatSubAssembliesAsComponents", True),
    ("IncludeMultibodyPartInterferences", True),
    ("MakeInterferingPartsTransparent", False),
    ("CreateFastenersFolder", False),
    ("IgnoreHiddenBodies", True),
    ("ShowIgnoredInterferences", False),
    ("UseTransform", False),
)
```

`call(obj, name)` with no arguments is plain attribute access
([connect/02](../connect/02-attach-from-python.md)).

Interfaces and units:

- `InterferenceDetectionManager` on **IAssemblyDoc** (the assembly's document,
  late-bound): a property get returning the manager.
- The eight options, `GetInterferenceCount`, `GetInterferences` and `Done` on
  **IInterferenceDetectionMgr**. The count is an integer; the list a tuple of
  interference objects, or empty.
- `Volume` on **IInterference**, in **cubic metres**; `× 1e9` for mm³.

## Why it is not obvious

**Three are property gets, one is a method.** Through pywin32's dynamic
dispatch, `GetInterferenceCount`, `GetInterferences` and `IInterference.Volume`
came back as their values by attribute access, despite their `Get` names.
`Done` came back as a Python `method` object and had to be called:
`manager.Done()`. Reached as `call(manager, "Done")` it would be fetched and
never run, which is the `FixComponent` trap ([GOTCHAS §31](../../GOTCHAS.md)).
The probe did not assume either way: it read each member and called what came
back only if it was a method, and recorded which.

```python
def member(obj: Any, name: str) -> Tuple[Any, str]:
    """A zero-argument member, read, and called if what comes back is a method."""
    value = call(obj, name)
    if type(value).__name__ in ("method", "function"):
        return value(), "method"
    return value, "property"
```

**A check that has never failed proves nothing.** An interference check that
always says "none" looks exactly like a pair that meshes. The probe ran the same
two parts overlapping and apart, and the tool's end-to-end probe turned a
meshing gear half a pitch round to make its teeth collide. Both negative
controls came out as they should.

## The probe

From [`code/python/gear_generator/probe/p4_interference.py`](../../code/python/gear_generator/probe/p4_interference.py).
Two saved r 20 × 10 mm cylinders inserted with `AddComponent5`, their axes
30 mm apart (overlapping in a lens) and then 50 mm apart (clear):

```python
def lens_mm3(radius: float, distance: float, width: float) -> float:
    """Where two equal cylinders overlap: the lens of two circles, times the width."""
    if distance >= 2.0 * radius:
        return 0.0
    area = 2.0 * radius * radius * math.acos(distance / (2.0 * radius)) \
        - distance / 2.0 * math.sqrt(4.0 * radius * radius - distance * distance)
    return area * width
```

```python
def interferences(px: Px, asm: Any, label: str) -> Dict[str, Any]:
    """Run the interference check on the whole assembly. Returns what it found, as plain data."""
    got, manager = px.attempt(f"{label}: IAssemblyDoc.InterferenceDetectionManager",
                              lambda: call(asm, "InterferenceDetectionManager"))
    if not got or manager is None:
        raise Require("The assembly has no interference detection manager.")
    for name, value in OPTIONS:
        px.attempt(f"{label}: set {name} = {value}", lambda n=name, v=value: setattr(manager, n, v))
    got, counted = px.attempt(f"{label}: GetInterferenceCount", lambda: member(manager, "GetInterferenceCount"))
    count, count_form = (int(counted[0]), counted[1]) if got else (None, None)
    got, listed = px.attempt(f"{label}: GetInterferences", lambda: member(manager, "GetInterferences"))
    items, list_form = (list(listed[0] or ()), listed[1]) if got else ([], None)
    volumes: List[float] = []
    for item in items:
        ok, value = px.attempt(f"{label}: IInterference.Volume", lambda i=item: member(i, "Volume"))
        if ok:
            volumes.append(float(value[0]) * 1e9)
    got, done = px.attempt(f"{label}: Done", lambda: member(manager, "Done"))
    return {"count": count, "count_form": count_form, "listed": len(items), "list_form": list_form,
            "volumes_mm3": volumes, "done_form": done[1] if got else None}
```

(`OPTIONS` there is the same tuple as `INTERFERENCE_OPTIONS` above.)

## What it does not do

- The options were set once, to the values above, and never varied. What each
  one changes is not observed; in particular `TreatCoincidenceAsInterference`
  false means faces merely touching were not tested either way.
- Only whole-assembly detection was run. Checking a chosen subset of
  components, and reading which components an interference is between, were
  not tried.
- `Volume` was read only through the probe's `member` helper and the tool's
  `float(call(item, "Volume"))`; both worked. Other members of `IInterference`
  were not read.
- How long detection takes on large assemblies was not measured.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe `interference`
in development runs 20260915-020933 and 20260915-022314 (the second passing all
41 probes it ran), same
facts in both. Excerpts:
[`probe-log-excerpts-20260915.txt`](../../code/python/gear_generator/probe/results/probe-log-excerpts-20260915.txt).

- Every option put returned `None`.
- 30 mm apart: `interference_overlap = {'count': 1, 'count_form': 'property', 'listed': 1, 'list_form': 'property', 'volumes_mm3': [1813.2470159104396], 'done_form': 'method'}`;
  "its volume is the lens, 1813.247 mm³ (expected 1813.247)".
- 50 mm apart: `interference_apart = {'count': 0, 'count_form': 'property', 'listed': 0, 'list_form': 'property', 'volumes_mm3': [], 'done_form': 'method'}`.
- `interference_route = {'count': 'property', 'list': 'property', 'done': 'method'}`.
- Through the tool's `interference_count`, probe `bevel_end_to_end`: a straight
  bevel pair (module 3, 20 and 40 teeth, shafts at 90°) as mated, "ok no
  interference between the gears"; with gear 2 turned half a pitch and
  rebuilt, `bevel_half_pitch_interference = {'count': 4, 'volume_mm3': 482.40878390053473}`.
- Probe `crossed_end_to_end`: a crossed helical pair (normal module 2, 20 and 40
  teeth, 45° right-hand, shafts at 90°) as mated, no interference; half a pitch
  round, `crossed_half_pitch_interference = {'count': 3, 'volume_mm3': 102.56289208058003}`.

## See also

- [assemblies/01 — New assembly and insert components](01-new-assembly-and-insert-components.md) — the parts under test
- [assemblies/02 — Mates from code](02-mates-from-code.md)
- [assemblies/04 — Mates between non-parallel axes](04-mates-between-non-parallel-axes.md) — the pairs the tool checked
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — attribute access versus a call
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md) — checking by a volume you can compute
- [connect/11 — Probe an API member on a live session](../connect/11-probe-an-api-member-on-a-live-session.md) — the negative control
- [GOTCHAS §31, §40](../../GOTCHAS.md)
