---
id: drawings-06-alternate-position-views
title: Show an assembly's other positions as phantom lines in a drawing view
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IView.InsertAlternateView, IView.ReferencedConfiguration, IView.Type, IModelDoc2.ShowConfiguration2, IModelDoc2.ForceRebuild3, IModelDocExtension.SelectByID2, IModelDoc2.EditDelete, ISldWorks.ActivateDoc3]
keywords: [alternate position view, phantom lines, motion drawing, range of motion, two positions one view, InsertAlternateView, ReferencedConfiguration, configuration in drawing view, wrong pose in drawing, view shows wrong configuration, last solved position, delete alternate position, Alternate Position1, DRAWINGVIEW]
answers: "How do I draw an assembly in one position with its other positions overlaid as phantom lines, from configurations, and make sure each pose is the right one?"
---

# Show an assembly's other positions as phantom lines in a drawing view

## What this is for

A mechanism drawing: one position solid, its other positions in phantom
(dash-dot) lines on the same view, each from its own assembly configuration.
The positions come from configurations that pin the mechanism in place — see
[assemblies/07](../assemblies/07-configurations-that-hold-different-positions.md)
for making those. The trap is that a view can name the right configuration and
still draw a different pose.

Attach as in [drawings/01](01-new-drawing-and-place-views.md#attaching).

## The base view and its alternate positions

Make the base view, set its configuration, then `InsertAlternateView(config)`
on the **selected** base view, once per extra position:

```python
base = drw.CreateDrawViewFromModelView3(ASM, "*Front", 0.20, 0.17, 0)
base.ReferencedConfiguration = "Position A"
base.UseSheetScale = True; sheet.SetScale(2, 1, False, False); call(drw, "EditRebuild3")
print("base:", call(base, "GetName2"), "config", call(base, "ReferencedConfiguration"), "scale", call(base, "ScaleDecimal"))
alts = []
for cfg in ("Position B", "Position C"):
    call(drw, "ClearSelection2", True); ext.SelectByID2(call(base, "GetName2"), "DRAWINGVIEW", 0, 0, 0, False, 0, nul, 0)
    v = base.InsertAlternateView(cfg); call(drw, "ClearSelection2", True)
    alts.append(v); print(f"alternate {cfg}:", None if v is None else (call(v, "GetName2"), "type", call(v, "Type"), "config", call(v, "ReferencedConfiguration")))
```

Each call returned a view named `Alternate PositionN`, `Type` 10, whose
`ReferencedConfiguration` was the configuration asked for. It is drawn in phantom
lines over the base view and lists after it in `GetNextView`.

## Make sure the solid pose is the configuration's

**The base view drew the assembly's last solved position, not its
configuration's.** It read `ReferencedConfiguration` = Position A and drew the
pose of another configuration, the one the assembly had last been rebuilt in.
`ForceRebuild3` on the drawing alone returned `True` and changed nothing.
Activating the configuration in the assembly, rebuilding the assembly, then
rebuilding the drawing fixed it:

```python
app.ActivateDoc3(call(asm, "GetTitle"), False, 0, z())
print("show Position A:", asm.ShowConfiguration2("Position A")); print("asm rebuild:", asm.ForceRebuild3(False))
app.ActivateDoc3(call(drw, "GetTitle"), False, 0, z())
print("drw rebuild:", drw.ForceRebuild3(False)); call(drw, "ViewZoomtofit2")
```

**Check the picture, not the property.** Export a PNG
([drawings/01](01-new-drawing-and-place-views.md#hidden-lines-exploded-state-and-a-picture-of-the-sheet))
and confirm which outline is solid. Two positions that share a part's location
draw that part's phantom exactly over the solid one, so it vanishes: that is
correct, but a reader cannot tell it from a missing outline. Give that position
its own view with a note saying the part did not move, rather than three poses
on one view.

([GOTCHAS §82](../../GOTCHAS.md))

## Removing one alternate position

Select it by its view name as a `DRAWINGVIEW` and `EditDelete`:

```python
def delete(name, typ):
    call(drw, "ClearSelection2", True)
    ok = ext.SelectByID2(name, typ, 0, 0, 0, False, 0, nul, 0)
    r = call(drw, "EditDelete") if ok else None
    print(f"delete {name}: selected={ok}"); call(drw, "ClearSelection2", True)
delete("Alternate Position2", "DRAWINGVIEW")
```

Annotations attached to geometry of that position (a leader on its phantom pin)
were deleted first, by name the same way, as `NOTE` (`DetailItem56@Drawing View1`)
or `DIMENSION` (`RD2@Drawing View1`); each selected `True`. What happens to them
if the view goes first was not tried.

A second base view of the same assembly, with its own alternate position, lines
up with the first by model origin, not by `Position`; see
[drawings/01](01-new-drawing-and-place-views.md#laying-views-out).

## What it does not do

- **Whether the base view keeps its pose** after the assembly is switched back to
  another configuration and the drawing rebuilt is open. The fix above leaves the
  assembly in the base view's configuration. Settle it by switching back,
  rebuilding both, and exporting again.
- **Phantom line style** is the document's; not set.
- **Alternate positions from a moved component** (rather than a configuration)
  were not made.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-30, an assembly with a
sliding part and four configurations. `InsertAlternateView` returned `Alternate
Position1` and `Alternate Position2`, `Type` 10, each with the configuration
asked for. The base view's `ReferencedConfiguration` read the end-of-travel
configuration while the export showed the solid sliding part at the *other* end
(the assembly's active configuration was the default, last solved there);
`ForceRebuild3` on the drawing returned `True` and the export was unchanged. After
`ShowConfiguration2` (`True`) and `ForceRebuild3` on the assembly, then on the
drawing, the solid part sat at the configuration's end and the phantom a full
stroke away at the other, matching the configurations' own measurements. Deleting
`Alternate Position2` by `SelectByID2(…, "DRAWINGVIEW")` left the view list
`['Sheet1', 'Drawing View1', 'Alternate Position1']`; a second base view with
its own alternate (`Alternate Position3`) was then added below. Configuration
names are placeholders here.

## See also

- [assemblies/07 — Configurations that hold different positions](../assemblies/07-configurations-that-hold-different-positions.md)
- [drawings/01 — New drawing and place views](01-new-drawing-and-place-views.md)
- [drawings/03 — Notes, the title block, and deleting annotations](03-notes-title-block-and-deleting-annotations.md) — a note inserted right after this landed in the view
- [GOTCHAS §23, §82](../../GOTCHAS.md)
