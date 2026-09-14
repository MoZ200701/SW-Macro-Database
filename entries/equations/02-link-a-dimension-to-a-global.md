---
id: equations-02-link-a-dimension-to-a-global
title: Link a dimension to a global variable
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IEquationMgr.Add2, IEquationMgr.Equation, IDimension.Name, IDimension.FullName, IDimension.SystemValue, IModelDoc2.ForceRebuild3]
keywords: [link dimension, dimension equation, drive dimension, D1@Sketch1, Dim@Feature, global variable, parametric, document units, millimetres, -1, dimension does not exist yet, mate dimension, pattern count]
answers: "How do I make a sketch, feature or mate dimension follow a global variable from code?"
---

# Link a dimension to a global variable

## What this is for

A global on its own drives nothing. What makes a part change when a number
changes is a second kind of equation, whose left-hand side is a dimension:
`"OD@Blank Sketch"= "Outer Diameter"`. This entry is how to add that from code,
what name to use for the dimension, what units the right-hand side is in, and
the one ordering rule that makes the add fail.

## The call

It is the same `Add2` as a global
([equations/01](01-global-variables-from-code.md)), with the dimension's name
quoted on the left:

```python
def link_dimension(self, dimension: str, expression: str) -> int:
    """A dimension equation, which is refused until the dimension exists (probe dimension_link)."""
    return self._add_equation(equation_text(dimension, expression))
```

where `_add_equation` calls `IEquationMgr.Add2(-1, text, True)` and reads
`Equation(i)` back, and `equation_text("OD@Probe Link", '"Probe D"')` makes
`"OD@Probe Link"= "Probe D"`.

The probe that established it, in full order — global, sketch with a named
dimension, close, link, rebuild, read, change, rebuild, read:

```python
global_index = add('"Probe D"= 30')
px.require(global_index >= 0, "the global is added")

before = sc.feature_names(doc)
manager = sc.open_sketch(px, doc, 1)
circle = call(manager, "CreateCircleByRadius", 0.0, 0.0, 0.0, 0.010)
sc.select(doc, circle)
display = call(doc, "AddDimension2", 0.015, 0.015, 0.0)
call(doc, "ClearSelection2", True)
made = call(display, "GetDimension2", 0)
made.Name = "OD"
sketch = sc.close_sketch(px, doc, "Probe Link", before)

link = add(f'"OD@{sketch}"= "Probe D"')
px.require(link >= 0, f'the link "OD@{sketch}"= "Probe D" is accepted once the dimension exists')
px.fact("link_text_reads_back", str(call(eqm, "Equation", link)))
call(doc, "ForceRebuild3", False)
dim = sc.dimension_named(sc.feature_by_name(doc, sketch), "OD")
value = float(call(dim, "SystemValue"))
units = "mm" if abs(value - 0.030) < 1e-9 else "inch" if abs(value - 0.762) < 1e-9 else "?"
px.fact("equation_length_units", units, f"a global of 30 drives the dimension to {value!r} m")
px.check(units == "mm", "equations are in millimetres in this document")

sc.put_indexed(eqm, "Equation", global_index, '"Probe D"= 40')
call(doc, "ForceRebuild3", False)
value = float(call(sc.dimension_named(sc.feature_by_name(doc, sketch), "OD"), "SystemValue"))
px.check(abs(value - 0.040) < 1e-9, f"changing the global and rebuilding moves the dimension to {value!r} m")
```

`add` is `Add2(-1, text, True)` returning the index; `sc.*` are small helpers
in [`scaffold.py`](../../code/python/gear_generator/probe/scaffold.py) that
select, open and close sketches, and walk a feature's dimensions
([features/04](../features/04-read-a-features-dimensions.md)).

### The dimension's name

`Name@Owner`, where the owner is the sketch or feature the dimension belongs
to, by its current tree name:

| Link that worked | Dimension |
|---|---|
| `"OD@Probe Link"` | a circle's diameter in a sketch named `Probe Link` |
| `"Width@Blank"` | an extrusion's depth, renamed `Width`, on the extrusion `Blank` |
| `"Count@Teeth"` | a circular pattern's instance count, renamed `Count` |
| `"D1@Centre Distance"` | a distance mate's dimension, on a mate renamed `Centre Distance`, in an assembly |
| `"D1@Mesh Angle"` | an angle mate's dimension, 3° (0.05235987755983 rad), on a mate renamed `Mesh Angle`. Accepted and read back; the global was not changed afterwards, so following it is not observed |

Name the dimension (`IDimension.Name`) and its owner before linking, and read
both back ([curves/07](../curves/07-rename-a-feature.md)): the link is text,
and a name SolidWorks did not keep breaks it.
`IDimension.FullName` reads with the document on the end,
`'OD@Probe Dims@Part223.Part'`; the equation uses only the first two parts.

### Units

The right-hand side is in **document units**. A global of 30 drove the
dimension to `SystemValue` 0.030 m in a millimetre part. `SystemValue` itself
is always metres (radians for an angle). A pattern count is a plain number.

## Why it is not obvious

**The dimension must exist before its equation.** `"OD@Probe Link"= 30`, added
before the sketch and its dimension had been made, returned `-1`. The same text
added afterwards returned an index. So a build adds its globals first and its
links last, after the geometry they name.

**A document from an inch template reads the same equation in inches.** Only a
millimetre document was measured here; the probe checked for the inch reading
(0.762 m) and did not see it. See
[documents/01](../documents/01-new-part-from-template.md) for why a new part
can be in inches.

**A mate's dimension is linked the same way, and it moves the component.** In
an assembly, the distance mate's dimension was `D1`, and linking
`"D1@Centre Distance"` to an assembly global of 60, then changing that global
to 70 and rebuilding, moved the second component from 60 mm to 70 mm. See
[assemblies/02](../assemblies/02-mates-from-code.md).

## What it does not do

- Driven (reference) dimensions were not tried; every dimension here was a
  driving one.
- Links whose right-hand side is an arithmetic expression rather than a single
  global were not tested separately, though the gear build's links name single
  globals whose own equations do the arithmetic.
- An angle dimension was dimensioned and read, but never linked to a global.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426.

- `dimension_link`: link before the dimension, `-1`; global added; link added
  afterwards and read back as `'"OD@Probe Link"= "Probe D"'`; 30 gave 0.03 m;
  after the indexed put to 40 and `ForceRebuild3`, 0.04 m.
- `extrude`: `"Width@Blank"= "Probe W"` with `"Probe W"= 12` made the blank
  15079.645 mm³, which is π × 20² × 12.
- `pattern_count_link`: `"Count@Teeth"= "Probe N"` with 8 left 10304.424 mm³,
  a blank less eight holes.
- `assembly_distance_link`: component B at (60.0, 0.0, -5.0) mm, then at
  (70.0, 0.0, -5.0) after the global went from 60 to 70.
- `end_to_end`: a gear built with six links (`OD@Blank Sketch`, `Width@Blank`,
  `Root@Tooth Space Sketch`, `Fillet@Tooth Space Sketch`, `Count@Teeth`,
  `Bore@Bore Sketch`); after the teeth and module globals changed,
  `OD@Blank Sketch` read 33.75 mm and `Count@Teeth` 13.

## See also

- [equations/01 — Global variables from code](01-global-variables-from-code.md)
- [sketches/04 — Dimensions](../sketches/04-dimensions.md) — making and naming the dimension
- [features/04 — Read a feature's dimensions](../features/04-read-a-features-dimensions.md) — finding an extrusion's or pattern's dimension to name
- [assemblies/02 — Mates from code](../assemblies/02-mates-from-code.md)
- [documents/01 — New part from a template](../documents/01-new-part-from-template.md) — document units
- [reading/08 — Dimensions and equations](../reading/08-dimensions-and-equations.md) — listing a part's dimensions and their full names
