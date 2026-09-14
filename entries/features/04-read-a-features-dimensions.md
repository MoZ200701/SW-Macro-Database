---
id: features-04-read-a-features-dimensions
title: Read a feature's dimensions, and find one by value
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeature.GetFirstDisplayDimension, IFeature.GetNextDisplayDimension, IDisplayDimension.GetDimension2, IDimension.Name, IDimension.SystemValue, IDimension.FullName]
keywords: [GetFirstDisplayDimension, GetNextDisplayDimension, GetDimension2, display dimension, D1, D3, feature dimensions, extrusion depth, pattern count, mate distance, rename dimension, find by value, FullName]
answers: "How do I list a feature's dimensions from code and pick out one, such as an extrusion's depth or a pattern's count?"
---

# Read a feature's dimensions, and find one by value

## What this is for

Features you create from code carry dimensions SolidWorks named for itself: an
extrusion's depth, a pattern's count, a mate's distance. To link one to a
global ([equations/02](../equations/02-link-a-dimension-to-a-global.md)) you
have to find it and give it a name you control. Its default name and its
position in the list are both things you would guess at, so do not.

## The call

Walk the feature's display dimensions on **IFeature**, taking each one's
**IDimension** through `IDisplayDimension.GetDimension2(0)`:

```python
@staticmethod
def _dimensions(feature: Any) -> List[Any]:
    out = []
    display = call(feature, "GetFirstDisplayDimension")
    guard = 0
    while display is not None and guard < 500:
        guard += 1
        out.append(call(display, "GetDimension2", 0))
        display = call(feature, "GetNextDisplayDimension", display)
    return out
```

Then pick by value and name it:

```python
def _name_dimension_by_value(self, feature: Any, value: float, name: str, what: str,
                             ignore: Sequence[str] = ()) -> str:
    """Find the one dimension with this value — never assume D1 — and name it."""
    matches = [d for d in self._dimensions(feature)
               if abs(float(call(d, "SystemValue")) - value) < 1e-9 and str(call(d, "Name")) not in ignore]
    if len(matches) != 1:
        raise SolidWorksError(f"{len(matches)} dimensions of {call(feature, 'Name')} have the {what}'s value, "
                              "so which one is the " + what + " cannot be told.")
    matches[0].Name = name
    return str(call(matches[0], "Name"))
```

`GetNextDisplayDimension` takes the previous display dimension as its
argument. `IDimension.Name` is read and set by attribute; `SystemValue` is in
**metres** for a length, **radians** for an angle, and a plain number for a
count. Refusing unless exactly one dimension matches is the point: two
dimensions with the same value is a question for a person.

## Why it is not obvious

What came back, for three kinds of feature:

| Feature | Dimensions, in list order |
|---|---|
| Extrusion `Blank`, 10 mm deep, from a sketch with a 40 mm diameter `OD` | `[('D1', 0.01), ('OD', 0.04)]` |
| Circular pattern `Teeth`, 6 instances over 360° | `[('D3', 6.283185307179586), ('D1', 6.0)]` |
| Distance mate `Centre Distance`, 60 mm | `[('D1', 0.06)]` |

**A feature lists its sketch's dimensions too.** The extrusion's list includes
`OD`, which belongs to the sketch it consumed. Pass the sketch's dimension names
as `ignore` if a value might coincide.

**The first dimension is not the one you want.** On the pattern, `D1` is the
count but it is second; the first is the angle `D3`. Code that takes index 0,
or assumes the count is always `D1`, is relying on an order and a naming that
nothing promises. By value, the choice is unambiguous.

**Rename it, and the full name follows.** The depth renamed `Width` read back,
and its `IDimension.FullName` became `'Width@Blank@Part233.Part'`. A sketch
dimension's `FullName` was `'OD@Sketch1@Part223.Part'` while its sketch was
open and `'OD@Probe Dims@Part223.Part'` once the sketch was closed and
renamed. An equation refers to it as `"Width@Blank"`.

## What it does not do

- Features whose dimensions are not display dimensions were not examined.
- Two dimensions with the same value never occurred here; the code above stops
  rather than guesses in that case, and that path was never exercised.
- Toleranced or driven dimensions were not looked at.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426.

- `extrude`: `extrude_dimensions = [('D1', 0.01), ('OD', 0.04)]`,
  `depth_dimension_default_name = 'D1'`, `depth_full_name = 'Width@Blank@Part233.Part'`.
- `pattern`: `pattern_dimensions = [('D3', 6.283185307179586), ('D1', 6.0)]`,
  `pattern_count_dim_index = 1`, renamed `Count` and read back.
- `assembly_components_and_mates`: `distance_mate_dimensions = [('D1', 0.06)]`.
- `dimensions`: `full_name_while_open = 'OD@Sketch1@Part223.Part'`,
  `full_name_after_close = 'OD@Probe Dims@Part223.Part'`, and
  `dimension_names_after_close = ['OD', 'R', 'HalfSpace']`.
- `end_to_end`: the tool named `Width` and `Count` through
  `_name_dimension_by_value`; after a change of teeth and module,
  `teeth_dimensions` read `{'D3': 6.283185307179586, 'Count': 13.0}` and the
  blank `{'Width': 0.01, 'OD': 0.03375}`.

## See also

- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md)
- [features/01 — Boss extrude](01-boss-extrude.md)
- [features/03 — Circular pattern](03-circular-pattern.md)
- [assemblies/02 — Mates from code](../assemblies/02-mates-from-code.md)
- [sketches/04 — Dimensions](../sketches/04-dimensions.md)
- [curves/07 — Rename a feature](../curves/07-rename-a-feature.md) — read names back
- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md) — walking features and the sketches under them
- [GOTCHAS §33](../../GOTCHAS.md)
- [reading/08 — Dimensions and equations](../reading/08-dimensions-and-equations.md) — the same walk over every feature, for a whole part
