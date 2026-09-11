---
id: curves-01-sldcrv-format
title: The Curve Through XYZ Points file format
status: verified
verified_on: SolidWorks 2025 and 2026
language: [any]
api: []
keywords: [sldcrv, curve through xyz points, file format, mm suffix, units, tab delimited, CRLF, closed curve]
answers: "What exactly do I write into a .sldcrv file so SolidWorks reads it correctly?"
---

# The Curve Through XYZ Points file format

## The format

- Three columns per line: X, Y, Z
- **Tab** delimited
- Period as the decimal separator, whatever the machine locale is
- Six decimal places is the convention used here
- Every value carries a `mm` suffix
- **CRLF** line endings, including on the last line
- No header, no comments, no blank lines
- Extension `.sldcrv` or `.txt`; the dialog accepts both

```
0.000000mm	-25.000000mm	0.000000mm
-4.877258mm	-24.519632mm	0.000000mm
-9.567086mm	-23.096988mm	0.000000mm
```

Samples in [`code/samples/`](../../code/samples/): a closed 32-point section
with and without the suffix, and a guide curve.

## The units question, settled

**Write the suffix.** A suffix-less number is read in *document units* by the
import dialog but as *metres* by the API, and those are two code paths that can
disagree. Writing `mm` on every value makes the question moot: `175.000000mm`
lands as 175 mm in an inch document just as it does in a millimetre one.

This was verified by recording the dialog import of a file this format wrote.
The macro recorder produced:

```vb
Part.InsertCurveFileBegin
boolstatus = Part.InsertCurveFilePoint(0, 0, -0.05)
boolstatus = Part.InsertCurveFilePoint(0, 0.009754516, -0.049039264)
...                                    ' 33 calls
boolstatus = Part.InsertCurveFileEnd()
```

`-0.05` is the file's `-50.000000mm`, and `0.009754516` is its `9.754516mm`. So
the dialog parsed the suffix correctly, and SolidWorks holds the result in
metres internally. Both halves confirmed at once.

Measured afterwards in the part: a radius-25 circle came back with a curve
length of **157.08 mm** against 2π×25 = 157.0796, and two stations 100 mm apart
measured **100.00 mm**. Units round-trip exactly.

## Closed curves

**A closed section works.** Repeat the first point as the last, and SolidWorks
keeps it: a 32-point closed section produced 33 `InsertCurveFilePoint` calls,
so the repeated row was neither rejected as a duplicate nor silently dropped.

Zebra stripes across the seam of a real surface built this way flowed unbroken,
so it is at least visually smooth and not merely closed. No deviation analysis
number was taken, so a subtle curvature break at the seam cannot be excluded.

If a future shape does show a seam, the fallback is to export the section as
two open halves split at the poles and pick both as profiles.

## Negative zero

Normalise it. `-0.000000mm` and `0.000000mm` are different strings, and a
section that differs from its mirror by a minus sign on a zero is not
bit-identical to it. Strip the sign **after** formatting, not before, because
testing `value == 0` leaves `-1e-9` to print as `-0.000000`:

```python
def format_value(value, decimals=6, unit="mm"):
    text = f"{value:.{decimals}f}"
    if text.startswith("-") and float(text) == 0.0:
        text = text[1:]
    return text + unit
```

## Why bit-identical text matters

If several curves are meant to touch — a guide curve crossing a section curve,
say — compute the crossing **once** and write the same characters into both
files. SolidWorks then finds them coincident by construction rather than within
tolerance.

This is what makes a boundary surface accept guide curves without the "guide
curves must intersect all the profiles" error. It was verified on a probe
cylinder with four guides across three sections, and on a real fuselage where
all 32 crossings share an identical text row.

The same trick applies to mirroring. Negate the lateral column's **text**, not
the number:

```typescript
export function mirrorRow(row: string, axes: AxisMap): string {
  const cols = row.split("\t");
  const v = cols[axes.lateralColumn]!;
  let neg = v.startsWith("-") ? v.slice(1) : `-${v}`;
  if (/^-0(\.0*)?(mm)?$/.test(neg)) neg = neg.slice(1);
  cols[axes.lateralColumn] = neg;
  return cols.join("\t");
}
```

The interpolant then crosses the symmetry plane perpendicularly by symmetry, so
there is no kink at the centreline to explain away later.

## Writing the file

```python
EXTENSIONS = (".sldcrv", ".txt")
DECIMALS = 6
DELIMITER = "\t"
UNIT_SUFFIX = "mm"
LINE_ENDING = "\r\n"


def format_points(points, decimals=DECIMALS, unit=UNIT_SUFFIX, delimiter=DELIMITER):
    """One point per line, three columns, terminated by CRLF including the last."""
    rows = [
        delimiter.join(format_value(c, decimals, unit) for c in point)
        for point in points
    ]
    return "".join(row + LINE_ENDING for row in rows)


def write_curve(path, points):
    if not points:
        raise ValueError(f"Refusing to write an empty curve to {path}.")
    with open(path, "w", encoding="ascii", newline="") as handle:
        handle.write(format_points(points))
    return len(points)
```

Open with `newline=""` and put the CRLF in yourself, so the same code produces
the same bytes on every platform. ASCII encoding, so a stray non-ASCII
character fails loudly at write time rather than confusing the dialog later.

## Refuse to write nonsense

```python
if not math.isfinite(value):
    raise ValueError(f"refusing to write a non-finite coordinate: {value}")
```

A NaN in a curve file produces a feature that looks fine and rebuilds wrong.

## Reading one back

A curve file carries no metadata. Points, and nothing else. Neither chord nor
plane nor any parameter survives into SolidWorks, and none can be read back
out. If you need to know how a curve was made, store that alongside the part
yourself.

## See also

- [curves/02 — Insert a curve from a file](02-insert-curve-from-file.md)
- [curves/09 — Curves as loft profiles](09-curves-as-loft-profiles.md)
- [surfacing/02 — Axis conventions](../surfacing/02-axis-conventions.md)
