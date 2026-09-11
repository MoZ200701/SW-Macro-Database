---
id: curves-06-composite-curve
title: Join several curves into one selectable curve
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [IModelDoc2.InsertCompositeCurve, IModelDocExtension.SelectByID2, IModelDoc2.ClearSelection2]
keywords: [InsertCompositeCurve, composite curve, SelectByID2, selection mark, REFERENCECURVES, sharp corner]
answers: "How do I join two curves into one thing a loft can select, keeping the corner sharp?"
---

# Join several curves into one selectable curve

## The problem it solves

A Curve Through XYZ Points is **one spline through its points**. So an airfoil
section plus the straight line closing its blunt trailing edge cannot be one of
those without the spline rounding the corner where they meet.

A composite curve joins them instead of refitting them. The corners stay sharp.
And because it is derived, it follows its inputs whenever they are reloaded,
which means it survives a live update loop.

## The call, and the trap

```python
COMPOSITE_SELECT_MARK = 1


def insert_composite_curve(self, sources, name):
    """Join several curves into one selectable curve, and name it."""
    if len(sources) < 2:
        raise SolidWorksError("A composite curve needs at least two curves to join.")

    doc = self._active()
    extension = call(doc, "Extension")
    call(doc, "ClearSelection2", True)
    for position, source in enumerate(sources):
        selected = call(
            extension, "SelectByID2", source, "REFERENCECURVES",
            0.0, 0.0, 0.0, position > 0, COMPOSITE_SELECT_MARK, _null_dispatch(), 0,
        )
        if not selected:
            call(doc, "ClearSelection2", True)
            raise SolidWorksError(f"{source} could not be selected to join.")

    before = set(self.feature_names())
    made = call(doc, "InsertCompositeCurve")
    call(doc, "ClearSelection2", True)
    if not made:
        raise SolidWorksError(
            f"SolidWorks would not join {' and '.join(sources)} into one curve."
        )

    created = [n for n in self.feature_names() if n not in before]
    if len(created) != 1:
        raise SolidWorksError(
            f"Joining added {len(created)} features, so which one it is cannot be told."
        )
    return self.rename_feature(created[0], name)
```

**The selection mark must be 1.** At mark 0, `InsertCompositeCurve` returns
`False` and says nothing about why. This is not documented anywhere obvious and
is pure trial and error to find.

## Reading the `SelectByID2` arguments

```
SelectByID2(name, type, x, y, z, append, mark, callout, selectOption)
```

| Argument | Value here | Why |
|---|---|---|
| `name` | the feature's name | which curve |
| `type` | `"REFERENCECURVES"` | the type string for a reference curve |
| `x, y, z` | `0, 0, 0` | ignored when selecting by name |
| `append` | `position > 0` | first clears, the rest add |
| `mark` | `1` | **the trap above** |
| `callout` | typed null | see [GOTCHAS §4](../../GOTCHAS.md) |
| `selectOption` | `0` | default |

## Clear the selection on every exit path

Including the failures. A leftover selection changes what the *next* call does,
and the resulting bug appears somewhere else entirely.

## Diff to find what you made

`InsertCompositeCurve` returns a boolean, same as `InsertCurveFile`. Same
solution: diff the feature names either side, and refuse to guess if the diff
is not exactly one. See [curves/02](02-insert-curve-from-file.md).

The type name of the result is `CompositeCurve`.

## See also

- [curves/02 — Insert a curve from a file](02-insert-curve-from-file.md)
- [curves/09 — Curves as loft profiles](09-curves-as-loft-profiles.md)
