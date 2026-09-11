---
id: curves-03-stream-curve-points
title: Create a curve by streaming points, with no file
status: verified
verified_on: SolidWorks 2025
language: [vba, vbscript, python]
api: [IModelDoc2.InsertCurveFileBegin, IModelDoc2.InsertCurveFilePoint, IModelDoc2.InsertCurveFileEnd]
keywords: [InsertCurveFileBegin, InsertCurveFilePoint, InsertCurveFileEnd, streaming, metres, no file]
answers: "Can I make a Curve Through XYZ Points feature without writing a file first?"
---

# Create a curve by streaming points, with no file

## Yes, and this is what the dialog actually does

Recording a manual import through **Insert → Curve → Curve Through XYZ Points**
does not produce a call taking a file path. It produces this:

```vb
Part.InsertCurveFileBegin
boolstatus = Part.InsertCurveFilePoint(0, 0, -0.05)
boolstatus = Part.InsertCurveFilePoint(0, 0.009754516, -0.049039264)
boolstatus = Part.InsertCurveFilePoint(0, 0.019134172, -0.046193977)
' ... one call per point ...
boolstatus = Part.InsertCurveFileEnd()
```

All three members are on **`IModelDoc2`**, not on `IFeatureManager`.

## Units: metres, always

`InsertCurveFilePoint` takes metres. There is no suffix and no document-unit
interpretation, unlike the file route. `-0.05` above is −50 mm.

Divide by 1000 on the way out and say so in a comment, because this is the one
bug that produces geometry a thousand times too big or too small and otherwise
perfectly correct.

## Generating it

```python
MM_PER_METRE = 1000.0

lines = ["Part.InsertCurveFileBegin"]
for (x, y, z) in points_mm:
    lines.append(
        "boolstatus = Part.InsertCurveFilePoint("
        f"{x / MM_PER_METRE:.9f}, {y / MM_PER_METRE:.9f}, {z / MM_PER_METRE:.9f})"
    )
lines.append("boolstatus = Part.InsertCurveFileEnd()")
```

Nine decimal places of a metre is a tenth of a micron, and is what the recorder
itself emits. Use a fixed-point format, not `str(n)`, so a very small value does
not come out in scientific notation, which VBA will not parse.

## Closed curves survive

A 32-point closed section, first point repeated as the last, produced 33
`InsertCurveFilePoint` calls. The repeat was accepted, not deduplicated.

## The trade-off against the file route

| | Streamed | From a file |
|---|---|---|
| Needs a file on disk | No | Yes |
| Carries a file association | **No** | Yes |
| Can use the 2025 Reload button | No | Yes |
| Can be refreshed later with `LoadPointsFromFile` | **Yes** | Yes |
| Macro size | One line per point | One line |

The important row is the third one. A point-streamed curve carries no path, so
the dialog's Reload button has nothing to reload from.

The fourth row is the reassuring one, and it was tested specifically: a curve
created by streaming **can** still be refreshed from a file afterwards using
`GetDefinition` and `LoadPointsFromFile`. Both insert routes produce the same
feature type, so either is refreshable later. See
[curves/04](04-reload-curve-in-place.md).

## When to use which

Stream when you are emitting a **self-contained macro** that someone will run
without your files. From a file when the curve is part of a **live loop** where
the file gets rewritten and re-pushed.

## See also

- [curves/02 — Insert a curve from a file](02-insert-curve-from-file.md)
- [curves/01 — The .sldcrv format](01-sldcrv-file-format.md)
- [sketches/05 — Units and number format](../sketches/05-units-and-number-format.md)
