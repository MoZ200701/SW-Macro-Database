---
id: equations-01-global-variables-from-code
title: Add, read and change global variables in the Equation Manager
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IModelDoc2.GetEquationMgr, IEquationMgr.Add2, IEquationMgr.Add3, IEquationMgr.GetCount, IEquationMgr.Equation, IEquationMgr.Value, IEquationMgr.GlobalVariable, IEquationMgr.Delete, IEquationMgr.Status, IEquationMgr.SetEquationAndConfigurationOption, IEquationMgr.AngularEquationUnits, IModelDoc2.ForceRebuild3]
keywords: [equation manager, global variable, IEquationMgr, Add2, Add3, -1, Equation, indexed property, property put, SetEquationAndConfigurationOption, sqr, sqr(0), IIF, precedence, comparison, degrees, radians, sin, pi, parametric, Status]
answers: "How do I add, read and change global variables in the Equation Manager from code?"
---

# Add, read and change global variables in the Equation Manager

## What this is for

Global variables are how a part is made parametric: every size is a named
equation, dimensions are linked to them
([equations/02](02-link-a-dimension-to-a-global.md)), and changing one number
and rebuilding changes the part. From code that needs three things: add a
global, read it back, and change it later in place. Each of the three has a
member that looks right and returns `-1`.

## The call

`IEquationMgr` comes from `IModelDoc2.GetEquationMgr`. Real code from a tool
whose build ran on SolidWorks 2026; `call` is the helper from
[connect/02](../connect/02-attach-from-python.md).

**Add**, with `Add2`, and read it back:

```python
def _add_equation(self, text: str) -> int:
    """``Add2`` (probe equation_add: Add3 returned -1 on a one-configuration part)."""
    manager = self._equation_manager()
    index = int(call(manager, "Add2", -1, text, True))
    if index < 0:
        raise SolidWorksError(f"The Equation Manager refused {text}: Add2 returned -1.")
    read = str(call(manager, "Equation", index))
    if read.replace(" ", "") != text.replace(" ", ""):
        raise SolidWorksError(f"The Equation Manager holds {read!r} where {text!r} was added.")
    return index
```

`Add2(index, text, solveNow)`: index `-1` appends and the call returns the new
index; `-1` back means refused. The text is the whole equation, name quoted:
`"Module"= 1.5`. SolidWorks reads it back as `'"Probe A"= 2.5'` — a space after
the `=` and none before — so compare with spaces removed.

**Read** by index: `Equation(i)` is the text, `Value(i)` the evaluated number,
`GlobalVariable(i)` whether it is a global, `GetCount` how many there are, and
`Delete(i)` removes one (it returned `0`).

**Change** in place, with an indexed property put:

```python
def set_global_variable(self, name: str, expression: str) -> None:
    """Change a global in place: an indexed put of ``Equation(i)`` (probe equation_set)."""
    manager = self._equation_manager()
    for index in range(int(call(manager, "GetCount"))):
        try:
            existing, _ = split_equation(str(call(manager, "Equation", index)))
        except ValueError:
            continue
        if existing == name:
            text = equation_text(name, expression)
            _put_indexed(manager, "Equation", index, text)
            read = str(call(manager, "Equation", index))
            if read.replace(" ", "") != text.replace(" ", ""):
                raise SolidWorksError(f"\"{name}\" reads {read!r} after being set to {text!r}.")
            return
    raise SolidWorksError(f"There is no global variable \"{name}\" in {call(self._doc(), 'GetTitle')}.")
```

`split_equation` and `equation_text` are the tool's own string helpers: the
first splits `"Name"= expr` into its name and expression, the second puts
them back together in that form. `_put_indexed` is the one line of pywin32 that
makes `Equation(i) = text` possible from Python; it is in
[connect/02](../connect/02-attach-from-python.md#setting-an-indexed-property).
Rebuild afterwards (`IModelDoc2.ForceRebuild3(False)`) and whatever refers to
the global follows.

**Measure the trig units** rather than assuming them:

```python
def detect_trig_units(self) -> str:
    """Measure the document's trig units: add sin ( 90 ), read it, delete it."""
    index = self._add_equation('"Gear Generator Trig Check"= sin ( 90 )')
    manager = self._equation_manager()
    try:
        value = float(call(manager, "Value", index))
    finally:
        call(manager, "Delete", index)
    if abs(value - 1.0) < 1e-9:
        return DEGREES
    if abs(value - math.sin(90.0)) < 1e-9:
        return RADIANS
    raise SolidWorksError(f"sin ( 90 ) came to {value!r}, which is neither degrees nor radians.")
```

Units: a global is a bare number. Used as a length it is in **document
units**, millimetres in an MMGS part; see
[documents/01](../documents/01-new-part-from-template.md).

## Why it is not obvious

**`Add3` returned -1.** On a new part (one configuration),
`Add3(-1, text, True, 2, None)` returned `-1` and added nothing. The API help
says `Add3` only works on parts with several configurations and to use `Add2`
otherwise; this is that, observed. `Add2(-1, text, True)` added, and it is what
every equation in this collection was added with.

**`SetEquationAndConfigurationOption` returned -1 too.** With
`"Probe Base"= 3` and `"Probe Twice"= "Probe Base" * 2`,
`SetEquationAndConfigurationOption(index, '"Probe Base"= 6', 2, None)` returned
`-1`, and after a rebuild the dependent global still read 10 (from the earlier
change). The indexed put `Equation(i) = '"Probe Base"= 5'` returned nothing and,
after `ForceRebuild3`, the dependent read 10.0.

**A refusal is `-1`, not an exception.** A parse error (`"Probe Bad"= ( 1 +`)
and a reference to a name that does not exist (`"No Such Name" * 2`) both made
`Add2` return `-1`, and `Status` then read `-1`. No dialog appeared and nothing
was raised. Check every return.

**Trig was degrees.** `sin ( 90 )` evaluated to 1.0, `cos ( 60 )` to 0.5,
`tan ( 20 )` to 0.36397 (tan 20°). `AngularEquationUnits` read `1` in the same
kind of part. This is the Equation Manager only: an Equation Driven Curve in a
degree part is still radians
([sketches/07](../sketches/07-equation-driven-curve.md)).

**`sqr ( 0 )` is refused, and `IIF` does not protect it.** See the table: the
square root of exactly zero returned `-1`, and so did `IIF ( 1 > 1 , sqr ( 0 ) , 0 )`,
whose false branch is the one that would be taken. `IIF` did not stop the
untaken branch being rejected. `0 ^ 0.5` gave 0. So write square roots as
`( ... ) ^ 0.5` whenever the argument can be exactly zero — for instance
`sqr ( ( a / b ) ^ 2 - 1 )` when `a` equals `b`.

**A comparison inside `IIF` binds tighter than arithmetic.**
`IIF ( 3 >= ( 1 + 5 ) , 1 , 0 )` gave 0, the right answer.
`IIF ( 3 >= 1 + 5 , 1 , 0 )` gave **1**, which is what you get if it is read
as `( 3 >= 1 ) + 5`. Bracket both sides of every comparison.

### What the Equation Manager accepted

In a part whose trig read degrees, each added with `Add2` and read with `Value`:

| Equation | Returned | Value |
|---|---|---|
| `pi` | index | 3.141592653589793 |
| `2 ^ 3` | index | 8.0 |
| `-2 ^ 2` | index | **-4.0** |
| `sqr ( 16 )` | index | 4.0 |
| `sqr ( 1 )` | index | 1.0 |
| `sqr ( 0 )` | **-1** | — |
| `sqr ( ( 35.238473279 / 35.238473279 ) ^ 2 - 1 )` | **-1** | — |
| `sqr ( abs ( ( 35.238473279 / 35.238473279 ) ^ 2 - 1 ) )` | **-1** | — |
| `sqr ( ( 35.46 / 35.238473279 ) ^ 2 - 1 )` | index | 0.1123054913132504 |
| `0 ^ 0.5` | index | 0.0 |
| `0.0001 ^ 0.5` | index | 0.01 |
| `IIF ( 1 <= 1.25 , 2.4 , 2.25 )` | index | 2.4 |
| `IIF ( 1.5 >= 2 , 1 , 0 )` | index | 0.0 |
| `IIF ( 3 >= ( 1 + 5 ) , 1 , 0 )` | index | 0.0 |
| `IIF ( 3 >= 1 + 5 , 1 , 0 )` | index | **1.0** |
| `IIF ( 1 > 1 , sqr ( -1 ) , 0 )` | **-1** | — |
| `IIF ( 1 > 1 , sqr ( 0 ) , 0 )` | **-1** | — |
| `"Probe Pi" * 2` (a reference) | index | 6.283185307179586 |
| `abs ( -2 )` | index | 2.0 |
| `( 1 +` | **-1** | `Status` -1 |
| `"No Such Name" * 2` | **-1** | `Status` -1 |

A real set of 24 interdependent globals for a spur gear then went in, in
order, including `( "Arc Length" / "Pitch Diameter" ) * 180 / pi`,
`tan ( "Pressure Angle" ) - "Pressure Angle" * pi / 180`, nested `IIF` with
bracketed comparisons, and `( abs ( ( a / b ) ^ 2 - 1 ) ) ^ 0.5`. All 24 were
accepted, all read back exactly as written, and all 24 values matched an
independent calculation to 1e-6.

## What it does not do

- **Multi-configuration parts were not tried.** Whether `Add3` works there, as
  its help says, is not observed.
- Why `SetEquationAndConfigurationOption` returned `-1` was not investigated;
  only the `(index, text, 2, None)` form was tried.
- A radian document was never produced, so this entry has not seen trig in
  radians in the Equation Manager. `detect_trig_units` handles both.
- `IEquationMgr.EvaluateAll` is not used and was never called.
- `Value(i)` was read after a rebuild or straight after `Add2(..., True)`; a
  value read with `solveNow` False was not tested.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426, probes `equation_add`, `equation_syntax`, `gear_globals` and
`equation_set` in
[`code/python/gear_generator/probe/p1_equations.py`](../../code/python/gear_generator/probe/p1_equations.py).

- `equation_add`: `AngularEquationUnits` 1; `Add2` returned 0 in an empty part;
  `GetCount` went 0 to 1; `Equation(0)` read `'"Probe A"= 2.5'`; `Value(0)` 2.5;
  `GlobalVariable(0)` true; `Delete(0)` returned 0 and `GetCount` went back to 0;
  `Add3` on the one-configuration part returned -1.
- `equation_syntax`: every row of the table above, recorded as index and value;
  `Status` after each refusal -1.
- `gear_globals`: 24 accepted, `globals_match_maths = True`, no text rewritten.
- `equation_set`: indexed put returned `None` and the dependent global followed
  to 10.0; `SetEquationAndConfigurationOption` returned -1 and it did not.
- `end_to_end`: the tool built a gear with these calls, then changed
  `"No. of Teeth"` 12 → 13 and `"Module"` 2 → 2.25 through
  `set_global_variable`, rebuilt once, and `"Outer Diameter"` read 33.75 and the
  pattern count 13.
- A later run of the tool on the same session, on a 25-tooth, module 1.5 gear:
  `set_global_variable("No. of Teeth", "40")` and one `ForceRebuild3`, which
  returned `True`, gave `"Outer Diameter"` 63.0, `Count@Teeth` 40,
  `OD@Blank Sketch` 0.063 m, and all three sketches still read
  `GetConstrainedStatus` 3. Changing it to 30 through the tool's Update values,
  which reported "Changed 1 variable ... and rebuilt it", gave 48.0, 30 and
  status 3 for all three again. That second rebuild's return value was not
  printed. That run's output is quoted here; its log is not kept in this repo.

## See also

- [equations/02 — Link a dimension to a global](02-link-a-dimension-to-a-global.md) — making globals drive geometry
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — the indexed property put
- [sketches/07 — Equation driven curve](../sketches/07-equation-driven-curve.md) — globals inside curve expressions, where trig is radians
- [documents/01 — New part from a template](../documents/01-new-part-from-template.md) — document units
- [curves/08 — Rebuild once, at the end](../curves/08-rebuild-once-at-the-end.md) — changing many globals in one batch
- [GOTCHAS §26, §27, §28](../../GOTCHAS.md)
- [reading/08 — Dimensions and equations](../reading/08-dimensions-and-equations.md) — reading the equations out of an existing part
