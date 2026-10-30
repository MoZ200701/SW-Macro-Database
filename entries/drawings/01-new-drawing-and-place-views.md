---
id: drawings-01-new-drawing-and-place-views
title: Make a drawing from a template and place, scale and align its views
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [ISldWorks.NewDocument, IDrawingDoc.Create3rdAngleViews2, IDrawingDoc.CreateDrawViewFromModelView3, IDrawingDoc.CreateUnfoldedViewAt3, ISheet.GetProperties2, ISheet.SetScale, IView.UseSheetScale, IView.ScaleDecimal, IView.GetOutline, IView.Position, IView.ModelToViewTransform, IView.SetDisplayMode4, IView.Type, IView.GetName2, IView.ShowExploded, IView.IsExploded, IDrawingDoc.GetFirstView, IView.GetNextView, IModelDocExtension.SaveAs]
keywords: [drawing, SLDDRW, drawing template, DRWDOT, new drawing, standard 3 views, third angle, first angle, projected view, unfolded view, model view, isometric view, sheet scale, view scale, UseSheetScale, sheet rescaled, 2:1 became 1:2, view position, view outline, align views, ModelToViewTransform, hidden lines visible, SetDisplayMode4, exploded drawing view, ShowExploded, PNG export of a drawing, sheet properties, GetProperties2]
answers: "How do I make a drawing from a template, place standard, model and projected views at the scale I want, line them up, and export a picture to check it?"
---

# Make a drawing from a template and place, scale and align its views

## What this is for

Building a drawing sheet from code: a new document from a drawing template, the
views on it, at a chosen scale, laid out where you want them, with hidden lines
shown, and a PNG of the sheet to look at without a screenshot. The calls are
simple; what goes wrong is the scale (the first view rescales the sheet) and the
layout (a view's `Position` is not where its model is).

## Attaching

Python over pywin32 with `swcom` from [`code/python`](../../code/python/swcom.py),
picking the session from `find_candidates()` as in
[connect/05](../connect/05-choosing-among-versions.md#the-floor-belongs-to-the-application-not-to-swcom).
`call(obj, name)` reaches zero-argument members ([GOTCHAS §5, §55](../../GOTCHAS.md)).

```python
import pythoncom
from win32com.client import VARIANT
import swcom
from swcom import call
app = sorted(swcom.find_candidates(), key=lambda c: (c.has_active_doc, c.revision), reverse=True)[0].app
nul = VARIANT(pythoncom.VT_DISPATCH, None); z = lambda: VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
```

## A new drawing and the three standard views

`ISldWorks.NewDocument(template, paperSize, width, height)` with a `.drwdot`
path. The template's own sheet (size, format, title block) survived passing
`0, 0, 0`. `IDrawingDoc.Create3rdAngleViews2(modelPath)` takes a saved part or
assembly path; the model must be saved.

```python
drw = app.NewDocument(TEMPLATE, 0, 0, 0)
sheet = call(drw, "GetCurrentSheet")
p = list(call(sheet, 'GetProperties2'))
print(f"sheet: paperSize={int(p[0])} template={int(p[1])} scale={p[2]:g}:{p[3]:g} firstAngle={bool(p[4])} size={p[5]*1000:.0f}x{p[6]*1000:.0f} mm format={call(sheet, 'GetTemplateName')}")
ok = drw.Create3rdAngleViews2(ASM)
call(drw, "EditRebuild3")
v = call(drw, "GetFirstView")
while v is not None:
    nm = call(v, "GetName2"); typ = call(v, "Type")
    if typ != 1:  # skip the sheet itself
        o = [x * 1000 for x in call(v, 'GetOutline')]; pos = [x * 1000 for x in call(v, 'Position')]
        print(f"view {nm:12} type={typ} scale={call(v, 'ScaleDecimal'):.4g} pos=({pos[0]:.0f},{pos[1]:.0f}) outline x {o[0]:.0f}..{o[2]:.0f} y {o[1]:.0f}..{o[3]:.0f} mm")
    v = call(v, "GetNextView")
```

- `GetFirstView` returns the **sheet** first (`Type` 1); the views follow via
  `GetNextView`. The base view of `Create3rdAngleViews2` reported `Type` 7 and its
  two projections `Type` 4.
- `ISheet.GetProperties2` item 4 is first-angle (`False` = third angle); items 2
  and 3 are the scale as numerator and denominator; 5 and 6 the size in metres.
  Read item 4 to answer "do these views follow third-angle projection?"
- `GetOutline` is `[xmin, ymin, xmax, ymax]` and `Position` `[x, y]`, both on
  the sheet in **metres**.

## One model view, then projected views from it

`CreateDrawViewFromModelView3(modelPath, viewName, x, y, z)` places a named model
view (`"*Front"`, `"*Top"`, `"*Right"`, `"*Isometric"`) at a sheet point in metres.
`CreateUnfoldedViewAt3(x, y, z, notAligned)` projects from the **selected** view
toward the point you give: above it is the top view, left of it the left view,
following the sheet's projection angle.

```python
ext = call(drw, "Extension"); sheet = call(drw, "GetCurrentSheet")
mm = lambda a: [x * 1000 for x in a]
first = [True]
def base(part, mv, x, y):
    v = drw.CreateDrawViewFromModelView3(part, mv, x / 1000, y / 1000, 0); v.UseSheetScale = True
    if first[0]: sheet.SetScale(2, 1, False, False); first[0] = False
    return v
def unfold(b, x, y):
    call(drw, "ClearSelection2", True); ext.SelectByID2(call(b, "GetName2"), "DRAWINGVIEW", 0, 0, 0, False, 0, nul, 0)
    v = drw.CreateUnfoldedViewAt3(x / 1000, y / 1000, 0, False); call(drw, "ClearSelection2", True); return v
b_br = base(PART_A, "*Right", 120, 190); t_br = unfold(b_br, 120, 255); l_br = unfold(b_br, 45, 190)
```

**The first model view rescales the sheet.** With the sheet set to 2:1 before
any view, the first `CreateDrawViewFromModelView3` set the sheet to suit the
model (a 2:1 sheet came back 1:2) and the view came out at 0.5. Set
`IView.UseSheetScale = True` on the view and call `ISheet.SetScale(2, 1, False,
False)` **after** the first view, then check `ScaleDecimal` on every view. A
projected view takes its parent's scale. ([GOTCHAS §77](../../GOTCHAS.md))

A view you want at its own scale (an exploded isometric at 1:2 on a 1:1 sheet)
needs a "SCALE 1:2" note under it: the title block only states the sheet scale.

## Laying views out

`IView.Position` is the centre of the view's **outline**, not the model's origin.
Two views of the same assembly whose outlines differ — one has an extra phantom
position, or a projection is wider — are offset by the difference even at the same
`Position`. Moving by outline works for spacing:

```python
def outline(v): return mm(call(v, "GetOutline"))
def place(v, cx, cy):
    o = outline(v); p = mm(call(v, "Position"))
    v.Position = VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, [(p[0] + cx - (o[0] + o[2]) / 2) / 1000, (p[1] + cy - (o[1] + o[3]) / 2) / 1000])
```

To line two views up **by the model**, compare where each puts the model origin.
`IView.ModelToViewTransform.ArrayData` items 9 and 10 are the sheet position of
the model origin in metres:

```python
def origin(view):  # sheet position (mm) of the model origin
    a = list(call(call(view, "ModelToViewTransform"), "ArrayData")); return a[9] * 1000, a[10] * 1000
to, bo = origin(top), origin(bot)
p = [x * 1000 for x in call(bot, "Position")]
bot.Position = VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, [(p[0] + to[0] - bo[0]) / 1000, p[1] / 1000])
call(drw, "EditRebuild3")
```

Moving a projected view only along its alignment line (x for one beside its
parent, y for one above) keeps it aligned; `place` above was used that way.

## Hidden lines, exploded state, and a picture of the sheet

```python
for v in ALL.values(): v.SetDisplayMode4(False, 1, False, True, False)
```

drew hidden edges as dashed lines in every view. Only these argument values were run.

`IView.ShowExploded(True)` on a drawing view of an assembly with an exploded view
returned `True`, and `IView.IsExploded` then read `True`; see
[assemblies/08](../assemblies/08-exploded-view-from-code.md) for making the explode.

`IModelDocExtension.SaveAs(path.png, 0, options, null, err, warn)` writes the
sheet as it appears in the window. Options `1` saved the drawing to its path; `3`
(silent | copy) wrote a PNG and left the document's own path alone. Call
`ViewZoomtofit2` first, or the PNG shows whatever the user had zoomed to.

```python
call(drw, "ViewZoomtofit2")
for path, opts in ((OUT, 1), (PNG, 3)):
    err, warn = z(), z(); r = ext.SaveAs(path, 0, opts, nul, err, warn); print(f"save {os.path.basename(path)} ret={r} err={err.value}")
```

## What it does not do

- **Paper size from code.** The template set it; `NewDocument`'s paper arguments
  were left at 0 and not varied.
- **Display-mode arguments other than the one call above** were not run.
- **Breaks, crops and broken-out sections** were not tried. A section view was
  not made from code either.
- **The PNG is of the window.** It depends on the window size; crop it by
  fractions, not pixels.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-29/30. Six drawings were
built from one template on A3. `Create3rdAngleViews2` on an assembly returned
`True` and gave views of `Type` 7, 4, 4 at the sheet scale, aligned. A part sheet
set to 2:1 before its first model view came back with that view at 0.5; with
`UseSheetScale` and `SetScale` after it, every view read `ScaleDecimal` 2.0. On
a three-part sheet, seven views (three model views, four `CreateUnfoldedViewAt3`
projections) landed above and beside their parents, and moving them along their
alignment line kept them aligned. Two `*Front` views of one assembly placed at
the same `Position` were offset on the sheet by the moving part's travel; `ModelToViewTransform` put
their origins at x 215.65 and 187.95 mm, and after the shift both read 215.65 and
the stationary parts sat one above the other in the exported PNG. File names and
paths are placeholders here.

## See also

- [drawings/02 — BOM table and balloons](02-bom-table-and-balloons.md)
- [drawings/03 — Notes, the title block, and deleting annotations](03-notes-title-block-and-deleting-annotations.md)
- [drawings/04 — Centre marks and centre lines](04-centre-marks-and-centre-lines.md)
- [drawings/05 — Read and tidy dimensions](05-read-and-tidy-dimensions.md)
- [drawings/06 — Alternate position views](06-alternate-position-views.md)
- [drawings/07 — Explode lines in a drawing view](07-explode-lines-in-a-drawing-view.md)
- [documents/01 — New part from a template](../documents/01-new-part-from-template.md) — the same `NewDocument`, for parts
- [connect/05 — Choosing among versions](../connect/05-choosing-among-versions.md)
- [GOTCHAS §77, §86](../../GOTCHAS.md)
