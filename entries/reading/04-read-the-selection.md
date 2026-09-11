---
id: reading-04-read-the-selection
title: Find out what the user just clicked
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [ISelectionMgr.GetSelectedObjectCount2, GetSelectedObject6, GetSelectedObjectType3, GetSelectedObjectsSketch, GetPoint, GetCurve, IsLine, GetSurface, IsPlane, PlaneParams]
keywords: [selection manager, GetSelectedObject6, GetSelectedObjectType3, pick, click, vertex, edge, plane, duck typing]
answers: "How do I let a user click something in SolidWorks and read what it was?"
---

# Find out what the user just clicked

## Why this is useful

It turns "type the coordinates of the plane you want" into "click the plane you
want". For any tool that places geometry, this is the single biggest usability
win available.

## Taking a selection, rather than reading it

```python
def take_selection(self):
    """What is selected right now, taken rather than merely read."""
    doc = self._active()
    manager = call(doc, "SelectionManager")
    if manager is None:
        return None
    if int(call(manager, "GetSelectedObjectCount2", -1) or 0) < 1:
        return None
    try:
        return _interpret(manager, 1)
    finally:
        call(doc, "ClearSelection2", True)
```

**Clearing after the read is what turns a selection into an event.** You are
polling, perhaps five times a second, not being told. Without clearing you
cannot tell one click from the same thing still being selected a fifth of a
second later. Clearing also shows the user their click landed, which is free
feedback.

`GetSelectedObjectCount2(-1)` counts across all selection marks. Indices are
**1-based**.

## Ask the object what it is, not what number it is

`GetSelectedObjectType3` returns a `swSelectType_e` number, and there is a long
list of them. Branching on the number means getting one wrong is silent.

Instead, **duck-type**: try to read the thing three ways, in order, and take the
first that answers.

```python
def _try(obj, name, *args):
    """A member the object may simply not have."""
    if obj is None:
        return None
    try:
        return call(obj, name, *args)
    except Exception:
        return None      # "no such member" is the answer, not a fault


def _interpret(manager, index):
    obj = call(manager, "GetSelectedObject6", index, -1)
    if obj is None:
        return Refused("something this app cannot read")
    for reader in (_as_plane, _as_line, _as_point):
        found = reader(manager, index, obj)
        if found is not None:
            return found
    type_id = _try(manager, "GetSelectedObjectType3", index, -1)
    return Refused(SELECTION_NAMES.get(int(type_id or 0), "something this app cannot use"))
```

The type number is used **only to put a noun in a refusal message**, never to
decide how to read something. So a number missing or wrong from the table costs
one imprecise word in one message and nothing else.

```python
SELECTION_NAMES = {
    1: "an edge", 2: "a face", 3: "a corner", 4: "a plane", 5: "an axis",
    6: "a reference point", 9: "a sketch", 24: "a sketch curve", 25: "a sketch point",
}
```

## Reading a plane

Two cases, because a planar face and a reference plane are different objects.

```python
def _as_plane(manager, index, obj):
    surface = _try(obj, "GetSurface")
    if surface is not None and _try(surface, "IsPlane"):
        # PlaneParams runs normal first, then a root point on the plane.
        params = _try(surface, "PlaneParams")
        if params is not None and len(params) >= 6:
            normal = (float(params[0]), float(params[1]), float(params[2]))
            return PickedPlane(normal=normal, root=_in_mm(params[3:6]))
        return None

    # A reference plane arrives as a feature, and carries its frame as a
    # transform rather than as parameters. Its local Z is the normal, read by
    # sending the local origin and a step along Z through the same transform
    # rather than by picking the rotation apart.
    data = _try(_try(_try(obj, "GetSpecificFeature2"), "Transform"), "ArrayData")
    if data is None or len(data) < 12:
        return None
    root = transform_point(data, (0.0, 0.0, 0.0))
    tip = transform_point(data, (0.0, 0.0, 1.0))
    return PickedPlane(
        normal=(tip[0] - root[0], tip[1] - root[1], tip[2] - root[2]),
        root=_in_mm(root),
    )
```

`PlaneParams` is normal first, then a root point, verified by probing the six
faces of a box: the first three always came back a unit axis vector, the last
three a corner.

Getting the normal by transforming two points rather than by reading the
rotation out is deliberate. It cannot get the row-versus-column question wrong.
See [reading/05](05-sketch-to-model-transform.md).

## Reading a line

```python
def _as_line(manager, index, obj):
    # An edge and a sketch segment both answer GetCurve, and that is what keeps
    # arcs and splines out: only a straight one says IsLine. They part company
    # over their ends — an edge has vertices, a sketch segment has points.
    curve = _try(obj, "GetCurve")
    if curve is None or not _try(curve, "IsLine"):
        return None

    start = _try(_try(obj, "GetStartVertex"), "GetPoint")
    end = _try(_try(obj, "GetEndVertex"), "GetPoint")
    if start is not None and end is not None:
        return PickedLine(start=_in_mm(start), end=_in_mm(end))

    to_model = _sketch_transform(manager, index, obj)
    start = _sketch_coords(_try(obj, "GetStartPoint2"), to_model)
    end = _sketch_coords(_try(obj, "GetEndPoint2"), to_model)
    if start is None or end is None:
        return None
    return PickedLine(start=start, end=end)
```

The `IsLine` check is what excludes arcs and splines, which also answer
`GetCurve`.

## Reading a point

```python
def _as_point(manager, index, obj):
    where = _try(obj, "GetPoint")
    if where is not None and len(where) >= 3:
        return PickedPoint(where=_in_mm(where))
    coords = _sketch_coords(obj, _sketch_transform(manager, index, obj))
    return None if coords is None else PickedPoint(where=coords)
```

A vertex and a reference point both answer `GetPoint`. A sketch point does not,
and needs the transform. See [reading/05](05-sketch-to-model-transform.md).

## Return plain data

```python
@dataclass(frozen=True)
class PickedPoint:
    where: Vec3

@dataclass(frozen=True)
class PickedLine:
    start: Vec3
    end: Vec3

@dataclass(frozen=True)
class PickedPlane:
    normal: Vec3
    root: Vec3

@dataclass(frozen=True)
class Refused:
    """Something was selected, but not something this step can use."""
    what: str
```

Millimetres, frozen, no COM pointers. Everything downstream — deciding what the
pick means, filling in a form, exporting — is then testable on a machine with no
CAD package. `Refused` carrying a noun is what lets the UI say "that's an axis,
I need a plane" instead of doing nothing.

## Full source

[`code/python/swcom.py`](../../code/python/swcom.py), plus
[`code/python/pick.py`](../../code/python/pick.py) for turning a pick into
application meaning.

The module also ships a diagnostic: `python -m airfoil_converter.swcom
--selection` prints what is selected and every way of reading it. Writing one of
those is how the conventions above got established.

## See also

- [reading/05 — Sketch to model transform](05-sketch-to-model-transform.md)
- [connect/06 — One apartment thread](../connect/06-one-apartment-thread.md)
