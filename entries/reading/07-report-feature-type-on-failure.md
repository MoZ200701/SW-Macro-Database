---
id: reading-07-report-feature-type-on-failure
title: Make a failing call say what it found
status: verified
verified_on: SolidWorks 2026
language: [vbscript, python]
api: [IFeature.GetTypeName2]
keywords: [GetTypeName2, error handling, diagnostics, CurveInFile, CompositeCurve, failure message]
answers: "My macro fails on a feature and I can't tell why. How do I make the error useful?"
---

# Make a failing call say what it found

## The principle

When you call a member that only exists on certain feature types, a failure has
two possible causes: the call is wrong, or the feature is not the type you
thought. **The error message must distinguish them**, and the only way to do
that is to name the type it found.

`IFeature::GetTypeName2` returns a string. `CurveInFile` for an imported curve,
`CompositeCurve` for a joined one.

## The pattern

Read the type **first**, defensively, before the call that might fail:

```vb
typeName = "?"
On Error Resume Next
typeName = feat.GetTypeName2()
Err.Clear
```

Then name it in every failure message downstream:

```vb
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
```

Defaulting `typeName` to `"?"` matters. If `GetTypeName2` itself fails, you
still get a message, and the `?` tells you the feature is stranger than expected.

## Why this pays

The concrete case it was built for: the feature-data interface for a Curve
Through XYZ Points feature is not something public sources settle. So the script
was built to **tell us what it found** rather than to assume. When it reported
`LoadPointsFromFile not supported on type CurveInFile`, that would have been a
real finding about the API. When it reported a different type name, it would
have been a naming collision with someone else's feature.

Those need completely different fixes and are indistinguishable without the
type name.

## The general form

Every call in a generated script gets individually error-trapped, and each trap
reports:

1. **Which item** — the feature name you were working on
2. **Which call** — the member that failed, by name
3. **What it found** — the type, or whatever context distinguishes the causes
4. **What SolidWorks said** — `Err.Description`

Four pieces. Dropping any one of them produces a class of bug you cannot place
from the log.

## Fixed-width status tokens

```
  ok      S1
  ok      S2
  MISSING S9 - import it by hand, or run with --insert
  FAIL    S3 - ModifyDefinition: Type mismatch
```

Lowercase for the ordinary case, uppercase for anything a person must act on.
The eye finds the uppercase lines instantly in a long log, and a caller can
parse them with a `startsWith`.

## In Python

Raise with the same content rather than returning a boolean:

```python
raise SolidWorksError(f"{name} would not read {os.path.basename(path)}.")
```

Distinct exception types for distinct causes — not available, not running,
wrong version, general COM error — let the caller decide what to do without
parsing a message. The Python module here defines four.

## See also

- [curves/04 — Reload a curve in place](../curves/04-reload-curve-in-place.md)
- [reading/14 — The journal records API calls](14-the-journal-records-api-calls.md) — what SolidWorks recorded when the program did not
- [connect/01 — Attach from VBScript](../connect/01-attach-from-vbscript.md)
- [reading/10 — Mass properties as an oracle](10-mass-properties-as-an-oracle.md) — checking what a call did, not only that it returned
- [connect/11 — Probe an API member on a live session](../connect/11-probe-an-api-member-on-a-live-session.md) — recording a failure with its HRESULT
