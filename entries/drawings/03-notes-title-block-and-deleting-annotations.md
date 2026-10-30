---
id: drawings-03-notes-title-block-and-deleting-annotations
title: Add, edit and delete notes on a drawing, including the title block
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IModelDoc2.InsertNote, IDrawingDoc.EditSheet, IDrawingDoc.EditTemplate, IView.GetNotes, IView.GetFirstNote, INote.GetNext, INote.GetText, INote.SetText, INote.GetName, INote.GetExtent, INote.GetTextPoint2, INote.GetAnnotation, IAnnotation.SetPosition2, IAnnotation.GetPosition, IAnnotation.GetLeaderCount, IAnnotation.Select3, IModelDocExtension.SelectByID2, IModelDoc2.EditDelete]
keywords: [note, drawing note, InsertNote, sheet note, note attached to view, note moves with view, multi-line note, line break, newline, "<CR>", title block, sheet format, EditTemplate, EditSheet, DRAWING TITLE, placeholder text, change note text, SetText, leader, GetLeaderCount, delete note, delete balloon, delete annotation, Select3 does nothing, EditDelete, DetailItem, NOTE select type]
answers: "How do I add a note to a drawing sheet from code, fill in the title block, change a note's text, and delete notes or balloons reliably?"
---

# Add, edit and delete notes on a drawing, including the title block

## What this is for

Text on a drawing: general notes, labels under views, the title block's fields,
and getting rid of annotations again. Three of the four have a silent trap: the
note goes into whichever view is active, the title block is not on the sheet,
and selecting an annotation through its own object deletes nothing.

Attach as in [drawings/01](01-new-drawing-and-place-views.md#attaching).

## A sheet note

`IModelDoc2.InsertNote(text)` puts the note **in the active view**. Straight after
creating views from code, a view is active, and the note becomes that view's: it
moves when the view is dragged and is listed under the view, not the sheet.
`EditSheet` and an empty selection first make it a sheet note:

```python
def sheet_note(text, x, y):
    call(drw, "EditSheet"); call(drw, "ClearSelection2", True)
    n = drw.InsertNote(text)
    if n is not None: call(n, "GetAnnotation").SetPosition2(x / 1000, y / 1000, 0)
    call(drw, "ClearSelection2", True); return n
sheet_note("UNLESS OTHERWISE SPECIFIED:\nGENERAL TOLERANCES ISO 2768-m\nQTY 1 EACH", 20, 58)
```

- Line breaks are `"\n"`. `GetText` hands them back as `"\r\n"`.
- `SetPosition2` takes sheet **metres**; the point is the note's top-left.
  `INote.GetExtent` reads its box back, `[xmin, ymin, z, xmax, ymax, z]` in metres.
- To find which view owns a note, walk every view's `GetFirstNote` / `GetNext`;
  the sheet is the first "view" from `GetFirstView`.

([GOTCHAS §79](../../GOTCHAS.md))

## The title block

The title block's text belongs to the sheet **format**. Its notes are listed on
the sheet's `GetFirstNote` / `GetNotes` even while the sheet is being edited (a
review walk read `SCALE:2:1` and the author field that way). They were changed
from inside the format, `EditTemplate` … `EditSheet`:

```python
call(drw, "EditTemplate"); changed = []
for n in list(call(call(drw, "GetFirstView"), "GetNotes") or []):
    t = str(call(n, "GetText")).strip()
    if t == "DRAWING TITLE": n.SetText("PART TITLE"); changed.append(t)
    elif t == "XXX": n.SetText("3, 5, 6"); changed.append(t)
call(drw, "EditSheet"); print(" title block:", changed)
```

Match on the placeholder text your template uses. Always go back with
`EditSheet`, or the user is left editing the format. Fields that are linked to
document properties in the template (scale, sheet number) update themselves and
should be left alone. Whether `SetText` on a format note works *without*
`EditTemplate` was not tried.

## Changing a note's text

`INote.SetText(newText)` on any note, found by walking the views:

```python
for n in list(call(v, "GetNotes") or []):
    t = str(call(n, "GetText")); t2 = t
    for old, new in REPL: t2 = t2.replace(old, new)
    if t2 != t: n.SetText(t2); print(f"  [{vn}] note: {t!r} -> {str(call(n, 'GetText'))!r}")
```

To move a note from one view to the sheet, read its text and position, delete
it (below), `EditSheet`, insert it again and `SetPosition2` to the same point.
The re-inserted note's extent matched the old one to 0.1 mm.

`IAnnotation.GetLeaderCount` tells a note with a leader (1) from a plain one (0).

## Deleting an annotation

Select it **by name** with `SelectByID2(f"{note.GetName}@{viewName}", "NOTE", …)`,
appending, then `EditDelete`:

```python
call(drw, "ClearSelection2", True)
for vn, n in bs:
    name = f"{call(n, 'GetName')}@{vn}"
    ok = ext.SelectByID2(name, "NOTE", 0, 0, 0, True, 0, nul, 0)
    print(f"  select {name}: {ok}")
print("selected:", call(sm, "GetSelectedObjectCount2", -1))
r = drw.EditDelete() if callable(getattr(drw, "EditDelete", None)) else call(drw, "EditDelete")
```

Names look like `DetailItem62`. Balloons are notes and delete the same way.

**`IAnnotation.Select3(True, null)` then `EditDelete` deleted nothing** — on nine
balloons, with no error and no return value to check. Count before and after.
([GOTCHAS §80](../../GOTCHAS.md))

## What it does not do

- **Leaders from code.** Every leader note here was made by hand; `InsertNote`
  was only used without a selection, so it made notes without leaders.
- **Note fonts and text height** were the template's; not set from code.
- **Open:** whether `Select3` fails only through late binding. Settle it with the
  same deletion from VBA.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-29/30. A note inserted
right after `CreateDrawViewFromModelView3` and `InsertAlternateView` was listed
under `Drawing View1`; after deleting it and re-inserting with `EditSheet` first,
the walk listed it under `Sheet1`, extent `[266.3, 73.7, 360.1, 100.9]` mm against
the old `[266.4, 73.6, 360.1, 100.8]`. Title blocks on six drawings were filled
through `EditTemplate`; each run printed `['XXX', 'DRAWING TITLE']`. A three-line
note inserted with `"\n"` displayed on three lines and read back with `"\r\n"`.
Nine balloons each passed through `GetAnnotation().Select3(True, null)` and
`EditDelete`: the next count was still 9. `SelectByID2` by `DetailItemNN@view`
returned `True` nine times, `GetSelectedObjectCount2` read 9, and after
`EditDelete` the count was 0. Names and text are placeholders here.

## See also

- [drawings/01 — New drawing and place views](01-new-drawing-and-place-views.md)
- [drawings/02 — BOM table and balloons](02-bom-table-and-balloons.md)
- [drawings/04 — Centre marks and centre lines](04-centre-marks-and-centre-lines.md) — centre marks delete through their own `Select`
- [drawings/06 — Alternate position views](06-alternate-position-views.md) — where the stray view note came from
- [GOTCHAS §79, §80](../../GOTCHAS.md)
