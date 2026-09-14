---
id: sketches-07-equation-driven-curve
title: Draw an equation driven curve, and drive it from globals
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [ISketchManager.CreateEquationSpline2, ISketchSpline.GetPoints2, ISketchSegment.GetType, ISketch.GetSketchPoints2, IModelDoc2.SketchAddConstraints]
keywords: [equation driven curve, CreateEquationSpline2, parametric curve, involute, x(t) y(t), radians, degrees, 180/pi, refused, returns None, global variable in curve, GetPoints2, sgFIXED, curve end point, spline]
answers: "How do I draw an equation driven curve from code, and make it follow global variables?"
---

# Draw an equation driven curve, and drive it from globals

## What this is for

Some profiles are formulae: an involute gear flank, a spiral, a cam. An
Equation Driven Curve is a sketch spline defined by `x(t)` and `y(t)` over a
range of `t`, and when its expressions name global variables it follows them
on a rebuild, so the curve stays parametric instead of being a frozen point
list. This entry is how to make one from code, what units its expressions are
in, which spellings SolidWorks refuses, and how to tie its ends to the rest of
a sketch.

## The call

`CreateEquationSpline2` is on **ISketchManager**, with a sketch open. The
arguments as they ran, from the tool whose gear build used it:

```python
def sketch_equation_curve(self, handle: str, x: str, y: str, t1: str, t2: str,
                          start: Tuple[float, float], end: Tuple[float, float]) -> None:
    """``CreateEquationSpline2`` with both ends locked, checked against where it should end.

    Probe curve_literal: the expressions are in document units with radian
    trig, and a broken expression makes nothing and raises nothing, which
    is why the ends are checked rather than the call trusted.
    """
    entity = call(self._manager(), "CreateEquationSpline2", x, y, "", t1, t2, False, 0.0, 0.0, 0.0, True, True)
    if entity is None:
        raise SolidWorksError(f"SolidWorks would not make the curve {handle} from x = {x}, y = {y}.")
    points = list(call(entity, "GetPoints2") or ())
    if not points:
        raise SolidWorksError(f"The curve {handle} has no points.")
    got = [(float(call(p, "X")) * MM_PER_METRE, float(call(p, "Y")) * MM_PER_METRE) for p in (points[0], points[-1])]
    for label, wanted, found in (("start", start, got[0]), ("end", end, got[1])):
        if math.hypot(wanted[0] - found[0], wanted[1] - found[1]) > 1e-3:
            raise SolidWorksError(
                f"The curve {handle} {label}s at ({found[0]:.4f}, {found[1]:.4f}) mm, not at "
                f"({wanted[0]:.4f}, {wanted[1]:.4f}) mm."
            )
    # Its ends as sketch points, captured now: a line drawn later from the same
    # spot has a point there too, and found by location afterwards the line's
    # point came back and the relation selected one point twice (2026).
    self._handles[handle] = {"entity": entity, "start": self._point_at(start), "end": self._point_at(end),
                             "curve": True}
```

The eleven arguments, exactly as passed in every call here:
`(x, y, "", t1, t2, False, 0.0, 0.0, 0.0, True, True)`. The expressions and
the range are **strings**. The probe calls the last two `lock` and also ran
`False, False` once. What the third string, the `False` and the three zeros
control was not explored, so this entry does not name them.

It returns the new segment (`ISketchSegment.GetType` 3, a spline), or `None`.
`ISketchSpline.GetPoints2` returns the spline's points as **sketch point
objects** with `X` and `Y` in metres, not a flat array of doubles; the code
above reads each end through its `X` and `Y`.

A real call from the gear build, the upper involute flank:

```python
x = '"Base Radius" * ( cos ( t + "Flank Offset" ) + t * sin ( t + "Flank Offset" ) )'
y = '"Base Radius" * ( sin ( t + "Flank Offset" ) - t * cos ( t + "Flank Offset" ) )'
# t1 = '"Involute t1"', t2 = '"Involute t2"'
```

## Units

- **Expressions are in document units.** `x = t`, `y = 2*t`, `t` from 0 to 10,
  in a millimetre part, ended at (10.0, 20.0) mm.
- **Trig is radians, even in a part whose equations are degrees.**
  `10*cos(t)`, `10*sin(t)` from 0 to `pi/2` ended at (0, 10) mm. In the gear
  build, the part's Equation Manager measured degrees (`sin ( 90 )` = 1), and
  the flank curves above, whose arguments are radians, still started and ended
  within 1e-3 mm of where the radian maths put them.
- `pi` is accepted in expressions and in the range.

## Why it is not obvious

**A degree conversion inside the expression is refused.** In a part where
`cos ( t )` is radians, the obvious move for someone who wants degrees is
`cos ( t * 180 / pi )`. Every spelling of that tried returned `None` and made
no curve:

| x expression (y the same with sin) | Made |
|---|---|
| `10*cos(t)` | yes |
| `10 * cos ( t )` | yes |
| `10*cos((t+0.1)-0.1)` | yes |
| `10*cos(t*180/pi)` | **no, `None`** |
| `10 * cos ( t * 180 / pi )` | **no, `None`** |
| `10*cos((t)*180/pi)` | **no, `None`** |

Brackets and spaces are fine; it is the `*180/pi` inside the trig argument
that was refused. So keep curve trig in radians and convert any degree-valued
global in its own equation (`"Pressure Angle" * pi / 180` was accepted in the
Equation Manager).

**A refusal is silent.** A broken expression, `x = "t *"`, returned `None`:
no exception, no dialog, and the next call ran normally. Hence the end check in
the code above: a `None` is easy to test, but a curve made from the wrong
formula is only caught by looking at where it ends.

**Quoted global names work, in the expressions and in the range.**
`"Probe R" * cos ( t )` with `"Probe R"= 10`, and a range ending at
`"Probe End"` with `"Probe End"= pi / 2`, each made the curve and it ended at
(0, 10) mm.

**It follows the global on a rebuild.** With a curve over `"Probe R"`,
changing the global from 10 to 20 (indexed put of `Equation(i)`, see
[equations/01](../equations/01-global-variables-from-code.md)) and calling
`ForceRebuild3(False)` left the sketch still holding one spline, now ending at
(0, 20) mm.

**A locked curve does not make its sketch fully defined; `sgFIXED` does, and
it still follows.** A sketch holding only one such curve read
`GetConstrainedStatus` 2 (under-defined), with the last two arguments `True`
and with them `False`. After `sgFIXED` on the curve it read 3 (fully defined).
A fixed curve over globals, after its global went 10 to 20 and a rebuild, still
ended at (0, 20) mm and still read 3. See
[sketches/10](10-is-the-sketch-fully-defined.md).

## Relating the curve's ends to other geometry

A curve has no `GetStartPoint2`. Its ends are sketch points, found in
`ISketch.GetSketchPoints2` by location:

```python
def _point_at(self, where: Tuple[float, float]) -> Any:
    """A curve's end as a sketch point, found by where it is (probe curve_relations)."""
    sketch = self._handles[self._open_sketch]["sketch"]
    best, distance = None, 1e-3
    for point in call(sketch, "GetSketchPoints2") or ():
        gap = math.hypot(float(call(point, "X")) * MM_PER_METRE - where[0],
                         float(call(point, "Y")) * MM_PER_METRE - where[1])
        if gap <= distance:
            best, distance = point, gap
    if best is None:
        raise SolidWorksError(f"No sketch point sits at ({where[0]:.4f}, {where[1]:.4f}) mm.")
    return best
```

Two observed behaviours decide how to use it:

- **A point found that way relates normally.** A free line's end made
  `sgCOINCIDENT` with the curve's start point moved onto (10.0, 0.0) mm, and
  the locked curve did not move to meet it.
- **An entity drawn starting exactly on the curve's end shares that point.**
  With a line created starting at the curve's start, there was still exactly
  one sketch point there, and `line.GetStartPoint2()` compared equal to the
  captured curve point. Selecting that point and then the line's start put
  **one** object in the selection. So a coincident relation between them is
  already true and cannot even be selected; skip it. The same held for a line
  started on another line's end and on an arc's end; a line started 0.1 mm away
  got its own point. Capture the curve's end points **before** drawing
  anything else there, as the code above does, or a search by location may hand
  back the line's end instead.

A selection helper that judges each pick by `GetSelectedObjectCount2` going up,
as the helpers in this collection do, reports that shared point as a failed
selection on the second pick: the count stays at 1. So compare the two point
objects first and skip the relation when they are the same one. The tool's
relation call:

```python
def add_relation(self, kind: str, refs: Sequence[str]) -> str:
    """``SketchAddConstraints`` on the selection; constants from :data:`findings.RELATIONS`.

    Returns a note when there was nothing to add. An entity drawn starting
    exactly on another's end does not get a point of its own there: on
    SolidWorks 2026 a line started on a curve's end had that very point as
    its start, and so did a line started on a line's or an arc's end
    (probe curve_end_points). Coincident with itself already holds, and
    selecting the one point a second time adds nothing to the selection —
    the count stays at 1 — so the relation is skipped, not attempted.
    """
    constant = findings.RELATIONS.get(kind)
    if constant is None:
        raise SolidWorksError(f"There is no verified constant for a {kind} relation.")
    if kind == "coincident" and len(refs) == 2:
        a, b = self._entity(refs[0]), self._entity(refs[1])
        try:
            same = bool(a == b)
        except Exception:  # noqa: BLE001 - objects that will not compare are not the same one
            same = False
        if same:
            return "already one point"
    self._select_entities(refs)
    call(self._doc(), "SketchAddConstraints", constant)
    call(self._doc(), "ClearSelection2", True)
    return ""
```

`a == b` on the two pywin32 objects was `True` for the shared point in the probe
(`line_start_is_the_curve_start_point`), and `False` for a line started 0.1 mm
away.

## What it does not do

- The meaning of arguments 3 and 6 to 9 is not established.
- Only `x(t)`, `y(t)` curves in a 2D sketch were made; no explicit `y(x)`
  form and no 3D sketch.
- Degree conversion was only tried inside the trig argument of the curve's own
  expressions. Whether a degree-valued global divided down in the range
  expression is accepted was not tried.
- `sgFIXED` was verified on a whole curve. Relations on a curve's interior, or
  tangency to its ends, were not tried; see
  [sketches/06](06-what-the-sketch-api-cannot-do.md) for the general spline
  limits.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426: probes `curve_literal`, `curve_globals`, `curve_rebuild`,
`curve_relations`, `curve_bad`, `curve_fixed_rebuild` and `curve_end_points` in
[`code/python/gear_generator/probe/p1_curves.py`](../../code/python/gear_generator/probe/p1_curves.py),
all passed. Every sketch was on the first reference plane of a millimetre part.

- `straight_curve_ends_mm = ((0.0, 0.0), (10.0, 20.0))`; segment type 3.
- `quarter_circle_ends_mm` end (1.2e-10, 10.000000000000002).
- `curve_spellings`: the three `*180/pi` spellings `made: False`; spaces and a
  bracketed sum `made: True`.
- `curve_globals`: name in expression, name in range and both, each ending at
  (≈0, 10.0).
- `ends_after_global_change_mm` ending at (-7.7e-15, 20.0).
- Statuses: one locked curve 2; after `sgFIXED` 3; one unlocked curve 2; fixed
  curve over globals 3, and 3 again after the rebuild with its end at (≈0, 20.0).
- `curve_relations`: line end after the relation (9.99999999999999, -1.6e-10);
  curve start unmoved.
- `broken_expression_makes = 'nothing'`.
- `curve_end_points`: 1 point at the curve start before and after the line;
  `line_start_is_the_curve_start_point` True; selecting it twice left 1
  selected; 1 point at a shared line end and at a shared arc end;
  `a_line_0_1_mm_away_is_its_own_point` True.
- `end_to_end`: the gear's two involute curves built, fixed, related through
  their captured ends, and the sketch read fully defined before and after the
  teeth and module changed.

## See also

- [equations/01 — Global variables from code](../equations/01-global-variables-from-code.md) — the globals the curve names, and why their trig is degrees
- [sketches/10 — Is the sketch fully defined](10-is-the-sketch-fully-defined.md)
- [sketches/03 — Relation constants](03-relation-constants.md) — `sgFIXED` and `sgCOINCIDENT`
- [sketches/01 — A 2D sketch as VBA](01-2d-sketch-as-vba.md) — ends drawn on ends
- [sketches/06 — What the sketch API cannot do](06-what-the-sketch-api-cannot-do.md)
- [GOTCHAS §29, §35](../../GOTCHAS.md)
