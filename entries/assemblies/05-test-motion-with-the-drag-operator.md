---
id: assemblies-05-test-motion-with-the-drag-operator
title: Test whether an assembly can move, with the drag operator
status: partly-verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IAssemblyDoc.GetDragOperator, IDragOperator.AddComponent, IDragOperator.TransformType, IDragOperator.UseAbsoluteTransform, IDragOperator.BeginDrag, IDragOperator.Drag, IDragOperator.EndDrag, IMateEntity2.EntityParams, IFeature.SetSuppression2, IFeature.GetErrorCode2, IComponent2.Transform2]
keywords: [drag operator, GetDragOperator, IDragOperator, BeginDrag, EndDrag, move component, rotate component, motion test, mechanism will not move, steering locked, gear mate, mate diagnosis, which mate locks, suppress mate, EntityParams, error 51, Transform2 put, EditRebuild3]
answers: "How do I find out from code whether an assembly can move, and which mate is stopping it?"
---

# Test whether an assembly can move, with the drag operator

## What this is for

A mechanism that will not move in SolidWorks often shows no mate errors at all.
To find the mate that is stopping it, you need to try a motion, see how much of
it SolidWorks allows, and repeat with suspect mates suppressed one at a time.
Doing that by hand is slow. This entry does it from Python: a drag that behaves
like a mouse drag, measured afterwards, with every position and suppression put
back at the end.

## The call

All of it is on the assembly's `IAssemblyDoc` and the `IDragOperator` it hands
back. `swcom` and `maths` are the SW-Mates-Controller modules: `call` is the
late-binding helper (GOTCHAS §55), `create_transform` is the
`IMathUtility.CreateTransform` route from [assemblies/04](04-mates-between-non-parallel-axes.md),
and `maths.about_line(point, axis, degrees)` is a rotation about a line as a
frame. Points are in **metres**, angles in **degrees** for `about_line`.

```python
def drag(comp, line, deg, steps=10, recipe=None, watch=()):
    restore()
    op = call(doc, "GetDragOperator")
    call(op, "AddComponent", comps[comp], False)
    tt, absol, mode, how = recipe
    if tt is not None: op.TransformType = tt
    if absol is not None: op.UseAbsoluteTransform = absol
    if mode is not None: op.DragMode = mode
    call(op, "BeginDrag")
    oks = []
    for i in range(steps):
        d = deg / steps
        if absol:
            fr = maths.compose(maths.about_line(line[0], line[1], d * (i + 1)), snap[comp])
        else:
            fr = maths.about_line(line[0], line[1], d)
        xf = swcom.create_transform(app, fr)
        oks.append(getattr(op, how)(xf))
    call(op, "EndDrag")
    res = {w: round(rot(w), 2) for w in (comp,) + tuple(watch)}
    return oks, res, errors()
```

The recipe that ran is `(0, False, None, "Drag")`: `TransformType = 0`,
`UseAbsoluteTransform = False`, `DragMode` left as it came, and `Drag` given
each step as a **relative** transform, a rotation of `deg / steps` about a
fixed line in the assembly. `Drag` returned `True` on every step.

`rot(name)` is the angle between a component's frame now and its snapshot,
`acos((trace(R1·R0ᵀ) − 1) / 2)`. `restore()` puts every component back with a
`Transform2` put of its snapshot, then `EditRebuild3`:

```python
def restore():
    for n, k in comps.items(): k.Transform2 = swcom.create_transform(app, snap[n])
    call(doc, "EditRebuild3")
```

`errors()` reads `GetErrorCode2` on every unsuppressed mate, with the warning
flag passed as `VARIANT(VT_BYREF | VT_BOOL, False)`.

### Calibrate the recipe on two controls first

Before trusting any result, the probe dragged two components whose answer is
known, and used the first recipe that got both right:

| Control | Mates | Asked | Got |
|---|---|---|---|
| A wheel hinged to a fixed knuckle | concentric + coincident only | 30° about its axle | **30.0°** |
| A plate held by a coordinate-system mate | fully defined | 10° | **0.0°** |

The first recipe tried passed, so the other `TransformType`, absolute and
`DragAsUI` combinations were **never run**.

### Finding the axis to drag about

`IMateEntity2.EntityParams`, read from a mate that already references the
face, gives the axis directly in assembly coordinates, in metres:

```python
def axis_of(mate_name, idx):
    me = call(mates[mate_name], "GetSpecificFeature2").MateEntity(idx)
    p = list(me.EntityParams)
    return tuple(p[0:3]), maths.unit(p[3:6]), p
```

For a cylindrical face, items 0–2 are a point on the axis, 3–5 the direction
and 6 the radius. For a sketch point, the radius was 0. A concentric mate's
two faces gave the same axis.

### Suppressing a mate for one test

`IFeature.SetSuppression2(0, 1, None)` on a mate sub-feature suppressed it and
`SetSuppression2(1, 1, None)` brought it back, with `IsSuppressed` reading
`True` and `False`. Record every mate's `IsSuppressed` before you start, and in
a `finally` put back any that differ.

## Why it is not obvious

**Placing a component and rebuilding is not a motion test.** The obvious
shortcut is to set the component's `Transform2` to the turned frame and call
`EditRebuild3`. Positional mates are enforced: the fully-defined plate snapped
back to 0°. **Gear mates are not.** On the same assembly:

- A pinion placed 30° round turned its gear-mated ring gear 0°, and with the
  rest of the drivetrain's mates suppressed it reported **no mate errors at
  all**.
- With everything active, the same placement left 22 mates at
  `GetErrorCode2` = **51** with the warning flag `True`, and nothing moved.
- A steering knuckle placed 10° round kept all 10° with no errors, while the
  drag operator on the same knuckle only reached **8.77°**. The placement hid
  the very resistance that was being looked for.

So use placement only to restore a snapshot, never to ask whether something
can move.

**A drag that only gets part of the way is the signal.** A jammed mechanism
does not usually refuse outright. It gives up part of each step. In the case
this came from, a 10° steer reached 8.77° and a 30° wheel spin reached 15.4°,
both with no mate errors. Suppressing one mate at a time and dragging again,
the culprit was the one whose suppression gave the full 10° and 30°. That was
a tangent mate between two circular edges of a mated pinion and ring gear,
added next to their gear mate.

**Gear mates did carry motion through a drag.** With the tangent mate
suppressed, dragging the wheel 30° turned the side gear and ring gear 30° and
back-drove the pinion **123°**: 30 × 4.1, the gear mate's
`GearRatioNumerator / GearRatioDenominator` of 0.0205 / 0.005.

## What it does not do

- **Dragging the driving gear did not drive the driven one.** Dragging the
  pinion 30° about its own axis turned it 30° with no tilt, but the ring gear
  stayed at 0°, both with and without the tangent mate. Dragging the ring gear
  12° the other way did drive the pinion. Why the gear mate carries motion one
  way and not the other under this recipe is **open**. The pinion and ring
  gear in that assembly were concentric to sketch points rather than axes,
  which is the first thing to rule out. A second recipe (`DragAsUI`, or another
  `TransformType`) is the second.
- `DragMode` was never set, so what it changes is not known.
- The file is flagged modified (`GetSaveFlag` `True`) after a restore, even
  though every component was back within 0.01° and every mate's suppression
  matched the start. Do not save on the user's behalf. Tell them the contents
  are unchanged.
- Run on SolidWorks 2024 only.

## Evidence

SolidWorks 2024 SP5 (32.5.0), 2026-10-06, a front differential and steering
assembly of 15 components and 30 mates, driven from WSL through Windows Python
and pywin32 ([connect/10](../connect/10-driving-from-outside-windows.md)),
attached through the Running Object Table moniker (GOTCHAS §1: the unversioned
ProgID failed with `MK_E_UNAVAILABLE`).

- Controls as in the table above: 30.0° of 30° free, 0.0° of 10° fixed.
- Steer 10°: 8.77° as modelled. 10.0° with the tangent mate suppressed, the
  other knuckle following 9.39° through the tie rod.
- Wheel spin 30°: 15.44° as modelled. 30.0° with the tangent mate suppressed,
  pinion 123.0°.
- Suppressing the four spider gear mates, the U-joint's second concentric mate,
  or the gear mate alone did not release the steering. Only the tangent mate
  did.
- After every run, every component read back within 0.01° and 0.01 mm of its
  snapshot and no mate's suppression differed.

## See also

- [assemblies/02 — Mates from code](02-mates-from-code.md) — walking `MateGroup`, `AddMate5`
- [assemblies/04 — Mates between non-parallel axes](04-mates-between-non-parallel-axes.md) — `CreateTransform` and the `Transform2` put used to restore
- [reading/12 — Snapshot suppression before you suppress](../reading/12-snapshot-suppression-before-you-suppress.md)
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md)
- [connect/10 — Driving from outside Windows](../connect/10-driving-from-outside-windows.md)
- [GOTCHAS §1, §10, §55, §73](../../GOTCHAS.md)
