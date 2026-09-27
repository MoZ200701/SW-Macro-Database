---
id: connect-08-find-feature-by-name
title: Find a feature by name in the tree
status: verified
verified_on: SolidWorks 2026
language: [vbscript, python]
api: [IPartDoc.FeatureByName, IModelDoc2.FirstFeature, IFeature.GetNextFeature, IFeature.Name, IFeature.GetFirstSubFeature, IFeature.GetNextSubFeature]
keywords: [feature tree, walk features, FirstFeature, GetNextFeature, find feature, GetFirstSubFeature, GetNextSubFeature, sub-feature, absorbed sketch, repeated names, FeatureByName, fast lookup, feature in a folder]
answers: "How do I get an IFeature by its name so I can modify or reference it?"
---

# Find a feature by name in the tree

## Why you need this

Almost every "modify something that already exists" operation starts here.
There is no lookup-by-name call; you walk the tree.

## VBScript

```vb
Function FindFeature(model, featName)
    Dim f, guard
    Set FindFeature = Nothing
    Set f = model.FirstFeature()
    guard = 0
    Do While (Not f Is Nothing) And guard < 5000
        guard = guard + 1
        If f.Name = featName Then
            Set FindFeature = f
            Exit Function
        End If
        Set f = f.GetNextFeature()
    Loop
End Function
```

## Python

On a part, ask for it by name first; walk only if that is not available:

```python
def _curve_feature(self, name: str) -> Any:
    doc = self._active()
    # A part answers by name in one call: a hundredth of a second, against
    # three for the walk below on a part of 560 features.
    try:
        found = call(doc, "FeatureByName", name)
    except Exception:  # noqa: BLE001 - the walk below always works
        found = None
    if found is not None and str(call(found, "Name")) == name:
        return found
    feature = call(doc, "FirstFeature")
    guard = 0
    while feature is not None and guard < 5000:
        guard += 1
        if str(call(feature, "Name")) == name:
            return feature
        feature = call(feature, "GetNextFeature")
    raise SolidWorksError(f"No feature called {name!r} is in {call(doc, 'GetTitle')}.")
```

`FeatureByName` is on **IPartDoc** (the part's document; through late binding
it answers on the `IModelDoc2` you already hold). On SolidWorks 2026 SP0.0,
2026-09-23 (`e59`), on a copy of a 559-feature part: the walk above found a
curve in **2.92 s**; `FeatureByName` found the same curve in **0.01 s**, and
found a curve that sits **inside a feature-tree folder** in under 0.01 s too,
which a top-level walk does not reach. The name is checked on the way out
because the walk's own caveats about names (below) apply to what you asked
for, not to how it was found. The Airfoil Converter looks every feature up
this way since commit `d155c6d`; a push of 99 curves made 76 such lookups in
1.0 s in total.

The assembly equivalent, `IAssemblyDoc.FeatureByName`, is only partly
verified — see [API-LEDGER](../../API-LEDGER.md).

## Things worth knowing

**`GetNextFeature` is siblings only.** It walks the top level of the tree. To
reach features nested inside a folder or a body you need `GetFirstSubFeature`
and recurse. For most automation the top level is what you want, because that
is where imported curves, sketches and reference geometry land.

A recursive walk, if you need one. **Correction, from SolidWorks 2026:** it
steps sub-features with `GetNextFeature`, which repeats names; the fix follows it.

```python
def _walk(self, first, depth=0):
    out = []
    feature = first
    guard = 0
    while feature is not None and guard < 5000:
        guard += 1
        out.append(self._describe(feature))
        sub = call(feature, "GetFirstSubFeature")
        if sub is not None and depth < 4:
            out.extend(self._walk(sub, depth + 1))
        feature = call(feature, "GetNextFeature")
    return out
```

Cap the depth. A feature tree can contain cycles through certain reference
types, and an uncapped recursion finds them.

**Step sub-features with `GetNextSubFeature`.** `GetNextFeature` called on a
sub-feature does not stay among its siblings. On SolidWorks 2026 (revision
34.0.0) a walk shaped like the one above, over a gear part (an axis, an
extrusion, a cut, a circular pattern and a second cut, each cut and the
extrusion made from its own sketch), listed

```
'Gear Axis', 'Blank Sketch', 'Blank', 'Blank Sketch', 'Blank', 'Blank Sketch', 'Blank',
'Blank Sketch', 'Blank', 'Blank Sketch', 'Blank', 'Tooth Space Sketch', 'Tooth Space',
'Teeth', 'Bore Sketch', 'Bore', 'Tooth Space Sketch', 'Tooth Space', ...
```

and went on repeating the later features. The first sketch and its extrusion
come round five times, which matches the depth cap of 4 being what stopped it;
that reading fits the output but was not established. Stepping sub-features with
`GetNextSubFeature` fixed it:

```python
def _walk(self, first: Any, depth: int = 0) -> List[FeatureInfo]:
    """Features from ``first`` on, each followed by its sub-features.

    Sub-features are stepped with ``GetNextSubFeature``: ``GetNextFeature``
    from a sketch under an extrusion carries on through the main tree, and
    on SolidWorks 2026 that listed every later feature again once per
    absorbed sketch.
    """
    out: List[FeatureInfo] = []
    feature = first
    step = "GetNextFeature" if depth == 0 else "GetNextSubFeature"
    guard = 0
    while feature is not None and guard < 5000:
        guard += 1
        try:
            type_name = str(call(feature, "GetTypeName2"))
        except Exception:  # noqa: BLE001 - a feature that will not describe itself
            type_name = "?"
        out.append(FeatureInfo(name=str(call(feature, "Name")), type_name=type_name))

        if depth < 4:  # a sketch sits under the feature that absorbed it
            try:
                child = call(feature, "GetFirstSubFeature")
            except Exception:  # noqa: BLE001
                child = None
            if child is not None:
                out.extend(self._walk(child, depth + 1))

        feature = call(feature, step)
    return out
```

(`FeatureInfo` is the tool's plain record of a name and a type name.) The same
part then listed, from the origin on,

```
'Origin', 'Gear Axis', 'Blank Sketch', 'Blank', 'Blank Sketch', 'Tooth Space Sketch',
'Tooth Space', 'Tooth Space Sketch', 'Teeth', 'Bore Sketch', 'Bore', 'Bore Sketch'
```

So even a correct descending walk sees each consumed sketch twice: in the
top-level chain just before the feature made from it, and again as that
feature's sub-feature. De-duplicate by name if that matters.

The corrected listing is the `feature_names` fact of probe `end_to_end` in runs
20260914-180426 and 20260914-183429
([`probe-results-20260914-183429.json`](../../code/python/gear_generator/probe/results/probe-results-20260914-183429.json)),
and it was identical in both. The repeating listing came from a development run
of the same probe a few minutes earlier (20260914-180134, not kept in this repo),
made with the walk as it was before the fix, which stepped every level with
`GetNextFeature`.

**The guard is not paranoia.** These loops sit inside `On Error Resume Next`
regions where a failed call leaves the variable unchanged. 5000 is chosen to be
well past any real tree.

**The last feature is the one you just made.** Several creation calls return
only a boolean, so "walk to the end and take the last" is how you get hold of
what you created:

```vb
Function LastFeature(model)
    Dim f, last, guard
    Set last = Nothing
    Set f = model.FirstFeature()
    guard = 0
    Do While (Not f Is Nothing) And guard < 5000
        guard = guard + 1
        Set last = f
        Set f = f.GetNextFeature()
    Loop
    Set LastFeature = last
End Function
```

A more robust version of the same idea diffs the set of feature names either
side of the call. See [curves/02](../curves/02-insert-curve-from-file.md).

**Names are not stable.** A user can rename anything. If you need a handle that
survives renaming, use a persistent reference:
[curves/10](../curves/10-persistent-references.md).

## See also

- [curves/07 — Rename a feature](../curves/07-rename-a-feature.md)
- [reading/07 — Report the feature type on failure](../reading/07-report-feature-type-on-failure.md)
- [curves/11 — Feature-tree folders](../curves/11-feature-tree-folders.md) — selecting a found feature with `IFeature.Select2`
- [features/04 — Read a feature's dimensions](../features/04-read-a-features-dimensions.md) — a feature's dimensions include those of the sketch under it
- [curves/02 — Insert a curve from a file](../curves/02-insert-curve-from-file.md) — naming what an insert made, without a walk
- [reading/12 — Snapshot suppression](../reading/12-snapshot-suppression-before-you-suppress.md) — a full walk that repeated the tail of the tree until it was fixed
- [GOTCHAS §37, §66](../../GOTCHAS.md)
- [assemblies/02 — Mates from code](../assemblies/02-mates-from-code.md) — walking sub-features to find a mate
- [reading/08 — Dimensions and equations](../reading/08-dimensions-and-equations.md) — a feature walk that reads each feature's dimensions
- [reading/11 — Cheap change detection](../reading/11-cheap-change-detection.md) — `IFeatureManager.GetFeatures` lists every feature in one call, and why a walk every second is too slow
