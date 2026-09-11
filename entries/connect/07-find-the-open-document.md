---
id: connect-07-find-open-document
title: Find the open document you want to work on
status: verified
verified_on: SolidWorks 2026
language: [vbscript, python]
api: [ISldWorks.ActiveDoc, ISldWorks.GetFirstDocument, IModelDoc2.GetNext, IModelDoc2.GetTitle]
keywords: [active document, open documents, walk documents, GetTitle, part not open]
answers: "How do I get the ModelDoc2 for the part I care about, rather than whatever is on top?"
---

# Find the open document you want to work on

## The problem

`ActiveDoc` gives you whatever is on top. That is usually right and sometimes
catastrophically wrong: the user alt-tabbed to a drawing, and your macro just
edited it. If your automation targets a named part, check the name.

## The pattern

Try the active document first, because it is almost always the answer and it is
one call. Fall back to walking every open document.

```vb
Function FindDoc(app, partName)
    Dim d, nxt, guard
    Set FindDoc = Nothing
    On Error Resume Next
    Set d = app.ActiveDoc
    If Err.Number = 0 And Not d Is Nothing Then
        If InStr(1, d.GetTitle(), partName, 1) > 0 Then
            Set FindDoc = d
            On Error GoTo 0
            Exit Function
        End If
    End If
    Err.Clear

    ' Fall back to walking the open documents. The guard matters: if GetNext
    ' errors, d would keep its old value and the loop would never end.
    Set d = app.GetFirstDocument()
    guard = 0
    Do While (Not d Is Nothing) And guard < 200
        guard = guard + 1
        If InStr(1, d.GetTitle(), partName, 1) > 0 Then
            Set FindDoc = d
            On Error GoTo 0
            Exit Function
        End If
        Set nxt = Nothing
        Set nxt = d.GetNext()
        If Err.Number <> 0 Or nxt Is Nothing Then
            Err.Clear
            Exit Do
        End If
        Set d = nxt
    Loop
    On Error GoTo 0
End Function
```

## The two details that are not obvious

**The loop guard is load-bearing.** `GetNext` can error rather than returning
`Nothing`. With `On Error Resume Next` in force, `d` then keeps its old value
and the loop runs forever. Assigning into a fresh `nxt` and checking both
`Err.Number` and nullity is what makes it terminate. The counter is a second
belt.

**Match case-insensitively, and on a substring.** `GetTitle` returns what the
tab says, which may or may not carry the extension depending on the user's
settings. `InStr(1, title, name, 1)` — the trailing `1` is
`vbTextCompare` — handles both.

## Failing usefully

If the document is not open, say so in terms the user can act on, and pick an
exit code a caller can branch on:

```vb
Set swModel = FindDoc(swApp, PART_NAME)
If swModel Is Nothing Then
    WScript.Echo "FATAL   " & PART_NAME & ".sldprt is not open in SolidWorks."
    WScript.Quit 2
End If
WScript.Echo "part    " & swModel.GetTitle()
```

Echoing the title you settled on is worth the line. When the wrong document
gets edited, this is the log line that tells you.

## Python equivalent

```python
def documents(self):
    doc = call(self._app, "GetFirstDocument")
    out = []
    guard = 0
    while doc is not None and guard < 200:
        guard += 1
        out.append(self._describe(doc))
        doc = call(doc, "GetNext")
    return out
```

Note `call`, not `doc.GetNext()`. In late-bound Python a zero-argument member
is reached by attribute access. See [connect/02](02-attach-from-python.md).

## See also

- [connect/08 — Find a feature by name](08-find-a-feature-by-name.md)
