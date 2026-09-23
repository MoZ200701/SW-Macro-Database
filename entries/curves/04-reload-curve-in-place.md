---
id: curves-04-reload-curve-in-place
title: Refresh a curve's points in place, without breaking what uses it
status: verified
verified_on: SolidWorks 2026
language: [vbscript, python]
api: [IFeature.GetDefinition, IFeature.ModifyDefinition, LoadPointsFromFile, IModelDoc2.ForceRebuild3]
keywords: [LoadPointsFromFile, ModifyDefinition, GetDefinition, refresh curve, in place, reload, live update, slow reload, 25 seconds per curve, rollback bar]
answers: "How do I change a curve's geometry without re-picking it in the boundary surface?"
---

# Refresh a curve's points in place

## Why this is the important one

Deleting a curve and re-importing it breaks every reference to it. A boundary
surface built on twelve curves then has twelve dangling references and has to
be rebuilt by hand. That makes an edit-and-see loop impossible.

Refreshing in place replaces the **points** and leaves the **feature** alone,
so everything referring to it carries on referring to it.

Confirmed on a real part: all 12 curves refreshed, `ForceRebuild3` returned
True, the boundary surface rebuilt with no reference re-picked.

The same holds for a loft. On SolidWorks 2026 a loft already in the part,
built from code on imported curves and composites of them
([features/12](../features/12-guided-loft.md)), followed its curves when they
were reloaded this way, so the tool leaves an existing loft exactly as it is
rather than rebuilding it. The one change reloading cannot carry is a change in
*how many* curves there are: see [curves/06](06-composite-curve.md) for a
composite whose pieces change.

## The three calls

```vb
Set feat = FindFeature(model, featName)
Set data = feat.GetDefinition()
data.LoadPointsFromFile path
modified = feat.ModifyDefinition(data, model, Nothing)
```

`GetDefinition` on a `CurveInFile` feature returns feature data that accepts
`LoadPointsFromFile`. `ModifyDefinition` commits it.

## The third argument

`ModifyDefinition(data, doc, component)`. The component is the assembly
component the feature belongs to, which a part does not have.

In VBScript, `Nothing` works. **In Python, `None` does not** — it is a type
mismatch, and you need a typed null:

```python
def _null_dispatch():
    """An absent COM object, typed. A bare None is a type mismatch."""
    return VARIANT(pythoncom.VT_DISPATCH, None)
```

## Full Python version

```python
def reload_curve(self, name, path):
    """Point an existing curve at a file again and commit it.

    The points are replaced; the feature, and everything referring to it,
    is untouched. No rebuild happens here — see rebuild().
    """
    doc = self._active()
    feature = self._curve_feature(name)
    definition = call(feature, "GetDefinition")
    if definition is None:
        raise SolidWorksError(f"{name} has no editable definition.")
    if not call(definition, "LoadPointsFromFile", os.path.abspath(path)):
        raise SolidWorksError(f"{name} would not read {os.path.basename(path)}.")
    if not call(feature, "ModifyDefinition", definition, doc, _null_dispatch()):
        raise SolidWorksError(f"SolidWorks would not commit the new points for {name}.")
```

## Full VBScript version, with the diagnostics that matter

```vb
Function RefreshCurve(model, featName, path)
    Dim feat, data, typeName, modified
    RefreshCurve = False

    Set feat = FindFeature(model, featName)
    If feat Is Nothing Then
        WScript.Echo "  MISSING " & featName & " - import it by hand, or run with --insert"
        Exit Function
    End If

    typeName = "?"
    On Error Resume Next
    typeName = feat.GetTypeName2()
    Err.Clear

    Set data = feat.GetDefinition()
    If Err.Number <> 0 Or data Is Nothing Then
        WScript.Echo "  FAIL    " & featName & " - no feature definition (type " & typeName & "): " & Err.Description
        Err.Clear
        Exit Function
    End If

    data.LoadPointsFromFile path
    If Err.Number <> 0 Then
        WScript.Echo "  FAIL    " & featName & " - LoadPointsFromFile not supported on type " & typeName & ": " & Err.Description
        Err.Clear
        Exit Function
    End If

    modified = feat.ModifyDefinition(data, model, Nothing)
    If Err.Number <> 0 Then
        WScript.Echo "  FAIL    " & featName & " - ModifyDefinition: " & Err.Description
        Err.Clear
        Exit Function
    End If
    On Error GoTo 0

    If modified Then
        WScript.Echo "  ok      " & featName
        RefreshCurve = True
    Else
        WScript.Echo "  FAIL    " & featName & " - ModifyDefinition returned False (type " & typeName & ")"
    End If
End Function
```

Every failure path names the **feature type**. That is deliberate: if this ever
gets pointed at a feature that is not a `CurveInFile`, you want the error to
tell you what it found. See
[reading/07](../reading/07-report-feature-type-on-failure.md).

## Works on streamed curves too

A curve created by streaming points, with no file association at all, still
accepts `LoadPointsFromFile` and `ModifyDefinition`. Both insert routes produce
the same feature type, so either is refreshable afterwards. Tested explicitly.

## On a big part, roll the tree back first

`ModifyDefinition` is charged for whatever is built below the curve. On a
397-feature part one reload cost **25 seconds**, with or without
`CommandInProgress`; with the rollback bar sitting just after the curves it
cost **0.5 seconds**. Thirty-nine curves is sixteen and a half minutes against
half a minute. See
[curves/12](12-roll-the-tree-back-before-reloading.md), which also covers
putting the bar back safely.

## Do not rebuild inside this

`RefreshCurve` deliberately does not rebuild. Refreshing twelve curves with the
rebuild live shows a real but transient error partway through, because
mid-refresh some curves carry the old geometry and some the new, so a surface
over them genuinely does not close. See
[curves/08](08-rebuild-once-at-the-end.md).

## Skipping work

Hash the file contents and record what SolidWorks was last **told**, not what
is on disk. Those are different questions: writing the file twice reports the
second write as unchanged even though SolidWorks has still not seen the first.

```typescript
const STATE_FILE = "fus-push-state.json";

interface PushState {
  part: string;
  pushed: Record<string, string>;   // feature name -> content hash
}
```

## See also

- [curves/08 — Rebuild once, at the end](08-rebuild-once-at-the-end.md)
- [curves/12 — Roll the tree back before reloading](12-roll-the-tree-back-before-reloading.md) — what this costs on a big part, and how to stop paying it
- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md)
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — a loft that follows reloaded curves
- [`code/vbscript/ImportCurves.vbs`](../../code/vbscript/ImportCurves.vbs)
