---
id: sketches-05-units-and-number-format
title: Units and number formatting for generated macros
status: verified
verified_on: SolidWorks 2025 and 2026
language: [vba, vbscript, python, typescript]
api: []
keywords: [metres, millimetres, radians, units, number format, scientific notation, negative zero, nine decimals]
answers: "What units does the API use, and how do I format numbers so VBA parses them?"
---

# Units and number formatting for generated macros

## The rule

**The API is metres. The file format is whatever you suffix. Angles are
radians.**

There is no document-unit interpretation anywhere in the API. Set an inch
document and `SystemValue = 0.06` is still 60 mm.

The one exception is the Curve Through XYZ Points *file*, which takes a `mm`
suffix and honours it — see [curves/01](../curves/01-sldcrv-file-format.md).
Between the file and the API, the value is converted for you: a file written as
`-50.000000mm` reaches the API as `-0.05`.

## Convert at one boundary and say so

```typescript
/** Millimetres, this repo's unit, to metres, the SolidWorks API's. */
const MM_PER_M = 1000;

export function m(mm: number): number {
  return mm / MM_PER_M;
}
```

Put a comment in the generated file itself, not only in the generator:

```
' UNITS: every coordinate, radius and linear dimension below is in METRES, the
' SolidWorks API's own unit. This sketch's source data is millimetres; each value
' has already been divided by 1000 on the way out. Angle values (radians) are
' unconverted.
```

This is the single bug that produces geometry a thousand times too big or too
small and otherwise perfectly correct, so it earns a header comment.

## Formatting: fixed point, nine decimals

```typescript
export function fmtNum(v: number): string {
  if (!Number.isFinite(v)) throw new Error(`refusing to emit a non-finite value: ${v}`);
  let s = v.toFixed(9);
  if (/^-0(\.0*)?$/.test(s)) s = s.slice(1);
  return s;
}
```

Three separate reasons for each part:

**Fixed point, never `String(n)`.** A small millimetre value divided by 1000
comes out as `9.754516e-7`, and **VBA will not parse scientific notation in a
literal**. This is a real failure mode and it only appears on small numbers, so
it survives testing on ordinary geometry.

**Nine decimals of a metre** is a tenth of a micron, and is the precision the
SolidWorks macro recorder itself emits — the recorded import showed
`0.009754516`. Deterministic, and well past anything that matters.

**Strip negative zero.** `-0.000000000` is a different string from
`0.000000000`, and a shape that differs from its mirror by a minus sign on a
zero is not bit-identical to it. Strip after formatting, not before: testing
`value === 0` leaves `-1e-12` to print as `-0.000000000`.

## Refuse non-finite values

A NaN reaching a macro produces geometry that looks fine and behaves wrong.
Throw at format time, where the stack trace still points at the cause.

## Locale

Always emit a period as the decimal separator. VBA parses literals with a
period regardless of the machine locale, but a naive formatter on a German
machine will emit commas. In JavaScript, `toFixed` is locale-independent and
safe; `toLocaleString` is not. In Python, f-string float formatting is
locale-independent; `locale.format_string` is not.

## Reading values back

Multiply by 1000 on the way in, at the same boundary:

```python
MM_PER_METRE = 1000.0


def _in_mm(values):
    return (float(values[0]) * MM_PER_METRE,
            float(values[1]) * MM_PER_METRE,
            float(values[2]) * MM_PER_METRE)
```

Keeping both conversions in the same module means there is one place to look
when something is off by a thousand.

## See also

- [curves/01 — The .sldcrv format](../curves/01-sldcrv-file-format.md)
- [curves/03 — Stream curve points](../curves/03-stream-curve-points.md)
- [GOTCHAS §2](../../GOTCHAS.md)
