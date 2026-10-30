---
id: assemblies-07-configurations-that-hold-different-positions
title: Make assembly configurations that each hold a mechanism in a different position
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IConfigurationManager.AddConfiguration2, IModelDoc2.ShowConfiguration2, IModelDoc2.GetConfigurationNames, IAssemblyDoc.AddMate5, IFeature.SetSuppression2, IFeature.IsSuppressed2, IFeature.IsSuppressed, IFeature.GetErrorCode2, ISketchManager.InsertSketch, ISketchManager.CreatePoint, ISketchManager.AddToDB, ISketch.GetSketchPoints2, IModelDoc2.BlankSketch, IMate2.MateEntity, IMateEntity2.Reference, IMateEntity2.EntityParams]
keywords: [configuration, positions, motion positions, end of stroke, mechanism position, configuration specific mate, suppress mate in configuration, SetSuppression2, swInConfigurationOpts_e, IsSuppressed2, AddConfiguration2, positions not per configuration, configuration shows wrong position, over defined, AddMate5 error 5, mate error 51, limit mate, extreme distance, point coincident mate, assembly sketch point, exact point, rounding]
answers: "How do I make configurations of an assembly that each put a mechanism in a different position, so switching configuration moves it and each position is exact?"
---

# Make assembly configurations that each hold a mechanism in a different position

## What this is for

A mechanism shown at several positions — the two ends of a stroke, a midpoint —
each as a configuration, for a drawing that overlays them
([drawings/06](../drawings/06-alternate-position-views.md)) or for checking
clearances at each. It looks like "add a configuration, drag the part, save". It
is not, because **component positions are not stored per configuration**.

Attach as in [assemblies/06](06-read-every-mate.md#attaching).

## Why dragging is not enough

A configuration stores which mates and components are suppressed, not where the
free components are. Whatever the mates leave free sits where it was last
solved, in *every* configuration. Switching to a configuration with no extra
mates leaves the mechanism wherever the previous one put it. So each
configuration needs **its own mates that fix the position completely**, active
only in that configuration.

## Mates that hold one exact position

The mates must have **one** solution with the assembly's other mates, and must
not fight them:

| Tried | Result |
|---|---|
| Distance from the moving pin to a plane, plus distance from pin to the slot wall | Added with error 5; four existing concentric/coincident mates went to error 51; both configurations showed the same position |
| A second limit distance mate per configuration | Added with error 5 in one configuration; that mate in error 51 |
| A point-to-point coincident on a point a hundredth of a millimetre off the pin's circle | Added with error 5, mates in error 51 |
| **Pin axis coincident with an exact point in a hidden assembly sketch**, plus one distance mate where the mechanism has play | Added cleanly (1); every configuration solved with no mate errors |

So: put the target positions as **points in an assembly sketch**, computed
exactly (not rounded for display), mate the moving axis to the point, and add a
distance mate only for the degree of freedom the point leaves (a slider with
play). An existing **limit** mate that already bounds the travel stays; the
equality mates sit inside its range.

```python
call(asm, "ClearSelection2", True); asm.FeatureByName("Front Plane").Select2(False, 0)
sm.InsertSketch(True); sm.AddToDB = True
pts = {cfg: sm.CreatePoint(x / 1000, y / 1000, 0) for cfg, (x, y) in POINTS.items()}
sm.AddToDB = False; sm.InsertSketch(True)
```

`CreatePoint` takes **metres** in the sketch's plane. Find the point again by its
coordinates, select the moving axis and the point at mark 1, and add a coincident
mate (`AddMate5` type 0):

```python
sk = call(sk_feat, "GetSpecificFeature2")
spts = list(call(sk, "GetSketchPoints2") or [])
def find_pt(x, y):
    return next(p for p in spts if abs(call(p, "X") * 1000 - x) < 0.01 and abs(call(p, "Y") * 1000 - y) < 0.01)
for cfg, (x, y) in POINTS.items():
    asm.ShowConfiguration2(cfg); asm.ForceRebuild3(False)
    sd = call(selmgr, "CreateSelectData"); sd.Mark = 1; sd2 = call(selmgr, "CreateSelectData"); sd2.Mark = 1
    call(asm, "ClearSelection2", True); call(lim().MateEntity(0), "Reference").Select4(False, sd); find_pt(x, y).Select4(True, sd2)
    err = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
    asm.AddMate5(0, 2, False, 0, 0, 0, 1, 1, 0, 0, 0, False, False, 0, err); call(asm, "ClearSelection2", True); asm.ForceRebuild3(False)
```

Here the pin's axis came from an existing mate (`MateEntity(0).Reference`); any
way of selecting the axis will do. Hide the helper sketch afterwards with
`BlankSketch` on the selected sketch feature.

## One configuration's mates, suppressed everywhere else

`AddConfiguration2(name, comment, "", 0, "", "", True)` adds and activates a
configuration. Then, for each new mate, `IFeature.SetSuppression2(0, 3, names)`
suppresses it (`0`) in the **listed** configurations (`3`):

```python
c = cm.AddConfiguration2(cfg, "End of travel", "", 0, "", "", True)
asm.FeatureByName(nm).SetSuppression2(0, 3, VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_BSTR, [n for n in names if n != cfg]))
```

Read the result back for every configuration at once with
`IsSuppressed2(3, names)`, which returns one flag per name:

```python
st = list(s.IsSuppressed2(3, VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_BSTR, CFGS)))
```

A mate added while a configuration is active can come out suppressed in that
configuration and live in the others — the inverse of what was meant; this check
catches it.

## Checking the positions

Switch through the configurations in an order that revisits each, rebuild, and
read where the moving parts are and whether any mate is in error:

```python
for cfg in ("Position A", "Position B", "Position A", "Default", "Position B", "Position A", "Position B"):
    asm.ShowConfiguration2(cfg); asm.ForceRebuild3(False)
    px, fx, errs = state(); fol[cfg] = fx; print(f"  {cfg:15} pin x={px:7.2f} slider x={fx:7.2f} errors {errs or 'none'}")
```

where `state()` reads the pin's `EntityParams` (assembly metres,
[assemblies/06](06-read-every-mate.md)), the moving component's box, and every
unsuppressed mate's `GetErrorCode2`. Note what `Default` shows: with no mates of
its own it keeps the last configuration's position.

## What it does not do

- **Positions from a motion study** were not tried.
- **Dragging into place first, then adding mates**, was not used: a
  `Transform2` put did not stay ([GOTCHAS §73](../../GOTCHAS.md)).
- **Open:** whether the mate suppressed in its own configuration came from
  `AddMate5` while that configuration was active or from a later hand edit. It
  was seen once, made by hand in the UI.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-30, a slider-crank-like
mechanism with a limit distance mate on the slider. Plane-distance mates per
configuration: add errors 5 and 1/5, both configurations then read the same pin
position with four mates at error 51, and the measured travel came out negative
and short. Point mates: `pin-to-point mate err 1` for both, and a seven-switch
test read the two pin and slider positions alternately, each repeat identical to
0.01 mm, with no errors, the travel within 0.01 mm of the design value. A third
point rounded to 0.01 mm added with result 5 and mates at error 51; on the exact
point it solved. Final check over four configurations: every configuration's
mates `IsSuppressed2` matched (own `-`, others `X`), the pin at 0°, 180° and an
intermediate angle, each exactly on the crank radius, the travel equal to the
design value, no mate errors.
Names are placeholders here.

## See also

- [assemblies/02 — Mates from code](02-mates-from-code.md) — `AddMate5`
- [assemblies/05 — Test motion with the drag operator](05-test-motion-with-the-drag-operator.md) — whether it can move at all
- [assemblies/06 — Read every mate](06-read-every-mate.md) — `EntityParams`, `GetErrorCode2`, limit mates
- [drawings/06 — Alternate position views](../drawings/06-alternate-position-views.md) — drawing the positions
- [files/02 — Explode configurations](../files/02-explode-configurations.md) — configurations of a part
- [GOTCHAS §23, §73, §83](../../GOTCHAS.md)
