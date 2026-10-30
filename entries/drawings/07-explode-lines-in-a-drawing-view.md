---
id: drawings-07-explode-lines-in-a-drawing-view
title: Show an assembly's explode lines in a drawing view, and what cannot be removed from them
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IModelDocExtension.SelectByID2, IModelDoc2.UnblankSketch, IView.RootDrawingComponent, ISelectionMgr.GetSelectedObjectType3, ISketchManager.InsertExplodeLineSketch, ISketch.GetSketchSegments, ISldWorks.RunCommand]
keywords: [explode lines, explode line sketch, smart explode lines, 3DExplode1, ExplodeLineProfileFeature, exploded view drawing, dashed lines exploded, trail lines, explode lines not showing in drawing, UnblankSketch, show sketch in drawing view, delete explode line segment, explode lines come back, dissolve smart explode lines, RunCommand]
answers: "My assembly has explode lines but the drawing's exploded view doesn't show them — how do I show them from code, and can I delete some of the segments?"
---

# Show an assembly's explode lines in a drawing view

## What this is for

An exploded isometric on an assembly drawing usually carries explode lines —
thin lines showing where each part travels back to. SolidWorks' Smart Explode
Lines make them in the assembly as a 3D sketch feature named `3DExplode1`, type
`ExplodeLineProfileFeature`. A drawing view that existed before the lines were
made does not show them, and the generated segments cannot be pruned.

Attach as in [drawings/01](01-new-drawing-and-place-views.md#attaching). The
exploded state itself is
[assemblies/08](../assemblies/08-exploded-view-from-code.md).

## Showing them in an existing view

The explode-line sketch is hidden in the view. Select it **through the view** by
`<sketch>@<root component>@<view>` as a `SKETCH`, then `UnblankSketch`:

```python
for name in ("3DExplode1@Assembly-4@Drawing View4",):
    for typ in ("SKETCH", "EXPLODELINES", "EXPLLINES", "EXPLSKETCH", "3DSKETCH"):
        call(drw, "ClearSelection2", True)
        if ext.SelectByID2(name, typ, 0, 0, 0, False, 0, nul, 0):
            print(f"selected {name!r} as {typ}: object type {sm.GetSelectedObjectType3(1, -1)}"); hit = (name, typ); break
    if hit: break
print("UnblankSketch ->", call(drw, "UnblankSketch"))
call(drw, "ClearSelection2", True); drw.ForceRebuild3(False)
```

- The root component's name, with its `-N` instance suffix, is the view's
  `RootDrawingComponent.Name`.
- `SKETCH` was the first type tried and it selected; the others in the loop
  were never reached. The selected object's type read 9.
- `UnblankSketch` returns nothing, so reach it through `call` and check the
  picture: it is a zero-argument member that returns nothing, which is not run
  by attribute access ([GOTCHAS §55](../../GOTCHAS.md)).

([GOTCHAS §85](../../GOTCHAS.md))

## Segments you do not want

Smart explode lines draw from every part that moved, so a group moved together
gets one line per part, and some run along each other. **Deleting segments does
not stick**:

```python
call(asm, "ClearSelection2", True)
print("open:", call(sm, "InsertExplodeLineSketch"))
sk = call(sm, "ActiveSketch"); print("active sketch segments:", len(segs(sk)) if sk is not None else None)
n = 0
for s, a, b in segs(sk):
    if any(match(a, x) and match(b, y) for x, y in DROP): n += bool(s.Select4(True, nul))
print("selected:", n); call(asm, "EditDelete"); call(asm, "ClearSelection2", True)
print("segments right after delete:", len(segs(call(sm, "ActiveSketch"))))
print("close:", call(sm, "InsertExplodeLineSketch"), "| still open:", call(sm, "ActiveSketch") is not None)
asm.ForceRebuild3(False)
```

Five segments selected, the count was unchanged right after `EditDelete`, and
after the rebuild all of them were back. Editing the sketch by selecting the
feature and deleting segments gave the same result, and
`RunCommand(3307, "")` — tried as a way to dissolve the smart lines into an
ordinary sketch; the ID's meaning was not established — returned `False` and
changed nothing.

What was done in the end: accept the generated lines. Untested alternatives:
explode so that fewer parts move per step, or draw explode lines by hand in the
assembly. A hand-made second sketch beside the smart one was started and
reverted before it was checked, so it is not recorded here.

([GOTCHAS §84](../../GOTCHAS.md))

## What it does not do

- **Routing explode lines from code** (jogs, which faces they start on) was not
  done.
- **Open:** a command ID that dissolves smart explode lines. Settle it by
  recording the UI's "Dissolve Smart Explode Lines" in the journal
  ([reading/14](../reading/14-the-journal-records-api-calls.md)) and replaying.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-30, an assembly with a
seven-step exploded view and smart explode lines. `SelectByID2("3DExplode1@<root>-4@Drawing View4",
"SKETCH", …)` returned `True`, selected object type 9; `UnblankSketch` returned
`None`, and the exported sheet then showed the explode lines in that view. In the
assembly the feature read type `ExplodeLineProfileFeature` with 11 segments;
deleting 5 by selection left 11 after rebuild, and through
`InsertExplodeLineSketch` 17 before, 17 straight after `EditDelete`, 17 after
rebuild with all 5 targets still present. `RunCommand(3307, "")` returned `False`
and the feature still had 11 segments. Names are placeholders here.

## See also

- [assemblies/08 — Exploded view from code](../assemblies/08-exploded-view-from-code.md)
- [drawings/01 — New drawing and place views](01-new-drawing-and-place-views.md) — `ShowExploded` on the view
- [drawings/02 — BOM table and balloons](02-bom-table-and-balloons.md)
- [reading/14 — The journal records API calls](../reading/14-the-journal-records-api-calls.md)
- [GOTCHAS §55, §84, §85](../../GOTCHAS.md)
