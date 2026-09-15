---
id: assemblies-04-mates-between-non-parallel-axes
title: Place and mate components whose axes are not parallel
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [ISldWorks.GetMathUtility, IMathUtility.CreateTransform, IComponent2.Transform2, IComponent2.GetModelDoc2, IModelDocExtension.SelectByID2, IAssemblyDoc.AddMate5]
keywords: [CreateTransform, IMathUtility, GetMathUtility, DISP_E_MEMBERNOTFOUND, Member not found, DISPATCH_METHOD, VT_ARRAY, VT_R8, Transform2 put, set component transform, component frame, rotation by columns, AddComponent5 offset, component origin, Point1@Origin, EXTSKETCHPOINT, angle mate between axes, point to plane distance mate, Flip, mate flip, negative side, bevel gears, crossed helical, skew axes, shaft angle]
answers: "How do I set a component's rotation from code and mate it so its axis sits at an angle to another component's, such as bevel or crossed gears?"
---

# Place and mate components whose axes are not parallel

## What this is for

Bevel gears, whose axes meet at a shaft angle, and crossed helical gears, whose
axes pass each other at one, cannot be mated the way a parallel pair is
([assemblies/02](02-mates-from-code.md)). A component inserted with
`AddComponent5` arrives unrotated, and a mate solver asked to swing it through
70° may satisfy the mates the other way round. What worked was to set each
component's whole frame first, then add mates that hold that frame and carry
the angles as dimensions. This entry is how the frame is set, how a component's
origin is selected, the two mate forms that are new here (an angle between two
axes, a point's distance from a plane), and the readback that shows the result.

## Setting a component's frame

The tool's form, written from the probe:

```python
def place_component(self, handle: str, rotation: Sequence[float], at: Tuple[float, float, float]) -> None:
    """Set a component's frame: a rotation given by rows, and its origin in mm (probe nonparallel_mates).

    ``IMathUtility.CreateTransform`` answered "member not found" to late
    binding's property get, so it is invoked as the method it is, with the
    16 numbers as a double array — the rotation in columns, then the
    translation in metres, the scale and three zeros — and put on
    ``Transform2``. The frame is read back.
    """
    component = self._handles[handle]["component"]
    m = MM_PER_METRE
    columns = [float(rotation[i * 3 + j]) for j in range(3) for i in range(3)]
    data = columns + [at[0] / m, at[1] / m, at[2] / m, 1.0, 0.0, 0.0, 0.0]
    utility = call(self._app, "GetMathUtility")
    oleobj = utility._oleobj_  # noqa: SLF001 - the method, not the property get
    raw = oleobj.Invoke(oleobj.GetIDsOfNames("CreateTransform"), 0, pythoncom.DISPATCH_METHOD, True,
                        VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, data))
    if raw is None:
        raise SolidWorksError("CreateTransform made no transform.")
    transform = raw if hasattr(raw, "ArrayData") else _dynamic.Dispatch(raw)
    component.Transform2 = transform
    call(self._doc(), "ForceRebuild3", False)
    got = [float(v) for v in call(call(component, "Transform2"), "ArrayData")]
    error = max(abs(got[i] - data[i]) for i in range(9))
    moved = max(abs(got[9 + i] - data[9 + i]) for i in range(3)) * m
    if error > 1e-6 or moved > 1e-6:
        raise SolidWorksError(f"{call(component, 'Name2')} did not take its frame "
                              f"(rotation off {error:.1e}, origin {moved:.1e} mm).")
```

`MM_PER_METRE` is 1000. `_dynamic` is `win32com.client.dynamic`; `VARIANT` is
`win32com.client.VARIANT`.

- `GetMathUtility` on **ISldWorks**; `CreateTransform(double array)` on
  **IMathUtility** returns an **IMathTransform**.
- The sixteen numbers: the rotation **by columns** (the first three are where
  the component's own X axis points in the assembly, then Y, then Z), the
  origin in **metres**, the scale `1.0`, then three zeros. The same layout
  `ArrayData` reads back ([reading/05](../reading/05-sketch-to-model-transform.md)).
- `Transform2` on **IComponent2**, **put** by plain assignment. The put returned
  `None`; the frame is judged by reading `Transform2.ArrayData` back after
  `ForceRebuild3`.

## Why `CreateTransform` needs `Invoke`

Through pywin32's late binding, `call(utility, "CreateTransform", data)` raised
**`DISP_E_MEMBERNOTFOUND` — "Member not found"**, with a Python list and with a
`VT_ARRAY | VT_R8` variant alike. Invoking the same dispid with
`pythoncom.DISPATCH_METHOD` and the typed double array made the transform. The
probe's comment on why: late binding reached `CreateTransform` as a property
get with an argument, which SolidWorks refused; that is its reading, not
something the run isolated. The probe's four forms, in the order tried:

```python
def make_transform(px: Px, data: List[float]) -> Any:
    utility = call(sc.app(px), "GetMathUtility")
    try:
        from win32com.client import VARIANT  # noqa: PLC0415 - Windows only
        import pythoncom  # noqa: PLC0415
    except ImportError as exc:  # pragma: no cover
        raise Require(f"pywin32 is needed to make a transform: {exc}")
    import win32com.client.dynamic as dynamic  # noqa: PLC0415

    def invoked(argument: Any) -> Any:
        # Late binding reached CreateTransform as a property get with an
        # argument, which SolidWorks answered "member not found"; invoke it as
        # the method the type library says it is.
        oleobj = utility._oleobj_  # noqa: SLF001
        dispid = oleobj.GetIDsOfNames("CreateTransform")
        raw = oleobj.Invoke(dispid, 0, pythoncom.DISPATCH_METHOD, True, argument)
        return dynamic.Dispatch(raw) if raw is not None and not hasattr(raw, "ArrayData") else raw

    array = VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, data)
    for label, thunk in (
        ("list", lambda: call(utility, "CreateTransform", data)),
        ("VT_ARRAY|VT_R8", lambda: call(utility, "CreateTransform", array)),
        ("Invoke DISPATCH_METHOD, VT_ARRAY|VT_R8", lambda: invoked(array)),
        ("Invoke DISPATCH_METHOD, list", lambda: invoked(data)),
    ):
        got, transform = px.attempt(f"IMathUtility.CreateTransform({label})", thunk)
        if got and transform is not None:
            px.shared["create_transform_argument"] = label
            return transform
    raise Require("CreateTransform made no transform from any argument form.")
```

The fourth form was never reached. The probe also had
`IComponent2.SetTransformAndSolve2` and a rows ordering as fallbacks; the first
form (`Transform2` put, columns) read back correctly every time, so neither was
tried.

## `AddComponent5` does not put the origin where you asked

Both parts in the probe were r 20 mm cylinders 10 mm thick, extruded from the
Front plane toward +Z. Inserted with `AddComponent5(path, 0, "", False, "",
0, 0, 0)`, a component's origin was **not** at the assembly origin:

- In [assemblies/01](01-new-assembly-and-insert-components.md), part B asked for
  (60, 0, 0) mm read a translation of (60.0, 0.0, −5.0) mm.
- In the first run of this probe (20260915-015506), gear 1 was left where
  `AddComponent5` put it and gear 2 was given an exact frame and mated
  origin-to-origin to it. Gear 2's origin then read **5.0 mm** from where the
  frame put it, with the rotation exact (`2.22e-16`).

The probe's reading is that `AddComponent5` places the part's middle, not its
origin, at the point given: half of 10 mm is 5 mm. Only one part thickness was
ever inserted, so that reading fits both observations but is not established.
The fix does not depend on it: **set every component's frame with
`Transform2`, the first one included**, before mating. With gear 1 set to the
identity at the origin as well, the next runs read 0 mm.

## Selecting a component's origin

```python
def _select_component_origin(self, component: Any, append: bool) -> None:
    """A component's origin point, by ``Point1@<origin>@<component>@<assembly>`` (probe nonparallel_mates)."""
    part = call(component, "GetModelDoc2")
    feature = call(part, "FirstFeature")
    origin = ""
    while feature is not None and not origin:
        if _type_name(feature) == "OriginProfileFeature":
            origin = str(call(feature, "Name"))
        feature = call(feature, "GetNextFeature")
    if not origin:
        raise SolidWorksError(f"{call(component, 'Name2')} has no origin to mate to.")
    doc = self._doc()
    if not append:
        call(doc, "ClearSelection2", True)
    before = self._selected()
    asm = os.path.splitext(str(call(doc, "GetTitle")))[0]
    full = f"{findings.ORIGIN_POINT}@{origin}@{call(component, 'Name2')}@{asm}"
    if not self._retry_select(lambda: call(call(doc, "Extension"), "SelectByID2", full, "EXTSKETCHPOINT", 0.0, 0.0,
                                           0.0, append, 1, _null_dispatch(), 0), before):
        raise SolidWorksError(f"{full} could not be selected for a mate.")
```

`findings.ORIGIN_POINT = "Point1"`. The name is
`Point1@<origin feature name>@<component Name2>@<assembly title without extension>`,
for example `Point1@Origin@Probe Skew B-1@<assembly title>`, type `"EXTSKETCHPOINT"`,
mark 1. The origin feature's name is read from the part's tree
(`OriginProfileFeature`) rather than typed. `GetModelDoc2` is on
**IComponent2**. `ISelectionMgr.GetSelectedObjectType3` read `25` (sketch point)
for both origins. `_retry_select` repeats the select up to four times, 0.25 s
apart, and counts it only if `GetSelectedObjectCount2(-1)` went up.

The probe had two more routes behind this one, the origin's sketch point through
`IComponent2.GetCorresponding` and `Select4`, and `SelectByID2` with
`"ORIGINFOLDER"`; the first route worked every time, so they were never tried.

## The mates

`AddMate5` on **IAssemblyDoc**, as in [assemblies/02](02-mates-from-code.md),
with the two entities at mark 1. The tool's form now also takes a point, a
point distance and an angle between axes, and passes `Flip`:

```python
types = {"coincident": findings.MATE_COINCIDENT, "distance": findings.MATE_DISTANCE,
         "angle": findings.MATE_ANGLE, "point": findings.MATE_COINCIDENT,
         "point distance": findings.MATE_DISTANCE, "axis angle": findings.MATE_ANGLE}
if kind not in types:
    raise SolidWorksError(f"There is no verified mate type for {kind!r}.")
before = self._mate_names()
self._component_ref(a, append=False)
self._component_ref(b, append=True)
distance = abs(value) / MM_PER_METRE if types[kind] == findings.MATE_DISTANCE else 0.0
angle = math.radians(value) if types[kind] == findings.MATE_ANGLE else 0.0
# A point distance is taken along the plane's normal; a negative one is flipped (probe nonparallel_mates).
flip = kind == "point distance" and value < 0
mate = call(self._doc(), "AddMate5", types[kind], findings.MATE_ALIGN_CLOSEST, flip, distance, distance,
            distance, 1, 1, angle, angle, angle, False, False, 0, _out_long())
```

(The rest of `add_mate`, which finds the new mate and its `D1`, is in
[assemblies/02](02-mates-from-code.md); constants `MATE_COINCIDENT 0`,
`MATE_DISTANCE 5`, `MATE_ANGLE 6`, `MATE_ALIGN_CLOSEST 2`.)

**An angle mate between two axes.** Two reference axes selected at mark 1 and
`AddMate5(6, 2, False, 0, 0, 0, 1, 1, a, a, a, False, False, 0, out)` with the
angle in radians made a mate typed `MatePlanarAngleDim`, the same type name as
an angle between planes, with one dimension `D1` holding the angle
(1.2217304763960306 for 70°).

**A point's distance from a plane is signed by the plane's normal.** Gear 2's
origin was placed 8 mm below gear 1's Top plane (y = −8 mm; Top's normal is
+Y), and a distance mate of 8 mm added between that origin and the plane with
`Flip` false. After rebuilding, the origin was at **y = +8 mm**: the mate
measured the distance along the normal and moved the point to the positive side,
16 mm from where it had been placed, although the rotation still held. With
`Flip` (the third argument) `True` for that mate, the origin stayed at
y = −8 mm. The dimension read `D1 = 0.008` both ways. So pass the distance as a
positive number and set `Flip` when the point is on the plane's negative side.
The Front-plane distance (z = +12 mm) needed no flip.

## The two frames that held

From [`code/python/gear_generator/probe/p4_nonparallel.py`](../../code/python/gear_generator/probe/p4_nonparallel.py).
Rotations are 3×3 matrices by rows; `crossed.rot_y(d)` is
`((c, 0, s), (0, 1, 0), (-s, 0, c))`, `rot_x(d)` `((1, 0, 0), (0, c, -s), (0, s, c))`,
`rot_z(d)` `((c, -s, 0), (s, c, 0), (0, 0, 1))` with `c, s` the cosine and sine
of `d` degrees, and `multiply` the ordinary matrix product. (Those three
helpers live in the tool's gear maths, which is not copied here.)

**Bevel-style**, origins together, gear 2's axis turned 70° about Y and spun 12°
about its own axis:

```python
bevel = crossed.multiply(crossed.rot_y(BEVEL_SHAFT), crossed.rot_z(BEVEL_SPIN))
asm, first, second = _assembly(px, path_a, path_b)
try:
    # AddComponent5 puts a component's middle, not its origin, at the point:
    # A is set to the assembly's own frame so B's frame reads back directly.
    place(px, asm, first, IDENTITY, (0.0, 0.0, 0.0))
    route, order = place(px, asm, second, bevel, (0.0, 0.0, 0.0))
    px.fact("place_route", route)
    px.fact("transform_array_order", order)
    px.fact("create_transform_argument", px.shared.get("create_transform_argument"))
    px.ok(f"a component's frame is set by {route} with ArrayData in {order}")

    selected = select_origin(px, asm, second, part_b, append=False)
    px.require(select_origin(px, asm, first, part_a, append=True) == selected,
               "both origins select by the same route")
    px.fact("origin_select_route", selected)
    px.fact("origin_select_types", [int(call(call(asm, "SelectionManager"), "GetSelectedObjectType3", i, -1))
                                    for i in (1, 2)])
    point = _mate(px, asm, "bevel: origins coincident", MATE_COINCIDENT)
    px.require(_select_in_component(px, asm, second, "Gear Axis", "AXIS", False) and
               _select_in_component(px, asm, first, top, "PLANE", True), "B's axis and A's top plane select")
    _mate(px, asm, "bevel: axis in top plane", MATE_COINCIDENT)
    px.require(_select_in_component(px, asm, second, "Gear Axis", "AXIS", False) and
               _select_in_component(px, asm, first, "Gear Axis", "AXIS", True), "the two axes select")
    shaft = _mate(px, asm, "bevel: angle between axes", MATE_ANGLE, angle=math.radians(BEVEL_SHAFT))
    px.fact("axis_angle_mate_dimensions", _dimension_names(asm, shaft))
    px.require(_select_in_component(px, asm, second, top, "PLANE", False) and
               _select_in_component(px, asm, first, top, "PLANE", True), "the two top planes select")
    _mate(px, asm, "bevel: spin by the top planes", MATE_ANGLE, angle=math.radians(BEVEL_SPIN))
    call(asm, "ForceRebuild3", False)
    rotation, origin = frame_error(second, bevel, (0.0, 0.0, 0.0))
```

**Crossed-style**, gear 2's axis turned −90° about X and spun 7°, its origin at
(60, −8, 12) mm, held by a 60 mm distance from its axis to gear 1's Right plane,
a 90° angle between the axes, the origin's distances from gear 1's Top (8 mm,
flipped) and Front (12 mm) planes, and a 97° angle from gear 2's Top plane to
gear 1's Right plane:

```python
for plane, value in ((top, CROSSED_ORIGIN[1]), (front, CROSSED_ORIGIN[2])):
    select_origin(px, asm, second, part_b, append=False)
    px.require(_select_in_component(px, asm, first, plane, "PLANE", True), f"A's {plane} selects")
    distances[plane] = _flipped_mate(px, asm, f"crossed: origin to {plane}", abs(value) / sc.MM,
                                     flip=value < 0)
```

```python
def _flipped_mate(px: Px, asm: Any, label: str, distance: float, flip: bool) -> Optional[str]:
    """A distance mate with AddMate5's Flip argument given; otherwise :func:`p4_assembly._mate`."""
    before = [n for n, _ in _mate_features(asm)]
    got, result = px.attempt(
        f"AddMate5 {label} (flip {flip})",
        lambda: call(asm, "AddMate5", MATE_DISTANCE, ALIGN_CLOSEST, flip, distance, distance, distance, 1, 1,
                     0.0, 0.0, 0.0, False, False, 0, sc.out_long()),
    )
```

**The oracle** is the component's own frame read back, rotation to 1e-6 and
origin to 1e-6 mm:

```python
def frame_of(component: Any) -> Tuple[Matrix, Tuple[float, float, float]]:
    """A component's rotation (rows) and origin in mm, reading ``ArrayData`` as columns (SW-Macro-Database reading/05)."""
    data = [float(v) for v in call(call(component, "Transform2"), "ArrayData")]
    matrix = tuple(tuple(data[j * 3 + i] for j in range(3)) for i in range(3))
    return matrix, (data[9] * sc.MM, data[10] * sc.MM, data[11] * sc.MM)  # type: ignore[return-value]
```

## What it does not do

- Whether the mates alone, without the frame set first, would have landed the
  same way was not tried. The recipe is place, then mate.
- `Flip` was set only on point-to-plane distance mates. Its effect on the other
  mate types, and alignment values other than `2`, were not tried.
- The angle mates' dimensions were linked to globals in the tool's builds
  (`link "D1@Shaft Angle"`, `link "D1@Mesh Angle"`), but no probe changed a
  global afterwards and measured the new frame, so an angle mate following its
  global is not observed here.
- Only two frames were tested. A shaft angle near 0° or 180°, where an angle
  mate's two solutions come together, was not.
- The `AddComponent5` offset was seen only on 10 mm cylinders; its rule is not
  established.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe
`nonparallel_mates`, three development runs on 2026-09-15. Excerpts from all
three:
[`probe-log-excerpts-20260915.txt`](../../code/python/gear_generator/probe/results/probe-log-excerpts-20260915.txt).

Every run: `IMathUtility.CreateTransform(list): raised DISP_E_MEMBERNOTFOUND —
Member not found.`, the same for `(VT_ARRAY|VT_R8)`, then
`(Invoke DISPATCH_METHOD, VT_ARRAY|VT_R8): returned 'CDispatch'`;
`Transform2 put (columns): returned None`; `place_route = 'Transform2 put'`,
`transform_array_order = 'columns'`; both origins selected by
`SelectByID2 EXTSKETCHPOINT Point1@Origin: returned True`,
`origin_select_types = [25, 25]`; every `AddMate5` returned a `CDispatch` and
added one mate; `axis_angle_mate_dimensions = [('D1', 1.2217304763960306)]`;
`point_distance_mate_dimensions = {'Top Plane': [('D1', 0.008)], 'Front Plane': [('D1', 0.012)]}`;
`crossed_spin_mate_angle = 97.00000000000001`.

1. **Run 20260915-015506** (probe FAIL): gear 1 not placed. Gear 2's put read
   back "rotation off by 2.22e-16, origin by 0.00e+00 mm", but after mating,
   `bevel_frame_error = {'rotation': 2.220446049250313e-16, 'origin_mm': 5.0, …}`.
   The crossed frame, also without `Flip`, came to
   `{'rotation': 2.220446049250313e-16, 'origin_mm': 16.76305461424021, …}`,
   which is √(16² + 5²): both effects at once.
2. **Run 20260915-020933** (probe FAIL): gear 1 set to the identity first
   ("rotation off by 0.00e+00, origin by 0.00e+00 mm"). Bevel frame
   `{'rotation': 2.220446049250313e-16, 'origin_mm': 0.0, 'mates': [('Coincident1', 'MateCoincident'), ('Coincident2', 'MateCoincident'), ('Angle1', 'MatePlanarAngleDim'), ('Angle2', 'MatePlanarAngleDim')]}`
   — passed. Crossed, still no `Flip`: `crossed_first_frame_error = (0.0, 0.0)`,
   `crossed_second_origin_mm = (60.0, 8.0, 12.000000000000002)`,
   `origin_mm: 16.0`.
3. **Run 20260915-022314** (41 of 41 passed): `AddMate5 crossed: origin to Top
   Plane (flip True)`, `origin to Front Plane (flip False)`;
   `crossed_second_origin_mm = (60.0, -8.0, 12.0)`;
   `crossed_frame_error = {'rotation': 2.220446049250313e-16, 'origin_mm': 0.0, 'mates': [('Distance1', 'MateDistanceDim'), ('Angle1', 'MatePlanarAngleDim'), ('Distance2', 'MateDistanceDim'), ('Distance3', 'MateDistanceDim'), ('Angle2', 'MatePlanarAngleDim')]}`;
   bevel frame `origin_mm: 0.0`, rotation 2.220446049250313e-16.

The tool's builds in run 20260915-022314 used `place_component` and `add_mate`:

```
ok      placed gear1
ok      placed gear2
ok      fixed gear1
ok      mate point gear2:origin ~ gear1:origin
ok      mate coincident gear2:Gear Axis ~ gear1:plane2
ok      mate axis angle gear2:Gear Axis ~ gear1:Gear Axis as Shaft Angle
ok      link "D1@Shaft Angle"
ok      mate angle gear2:plane2 ~ gear1:plane2 as Mesh Angle
ok      link "D1@Mesh Angle"
ok      4 globals read back and match the maths
ok      no interference between the gears
```

for the bevel pair, and for the crossed pair
`mate distance … as Centre Distance`, `mate axis angle … as Shaft Angle`,
`mate point distance gear2:origin ~ gear1:plane2 as Offset Y`,
`mate point distance gear2:origin ~ gear1:plane1 as Offset Z`,
`mate angle gear2:plane2 ~ gear1:plane3 as Mesh Angle`, each linked, then
"8 globals read back and match the maths" and "no interference between the
gears" ([assemblies/03](03-interference-detection.md)).

## See also

- [assemblies/01 — New assembly and insert components](01-new-assembly-and-insert-components.md) — `AddComponent5`, and the first sighting of the offset
- [assemblies/02 — Mates from code](02-mates-from-code.md) — `AddMate5` and finding the mate's `D1`
- [assemblies/03 — Interference detection](03-interference-detection.md) — checking the result
- [reading/05 — Sketch to model transform](../reading/05-sketch-to-model-transform.md) — `ArrayData` by columns
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — `Invoke` for what late binding cannot spell
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [GOTCHAS §10, §39, §41, §42](../../GOTCHAS.md)
