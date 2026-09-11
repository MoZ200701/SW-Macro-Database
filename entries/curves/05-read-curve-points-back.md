---
id: curves-05-read-curve-points-back
title: Read a curve's points back out
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [IFeature.GetDefinition, PointArray]
keywords: [PointArray, read curve, curve points, metres, round trip, verify]
answers: "How do I check what a curve feature actually contains?"
---

# Read a curve's points back out

## The call

```python
def curve_points(self, name):
    """The points a curve actually holds, in millimetres."""
    definition = call(self._curve_feature(name), "GetDefinition")
    flat = call(definition, "PointArray")
    if flat is None:
        return []
    values = [float(v) * MM_PER_METRE for v in flat]
    return [tuple(values[i:i + 3]) for i in range(0, len(values) - 2, 3)]
```

`PointArray` on the curve's feature data returns a **flat** array: x, y, z, x,
y, z, and so on. In metres, like everything else in the API.

## What it is for

Three real uses:

- **Verifying a push.** After refreshing a curve, read it back and compare to
  what you meant to send. This catches a path that pointed at a stale file.
- **Recognising a curve you made.** Given a feature the user clicked, its points
  tell you which of your exported curves it is.
- **Round-trip testing.** Write a file, insert it, read it back, compare. This
  is how the millimetre-to-metre boundary in
  [curves/01](01-sldcrv-file-format.md) was confirmed rather than assumed.

## What it will not tell you

**Everything except the points is gone.** A Curve Through XYZ Points feature
holds coordinates and a name. The chord, the plane it was built on, the angle
of attack, the source file, the parameters that generated it: none of it
survives into SolidWorks and none of it can be read back out.

If you want clicking a curve to restore the settings that made it, you have to
remember them yourself. The pattern that works is a JSON sidecar beside the
part:

```python
SCHEMA_VERSION = 1
SIDECAR_SUFFIX = ".airfoils.json"
```

It travels with the model, one part has one obvious file, and a part that has
never been saved has nowhere to put it, which is a real state to report rather
than paper over.

Match entries in the sidecar to features by name, and repair the mapping by
reconciling the two lists. Names drift, so for anything long-lived prefer a
persistent reference: [curves/10](10-persistent-references.md).

## Comparing floats

Do not compare the values you read back to the values you wrote with `==`. The
file carried six decimals of a millimetre, SolidWorks stores metres as doubles,
and the round trip is not exact. Compare with a tolerance of about a micron,
which is well below anything that matters and well above the noise.

## See also

- [curves/01 — The .sldcrv format](01-sldcrv-file-format.md)
- [curves/10 — Persistent references](10-persistent-references.md)
