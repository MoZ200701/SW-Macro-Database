---
id: features-09-twisted-sweep
title: Sweep a profile along a line with a constant twist, and choose which way it turns
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertProtrusionSwept4, IFeature.GetDefinition, IFeature.ModifyDefinition, ISweepFeatureData.D1ReverseTwistDir, ISweepFeatureData.TwistControlType, ISweepFeatureData.GetTwistAngle, ISweepFeatureData.AccessSelections, IMassProperty.CenterOfMass]
keywords: [InsertProtrusionSwept4, sweep, swept boss, twist, constant twist along path, swTwistControlConstantTwistAlongPath, 8, twist angle, radians, D1ReverseTwistDir, reverse twist, twist direction, helix, helicoid, right-hand, left-hand, helical gear, ModifyDefinition, GetDefinition, AccessSelections, selection mark 1, mark 4, Sweep, D4, 20 arguments]
answers: "How do I sweep a sketch along a straight path with a constant twist from code, drive the twist from a global, and make it turn the other way?"
---

# Sweep a profile along a line with a constant twist, and choose which way it turns

## What this is for

A section that turns uniformly as it travels along a straight line makes an
exact helicoid: a helical gear's tooth, a twisted bar, a screw thread's form. A
loft between two turned sections only approximates it. From code it is a sweep
with the twist control set to "constant twist along path", which raises three
questions this entry answers from a live run: what unit the twist is in, which
way a positive twist turns, and how to get the other hand.

## The call

Select the closed profile sketch at mark **1** and the path sketch at mark
**4**, then call **IFeatureManager** `InsertProtrusionSwept4` with twenty
arguments. From the probe,
[`code/python/gear_generator/probe/p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py):

```python
# swTwistControlType_e
TWIST_CONSTANT_ALONG_PATH = 8
# swSweepDirection_e
SWEEP_DIRECTION_1 = 0

def select_profile_and_path(doc: Any, profile: str, path: str) -> bool:
    return (sc.select_feature(doc, sc.feature_by_name(doc, profile), append=False, mark=1)
            and sc.select_feature(doc, sc.feature_by_name(doc, path), append=True, mark=4))


def boss_sweep_args(twist: float) -> Tuple[Any, ...]:
    """InsertProtrusionSwept4's 20 arguments: constant twist along path, merged, direction 1."""
    return (False, False, TWIST_CONSTANT_ALONG_PATH, False, False, 0, 0, False, 0.0, 0.0, 0, 0,
            True, True, True, twist, True, False, 0.0, SWEEP_DIRECTION_1)
```

```python
def _sweep_boss(px: Px, doc: Any, label: str, z0: float, z1: float, twist: float) -> Tuple[Optional[Any], str, str]:
    """A circle at (12, 0) on a plane at z0, swept to z1 with a twist; returns the feature and the two sketches."""
    if abs(z0) < 1e-12:
        plane = sc.planes(doc)[0]
    else:
        plane = offset_plane(px, doc, abs(z0), flip_for_side(px, 1.0 if z0 > 0 else -1.0))
    profile = circle_sketch(px, doc, plane, f"{label} Profile", (PROFILE_X, 0.0, z0))
    path = path_sketch(px, doc, f"{label} Path", z0, z1)
    px.require(select_profile_and_path(doc, profile, path), "the profile selects at mark 1 and the path at mark 4")
    before = sc.feature_names(doc)
    got, result = px.attempt(f"{label}: InsertProtrusionSwept4(twist {twist:.6f})",
                             lambda: call(sc.feature_manager(doc), "InsertProtrusionSwept4", *boss_sweep_args(twist)))
    call(doc, "ClearSelection2", True)
    return (made_feature(doc, before, result) if got else None), profile, path
```

What this entry claims about the twenty arguments is only what was varied or
read back: the **third** is the twist control type (`8`, read back as
`TwistControlType` 8), the **sixteenth** is the twist angle in **radians**
(read back by `GetTwistAngle`), and the **twentieth** is the sweep direction
(`0`). The rest were passed as shown and never changed. The arity was read from
`sldworks.tlb`. It returns the feature, `GetTypeName2` `"Sweep"`.

The path is a single line on the Top plane, along the model Z axis, drawn
through the sketch's own transform so it lands on Z whatever the Top sketch's
axes are ([reading/05](../reading/05-sketch-to-model-transform.md)):

```python
def path_sketch(px: Px, doc: Any, name: str, z0_mm: float, z1_mm: float, plane_index: int = 2) -> str:
    """A line on the axis from model z0 to z1, drawn on the Nth plane (Top) through its transform."""
    before = sc.feature_names(doc)
    manager = open_on(px, doc, sc.planes(doc)[plane_index - 1])
    data = model_to_sketch(call(manager, "ActiveSketch"))
    a = transform_point(data, (0.0, 0.0, sc.m(z0_mm)))
    b = transform_point(data, (0.0, 0.0, sc.m(z1_mm)))
    call(manager, "CreateLine", a[0], a[1], 0.0, b[0], b[1], 0.0)
    return sc.close_sketch(px, doc, name, before)
```

### The other hand: `D1ReverseTwistDir`

The sign of the twist argument does not choose the direction (below). The
direction is a flag on the feature's definition. From the same probe:

```python
data = call(feature, "GetDefinition")
px.fact("sweep_definition_type", type(data).__name__ if data is not None else None)
if data is not None:
    got, reverse = px.attempt("D1ReverseTwistDir", lambda: call(data, "D1ReverseTwistDir"))
    px.fact("d1_reverse_twist_dir_default", reverse if got else None)
    px.fact("twist_control_type_read", call(data, "TwistControlType"))
    px.fact("twist_angle_read", call(data, "GetTwistAngle"))
    ok = call(data, "AccessSelections", doc, sc.null())
    data.D1ReverseTwistDir = True
    got, modified = px.attempt("ModifyDefinition with D1ReverseTwistDir True",
                               lambda: call(feature, "ModifyDefinition", data, doc, sc.null()))
```

- `GetDefinition` and `ModifyDefinition(data, doc, component)` are on
  **IFeature**; the component is a typed null dispatch (`sc.null()`), since this
  is a part.
- `D1ReverseTwistDir`, `TwistControlType`, `GetTwistAngle` and
  `AccessSelections(doc, component)` are on the definition object,
  **ISweepFeatureData**. `D1ReverseTwistDir` is set by plain attribute
  assignment after `AccessSelections`.

The tool does the same on a swept cut, straight after making it
([features/10](10-swept-cut.md)).

## What a twist does, measured

The probe section is a circle r 3 mm centred 12 mm from the Z axis, swept
20 mm along Z with a quarter turn. The geometry is the oracle
([reading/10](../reading/10-mass-properties-as-an-oracle.md)):

- **Volume.** A section swept along a line encloses A·L whatever the twist
  (Cavalieri): π × 3² × 20 = 565.487 mm³.
- **Centre of mass.** A circle at r0 from the axis turned uniformly through θ
  has its centroid at `r0·(sin θ/θ, ±(1 − cos θ)/θ)`, (7.6394, ±7.6394) mm for a
  quarter turn. The size says the twist was uniform and in the unit given; the
  sign of y says which way it went.

| Case | Twist argument | Volume mm³ | Centre of mass mm |
|---|---|---|---|
| profile at z 0, path toward +Z | +π/2 | 565.6302 | (7.6397, **+7.6400**, 9.9998) |
| profile at z 0, path toward +Z | **−π/2** | 565.6302 | (7.6397, **+7.6400**, 9.9998) — identical |
| profile at z 20, path toward −Z | +π/2 | 565.6292 | (7.6397, **−7.6400**, 10.0002) |
| the first, then `D1ReverseTwistDir = True` | +π/2 | — | (7.6396, **−7.6401**, 9.9997) |

Read as the section moving toward **larger z**, the first and third cases both
turn **counter-clockwise about +Z**: a **right-hand** helix, whichever way the
path runs. The negative argument changed nothing. `D1ReverseTwistDir` turned it
the other way: a left-hand helix.

### Driving the twist from a global

The sweep's only dimension is the twist: `sweep_dimensions = [('D4',
1.5707963267948966)]`, in radians. Found by value, renamed `Twist`, and linked
to a global of 45 in probe `sweep_twist_link`:

```python
twist = [d for d in sc.dimensions_of(feature) if near(abs(float(call(d, "SystemValue"))), QUARTER, 1e-9)]
px.require(len(twist) == 1, "exactly one dimension of the sweep has the twist's value")
twist[0].Name = "Twist"
px.check(str(call(twist[0], "Name")) == "Twist", "the twist dimension renames to Twist")
eqm = call(doc, "GetEquationMgr")
add = adder(px, eqm)
px.require(add('"Probe T"= 45') >= 0, "a twist global is added")
link = add('"Twist@Probe Sweep"= "Probe T"')
px.require(link >= 0, 'the link "Twist@Probe Sweep"= "Probe T" is accepted')
```

The dimension's `SystemValue` then read 0.78539816339745: a global of 45 is
**45 degrees** on the dimension (in a part whose Equation Manager trig is
degrees), though the call took radians. The centre of mass moved to
(10.8063, 4.4742) mm against (10.8038, 4.4751) for an eighth of a turn.

## Why it is not obvious

**The sign of the twist is ignored.** −π/2 made exactly the same solid as +π/2,
to every digit of the centre of mass. A left-hand helix made by negating the
angle is silently a right-hand one. See [GOTCHAS §45](../../GOTCHAS.md).

**Radians in the call, degrees through a global.** The argument and
`GetTwistAngle` are radians; the linked dimension took 45 as 45°. Pass
radians, link degrees.

**It is not exact to sixteen figures.** A twisted sweep is a spline surface.
The volume came out 565.630 against 565.487 (2.5e-4) and the centroid within a
few µm, where a straight extrusion matches π r² w to sixteen figures. Compare
with a relative tolerance ([GOTCHAS §48](../../GOTCHAS.md)).

## What it does not do

- Only a straight path along Z, a circular profile, and constant twist along
  path were tried. Other twist controls (follow path, normal constant), curved
  paths, guide curves and thin sweeps were not.
- Whether the handedness depends on which plane the path is sketched on, or on
  a path not along Z, was not tested; measure it the same way.
- A helix curve (`InsertHelix`) as a guide was the plan's fallback and was never
  needed, so it was not probed.
- The other members of `ISweepFeatureData`, and whether `ReleaseSelectionAccess`
  should follow `AccessSelections`, were not examined; the probe did not call it.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probes `sweep_twist` and
`sweep_twist_link` in
[`p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py), with
the same facts in run 20260915-002812 (43 of 43 probes passed) and run
20260915-023929 (54 of 54);
[`probe-results-20260915-023929.txt`](../../code/python/gear_generator/probe/results/probe-results-20260915-023929.txt).

- "the profile selects at mark 1 and the path at mark 4";
  `Probe Sweep: InsertProtrusionSwept4(twist 1.570796): returned 'CDispatch'`.
- `sweep_plus_z = {'volume_mm3': 565.6302, 'centre_mm': (7.6396655197851375, 7.640049472730133, 9.999796332411384), 'type': 'Sweep'}`.
- `sweep_negative_angle = {'volume_mm3': 565.6302, 'centre_mm': (7.6396655197851375, 7.640049472730133, 9.999796332411384), 'type': 'Sweep'}`.
- `sweep_minus_z = {'volume_mm3': 565.6292, 'centre_mm': (7.639677113760393, -7.640047373419246, 10.000214542182736), 'type': 'Sweep'}`;
  "+1 is counter-clockwise about +Z, travelling toward +Z, i.e. a right-hand helix".
- `sweep_dimensions = [('D4', 1.5707963267948966)]`; `twist_dim_default_name = 'D4'`.
- `d1_reverse_twist_dir_default = False`; `twist_control_type_read = 8`;
  `twist_angle_read = 1.5707963267948966`; `modify_definition_access = True`;
  `ModifyDefinition with D1ReverseTwistDir True: returned True`;
  `centre_after_reverse_mm = (7.639557038359682, -7.640143904440734, 9.99973592335336)`;
  `reverse_flag_flips = True`.
- `sweep_twist_link`: "the twist dimension renames to Twist"; "the link
  "Twist@Probe Sweep"= "Probe T" is accepted";
  `twist_system_value_after_link = 0.78539816339745`;
  `centre_at_45_mm = (10.806295537686967, 4.474164561419678, 9.99874723794811)`.

## See also

- [features/10 — Swept cut](10-swept-cut.md) — the same twist as a cut, as the tool builds a helical tooth space
- [features/08 — Offset reference plane](08-offset-reference-plane.md) — the plane the profile sits on
- [features/11 — Mirror a body](11-mirror-body.md) — two opposite halves, for a herringbone
- [features/07 — Loft cut](07-loft-cut.md) — a section that changes size rather than turns
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md) — the centroid of a twist
- [reading/05 — Sketch to model transform](../reading/05-sketch-to-model-transform.md) — drawing the path on Z
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [features/04 — Read a feature's dimensions](04-read-a-features-dimensions.md) — finding `D4` by value
- [GOTCHAS §45, §48](../../GOTCHAS.md)
