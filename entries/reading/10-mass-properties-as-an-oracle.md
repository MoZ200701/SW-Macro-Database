---
id: reading-10-mass-properties-as-an-oracle
title: Use the part's volume to check that a feature did what you meant
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IModelDocExtension.CreateMassProperty, IMassProperty.Volume, IMassProperty.CenterOfMass, IModelDoc2.ForceRebuild3]
keywords: [CreateMassProperty, mass property, Volume, cubic metres, oracle, verify feature, test geometry, cut removed nothing, did it work, regression check, CenterOfMass, centre of mass, Cavalieri, twin, twisted sweep, spline, tolerance, fresh build]
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

## When the arithmetic is not exact, and other oracles

**Spline surfaces weigh close, not exact.** A twisted sweep, a twisted cut and
a patterned tooth space all measured within about 1e-3 of the arithmetic
rather than to sixteen figures ([GOTCHAS §48](../../GOTCHAS.md)). Compare them
with a relative tolerance, or against another build rather than a formula.

**The centre of mass says which way, and how much.** A circle r0 from the
axis turned uniformly through θ has its centroid at
`r0·(sin θ/θ, ±(1 − cos θ)/θ)`; the sign of y gave the twist's direction and
its size the twist's units ([features/09](../features/09-twisted-sweep.md)).
With one tooth space cut from a round blank, the part's centre of mass moved
to −y for a right-hand helical space and +y for a left-hand one, which is how
the gear's hand was checked ([features/10](../features/10-swept-cut.md)).

**A twin, when there is no formula.** A section swept along a line with a
twist encloses the same volume as the same section cut straight (Cavalieri).
So a helical gear was checked against the same build plan with its swept cut
replaced by a straight one. From
[`p5_helical.py`](../../code/python/gear_generator/probe/p5_helical.py):

```python
def twin(plan: BuildPlan, path: str) -> BuildPlan:
    """The plan with its twisted sweep cut straight, saved elsewhere."""
    ops = []
    for op in plan.ops:
        if isinstance(op, SweepCut):
            ops.append(Cut(op.id, op.profile, op.name))
        elif isinstance(op, LinkDimension) and op.dimension.startswith("Twist@"):
            continue
        elif isinstance(op, SaveAs):
            ops.append(SaveAs(path))
        else:
            ops.append(op)
    return replace(plan, ops=tuple(ops), path=path)
```

The tolerance there is 5e-4 relative. The helical gear weighed 4961.165 mm³
against its twin's 4961.035, and the herringbone 6005.963 against 6005.623.

**A fresh build, after an edit.** A part changed through its globals and a
new part built straight from the changed values should weigh the same, and
here they did to 1e-13 or better: a ring gear after 60 → 64 teeth,
34284.14386641376 against 34284.143866418235 mm³, and a bevel pinion after
20 → 21 teeth, 43638.719829502406 against 43638.719829502304. That catches an
equation that did not propagate, which no formula for the new part would.

## What it does not do

- Mass and density were not read: these parts had no material assigned.
  Centre of mass was read on later probes, above.
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
- Run 20260915-023929 (54 of 54 probes passed),
  [`probe-results-20260915-023929.txt`](../../code/python/gear_generator/probe/results/probe-results-20260915-023929.txt):
  `helical_volumes_mm3 = {'part': 4961.164729903562, 'twin': 4961.0349155102585}`;
  `herringbone_volumes_mm3 = {'part': 6005.962761822714, 'twin': 6005.622503453732}`;
  `helical_right_one_space_centre_mm = (-0.35200349853589447, -0.06554931397919656, 4.999985102287864)`;
  `helical_left_one_space_centre_mm = (-0.3520003644086587, 0.06557239075310127, 4.999984857178944)`;
  `spur_ring_volumes_mm3 = {'ring': 32287.475563673983, 'blank': 41728.20442130643, 'one_space': 157.49383390868024, 'expected': 32278.574386785614}`;
  `spur_volumes_after_mm3 = {'updated': 34284.14386641376, 'fresh': 34284.143866418235}`;
  `bevel_volumes_after_mm3 = {'updated': 43638.719829502406, 'fresh': 43638.719829502304}`.

## See also

- [features/01 — Boss extrude](../features/01-boss-extrude.md)
- [features/02 — Cut extrude](../features/02-cut-extrude.md)
- [features/03 — Circular pattern](../features/03-circular-pattern.md)
- [connect/11 — Probe an API member on a live session](../connect/11-probe-an-api-member-on-a-live-session.md) — checking by outcome, not by return value
- [reading/07 — Report the feature type on failure](07-report-feature-type-on-failure.md)
- [reading/09 — Bounding box as a check](09-bounding-box.md) — the part's extents as the same kind of check
- [features/05 — Revolve](../features/05-revolve.md) — Pappus volume, and the centre of mass on the axis
- [features/07 — Loft cut](../features/07-loft-cut.md) — a frustum's volume
- [features/09 — Twisted sweep](../features/09-twisted-sweep.md) — the centroid of a twist
- [features/10 — Swept cut](../features/10-swept-cut.md) — the twin and the one-space hand check
- [assemblies/03 — Interference detection](../assemblies/03-interference-detection.md) — an interference volume checked against a lens
