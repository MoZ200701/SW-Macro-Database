---
id: connect-08-find-feature-by-name
title: Find a feature by name in the tree
status: verified
verified_on: SolidWorks 2026
language: [vbscript, python]
api: [IModelDoc2.FirstFeature, IFeature.GetNextFeature, IFeature.Name]
keywords: [feature tree, walk features, FirstFeature, GetNextFeature, find feature]
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

```python
def _curve_feature(self, name):
    doc = self._active()
    feature = call(doc, "FirstFeature")
    guard = 0
    while feature is not None and guard < 5000:
        guard += 1
        if str(call(feature, "Name")) == name:
            return feature
        feature = call(feature, "GetNextFeature")
    raise SolidWorksError(f"No feature called {name!r} is in {call(doc, 'GetTitle')}.")
```

## Things worth knowing

**`GetNextFeature` is siblings only.** It walks the top level of the tree. To
reach features nested inside a folder or a body you need `GetFirstSubFeature`
and recurse. For most automation the top level is what you want, because that
is where imported curves, sketches and reference geometry land.

A recursive walk, if you need one:

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
