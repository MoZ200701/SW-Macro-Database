---
id: surfacing-02-axis-conventions
title: Axis conventions, and landing geometry the right way up
status: verified
verified_on: SolidWorks 2026
language: [typescript]
api: []
keywords: [axes, front plane, top plane, right plane, orientation, Y up, Z up, axis map, mirror, standard views]
answers: "Why does my exported geometry come out lying on its side?"
---

# Axis conventions, and landing geometry the right way up

## The clash

SolidWorks orients its standard views around **Y up**. Its Front view shows X to
the right and Y up, looking down −Z.

Most engineering domains do not use that. Aircraft convention is X nose to tail,
Y to starboard, **Z up**. Write a model with Z up straight into SolidWorks and it
lands on its side: the part's Front view shows the plan rather than the
elevation, and every standard view is wrong.

## The three standard planes

| Plane | In-plane axes | Normal |
|---|---|---|
| Front | X right, Y up | Z toward the viewer |
| Top | X right, Z toward the viewer | Y up |
| Right | Z right, Y up | X |

## Remap at the boundary, once

Do not carry two coordinate systems around. Keep your model in whatever
convention its domain uses, and remap in **one place**, where the file is
written:

```typescript
export type AxisName = "+X" | "-X" | "+Y" | "-Y" | "+Z" | "-Z";

/** Which SolidWorks axis the nose points along, and which is the model's up. */
export interface AxisSpec {
  forward: AxisName;
  up: AxisName;
}

export interface AxisMap {
  /** Which column (0-2) carries the lateral coordinate, and so gets mirrored. */
  lateralColumn: number;
  columns(p: Pt3): [number, number, number];
  basis: { x: Vec3; y: Vec3; z: Vec3 };
}
```

The convention used by the fuselage tool here is **nose toward +Z, up +Y**,
which makes the part open in SolidWorks with its standard views meaning what
they should.

Derive everything from `forward` and `up`. The third axis is their cross
product, and getting it from the other two is what stops a left-right mirror
creeping in.

## Keep the inverse next to it

The viewer that shows the model needs the mapping the other way, so it can
display the part in the orientation SolidWorks will open it in. Derive it from
the same two vectors rather than writing a second mapping:

```typescript
/**
 * The inverse of `columns`. It lives here rather than in the viewer on purpose:
 * derived from the same forward and up, the viewport cannot become a second
 * coordinate convention.
 *
 * `columns` is a rotation, so its inverse is its transpose, and the transpose's
 * columns are that matrix's rows — which is all this is.
 */
basis: { x: Vec3; y: Vec3; z: Vec3 };
```

A second, independently-written inverse is where a mirror hides. There is no
test that catches it on a symmetric model, which is exactly the kind you test on.

## Mirroring

Know which output column carries the lateral coordinate, because that is the one
a mirror negates. Negating the wrong column produces a model that looks
plausible and is inside out.

Mirror the **text**, not the number, so the two halves are bit-identical:

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

Normalising negative zero is part of this. See
[curves/01](../curves/01-sldcrv-file-format.md).

## Show the file's numbers, not the remapped ones

If your tool has a UI, keep the panels in **your model's** coordinates and only
the 3D viewport in SolidWorks'. A station the user edits as `x: 250` should read
as `x: 250` in every panel, even though it is written to the file as
`z: -250`.

The viewport speaks SolidWorks; the panels speak the file. Mixing them means the
number on screen is not the number you type.

## Picking a plane interactively

If the user clicks a plane in SolidWorks to say where geometry should go, you
get a normal and a root point back, in model coordinates. See
[reading/04](../reading/04-read-the-selection.md).

A picked plane and a picked line together reduce exactly to three points, so
they can feed a plane-through-three-points mode you already have, and nothing
downstream needs to know a pick happened.

One rule that is invisible when broken: **a line has two ends and says nothing
about which is which.** If direction matters, decide it from something else, for
instance the end nearer a second point the user picked. Get it backwards and the
geometry comes out the right shape, on the right plane, facing the wrong way.

## See also

- [curves/01 — The .sldcrv format](../curves/01-sldcrv-file-format.md)
- [reading/04 — Read the selection](../reading/04-read-the-selection.md)
