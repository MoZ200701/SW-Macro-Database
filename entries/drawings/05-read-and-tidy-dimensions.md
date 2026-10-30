---
id: drawings-05-read-and-tidy-dimensions
title: Read every dimension on a drawing with its tolerance, and fix its precision and text
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IView.GetFirstDisplayDimension5, IDisplayDimension.GetNext5, IDisplayDimension.GetDimension2, IDimension.SystemValue, IDimension.FullName, IDimension.Tolerance, IDimensionTolerance.Type, IDimensionTolerance.GetMaxValue, IDimensionTolerance.GetMinValue, IDimensionTolerance.GetHoleFitValue, IDimensionTolerance.GetShaftFitValue, IDisplayDimension.ShowParenthesis, IDisplayDimension.GetText, IDisplayDimension.SetText, IDisplayDimension.SetPrecision3, IDisplayDimension.Type2]
keywords: [drawing dimensions, read dimensions, review a drawing, trailing zeros, 24.70, precision, decimal places, SetPrecision3, tolerance precision, fit tolerance, H7, h9, hole fit, shaft fit, GetHoleFitValue, GetShaftFitValue, swTolType_e, bilateral, symmetric, limit, reference dimension, parentheses, ShowParenthesis, prefix, suffix, 2X, THRU, MOD-DIAM, diameter symbol, GetText, SetText, swDimensionTextParts_e]
answers: "How do I list every dimension on a drawing with its tolerance or fit, and fix trailing zeros, tolerance decimals and prefix text from code?"
---

# Read every dimension on a drawing with its tolerance, and fix its precision and text

## What this is for

Checking a drawing someone dimensioned by hand: what each dimension is, whether it
carries a tolerance or fit, whether it is a reference, what text surrounds it —
and then fixing the mechanical slips (a trailing zero such as 5.20, a fit showing -0.04 for
-0.043, a Ø on a width) without touching the layout.

Attach as in [drawings/01](01-new-drawing-and-place-views.md#attaching).

## Reading them

Dimensions live on the views (the sheet has none), as display dimensions. The
value is on the model dimension behind it, in **metres**:

```python
TYPES = {0: "none", 1: "basic", 2: "bilateral", 3: "limit", 4: "symmetric", 5: "MIN", 6: "MAX", 7: "fit", 8: "fit+tol", 9: "fit(tol only)", 10: "block"}
v = call(call(d, "GetFirstView"), "GetNextView")
while v is not None:
    dd = call(v, "GetFirstDisplayDimension5")
    while dd is not None:
        dim = dd.GetDimension2(0); tol = call(dim, "Tolerance"); tt = call(tol, "Type")
        extra = ""
        if tt in (7, 8, 9): extra = f"hole={call(tol, 'GetHoleFitValue')} shaft={call(tol, 'GetShaftFitValue')}"
        elif tt in (2, 3, 4): extra = f"{round(call(tol, 'GetMaxValue')*1000,3)}/{round(call(tol, 'GetMinValue')*1000,3)}"
        print(f"  {call(v,'GetName2'):14} {round(call(dim,'SystemValue')*1000,3):8}  {TYPES.get(tt, tt):10} {extra}  paren={call(dd,'ShowParenthesis')}  pre={dd.GetText(1)!r} suf={dd.GetText(2)!r}")
        dd = call(dd, "GetNext5")
    v = call(v, "GetNextView")
```

What came back, and how it looked on the sheet:

| Read | Displayed |
|---|---|
| `Tolerance.Type` 8, hole `H9` | `12 H9 +0.043 / 0` |
| `Type` 8, shaft `h6` | `Ø6 h6 0 / -0.008` |
| `Type` 4, max 0.05 mm | `<value> ±0.05` |
| `Type` 2, max 0.1, min 0 mm | `<value> +0.1 / 0` |
| `ShowParenthesis` `True` | `(<value>)`, a reference dimension |
| `GetText(1)` `'2X <MOD-DIAM>'` | prefix `2X Ø` |
| `GetText(2)` `' THRU'` | suffix ` THRU` |

`GetText(1)` is the prefix and `GetText(2)` the suffix (`swDimensionTextParts_e`).
`<MOD-DIAM>` is the Ø symbol. `GetMaxValue` / `GetMinValue` are metres. The type
names in `TYPES` match what each type displayed; `swTolType_e` values not in the
table above were not seen.

A dimension SolidWorks put on an arc as a diameter reported `Type2` 6.

## Fixing precision

`IDisplayDimension.SetPrecision3(primary, dual, primaryTol, dualTol)`; `-1`
leaves a field unchanged. Drop trailing zeros by setting the primary decimals to
the fewest that show the value:

```python
def decimals(mm):
    for k in range(0, 4):
        if abs(round(mm, k) - mm) < 1e-6: return k
    return 3
dim = dd.GetDimension2(0); val = call(dim, "SystemValue") * 1000
want = decimals(val); r = dd.SetPrecision3(want, KEEP, KEEP, KEEP)
```

with `KEEP = -1`. Tolerances need their own decimals: the same rule on a ± or +/−
tolerance, and **always 3 on a fit**, whose limits come from the ISO tables:

```python
if tt in (2, 3, 4):
    k = max(decimals(abs(call(tol, "GetMaxValue")) * 1000), decimals(abs(call(tol, "GetMinValue")) * 1000), 1)
    print(f"  [{vn}] {val} tol -> {k} dp:", dd.SetPrecision3(-1, -1, k, -1))
if call(call(dim, "Tolerance"), "Type") in (8, 9):
    print(f"  [{vn}] {round(call(dim,'SystemValue')*1000,3)} fit tol -> 3 dp:", dd.SetPrecision3(-1, -1, 3, -1))
```

A dimension a user adds later takes the document's default precision, so the
same slip comes back with each new dimension: run the pass again after edits.

## Fixing the text

`IDisplayDimension.SetText(1, text)` replaces the prefix:

```python
if vn == "Drawing View3" and tt in (7, 8, 9) and dd.GetText(1) == "2X <MOD-DIAM>":
    dd.SetText(1, "2X "); print(f"  [{vn}] {val} prefix ->", repr(dd.GetText(1)))
```

**Know what the dimension is attached to before you change its symbol.** That Ø
was on a dimension SolidWorks had made on the *arc* of a slot's round end, not
on the slot's width; removing it made the arc dimension read like a second width.
Check `Type2` (6 was the arc's diameter) before editing.

## What it does not do

- **Changing a tolerance type or fit from code.** `IDimensionTolerance.SetType`
  and `SetFitValues`, and `IDisplayDimension.Diametric`, were written into a fix
  but the run stopped on a check before reaching them. Unverified.
- **Angular dimensions** were not met; their `SystemValue` is radians.
- **Ordinate, chain and baseline dimensions** were not met.
- **Layout.** Moving dimension text off a part's outline was done by hand.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-29/30. The read loop ran
over six hand-dimensioned drawings, every row checked against the exported sheet
(table above). `SetPrecision3` returned 0 on every call; after a rebuild and
re-export, eight dimensions showing a trailing zero (a reference in brackets and
three diameters among them) displayed without it; a `+0.10` tolerance as `+0.1`;
an `h9 -0.04` as `h9 -0.043`. `SetText(1, "2X ")` changed the prefix
from `'2X <MOD-DIAM>'`, read back as `'2X '`.

## See also

- [drawings/01 — New drawing and place views](01-new-drawing-and-place-views.md)
- [drawings/03 — Notes, the title block, and deleting annotations](03-notes-title-block-and-deleting-annotations.md) — note text, changed the same way
- [features/04 — Read a feature's dimensions](../features/04-read-a-features-dimensions.md) — the model side of the same `IDimension`
- [reading/08 — Dimensions and equations](../reading/08-dimensions-and-equations.md)
- [GOTCHAS §2](../../GOTCHAS.md)
