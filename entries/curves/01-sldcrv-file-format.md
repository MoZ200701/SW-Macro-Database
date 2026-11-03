---
id: curves-01-sldcrv-format
title: The Curve Through XYZ Points file format
status: verified
verified_on: SolidWorks 2025 and 2026 (the periodic closed curve on 2026, 34.0.0)
language: [any]
api: []
keywords: [sldcrv, curve through xyz points, file format, mm suffix, units, tab delimited, CRLF, closed curve, periodic spline, seam, corner at the seam rounded, GetBCurveParams5, Periodic, duplicate points, near-duplicate, refused file, thin points, natural cubic spline, chord length parametrisation, interpolation]
answers: "What exactly do I write into a .sldcrv file so SolidWorks reads it correctly?"
---

# The Curve Through XYZ Points file format

## The format

- Three columns per line: X, Y, Z
- **Tab** delimited
- Period as the decimal separator, whatever the machine locale is
- **Ten** decimal places of a millimetre — six were used here until 2026-09-27, and are not enough for a curve that ends a solid loft; see *Write ten decimals* below
- Every value carries a `mm` suffix
- **CRLF** line endings, including on the last line
- No header, no comments, no blank lines
- Extension `.sldcrv` or `.txt`; the dialog accepts both

```
0.0000000000mm	27.4132465453mm	-173.7169001036mm
0.0000000000mm	27.5042566379mm	-173.6675138191mm
0.0000000000mm	27.6080669816mm	-173.6305266420mm
```

(The first three lines of a wing section's upper curve as the Airfoil Converter
wrote it on 2026-09-27, tabs and CRLF as listed above.)

(The samples in `code/samples/` are six-decimal files from before the change;
SolidWorks reads both.)

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

**SolidWorks 2026 draws a closed file as a periodic spline.** When the last row
is the first row again, it fits **one periodic cubic** through the loop instead
of an open spline with natural ends. Measured on 36 closed sections of 48
points (2026-11-03):

- `ICurve.GetBCurveParams5(False, False, False, True)` gave `Periodic` 1,
  order 4, 50 control points, 51 knots on [0, 1], end multiplicities [1, 3].
  (That read passed `IsClosed` True, so on its own it is not independent
  evidence. The next two are.)
- A chord-length **periodic** cubic interpolant through the same points matched
  the drawn curve to 0.0000000 mm at 12 points either side of the seam, on every
  section. A natural-end cubic missed by 0.01–0.87 mm.
- At the seam the two end tangents from `ICurve.Evaluate2(t, 2)` were equal
  and the curvature was the same, non-zero, at t = 0 and t = 1: the seam is G2.
  On a smooth section the curve lay within 0.0014 mm of the analytic section
  over its whole length.

So the seam is always smooth, which settles the question this section used to
leave open: a Boundary Surface over such sections had no edge at the seam line
and halved its normal angle across it with the step
([surfacing/05](../surfacing/05-boundary-surface-from-code.md)). The same held
for a 48-point circle of radius 30 mm: it lay 0.023 µm from the exact circle at
worst, and the same at the seam as anywhere else.

**The other side of that: a corner at the seam is rounded away.** V-keel
sections with corners of 16° to 102° at the seam row all came out with a
0.000° turn there; the curve passed through the corner row and swung up to
0.88 mm outside the V nearby. To keep a corner on a closed section, either:

- **cut there**: write the section as open pieces that start and end at the
  corner, and join them with a composite
  ([curves/06](06-composite-curve.md#a-composites-corner-is-set-by-its-pieces-ends));
  or
- **leave the file open by a hair**: the last row moved 0.0001 mm off the
  first gave `Periodic` 0, clamped end knots [4, 4], natural ends, and the
  corner to within 0.05°. A Boundary Surface over six such sections built, with
  a 0.0001 mm slit along the corner (two free edges) that a later knit has to
  close. That knit was not tried.

Either way the corner then sits between two natural ends, which lean in on
curved sides ([GOTCHAS §65](../../GOTCHAS.md)); the 0.05° and 0.11° above were
on **straight** V sides. [GOTCHAS §90](../../GOTCHAS.md).

Plane-Builder probe runs on SolidWorks 2026 (34.0.0): `p9_creases-20261103-040014`
(corners at the seam, and halves joined), `p9_creases-20261103-040223` (a smooth
seam), `p9_creases-20261103-043213` (one open curve, the hair gap),
`p11_nose-20261103-035854` (the circle).

## Negative zero

Normalise it. `-0.0000000000mm` and `0.0000000000mm` are different strings,
and a section that differs from its mirror by a minus sign on a zero is not
bit-identical to it. Strip the sign **after** formatting, not before, because
testing `value == 0` leaves `-1e-12` to print as `-0.0000000000`:

```python
def format_value(value, decimals=10, unit="mm"):
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
DECIMALS = 10   # six until 2026-09-27; see "Write ten decimals" above
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

## Points closer than 0.01 mm: thin them

SolidWorks 2026 refused curve files that held near-duplicate points: two
neighbouring rows all but on top of each other, as an offset or a trim easily
leaves. A spline forced through two nearly coincident points ties a knot in
itself. Dropping any point closer than **0.01 mm** (in the file's millimetres)
to the one kept before it, while keeping both ends, fixed it. From the Airfoil
Converter's `geometry.py`:

```python
# Points of an exported curve closer together than this, in mm, are one point.
# A spline forced through two all but coincident points ties a knot in itself,
# and SolidWorks refuses the file.
MIN_STEP = 0.01


def thin_curve(points: Sequence[Sequence[float]], min_step: float = MIN_STEP) -> List:
    """Drop points that crowd the one before, keeping both ends where they are."""
    pts = list(points)
    if len(pts) <= 2:
        return pts
    kept = [pts[0]]
    for p in pts[1:-1]:
        if math.dist(p, kept[-1]) >= min_step:
            kept.append(p)
    last = pts[-1]
    while len(kept) > 1 and math.dist(last, kept[-1]) < min_step:
        kept.pop()
    kept.append(last)
    return kept
```

A closed curve's repeated last point is an end, so it is kept; that is the
deliberate duplicate described above, and it was accepted. What the refusal
looked like (a `False` from `InsertCurveFile`, or the dialog's message) and the
exact spacing at which it starts were not recorded; 0.01 mm is the step that
worked, not a measured threshold. See [GOTCHAS §53](../../GOTCHAS.md).

## Write ten decimals, not six

**Six decimals of a millimetre are not enough for a curve that ends a solid
loft.** Rounded to six, a section that lies in a plane stands up to half a
millionth of a millimetre (5e-7 mm) off it. SolidWorks 2026 asks whether a
loft's end section is flat, and asks more strictly than that: on one wing, of
22 planar sections written to six decimals, **9 were refused as the end of a
solid loft** — the solid loft between any two neighbours failed whenever one
of them was among the nine — and the same points written to **ten decimals
lofted as a solid between every pair**, and through all 22 at once. Nothing
else changed: same points, same composites, same guides.

Through the API the refusal is silent (`InsertProtrusionBlend2` returns
`Nothing`); in the Loft property page it is a rebuild error:

> The end sections for a loft must be either planar or 3D faces or surfaces.
> A 3D section that does not bound a face or surface cannot be used as an end
> section.

Which sections trip it looks random — on that wing s03, s06 and s12 at
mid-span and every section past s16 at the tip, with neighbours of the same
shape passing — because it is the rounding, not the shape: every one of the
22 was flat to 5–9e-7 mm as SolidWorks drew it (`ICurve.GetTessPts` at a
1e-10 m tolerance), none crossed itself, and the pieces met end to end exactly.
A surface loft never asks the question, so it builds through the same curves
regardless ([features/12](../features/12-guided-loft.md)). This is what made
every "silently refused solid loft" in [surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md)
and [surfacing/04](../surfacing/04-cap-a-refused-loft-into-a-solid.md)
unexplained for a day: the profiles had been checked flat — to 1e-6 mm, which
was taken as flat enough, and is exactly the scale that is not.

Ten decimals of a millimetre is 1e-10 mm, still well above double precision at
wing-sized coordinates (a 1000 mm coordinate carries about 1e-13 mm), so the
last digit is not noise. Files get about 40 % longer. SolidWorks reads them
exactly as before, suffix and all.

What is not known: the tolerance SolidWorks uses, and whether seven or eight
decimals would do. Ten was tried because it worked; nothing smaller was.

Verified on SolidWorks 2026 SP0.0 (revision 34.0.0), 2026-09-27: the same 22
sections of a 1.35 mm inward offset wing inserted at six decimals and again at
ten, neighbouring pairs and the whole wing lofted as a solid with no guides,
three edge guides and 33 guides; and the original two-profile case (root and
tip plus three edge guides) refused at six decimals and built at ten.

## The curve SolidWorks draws through the points

**An open Curve Through XYZ Points is a natural cubic spline through the file's
points, parametrised by chord length**: the parameter advances by the straight
distance from each point to the next, and the ends have zero second
derivative. A **closed** file (last row the same as the first) is the periodic
version of the same, with no ends at all: see *Closed curves* above. Modelled that way, the curve matched the same curve read out of a
SolidWorks STEP export to **0.0007 mm** on SolidWorks 2026. Straight lines
between the same 57 points of a wing rib sat 0.18 mm inside the drawn curve at
its nose.

This matters whenever anything else has to meet the curve between its points,
for instance a loft guide
([surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md)): compute
the meeting point on the natural cubic spline, not on the polyline. Guides whose
ends were placed that way, between the file's points, were accepted by a loft.
Where they must meet exactly, the shared-characters rule above still applies:
a point written into both files is the safest crossing there is.

## Reading one back

A curve file carries no metadata. Points, and nothing else. Neither chord nor
plane nor any parameter survives into SolidWorks, and none can be read back
out. If you need to know how a curve was made, store that alongside the part
yourself.

## See also

- [curves/02 — Insert a curve from a file](02-insert-curve-from-file.md)
- [curves/09 — Curves as loft profiles](09-curves-as-loft-profiles.md)
- [surfacing/02 — Axis conventions](../surfacing/02-axis-conventions.md)
- [surfacing/03 — How a loft fills between two profiles](../surfacing/03-how-a-loft-fills-between-profiles.md) — guides that meet the drawn spline
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — the solid loft that asks whether an end is flat
- [surfacing/04 — Cap a refused loft into a solid](../surfacing/04-cap-a-refused-loft-into-a-solid.md) — what was built when the rounding was not yet known
- [curves/06 — Composite curves](06-composite-curve.md) — cutting a section where it must keep a corner
- [surfacing/05 — Build a Boundary Surface from code](../surfacing/05-boundary-surface-from-code.md) — closed sections as profiles, and the seamless face they make
- [GOTCHAS §53, §63, §65, §90](../../GOTCHAS.md)
