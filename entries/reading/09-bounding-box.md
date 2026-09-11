---
id: reading-09-bounding-box
title: Measure a part's bounding box, and use it as a check
status: verified
verified_on: SolidWorks 2026 SP1.1
language: [csharp]
api: [IPartDoc.GetPartBox, IModelDoc2.ShowConfiguration2, IModelDoc2.EditRebuild3]
keywords: [GetPartBox, bounding box, extents, units, document units, metres, inches, verification, sanity check, ShowConfiguration2]
answers: "How big is this part, and how do I check that a generated file is the size it should be?"
---

# Measure a part's bounding box, and use it as a check

## What this is for

Two jobs. Finding out how big something is without opening it on screen, and
more usefully, **checking that a file an automation just produced is the size it
was supposed to be**. A part can save cleanly and hold the wrong solid. The
bounding box is the cheapest thing that would notice.

## The call, and the flag that matters

```csharp
double[] b = (double[])((PartDoc)doc).GetPartBox(true);
double[] d = { (b[3]-b[0])/0.0254, (b[4]-b[1])/0.0254, (b[5]-b[2])/0.0254 };
Array.Sort(d);
```

`GetPartBox` is on `IPartDoc`, so cast the document. It returns six doubles:
`x1, y1, z1, x2, y2, z2`, the two opposite corners. The extents are the
differences, which is what the second line computes before converting to inches.

**The boolean argument is a units switch, and this is the exception to the rule
that the API is always metres.** `GetPartBox(true)` returns metres.
`GetPartBox(false)` converts to the document's own unit system, so the same call
on two parts can return two different scales depending on how each file was set
up.

Pass `true` and convert yourself. A check that silently depends on a part's unit
setting is not a check. See [GOTCHAS §2](../../GOTCHAS.md) and
[GOTCHAS §24](../../GOTCHAS.md).

## Per configuration

The box is of whatever is currently active, so activate first and rebuild before
measuring:

```csharp
doc.ShowConfiguration2(c);
doc.EditRebuild3();
string act = doc.ConfigurationManager.ActiveConfiguration.Name;
double[] b = (double[])((PartDoc)doc).GetPartBox(true);
```

Read the active name back rather than trusting `ShowConfiguration2`'s return
value, which is False when the configuration was already active. Skip
`EditRebuild3` and you can measure the previous configuration's geometry, which
looks like a correct measurement of the wrong thing.

## Using it as a check

Where a name implies a size, compare and flag rather than assert. The useful
shape is a tolerance and a count of suspects, not a hard failure:

```csharp
double expect = holes * HOLE_PITCH_IN;
double best = Math.Min(Math.Abs(d[0] - expect), Math.Min(Math.Abs(d[1] - expect), Math.Abs(d[2] - expect)));
string s = dims + "  expect " + expect.ToString("0.000") + "in";
if (best > TOL_IN) { suspect++; s += "  <-- SUSPECT"; }
```

**Compare against every edge, not the longest one.** A short piece of a wide
profile is wider than it is long, so the edge that carries the length is not
reliably the largest. Sorting the three and taking the closest match to the
expected value handles both.

A tolerance is required. This is measured geometry, so exact equality will fail
on rounding even when the part is right.

## Why it is not obvious

The trap is not the call, it is what a passing check is worth. This measures the
solid's extents and nothing else. A part with the right length and the wrong
hole pattern passes. Treat it as a smoke test that catches gross errors cheaply,
and do not let it stand in for looking at the output.

## What it does not do

- Extents only. No volume, no mass, no centre of mass. Those are on
  `IModelDocExtension.GetMassProperties`, not covered here and not tried.
- The box is axis-aligned in model space, not a minimal oriented box. A part
  that sits at an angle to its own axes measures larger than it is. Everything
  in the run behind this entry was axis-aligned; a rotated part was not tested.
- `GetPartBox` is on `IPartDoc`. An assembly needs a different route, which was
  not tried.

## Evidence

SolidWorks 2026 SP1.1. Every one of 315 generated single-configuration parts was
measured with `GetPartBox(true)` and compared against the length its name
implies, at a tolerance of 0.03 in.

313 matched. **Two were flagged, and both were real**: configuration `33` of
`Aluminum 2x2 Angle` and `Steel 2x2 Angle` measured 16.990 in where 33 holes at
0.5 in is 16.500 in, with `32` at 16.000 in and `34` at 17.000 in. The fault was
in the source design table, inherited faithfully by the generated files. Nothing
in the generation path could have detected it. The measurement did.

## Full source

The check as it ran is `CheckBox` in
[`code/csharp/SwExplodeConfigs.cs`](../../code/csharp/SwExplodeConfigs.cs).

## See also

- [reading/08 — Dimensions and equations](08-dimensions-and-equations.md)
- [files/02 — Explode configurations](../files/02-explode-configurations.md) —
  the run this was the check for
- [sketches/05 — Units and number format](../sketches/05-units-and-number-format.md)
- [GOTCHAS §2](../../GOTCHAS.md), [GOTCHAS §24](../../GOTCHAS.md)
