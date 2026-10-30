---
id: assemblies-08-exploded-view-from-code
title: Build an assembly's exploded view step by step from code, and check each step moved
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IAssemblyDoc.GetExplodedViewNames2, IAssemblyDoc.ShowExploded2, IConfiguration.AddExplodeStep2, IConfiguration.GetNumberOfExplodeSteps, IConfiguration.GetExplodeStep, IConfiguration.DeleteExplodeStep, IExplodeStep.Name, IComponent2.Select4, ISelectionMgr.CreateSelectData, IComponent2.Transform2]
keywords: [exploded view, explode, explode step, AddExplodeStep2, ShowExploded2, GetExplodedViewNames2, DeleteExplodeStep, GetExplodeStep, explode direction, explode distance, explode along axis, reverse direction, Exploded View1, Chain1, check component moved]
answers: "How do I make an exploded view of an assembly from code — which parts move, how far and which way — and confirm each step went where I meant?"
---

# Build an assembly's exploded view step by step from code

## What this is for

An exploded isometric for an assembly drawing: each part pulled out along an
axis, in steps. The step call takes a distance and an axis index; which way
"positive" goes is the only thing that needs checking, so the recipe measures
every step's movement against what was intended and stops at the first surprise.

Attach as in [assemblies/06](06-read-every-mate.md#attaching).

## The recipe

```python
STEPS = [  # (components, distance mm, axis index 0=X 1=Y 2=Z, expected unit direction in assembly coords)
    (["Part1"], 25, 2, (0, 0, 1)),
    (["Part2", "Part3", "Part4", "Part5"], 50, 2, (0, 0, 1)),
    (["Part5", "Part4"], 72, 0, (1, 0, 0)),
    (["Part3"], 25, 0, (-1, 0, 0)),
    (["Part6"], 35, 2, (0, 0, -1)),
    (["Part7"], 70, 2, (0, 0, -1)),
]
cfg = call(call(asm, "ConfigurationManager"), "ActiveConfiguration"); cname = call(cfg, "Name")
names = list(asm.GetExplodedViewNames2(cname) or [])
if not names:
    print("CreateExplodedView:", call(asm, "CreateExplodedView")); names = list(asm.GetExplodedViewNames2(cname) or [])
ev = names[0]; print("explode view:", ev, "in config", cname)
print("ShowExploded2:", asm.ShowExploded2(True, ev))
for i in reversed(range(call(cfg, "GetNumberOfExplodeSteps"))):
    s = cfg.GetExplodeStep(i); print("  deleting existing step", call(s, "Name")); cfg.DeleteExplodeStep(call(s, "Name"))
comps = {call(c, "Name2").rsplit("-", 1)[0]: c for c in call(call(cfg, "GetRootComponent3", True), "GetChildren")}
selmgr = call(asm, "SelectionManager")
def pos(c): return [x * 1000 for x in list(call(call(c, "Transform2"), "ArrayData"))[9:12]]
for k, (members, dist, axis, expect) in enumerate(STEPS[:only]):
    before = {m: pos(comps[m]) for m in members}
    call(asm, "ClearSelection2", True)
    for m in members:
        sd = call(selmgr, "CreateSelectData"); sd.Mark = 1
        comps[m].Select4(True, sd, False)
    err = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
    step = cfg.AddExplodeStep2(dist / 1000, axis, expect[axis] < 0, 0.0, -1, False, False, False, err)
    call(asm, "EditRebuild3")
    m0 = members[0]; dv = [a - b for a, b in zip(pos(comps[m0]), before[m0])]
    ok = all(abs(dv[i] - dist * expect[i]) < 0.5 for i in range(3))
    print(f"step {k+1} {members}: err={err.value} moved {m0} by ({dv[0]:.1f}, {dv[1]:.1f}, {dv[2]:.1f}) mm -> {'OK' if ok else 'UNEXPECTED'}")
    if not ok:
        print("stopping: direction not as expected"); break
```

- **`IConfiguration.AddExplodeStep2`** moves the components selected at mark 1.
  As run: distance in **metres**, an axis index (0, 1, 2 moved the parts along
  the assembly's X, Y, Z), and a reverse flag (`True` moved them the negative
  way). The remaining arguments were left as shown and not varied.
- The step's movement is read from each component's `Transform2`, items 9–11,
  the translation in metres, before and after. That is the check: compare it to
  the direction you meant, and stop at the first mismatch.
- Several components selected together move as one step.
- Explode steps belong to a **configuration**. `ShowExploded2(True, name)` shows
  the exploded state; a drawing view shows it through `IView.ShowExploded(True)`
  ([drawings/01](../drawings/01-new-drawing-and-place-views.md#hidden-lines-exploded-state-and-a-picture-of-the-sheet)).
- Rebuilding from scratch: delete the existing steps by name, last first, before
  adding.

## Changing a step's direction by hand

A step made in the UI and later edited needs no code: right-click the step under
the exploded view in the ConfigurationManager, Edit Feature, and drag the
triad arrow or flip the direction. A user-added step read back through
`GetExplodeStep` like the code-made ones (name `Chain1` in one case, `Explode
StepN` otherwise).

## What it does not do

- **`CreateExplodedView`** is in the recipe for an assembly with no exploded view,
  but every run here found one already there; it was never reached.
- **Radial explodes, rotation in a step, and moving along a part's own axis**
  were not tried.
- **Explode lines** are a separate feature: [drawings/07](../drawings/07-explode-lines-in-a-drawing-view.md).

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-30, a nine-part
assembly. `GetExplodedViewNames2` returned `Exploded View1`; `ShowExploded2`
returned `True`. Existing steps were deleted by name (`Chain1` on one run,
`Explode Step5`…`Step1` on another). The six-step list quoted above, on its own
run, returned `err=0` on every step and moved the first selected component by
exactly the intended vector: (0, 0, 25), (0, 0, 50), (72, 0, 0), (−25, 0, 0),
(0, 0, −35) and (0, 0, −70) mm. Earlier runs with other lists also moved a part
along +Y, (0, 45, 0), each matching its intended vector. A drawing view of the assembly then read `IsExploded` `True` after
`ShowExploded(True)`. Component names are placeholders here.

## See also

- [drawings/07 — Explode lines in a drawing view](../drawings/07-explode-lines-in-a-drawing-view.md)
- [drawings/01 — New drawing and place views](../drawings/01-new-drawing-and-place-views.md) — the exploded isometric on a sheet
- [drawings/02 — BOM table and balloons](../drawings/02-bom-table-and-balloons.md)
- [assemblies/01 — New assembly and insert components](01-new-assembly-and-insert-components.md) — `Transform2`
- [GOTCHAS §10, §84](../../GOTCHAS.md)
