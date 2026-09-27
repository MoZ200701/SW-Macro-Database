---
id: curves-02-insert-curve-from-file
title: Insert a curve from a file as a new feature
status: verified
verified_on: SolidWorks 2026
language: [vbscript, python]
api: [IModelDoc2.InsertCurveFile, IFeature.Name, IModelDoc2.GetFeatureCount, IModelDocExtension.GetLastFeatureAdded]
keywords: [InsertCurveFile, CurveInFile, import curve, error 438, IFeatureManager, insert feature]
answers: "How do I import a .sldcrv file as a feature from code, and get hold of what it made?"
---

# Insert a curve from a file as a new feature

## The call

```vb
ok = Part.InsertCurveFile(path)
```

**It is on `IModelDoc2`.** Calling it on `IFeatureManager` raises error 438,
"object doesn't support this property or method", which reads exactly like the
method does not exist. It does. This one error message has cost more than one
afternoon.

The resulting feature has type name `CurveInFile`.

## It returns a boolean and nothing else

You get `True` or `False`. Not the feature. So diff the feature names either
side of the call, and refuse to guess if the diff is not exactly one:

```python
def insert_curve(self, path, name):
    """Import a curve file as a new feature and give it the name we want."""
    doc = self._active()
    before = set(self.feature_names())
    if not call(doc, "InsertCurveFile", os.path.abspath(path)):
        raise SolidWorksError(f"SolidWorks refused to import {path}.")

    created = [n for n in self.feature_names() if n not in before]
    if len(created) != 1:
        raise SolidWorksError(
            f"Importing {os.path.basename(path)} added {len(created)} features, "
            "so which one it is cannot be told."
        )
    return self.rename_feature(created[0], name)
```

The cheaper alternative, taking the last feature in the tree, is right almost
always and silently wrong when it is not — with the tree rolled back, a new
feature lands at the bar, not at the end
([curves/12](12-roll-the-tree-back-before-reloading.md)).

### The diff is two full listings of the tree, every insert

**Correction, 2026-09-23:** "one extra tree walk" undersold it. The diff above
lists the whole tree twice per insert, and on a part of 560 features one
listing (`GetFeatures` plus a name and a type per feature) took **3.7 s**; the
rename after it walked the tree again by name, **2.9 s**. So each insert cost
about 10 s of bookkeeping around 1 s of SolidWorks work, and a 48-curve push
took nine minutes.

The same "exactly one new feature, and which" check costs a hundredth of a
second with a count and the document's own record of what it last added:

```python
def _feature_count(self) -> int:
    return int(call(self._active(), "GetFeatureCount"))

def _made_since(self, before: int) -> List[str]:
    """What the call since ``before`` was counted made, as names. Of more than
    one new feature only the last is known by name; the rest are only counted."""
    added = self._feature_count() - before
    if added <= 0:
        return []
    last = call(call(self._active(), "Extension"), "GetLastFeatureAdded")
    name = str(call(last, "Name")) if last is not None else ""
    if not name:
        raise SolidWorksError("SolidWorks added a feature but would not say which.")
    return [""] * (added - 1) + [name]

def insert_curve(self, path: str, name: str) -> str:
    doc = self._active()
    before = self._feature_count()
    if not call(doc, "InsertCurveFile", os.path.abspath(path)):
        raise SolidWorksError(f"SolidWorks refused to import {path}.")
    created = self._made_since(before)
    if len(created) != 1:
        raise SolidWorksError(
            f"Importing {os.path.basename(path)} added {len(created)} features, "
            "so which one it is cannot be told."
        )
    return self.rename_feature(created[0], name)
```

`GetFeatureCount` is on **IModelDoc2** and `GetLastFeatureAdded` on
**IModelDocExtension**. Observed on SolidWorks 2026 SP0.0 (revision 34.0.0),
2026-09-23, on a copy of a 559-feature part: after `InsertCurveFile` with the
tree forward, the count went 552 → 553 and `GetLastFeatureAdded` named the new
curve (`Curve122`) in 0.010 s; with the bar rolled back to just after another
curve, the count went 552 → 553 again and it named the new one (`Curve123`),
which a full listing confirmed sat directly after the bar. A diff of the full
listing agreed both times. The same pair replaced the diff after
`InsertCompositeCurve`, both lofts, `InsertPlanarRefSurface` and
`InsertSewRefSurface` in the Airfoil Converter, and every one of those was then
run live: pushes of 48–99 curves and 15–22 joins, solid and capped lofts, with
no failure. A new 48-curve wing pushed in 83 s where it had taken nine minutes.

`GetFeatureCount` is a different count from the listing's (552 against 559
on that part — the listing includes sub-features); only its *change* is used.
What neither call can do is name every feature when one call makes several;
that is refused rather than guessed, as before.

## Absolute paths

`os.path.abspath(path)`. A relative path is resolved against SolidWorks' own
working directory, not yours, and that is somewhere you did not choose.

## The VBScript version, with the failure reporting

```vb
Function InsertCurve(model, featName, path)
    Dim before, feat, ok
    InsertCurve = False
    before = LastFeatureName(model)
    On Error Resume Next
    ok = model.InsertCurveFile(path)
    If Err.Number <> 0 Then
        WScript.Echo "  FAIL    " & featName & " - InsertCurveFile: " & Err.Description
        Err.Clear
        Exit Function
    End If
    On Error GoTo 0
    If Not ok Then
        WScript.Echo "  FAIL    " & featName & " - InsertCurveFile returned False"
        Exit Function
    End If
    Set feat = LastFeature(model)
    If feat Is Nothing Then
        WScript.Echo "  FAIL    " & featName & " - inserted, but the new feature was not found"
        Exit Function
    End If
    If feat.Name = before Then
        WScript.Echo "  FAIL    " & featName & " - InsertCurveFile added no feature"
        Exit Function
    End If
    On Error Resume Next
    feat.Name = featName
    If Err.Number <> 0 Then
        WScript.Echo "  WARN    inserted as " & feat.Name & ", could not rename to " & featName
        Err.Clear
        Exit Function
    End If
    On Error GoTo 0
    WScript.Echo "  NEW     " & featName & " - inserted; still needs adding to the surface by hand"
    InsertCurve = True
End Function
```

Note that `ok` being `True` is checked separately from `Err.Number` being zero.
The call can fail either way.

## The thing insertion does not do

**It creates the curve and stops.** Adding it to a boundary surface, a loft or
anything else is still a human job.

This is why automatic insertion should be opt-in rather than the default. An
inserted curve that nothing consumes *looks* connected and is not, which is a
quieter failure than a missing curve. A tool that reports `MISSING S9` and
stops is more useful than one that leaves an orphan behind.

The message in the code above says so out loud for the same reason.

## Delete is deliberately absent

None of the tools in this collection delete features. Removing a station from
the source model leaves an orphaned curve in the part, to be deleted by hand.
A wrong delete destroys work that a wrong insert only clutters.

Cleaning up the *export folder* is a different matter and is safe, as long as
you only ever delete files a previous manifest says you wrote.

## See also

- [curves/03 — Stream curve points](03-stream-curve-points.md) — the other insert route
- [curves/04 — Reload a curve in place](04-reload-curve-in-place.md)
- [curves/07 — Rename a feature](07-rename-a-feature.md)
- [curves/11 — Feature-tree folders](11-feature-tree-folders.md) — gathering what you inserted into a folder
- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md) — the one-call lookup the rename uses
- [GOTCHAS §3, §11, §14, §66](../../GOTCHAS.md)
