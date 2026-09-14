---
id: reading-10-mass-properties-as-an-oracle
title: Use the part's volume to check that a feature did what you meant
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IModelDocExtension.CreateMassProperty, IMassProperty.Volume, IModelDoc2.ForceRebuild3]
keywords: [CreateMassProperty, mass property, Volume, cubic metres, oracle, verify feature, test geometry, cut removed nothing, did it work, regression check]
answers: "How do I check from code that a feature actually changed the solid the way I intended?"
---

# Use the part's volume to check that a feature did what you meant

## What this is for

A feature call that returns an object says the call succeeded. It does not say
the material went where you meant: a cut in the wrong direction, a pattern with
the wrong count, a dimension in the wrong units all build without error. The
part's volume is one number that changes with every one of those, and for
simple test geometry you can work out what it must be by hand. That makes it an
oracle: the check that decides whether an automated build is right.

## The call

```python
def volume_mm3(doc: Any) -> Optional[float]:
    """The part's volume through its mass properties, in cubic millimetres."""
    try:
        props = call(extension(doc), "CreateMassProperty")
        return float(call(props, "Volume")) * 1e9
    except Exception:  # noqa: BLE001
        return None
```

with a rebuild first:

```python
def _volume(doc: Any) -> float:
    call(doc, "ForceRebuild3", False)
    value = sc.volume_mm3(doc)
    if value is None:
        raise Require("The part's volume could not be read.")
    return value
```

`CreateMassProperty` is on **IModelDocExtension** (`IModelDoc2.Extension`) and
returns an **IMassProperty**; its `Volume` is in **cubic metres**, whatever the
document units. Multiply by 1e9 for mm³. `extension(doc)` is
`call(doc, "Extension")`.

## Why it is not obvious

**It is exact enough to compare against arithmetic.** For a cylinder r 20 mm ×
10 mm, `Volume` gave 12566.370614359175 mm³ against π × 20² × 10 =
12566.370614359173: sixteen significant figures. Tolerances of 0.1 % used in the
probe were far looser than needed.

**It told apart cuts that all "ran".** Four `FeatureCut4` forms were tried; the
volume before and after each is the only thing that showed which removed the
hole ([features/02](../features/02-cut-extrude.md)).

The comparisons that were made, each on a fresh part:

| Geometry | Expected mm³ | `Volume` × 1e9 |
|---|---|---|
| cylinder r 20 × 10 | π·20²·10 = 12566.371 | 12566.371 |
| same, width linked to a global of 12 | π·20²·12 = 15079.645 | 15079.645 |
| less one r 3 through-hole | 12566.371 − π·3²·10 = 12283.627 | 12283.627 |
| less six holes (circular pattern) | 10869.911 | 10869.911 |
| less eight holes (pattern count linked to 8) | 10304.424 | 10304.424 |
| cut in the default direction | 12283.627 | nothing removed (0 mm³) |

**For real parts, bracket it.** A gear has no closed form worth writing, so
the build checked that its volume lay strictly between a cylinder at the root
diameter and one at the outer diameter (both less the bore): 3531.57 mm³
between 2332.63 and 5654.87 for 12 teeth, module 2; and after changing to 13
teeth, module 2.25, 5545.11 between 3880.97 and 8443.52, and different from
before. A bound like that catches a missing pattern, a cut that removed
nothing, and a dimension a thousand times out.

## What it does not do

- Mass, density and centre of mass were not read. These parts had no material
  assigned, so only volume was meaningful.
- Whether a rebuild is needed before `CreateMassProperty` sees a change was not
  tested: every reading here was straight after `ForceRebuild3`.
- Assemblies were not measured.
- Volume cannot distinguish a hole in the right place from the same hole in the
  wrong place. Where position matters, read the geometry too.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426, probes `extrude`, `cut`, `pattern`, `pattern_count_link`,
`batched_under_command_in_progress` and `end_to_end` in
[`code/python/gear_generator/probe/`](../../code/python/gear_generator/probe/p2_solid.py);
the helper is in
[`scaffold.py`](../../code/python/gear_generator/probe/scaffold.py).

- `blank_volume_mm3 = 12566.370614359175`.
- `cut through all, default direction`: `removed_mm3` 0.0; the other three forms
  282.743 and 282.728.
- "six holes leave 10869.911 mm³ (expected 10869.911)"; "linked to 8 the
  pattern leaves 10304.424 mm³"; "after one rebuild the part is 12283.627 mm³".
- `volume_mm3_z12 = {'volume': 3531.5674902883707, 'root_cylinder': 2332.6325452904216, 'od_cylinder': 5654.8667764616275}`;
  `volume_mm3_z13 = {'volume': 5545.110964056051, 'root_cylinder': 3880.971393350672, 'od_cylinder': 8443.521130374693}`.

## See also

- [features/01 — Boss extrude](../features/01-boss-extrude.md)
- [features/02 — Cut extrude](../features/02-cut-extrude.md)
- [features/03 — Circular pattern](../features/03-circular-pattern.md)
- [connect/11 — Probe an API member on a live session](../connect/11-probe-an-api-member-on-a-live-session.md) — checking by outcome, not by return value
- [reading/07 — Report the feature type on failure](07-report-feature-type-on-failure.md)
- [reading/09 — Bounding box as a check](09-bounding-box.md) — the part's extents as the same kind of check
