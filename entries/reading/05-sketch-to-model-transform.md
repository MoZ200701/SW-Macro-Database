---
id: reading-05-sketch-to-model-transform
title: Convert sketch coordinates to model coordinates
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [ISketch.ModelToSketchTransform, IMathTransform.Inverse, IMathTransform.ArrayData, ISelectionMgr.GetSelectedObjectsSketch]
keywords: [ModelToSketchTransform, ArrayData, IMathTransform, column major, sketch coordinates, transpose, 16 doubles]
answers: "A sketch point reports coordinates that are not in model space. How do I convert them?"
---

# Convert sketch coordinates to model coordinates

## The problem

A sketch entity reports itself in **its own sketch's coordinates**. A point on
the Right plane says `X = 0.05, Y = 0.02, Z = 0`, and that Z of zero is the
sketch plane, not the model's Z. Read those numbers as model coordinates and
your geometry lands somewhere arbitrary.

## Getting the transform

```python
def _sketch_transform(manager, index, obj):
    """Sketch coordinates to model coordinates, if this thing is in a sketch."""
    sketch = _try(manager, "GetSelectedObjectsSketch", index) or _try(obj, "GetSketch")
    inverse = _try(_try(sketch, "ModelToSketchTransform"), "Inverse")
    return _try(inverse, "ArrayData")
```

`ModelToSketchTransform` goes the wrong way for this purpose, so take its
`Inverse`. `ArrayData` unpacks the transform into plain doubles, which is what
you want: no COM pointer crosses back into your own code.

Two ways of reaching the sketch, because they work in different situations.
`GetSelectedObjectsSketch` on the selection manager works for anything the user
clicked. `GetSketch` on the object itself works when you already hold the
entity. Try both.

## The sixteen doubles

`IMathTransform.ArrayData` is sixteen doubles:

| Index | Meaning |
|---|---|
| 0–8 | rotation, 3×3 |
| 9–11 | translation |
| 12 | scale |
| 13–15 | unused |

## The rotation is stored by COLUMNS

This is the trap, and it is the reason this entry exists.

The first three values are **where the local X axis ends up**. The next three
are where the local Y ends up. The next three, the local Z.

Reading them as rows instead transposes the rotation. That is **silent on the
Front plane**, whose transform is the identity, and wrong on every other plane.
The symptom when this was got wrong: the Top plane reported its normal as −Y and
the Right plane as −X.

If your test model uses the Front plane, this bug passes every test you write.

## The conversion

```python
def transform_point(data, point):
    """Put a point through a SolidWorks transform.

    The rotation is stored by columns: the first three values are where the
    local X axis ends up, the next three the local Y, the next three the local Z.
    """
    r = [float(v) for v in data[:9]]
    tx, ty, tz = (float(v) for v in data[9:12])
    s = float(data[12]) if len(data) > 12 else 1.0
    x, y, z = point
    return (
        s * (r[0] * x + r[3] * y + r[6] * z) + tx,
        s * (r[1] * x + r[4] * y + r[7] * z) + ty,
        s * (r[2] * x + r[5] * y + r[8] * z) + tz,
    )
```

Note the index pattern `r[0], r[3], r[6]` in the first row rather than
`r[0], r[1], r[2]`. That stride of 3 is the column-major reading.

## Applying it to a sketch point

```python
def _sketch_coords(obj, to_model):
    x, y, z = _try(obj, "X"), _try(obj, "Y"), _try(obj, "Z")
    if x is None or y is None or z is None or to_model is None:
        return None
    return _in_mm(transform_point(to_model, (float(x), float(y), float(z))))
```

`X`, `Y` and `Z` on a sketch point are properties, reached by attribute access
in late-bound Python. Convert to millimetres **after** transforming, so the
transform's own translation stays in the metres it was given in.

## A trick worth stealing

When you want a **direction** out of a transform rather than a position, do not
pick the rotation apart. Send two points through the same transform and
subtract:

```python
root = transform_point(data, (0.0, 0.0, 0.0))
tip = transform_point(data, (0.0, 0.0, 1.0))
normal = (tip[0] - root[0], tip[1] - root[1], tip[2] - root[2])
```

That cannot get the row-versus-column question wrong, because whatever
convention `transform_point` uses, it uses it consistently for both points.
This is how a reference plane's normal is read in
[reading/04](04-read-the-selection.md).

## How to test this without SolidWorks

`transform_point` is a pure function of sixteen numbers. Capture the arrays for
the Front, Top and Right planes once with a diagnostic script, paste them into a
test, and assert the normals come out as +Z, +Y and +X. That test then runs on
any machine and catches the transpose immediately.

## See also

- [reading/04 — Read the selection](04-read-the-selection.md)
- [GOTCHAS §10](../../GOTCHAS.md)
- [assemblies/01 — New assembly and insert components](../assemblies/01-new-assembly-and-insert-components.md) — a component's `Transform2.ArrayData`
- [assemblies/04 — Mates between non-parallel axes](../assemblies/04-mates-between-non-parallel-axes.md) — writing the same sixteen numbers, by columns, to set a component's frame
- [features/06 — Reference plane normal to a line](../features/06-reference-plane-normal-to-a-line.md) — a plane's sketch frame read back this way
- [features/05 — Revolve](../features/05-revolve.md) — drawing at model points through the transform
- [features/08 — Offset reference plane](../features/08-offset-reference-plane.md) — measuring where a plane landed
