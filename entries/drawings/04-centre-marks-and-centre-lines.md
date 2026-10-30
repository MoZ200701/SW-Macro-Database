---
id: drawings-04-centre-marks-and-centre-lines
title: Add and remove centre marks and axis centre lines in a drawing view
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IView.GetVisibleEntities2, IView.SelectEntity, IEdge.GetCurve, ICurve.IsCircle, ICurve.CircleParams, IFace2.GetSurface, ISurface.IsCylinder, ISurface.CylinderParams, IModelDoc2.InsertCenterMark3, IModelDoc2.InsertCenterLine2, IView.GetFirstCenterMark, ICenterMark.GetNext, ICenterMark.GetPosition, ICenterMark.Select]
keywords: [centre mark, center mark, centre line, center line, InsertCenterMark3, InsertCenterLine2, slot arc, cut slot, centre mark missing, fillet centre marks, duplicate centre mark, auto centre marks, delete centre mark, GetFirstCenterMark, GetVisibleEntities2, SelectEntity, visible edges, cylinder axis, axis line in side view]
answers: "How do I put centre marks on the holes and arcs I choose in a drawing view, a centre line on a cylinder's axis, and remove the centre marks I don't want?"
---

# Add and remove centre marks and axis centre lines in a drawing view

## What this is for

Holes and arcs in a drawing need centre marks; a shaft seen from the side needs
its axis drawn. Automatic insertion missed the arcs of a cut slot, and
inserting on every circle marks tiny fillets that should not have one. The
reliable route is to pick the circles yourself from the view's visible edges.

Attach as in [drawings/01](01-new-drawing-and-place-views.md#attaching).

## Centre marks on chosen circles

`IView.GetVisibleEntities2(null, 1)` returns the view's visible **edges**. Keep
the circular ones, one per centre, and filter by radius before marking:

```python
v = ALL[n]; seen = set()
for e in list(v.GetVisibleEntities2(nul, 1) or []):
    c = call(e, "GetCurve")
    if call(c, "IsCircle"):
        p = list(call(c, "CircleParams")); k = (round(p[1] * 1000, 1), round(p[2] * 1000, 1))
        if k in seen: continue
        seen.add(k); call(drw, "ClearSelection2", True); v.SelectEntity(e, False)
        print(f" centre mark {n} r {p[6]*1000:.2f}:", drw.InsertCenterMark3(2, False, False) is not None)
```

- `CircleParams` is in **model metres**: items 0–2 the centre, 6 the radius.
- `InsertCenterMark3(2, False, False)` marks the selected edge and returns the
  mark, or `None` when it made none. Only these arguments were run.
- **Filter the radius.** The loop above, unfiltered, put a mark on a corner
  round a tenth the size of the block's peg, as well as on the peg. Small fillets do not get
  centre marks.
- **Check for duplicates.** A view can already carry marks inserted when it was
  created (a document option), so the same circle ended up with two stacked marks.

## Reading and deleting centre marks

`IView.GetFirstCenterMark` / `ICenterMark.GetNext` walk a view's marks.
`ICenterMark.GetPosition(0)` is in the **model's** coordinates, in metres, *not*
scaled to the sheet — compare it with `CircleParams`, not with sheet positions.
Delete with the mark's own `Select(True, null)` and `EditDelete`:

```python
marks = []; cm = call(v, "GetFirstCenterMark")
while cm is not None: marks.append((cm, [x * 1000 for x in list(cm.GetPosition(0))[:3]])); cm = call(cm, "GetNext")
for _, p in marks: print("  mark at", [round(x, 2) for x in p])
centre = [cm for cm, p in marks if abs(p[0]) < 0.05 and abs(p[1]) < 0.05]
corner = [cm for cm, p in marks if abs(p[0]) > 1]
assert len(centre) == 2 and len(corner) == 1 and len(marks) == 3, "unexpected marks - stopping"
dele = corner + centre[1:]
call(drw, "ClearSelection2", True)
if sum(bool(cm.Select(True, nul)) for cm in dele) == len(dele): call(drw, "EditDelete")
```

The position test is specific to the part; the shape is what carries over: list
the marks, decide by position, refuse to delete if the count is not what you
expected, then select with `Select` and delete once.

Unlike notes ([drawings/03](03-notes-title-block-and-deleting-annotations.md#deleting-an-annotation)),
centre marks **do** delete through their own `Select`.

## An axis centre line in a side view

Select the cylinder's visible **face** — `GetVisibleEntities2(null, 3)` — and call
`InsertCenterLine2`:

```python
cyl = sorted((f for f in list(v.GetVisibleEntities2(nul, 3) or []) if call(call(f, "GetSurface"), "IsCylinder")),
             key=lambda f: -list(call(call(f, "GetSurface"), "CylinderParams"))[6])
if cyl:
    call(drw, "ClearSelection2", True); v.SelectEntity(cyl[0], False)
    print("   axis centreline:", call(drw, "InsertCenterLine2") is not None)
```

Sorting by radius picks the outside of a tube rather than its bore.

## What it does not do

- **Centre marks on hidden arcs.** `GetVisibleEntities2` returns visible edges
  only; an arc seen only as a hidden line gets no mark this way.
- **Centre mark style** (size, extended lines) came from the document; not set.
- **Open:** why `InsertCenterMark3` returned `None` for some of the small arcs and
  a mark for one. Settle it by reading which edge each call selected.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-29/30. On a slotted part,
13 marks were present after insertion; the two on the slot-end centres were kept
by position and 11 selected with `Select` (`GetSelectedObjectCount2` 11) and
deleted with one `EditDelete`; a recount read 2. On a block with end pegs the
unfiltered loop returned a mark for the peg and for one small corner round; the
view then held three marks — two at the peg centre (x and y 0 in the model) and
one off-centre at the corner. Deleting the corner mark and one
duplicate left 1, and the exported sheet showed one mark on the peg.
`InsertCenterLine2` on the largest cylindrical face returned a centre line on
each of four cylindrical parts and on a gear's side view. On the gear's face
view the loop marked its two holes, both `True`.

## See also

- [drawings/01 — New drawing and place views](01-new-drawing-and-place-views.md)
- [drawings/03 — Notes, the title block, and deleting annotations](03-notes-title-block-and-deleting-annotations.md)
- [reading/15 — Measure a part that has no dimensions](../reading/15-measure-a-part-that-has-no-dimensions.md) — the same face and curve parameters on a body
- [GOTCHAS §2](../../GOTCHAS.md)
