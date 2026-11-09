---
id: drawings-02-bom-table-and-balloons
title: Add a bill of materials and balloons to an assembly drawing
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IView.InsertBomTable5, IBomTableAnnotation.BomFeature, IBomFeature.GetConfigurationCount, IBomFeature.GetConfigurations, IBomFeature.SetConfigurations, IBomFeature.FollowAssemblyOrder2, IBomFeature.TableType, IView.GetTableAnnotations, ITableAnnotation.Type, ITableAnnotation.RowCount, ITableAnnotation.ColumnCount, ITableAnnotation.Text, IDrawingDoc.CreateAutoBalloonOptions, IDrawingDoc.AutoBalloon5, INote.IsBomBalloon, INote.GetText, IAnnotation.SetPosition2, IAnnotation.GetPosition, IModelDocExtension.CustomPropertyManager, ICustomPropertyManager.Add3, ITableAnnotation.Text, ITableAnnotation.InsertColumn2, ITableAnnotation.SetTextFormat, ITableAnnotation.SetColumnWidth, ITableAnnotation.SetRowHeight, IConfiguration.AlternateName, IConfiguration.UseAlternateNameInBOM, IConfiguration.BOMPartNoSource, IModelDocExtension.CreateBalloonOptions, IModelDocExtension.InsertBOMBalloon2, IView.GetVisibleComponents, IAnnotation.GetLeaderPointsAtIndex]
keywords: [BOM, bill of materials, parts list, InsertBomTable5, empty BOM, BOM shows only header, no QTY column, BomFeature, SetConfigurations, top-level only, bom-standard.sldbomtbt, item number, FollowAssemblyOrder2, balloons, auto balloon, AutoBalloon5, CreateAutoBalloonOptions, missing balloons, hidden parts not ballooned, IsBomBalloon, Description column, custom property, CustomPropertyManager, Add3, swCustomInfoText, BOM cell edit writes to part, description write-back, custom BOM column, InsertColumn2, PART NUMBER alternate name, BOMPartNoSource, balloon quantity, ShowQuantity, InsertBOMBalloon2, CreateBalloonOptions member not found, leader attach point, crossing leaders]
answers: "How do I put a bill of materials on an assembly drawing from code, make it actually list the parts with descriptions, and balloon every part?"
---

# Add a bill of materials and balloons to an assembly drawing

## What this is for

An assembly drawing's parts list and the numbered balloons that point at each
part. Inserting the table is one call; getting rows in it, filling the
DESCRIPTION column, and getting a balloon on *every* part are three separate
problems, none of which the insert call reports.

Attach as in [drawings/01](01-new-drawing-and-place-views.md#attaching).

## Insert the table — and switch its configuration on

`IView.InsertBomTable5` on a view of the assembly. The template is SolidWorks'
own `bom-standard.sldbomtbt` under `<install>\lang\english\`.

```python
bom = iso.InsertBomTable5(False, 0.408, 0.285, 2, 2, "", BOM_T, False, 0, False, False)
print("BOM inserted:", bom is not None)
feat = call(bom, "BomFeature"); feat.FollowAssemblyOrder2 = True
print("rows", call(bom, "RowCount"), "cols", call(bom, "ColumnCount"))
```

That returned a table with **one row and three columns** — the header, and no
QTY. The table had no configuration switched on:
`GetConfigurationCount(True)` (visible) read 0 and `GetConfigurationCount(False)`
(available) read 1. Turning the configuration on filled it:

```python
bom = [t for t in (call(call(drw, "GetFirstView"), "GetTableAnnotations") or []) if call(t, "Type") == 2][0]
feat = call(bom, "BomFeature")
print("TableType", call(feat, "TableType"), "configs visible/available:", feat.GetConfigurationCount(True), feat.GetConfigurationCount(False))
vis = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_VARIANT, None)
names = feat.GetConfigurations(False, vis); print("available:", names, "visible flags:", vis.value)
names = list(names)
ok = feat.SetConfigurations(True, VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_BOOL, [True] * len(names)), VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_BSTR, names))
print("SetConfigurations:", ok, "now visible:", feat.GetConfigurationCount(True))
call(drw, "EditRebuild3")
for r in range(call(bom, "RowCount")):
    print("   ", " | ".join(str(bom.Text(r, c)) for c in range(call(bom, "ColumnCount"))))
```

- The table is found again from the **sheet** (the first "view"):
  `GetTableAnnotations` and `ITableAnnotation.Type` 2 for a BOM.
- The arguments of `InsertBomTable5` were used only as shown: placed by
  `(0.408, 0.285)` m as its top-right corner on an A3 sheet, top-level parts only.
  The meaning of each other argument was not varied.
- `FollowAssemblyOrder2 = True` numbers items in tree order; balloons already
  placed renumber with it.

([GOTCHAS §78](../../GOTCHAS.md))

## Filling DESCRIPTION

The column reads each part's `Description` custom property. Write it on the
part, save the part, rebuild the drawing:

```python
cpm = call(call(part, "Extension"), "CustomPropertyManager", "")
r = cpm.Add3("Description", 30, text, 2)
err = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0); warn = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
ok = part.Save3(1, err, warn)
```

`CustomPropertyManager("")` is the file-level (not configuration) manager; 30 is
`swCustomInfoText`, 2 `swCustomPropertyReplaceValue`. `Add3` returned 0.

## Balloons

`AutoBalloon5` balloons the **selected** view with options from
`CreateAutoBalloonOptions`:

```python
call(drw, "ClearSelection2", True)
print("select iso:", ext.SelectByID2(VIEW_NAME, "DRAWINGVIEW", 0, 0, 0, False, 0, nul, 0))
o = call(drw, "CreateAutoBalloonOptions")
o.Layout = 1                 # square: balloons arranged around the view
o.ReverseDirection = False
o.IgnoreMultiple = True
o.InsertMagneticLine = False
o.LeaderAttachmentToFaces = True
o.Style = 1                  # circular
o.Size = 2                   # fits two characters
o.UpperTextContent = 1       # BOM item number
o.Layername = "-None-"
o.ItemNumberStart = 1; o.ItemNumberIncrement = 1
notes = drw.AutoBalloon5(o)
call(drw, "ClearSelection2", True)
```

It returns the balloon notes. **A part with no visible edge in the view gets no
balloon, and nothing says so**: on an isometric of nine parts it made seven. The
two missing were inside other parts. Count balloons against the BOM rows, then
balloon the missing parts in a view where they show, and keep only the new
balloon for that part. A balloon's text is its item number; map it through the
BOM's first two columns:

```python
def item_to_part():
    call(drw, "EditRebuild3")
    return {str(bom.Text(r, 0)).strip(): str(bom.Text(r, 1)).strip() for r in range(1, call(bom, "RowCount"))}
def balloons(view_name=None):
    out = []; v = call(drw, "GetFirstView")
    while v is not None:
        if view_name is None or call(v, "GetName2") == view_name:
            n = call(v, "GetFirstNote")
            while n is not None:
                if call(n, "IsBomBalloon"): out.append((call(v, "GetName2"), n))
                n = call(n, "GetNext")
        v = call(v, "GetNextView")
    return out
```

Move a balloon with `call(n, "GetAnnotation").SetPosition2(x, y, 0)` (sheet
metres); the leader stays on its part. To delete balloons, select them by name,
not through the annotation — see
[drawings/03](03-notes-title-block-and-deleting-annotations.md#deleting-an-annotation).

## Editing a BOM cell writes to the part

`ITableAnnotation.Text(row, col)` can be put on a BOM through `Invoke`, as on a
general table ([drawings/10](10-general-table.md)), and the cell shows the new
text. But on a column linked to a property, **the put is written back to the
component's property**:

- Appending " (NEW)" to DESCRIPTION cells changed each part's file-level
  `Description` custom property in memory, and the parts showed as modified
  (`GetSaveFlag` `True`). The next drawing built from those parts showed the
  appended text.
- On a read-only Toolbox library part, the put wrote the configuration-specific
  `Description` of the configuration in use. That cannot be saved, so it lasts
  only for the session.

So do not put text into a linked BOM cell to change what one drawing says. Fix
the property on the part instead (below), or put drawing-only information in a
column of your own. Restoring the properties with `Add3(…, 2)` and saving put
the parts back.

### A column of your own

`ITableAnnotation.InsertColumn2(where, index, title, widthStyle)` with `where`
`3` (`swTableItemInsertPosition_After`) and index 2 added a column after
DESCRIPTION titled as given, `ColumnCount` 4 → 5. Its cells, filled with the
same put, did **not** dirty any part (`NEW_PARTS` is a set of part numbers;
names here are placeholders):

```python
ok = bom.InsertColumn2(3, 2, "STATUS", 0); call(drw, "EditRebuild3")
for r in range(1, call(bom, "RowCount")):
    put_text(bom, r, 3, "NEW" if str(bom.Text(r, 1)).strip() in NEW_PARTS else "REUSED")
```

### PART NUMBER from the configuration

The PART NUMBER column shows the file name by default; on a part whose
configuration names are long (Toolbox gears name them after every parameter) it
showed the configuration name. A name of your choice, set on the part's
configuration and saved, filled the column without touching the table:

```python
cfg = call(d, "GetActiveConfiguration")
cfg.AlternateName = pn; cfg.UseAlternateNameInBOM = True; cfg.BOMPartNoSource = 8
e, w = z(), z(); ok = d.Save3(1, e, w)
```

`BOMPartNoSource` 8 is `swBOMPartNumber_UserSpecified` (1 document name,
2 configuration name). Read back 8 and the name; the BOM showed it after a
rebuild.

### Text size, column widths, row heights

The template's BOM text was 3.5 mm and wrapped every description. Smaller text
first, then widths and heights, all in metres:

```python
tf = call(bom, "GetTextFormat"); print("BOM char height was", round(call(tf, "CharHeight")*1000, 2))
tf.CharHeight = 0.0025; print("SetTextFormat:", bom.SetTextFormat(False, tf))
for c, wd in enumerate((0.013, 0.040, 0.100, 0.013)): bom.SetColumnWidth(c, wd, 0)
call(drw, "EditRebuild3")
for r in range(call(bom, "RowCount")): bom.SetRowHeight(r, 0.0055, 0)
```

Narrowing columns without shrinking the text made rows taller, not the table
smaller: a row does not shrink below its wrapped text.

## Balloons one at a time, without the quantity

`AutoBalloon5` on one template drew each balloon with its **quantity** beside
it ("1" next to every balloon, "2" next to a part used twice). The auto-balloon
options object has no quantity setting. Inserting each balloon on its own did:

```python
call(drw, "ClearSelection2", True); iso.SelectEntity(best, False)
bo = call(ext, "CreateBalloonOptions"); bo.Style = 1; bo.Size = 2; bo.UpperTextContent = 1; bo.ShowQuantity = False
note = ext.InsertBOMBalloon2(bo); call(drw, "ClearSelection2", True)
```

- `IModelDocExtension.CreateBalloonOptions` is property-style through late
  binding: `ext.CreateBalloonOptions()` raised "Member not found"
  ([GOTCHAS §55](../../GOTCHAS.md)).
- `InsertBOMBalloon2` balloons the selected **edge's** component and returned
  the balloon note. Selecting the component itself instead
  (`SelectByID2("<comp>-1@<assembly>@<view>", "COMPONENT", …)` selected it)
  returned `None`: it needs an edge or face.
- To pick an edge per component: `IView.GetVisibleComponents` lists the
  components with something visible; `IView.GetVisibleEntities2(component, 1)`
  gives that component's visible edges. In a drawing view the component names
  come back **prefixed with the root assembly**, `Assembly-3/Part-1`; strip up
  to the last `/` before matching.
- The leader's attachment point is the last point of
  `IAnnotation.GetLeaderPointsAtIndex(0)` (sheet metres, `[…, x, y, z]`). Sorting
  balloons into a left and a right column by that x, and ordering each column by
  its y, gave leaders that did not cross.
- A part with no visible edge in the view (behind larger parts in an exploded
  isometric) cannot be ballooned there; balloon it in another view where it
  shows (a section did), or change the explode so it is visible.

## What it does not do

- **Indented or parts-only BOMs**, and BOMs on a multi-configuration assembly
  with more than one configuration shown, were not tried.
- **Balloon placement** is SolidWorks' square layout; balloons that collide with
  each other or with the table were moved by hand-picked coordinates, not by a rule.
- **Open:** whether `AutoBalloon5` can be told to balloon hidden parts. Settle it
  by varying the options object on a view with a fully enclosed part.
- **Open:** where the auto-balloon quantity display comes from (a document
  setting of the template is the likely source; not found).

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-30, a nine-part assembly
drawing on A3. `InsertBomTable5` returned a table of 1 row × 3 columns
(`ITEM NO. | PART NUMBER | DESCRIPTION`). `BomFeature.TableType` read 2; visible
configurations 0, available 1 (`('Default',)`, flag `False`).
`SetConfigurations` returned `True`, visible became 1, and after a rebuild the
table read 10 rows × 4 columns, items 1–9 with QTY 1 each and DESCRIPTION empty.
`Add3` returned 0 on all nine parts, `Save3` `True`, and after `ForceRebuild3` the
DESCRIPTION column showed every text. `AutoBalloon5` on the isometric returned 7
balloons; the two hidden parts were each ballooned in an orthographic view
(1 new, kept 1), and the sheet then had 9 balloons whose numbers matched the BOM.
Part and view names are placeholders here.

Later run, SolidWorks 2024 SP5 (32.5.0), two assembly drawings of nine BOM items
each:
- `Text` puts on DESCRIPTION cells changed five parts' `Description` properties
  in memory, confirmed by reading `CustomPropertyManager("")` and `GetSaveFlag`;
  they were restored with `Add3` and saved.
- `InsertColumn2(3, 2, "STATUS", 0)` returned `True` and its cells left the parts
  clean.
- `AlternateName` with `BOMPartNoSource` 8 on six gear parts gave the PART NUMBER
  column those names.
- `CharHeight` 3.5 → 2.5 mm took row heights from 7.3–11.1 to 5.5 mm.
- `InsertBOMBalloon2` with `ShowQuantity = False` made 8 of 9 balloons from the
  isometric with no quantity shown; the ninth part, hidden there, was ballooned
  in the section view; after a later explode change all 9 were made in the
  isometric.

## See also

- [drawings/01 — New drawing and place views](01-new-drawing-and-place-views.md)
- [drawings/03 — Notes, the title block, and deleting annotations](03-notes-title-block-and-deleting-annotations.md)
- [assemblies/08 — Exploded view from code](../assemblies/08-exploded-view-from-code.md) — ballooning an exploded isometric
- [files/05 — Rename a component's file](../files/05-rename-a-component-file.md) — the PART NUMBER column is the file name
- [drawings/10 — A general table](10-general-table.md) — the same cell put, without the write-back
- [drawings/08 — Section views from code](08-section-views-from-code.md) — ballooning a part in a section
- [GOTCHAS §55, §78, §80, §81, §91, §92](../../GOTCHAS.md)
