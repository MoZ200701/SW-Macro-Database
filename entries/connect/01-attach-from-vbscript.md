---
id: connect-01-attach-vbscript
title: Attach to a running SolidWorks from VBScript
status: verified
verified_on: SolidWorks 2026
language: [vbscript]
api: [GetObject, CreateObject, ISldWorks.Visible]
keywords: [attach, connect, running object table, progid, GetObject, error 429, cscript, generated script]
answers: "How do I reach an already-open SolidWorks from a standalone script?"
---

# Attach to a running SolidWorks from VBScript

**Status:** Verified, SolidWorks 2026
**Language:** VBScript, run with `cscript //nologo`

## What this is for

You want a script you can run from a terminal, a batch file or another program,
that reaches into the SolidWorks the user already has open and does something
to the part on screen. No Visual Studio, no compiled binary, no macro file.

VBScript is the right tool here for one specific reason: **a `.swp` macro file
is a binary and cannot be emitted as text.** If your program needs to *generate*
the automation rather than ship a fixed one, plain VBScript is the only route
that stays a text file all the way through.

## The attach

Try each candidate ProgID against the Running Object Table and take the first
one that answers. Echo which one worked, because when this goes wrong the
version is always the reason.

```vb
' Try each ProgID against the Running Object Table and take the first hit.
Function Attach(ids)
    Dim k, app
    Set Attach = Nothing
    On Error Resume Next
    For k = 0 To UBound(ids)
        Err.Clear
        Set app = GetObject(, ids(k))
        If Err.Number = 0 And Not app Is Nothing Then
            WScript.Echo "attach  " & ids(k)
            Set Attach = app
            Err.Clear
            On Error GoTo 0
            Exit Function
        End If
    Next
    Err.Clear
    On Error GoTo 0
End Function
```

Called like this:

```vb
Dim progIds, swApp
progIds = Array("SldWorks.Application.34", _
                "SldWorks.Application.32", _
                "SldWorks.Application")
Set swApp = Attach(progIds)
If swApp Is Nothing Then
    WScript.Echo "FATAL   no running SolidWorks reachable over COM."
    WScript.Echo "        Tried: " & Join(progIds, ", ")
    WScript.Quit 2
End If
```

## Why the list, and why newest first

`GetObject(, "SldWorks.Application")` looks like it should always work. It does
not, when more than one major version is installed: the unversioned ProgID
resolves to one version's class id, and if the running session is a different
version it is registered under a different class id and the lookup misses. The
symptom is error 429 while SolidWorks is plainly open. [GOTCHAS §1](../../GOTCHAS.md)
has the registry evidence.

Generate the list rather than hard-coding it, so an upgrade does not break the
script. Enumerate the `SldWorks.Application.*` keys under `HKEY_CLASSES_ROOT`,
sort descending by the numeric suffix, and append the unversioned one as a last
resort. Majors advance by one a year: **32 is 2024, 33 is 2025, 34 is 2026.**

## Deliberately not falling back to CreateObject

`CreateObject` succeeds where `GetObject` fails, which makes it a tempting
fallback. Resist it by default. If no instance is reachable, `CreateObject` may
start a **second** SolidWorks rather than attaching to the one on screen, and
you then silently edit an invisible document. Make it an explicit opt-in flag:

```vb
If swApp Is Nothing Then
    WScript.Echo "note    nothing in the ROT; starting one with CreateObject"
    On Error Resume Next
    Set swApp = CreateObject(progIds(0))
    If Err.Number <> 0 Or swApp Is Nothing Then
        WScript.Echo "FATAL   CreateObject(" & progIds(0) & ") failed: " & Err.Description
        WScript.Quit 2
    End If
    swApp.Visible = True
    On Error GoTo 0
End If
```

## Error handling shape

VBScript has no structured exceptions, so every SolidWorks call gets the same
three-line treatment: turn on `On Error Resume Next`, make the call, check
`Err.Number`, clear it. Turn error handling back off with `On Error GoTo 0` as
soon as you are past the fragile call, so a genuine bug in your own script still
stops loudly.

Exit codes make the script usable from a build system. The convention used here:
0 fine, 1 some operations failed, 2 could not connect or the part was not open.

## Full working script

[`code/vbscript/ImportCurves.vbs`](../../code/vbscript/ImportCurves.vbs) is a
complete generated example: attach, find the document, refresh a list of curve
features from files, insert any that are missing, rebuild once, report per
feature. Roughly 200 lines, and every SolidWorks call in it is individually
error-trapped.

Run it with:

```
cscript //nologo ImportCurves.vbs
```

## See also

- [connect/05 — Choosing among versions](05-choosing-among-versions.md)
- [connect/07 — Find the open document](07-find-the-open-document.md)
- [connect/09 — Launch versus attach](09-launch-versus-attach.md)
- [files/01 — Batch-convert STEP](../files/01-batch-convert-step.md) — a job
  VBScript cannot do, and why
- [GOTCHAS §1](../../GOTCHAS.md)
