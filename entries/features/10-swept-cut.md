---
id: features-10-swept-cut
title: Cut a sketch along a path with a twist, and pattern the cut
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertCutSwept5, IFeature.GetDefinition, IFeature.ModifyDefinition, ISweepFeatureData.D1ReverseTwistDir, IFeatureManager.FeatureCircularPattern5, IPartDoc.GetBodies2]
keywords: [InsertCutSwept5, InsertCutSwept4, swept cut, cut sweep, SweepCut, twisted cut, helical cut, helical tooth space, helical gear, constant twist along path, twist, radians, overrun, lead-in, start ahead of the face, pattern of sweeps, geometry pattern, FeatureCircularPattern5, rebuild time, 22 arguments, selection mark 1, mark 4]
answers: "How do I cut a profile along a straight path with a twist from code, such as a helical gear's tooth space, and pattern that cut around an axis?"
---

# Cut a sketch along a path with a twist, and pattern the cut

## What this is for

A helical gear's tooth spaces: the spur gear's transverse section cut along
the gear's axis while turning uniformly, then repeated around the axis. From
code that is a swept cut, `InsertCutSwept5`, with the same twist control as a
swept boss ([features/09](09-twisted-sweep.md)), and a circular pattern of it.
This entry gives the call that ran, where the cut has to start and end to
remove exactly its volume, and how a pattern of such cuts behaved.

## The call

The tool's form, written from the probes:

```python
def sweep_cut(self, handle: str, profile: str, path: str, name: str, twist: float, reverse: bool,
              twist_dim: str) -> Tuple[str, str]:
    """A cut of one sketch along another with a constant twist; returns ``(name, twist dimension)``.

    Probes sweep_twist and sweep_cut_ends, SolidWorks 2026: profile at mark
    1, path at mark 4, ``InsertCutSwept5`` with twist control 8 and the twist
    in radians. A negative twist turned the same way as a positive one, so
    the other hand is ``D1ReverseTwistDir`` set through the feature's
    definition. The twist was the sweep's one dimension; it is named before
    the definition is touched.
    """
    doc = self._doc()
    self._select_feature(self._sketch_feature(profile), mark=findings.SWEEP_PROFILE_MARK)
    self._select_feature(self._sketch_feature(path), append=True, mark=findings.SWEEP_PATH_MARK)
    angle = math.radians(twist)
    before = self._top_names()
    made = call(call(doc, "FeatureManager"), "InsertCutSwept5", False, False, findings.TWIST_CONSTANT_ALONG_PATH,
                False, False, 0, 0, False, 0.0, 0.0, 0, 0, True, True, angle, True, False, False, False, False,
                0.0, findings.SWEEP_DIRECTION)
    call(doc, "ClearSelection2", True)
    if made is None:
        raise SolidWorksError(f"InsertCutSwept5 would not sweep {profile} along {path} as {name}.")
    feature = self._made_since(before, "SweepCut", "InsertCutSwept5")
    kept = self._rename(feature, name)
    dim = self._name_dimension_by_value(feature, angle, twist_dim, "twist")
    if reverse:
        data = call(feature, "GetDefinition")
        if data is None or not call(data, "AccessSelections", doc, _null_dispatch()):
            raise SolidWorksError(f"The definition of {kept} ({_type_name(feature)}) could not be opened to "
                                  "reverse its twist.")
        data.D1ReverseTwistDir = True
        if not call(feature, "ModifyDefinition", data, doc, _null_dispatch()):
            raise SolidWorksError(f"ModifyDefinition would not reverse the twist of {kept} ({_type_name(feature)}).")
    self._handles[handle] = {"feature": kept}
    return kept, dim
```

`findings.SWEEP_PROFILE_MARK = 1`, `findings.SWEEP_PATH_MARK = 4`,
`findings.TWIST_CONSTANT_ALONG_PATH = 8` (`swTwistControlType_e`),
`findings.SWEEP_DIRECTION = 0` (`swSweepDirection_e.swSweepDirection1`). The
tool's `twist` is in degrees and converted; the call takes **radians**.

Interfaces and arguments:

- `InsertCutSwept5` is on **IFeatureManager**, twenty-two arguments (arity from
  `sldworks.tlb`). As in the boss form, the **third** is the twist control and
  the **fifteenth** the twist angle; the **last** is the sweep direction. The
  rest were passed as shown and not varied.
- It returns the feature, `GetTypeName2` `"SweepCut"`, or `None`.
- The twist sign is ignored here as in the boss; the other hand is
  `D1ReverseTwistDir` on **ISweepFeatureData**, committed with
  **IFeature** `ModifyDefinition` ([features/09](09-twisted-sweep.md),
  [GOTCHAS §45](../../GOTCHAS.md)).

## Where the cut starts and ends

Probe `sweep_cut_ends` in
[`p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py) cut a
circle r 3 mm, 12 mm off the axis, through a blank r 20 × 10 mm with 30° of
twist per 10 mm, starting on the blank's face and starting 2 mm ahead of it on
an offset plane ([features/08](08-offset-reference-plane.md)):

```python
def cut_sweep_args(twist: float) -> Tuple[Any, ...]:
    """InsertCutSwept5's 22 arguments: constant twist along path, direction 1."""
    return (False, False, TWIST_CONSTANT_ALONG_PATH, False, False, 0, 0, False, 0.0, 0.0, 0, 0,
            True, True, twist, True, False, False, False, False, 0.0, SWEEP_DIRECTION_1)
```

```python
for label, start, end in (("flush", 0.0, BLANK_W * ez), ("overrun", -2.0 * ez, (BLANK_W + 2.0) * ez)):
    for call_name, args in (("InsertCutSwept5", cut_sweep_args), ("InsertCutSwept4", cut_sweep4_args)):
        with sc.quiet_dimensions(px), sc.scratch(px) as doc:
            _blank(px, doc)
            blank_volume = _volume(doc)
            plane = sc.planes(doc)[0] if abs(start) < 1e-12 else offset_plane(px, doc, abs(start),
                                                                              flip_for_side(px, math.copysign(1, start)))
            profile = circle_sketch(px, doc, plane, "Probe Profile", (PROFILE_X, 0.0, start))
            path = path_sketch(px, doc, "Probe Path", start, end)
            px.require(select_profile_and_path(doc, profile, path), "profile and path select")
            before = sc.feature_names(doc)
            twist = math.radians(30.0) * abs(end - start) / BLANK_W
            got, result = px.attempt(f"{label}: {call_name}",
                                     lambda: call(sc.feature_manager(doc), call_name, *args(twist)))
            call(doc, "ClearSelection2", True)
            feature = made_feature(doc, before, result) if got else None
            if feature is None:
                px.note(f"{label}: {call_name} made nothing")
                continue
            removed = blank_volume - _volume(doc)
            bodies = solid_bodies(doc)
            px.fact(f"cut_{label}_{call_name}", {"removed_mm3": round(removed, 4), "bodies": bodies,
                                                 "type": sc.type_name(feature)})
            if near(removed, removed_expected, 1e-3 * removed_expected) and bodies == 1:
                working.append(f"{label} {call_name}")
                break
```

`ez` is +1: the blank grows toward +Z ([features/01](01-boss-extrude.md)).
The cut must remove A·w = π × 3² × 10 = 282.743 mm³ and leave one body:

| Start | Path | Removed mm³ | Bodies |
|---|---|---|---|
| **flush**, on the Front plane (the blank's face) | z 0 → 10 | 282.7012 | 1 |
| **overrun**, 2 mm ahead on an offset plane | z −2 → 12 | 282.7428 | 1 |

Both cut, but the overrun cut came about eighty times closer to A·w, so the
tool starts a helical gear's tooth space a module ahead of one face and runs it
a module past the other (`SWEEP_ENDS = "overrun"`; a herringbone's half runs
from its mid plane to a module past the face).

## A pattern of swept cuts

Probe `pattern_sweep` patterned an overrun seed cut about an axis with
`FeatureCircularPattern5` exactly as in
[features/03](03-circular-pattern.md), varying the **fifth argument** (the
probe's geometry-pattern flag) and the count:

```python
made = call(sc.feature_manager(doc), "FeatureCircularPattern5", count, 2 * math.pi, False, "NULL",
            geometry, True, False, False, False, False, 1, 0.0, "NULL", False)
call(doc, "ClearSelection2", True)
px.require(made is not None, f"the pattern of {count} is made (geometry pattern {geometry})")
started = time.monotonic()
call(doc, "ForceRebuild3", False)
seconds = time.monotonic() - started
```

| Pattern | Volume mm³ | Expected | Bodies | `ForceRebuild3` s |
|---|---|---|---|---|
| 6 × r 3, fifth argument `False` | 10869.773 | 10869.911 | 1 | 0.119 |
| 24 × r 1, `False` | 11812.486 | 11812.388 | 1 | 0.375 |
| 24 × r 1, `True` | 11812.486 | 11812.388 | 1 | 0.201 |

The fifth argument made no difference to the solid and roughly halved the
rebuild. The tool leaves it `False`. The times are one timing each (run
20260915-002812 gave 0.121, 0.38 and 0.201).

## Why it is not obvious

**Start the cut outside the part.** A cut that starts exactly on the face
removed 282.7012 against 282.7428 for one that starts 2 mm ahead, and both
left one body. Nothing reports the difference; only the volume shows it.

**The twist is per path, not per part.** A cut that starts ahead of the face
is longer than the part, so its twist has to be scaled by path length over
face width (`twist = 30° × |end − start| / w` above), or the part's own
helix angle comes out wrong.

**A patterned spline cut weighs close, not exact.** 10869.773 against
10869.911, 1.3e-5 of the blank, where plain extruded holes matched to every
printed digit. Compare against a tolerance ([GOTCHAS §48](../../GOTCHAS.md)).

## What it does not do

- `IFeatureManager.InsertCutSwept4` (19 arguments) was the probe's fallback and
  was never called, because `InsertCutSwept5` worked first each time.
- Only straight paths along Z, and circular or gear-tooth profiles, were cut.
- What the fifth argument of `FeatureCircularPattern5` is named, beyond the
  probe calling it geometry pattern, was not checked against the help.
- Rebuild time was measured once per case on one machine.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probes
`sweep_cut_ends` and `pattern_sweep` in
[`p2_helical.py`](../../code/python/gear_generator/probe/p2_helical.py), run
20260915-002812 (43 of 43 passed) and run 20260915-023929 (54 of 54), same
volumes in both;
[`probe-results-20260915-023929.txt`](../../code/python/gear_generator/probe/results/probe-results-20260915-023929.txt).

- `flush: InsertCutSwept5: returned 'CDispatch'`;
  `cut_flush_InsertCutSwept5 = {'removed_mm3': 282.7012, 'bodies': 1, 'type': 'SweepCut'}`.
- `overrun: InsertCutSwept5: returned 'CDispatch'`;
  `cut_overrun_InsertCutSwept5 = {'removed_mm3': 282.7428, 'bodies': 1, 'type': 'SweepCut'}`;
  `sweep_cut_routes = ['flush InsertCutSwept5', 'overrun InsertCutSwept5']`.
- "6 r3 geometry False: 10869.773 mm³ (expected 10869.911)"; "24 r1 geometry
  False: 11812.486 mm³ (expected 11812.388)"; "24 r1 geometry True: 11812.486 mm³
  (expected 11812.388)"; "one body" for each;
  `pattern_sweep_rebuild_seconds = {'6 r3 geometry False': 0.119, '24 r1 geometry False': 0.375, '24 r1 geometry True': 0.201}`.
- Probe `helical_end_to_end` in
  [`p5_helical.py`](../../code/python/gear_generator/probe/p5_helical.py), same
  runs (figures below from 20260915-023929), built a module 2, 13-tooth, 20° right-hand helical gear through
  `Session.sweep_cut` and a circular pattern, and checked it three ways:
  - **Twin.** The same plan with the swept cut replaced by a straight cut of the
    same section weighed the same:
    `helical_volumes_mm3 = {'part': 4961.164729903562, 'twin': 4961.0349155102585}`.
  - **Hand.** With the pattern left out, one right-hand space pulled the part's
    centre of mass to −y and one left-hand space (`D1ReverseTwistDir`) to +y:
    `helical_right_one_space_centre_mm = (-0.35200349853589447, -0.06554931397919656, 4.999985102287864)`,
    `helical_left_one_space_centre_mm = (-0.3520003644086587, 0.06557239075310127, 4.999984857178944)`.
  - **Equations.** `"Helix Angle"` and `"Face Width"` changed through the tool,
    one rebuild, every sketch still fully defined, and the part then weighed
    what a fresh twin of the new gear did:
    `helical_volumes_after_mm3 = {'part': 6481.694787714392, 'twin': 6479.807712032862}`.

## See also

- [features/09 — Twisted sweep](09-twisted-sweep.md) — the twist's units, sense and reverse flag
- [features/03 — Circular pattern](03-circular-pattern.md) — the pattern call
- [features/02 — Cut extrude](02-cut-extrude.md) — the straight cut used as the twin
- [features/08 — Offset reference plane](08-offset-reference-plane.md) — the plane ahead of the face
- [features/11 — Mirror a body](11-mirror-body.md) — a herringbone from a helical half
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md) — the twin and the one-space centre of mass
- [GOTCHAS §45, §48](../../GOTCHAS.md)
