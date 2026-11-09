---
id: drawings-10-general-table
title: Put a general table on a drawing and fill it from code
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IDrawingDoc.InsertTableAnnotation2, ITableAnnotation.Text, ITableAnnotation.GetTextFormat, ITableAnnotation.SetTextFormat, ITableAnnotation.SetColumnWidth, ITableAnnotation.SetRowHeight, ITableAnnotation.GetRowHeight]
keywords: [general table, InsertTableAnnotation2, data table, gear data table, table text, indexed property put, Text(row, col), DISPATCH_PROPERTYPUT, table font size, CharHeight, column width, row height, swBOMConfigurationAnchorType_e, anchor top left]
answers: "How do I add a table of my own (not a BOM) to a drawing sheet and write text into its cells?"
---

# Put a general table on a drawing and fill it

## What this is for

A table that is not linked to the model: a gear data table, a list of
parameters, a revision block. Inserting it is one call; writing a cell needs
an indexed property put that late binding cannot spell, and the default text is
large enough that rows wrap.

Attach as in [drawings/01](01-new-drawing-and-place-views.md#attaching).
Positions and sizes are sheet **metres** in the API.

## The call

```python
def put_text(tbl, r, c, text):
    o = tbl._oleobj_; o.Invoke(o.GetIDsOfNames("Text"), 0, pythoncom.DISPATCH_PROPERTYPUT, False, r, c, text)
def data_table(x, y, rows, w=(0.038, 0.042)):
    t = drw.InsertTableAnnotation2(False, x / 1000, y / 1000, 1, "", len(rows), 2)
    if t is None: print("  table insert failed"); return None
    for r, (k, v) in enumerate(rows): put_text(t, r, 0, k); put_text(t, r, 1, v)
    tf = call(t, "GetTextFormat"); tf.CharHeight = 0.0025; t.SetTextFormat(False, tf)
    for c, wd in enumerate(w): t.SetColumnWidth(c, wd, 0)
    for r in range(len(rows)): t.SetRowHeight(r, 0.0048, 0)
    return t
```

- `IDrawingDoc.InsertTableAnnotation2(useAnchorPoint, x, y, anchorType,
  template, rows, columns)`. As run: `False`, a sheet point in metres, anchor
  `1` (`swBOMConfigurationAnchor_TopLeft` — the table's top-left corner went
  to the point), an empty template string, and the size. It returned the
  `ITableAnnotation`.
- **Writing a cell:** `ITableAnnotation.Text(row, column)` is an indexed
  property. Its put goes through `Invoke` with `DISPATCH_PROPERTYPUT`, the two
  indices first and the value last, the same pattern as the equation put in
  [connect/02](../connect/02-attach-from-python.md#setting-an-indexed-property).
  Reading is `tbl.Text(r, c)`.
- **Text size:** `GetTextFormat` is property-style; set `CharHeight` (metres)
  on the format object and hand it back with `SetTextFormat(False, tf)`
  (`False` = do not use the document's format). It returned `True`; the
  template's tables had been 3.5 mm.
- **Widths and heights:** `SetColumnWidth(index, width, 0)` and
  `SetRowHeight(index, height, 0)`, metres. A row does not shrink below its
  text: with 3.5 mm text the heights stayed at 7.3 mm or more, and only after
  the text was 2.5 mm did `SetRowHeight(r, 0.0055, 0)` read back 5.5. A cell
  that wraps keeps its row taller.

## Why it is not obvious

A **BOM** cell edited the same way is not the same: its Description column is
linked to the part, and the put writes back to the part's property. See
[drawings/02](02-bom-table-and-balloons.md#editing-a-bom-cell-writes-to-the-part).
A general table has no link, so its cells are plain text.

## What it does not do

- **Merged cells, borders and a header row style** were not set.
- **Table templates** were not used; the empty string gave SolidWorks' default.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0). Seven two-column tables of 11
to 13 rows were inserted on four drawings. Every one returned a table; every
cell read back the text put into it and showed in the exported PDF; with
`CharHeight` 2.5 mm and row height 4.8 mm, the only rows that stayed taller were
those whose second column wrapped.

## See also

- [drawings/02 — BOM table and balloons](02-bom-table-and-balloons.md) — the linked table, and why its cells behave differently
- [drawings/09 — Add dimensions and tolerances](09-add-dimensions-and-tolerances.md)
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md#setting-an-indexed-property) — the indexed put
