---
id: connect-04-attach-vba
title: Attach from VBA, inside SolidWorks
status: verified
verified_on: standard across versions
language: [vba]
api: [Application.SldWorks, ISldWorks.ActiveDoc]
keywords: [vba, macro, late binding, swp binary, bas file, Application.SldWorks, paste macro]
answers: "What is the preamble for a macro that runs inside SolidWorks?"
---

# Attach from VBA, inside SolidWorks

**Status:** Standard, universally documented
**Language:** VBA, in the SolidWorks macro editor

## The easy case

A macro running inside SolidWorks does not need to attach to anything. The
application object is handed to it:

```vb
Option Explicit

Sub CreateSketch()
    Dim swApp As Object, Part As Object
    Set swApp = Application.SldWorks
    Set Part = swApp.ActiveDoc
    If Part Is Nothing Then
        MsgBox "Open the target part and select the sketch plane first."
        Exit Sub
    End If

    ' ... work here ...
End Sub
```

That is the whole preamble, and it is the one every generated macro in this
collection starts with.

## Late binding, deliberately

`Dim swApp As Object` rather than `Dim swApp As SldWorks.SldWorks`. Declaring
the interop types requires a reference to be set in the VBA project, which the
person pasting your generated macro has to do by hand and will not. `Object`
plus late binding runs anywhere with no setup.

The cost is no IntelliSense and no compile-time member checking, which matters
much less for generated code than for hand-written code.

## Guard the document

`ActiveDoc` is `Nothing` when no document is open, and every subsequent line
then fails with a null reference. Check it once at the top and exit with a
message a person can act on.

## When to reach for VBA rather than VBScript

Use VBA when the automation is something a **person** runs, on a part they have
open, having first made a selection. It is the only route where "select a plane,
then run this" is a natural instruction, and sketch creation genuinely needs
that: `InsertSketch` uses whatever is currently selected.

Use VBScript when a **program** is driving, unattended.
See [connect/01](01-attach-from-vbscript.md).

## Getting a generated macro into SolidWorks

The generated files in this collection are `.bas` text. To run one:

1. **Tools → Macro → New**, save a `.swp` somewhere.
2. The editor opens. Delete the stub `Sub`.
3. Paste the whole `.bas` content in.
4. Make the selection the macro's header asks for, if any.
5. **Run**.

A `.swp` is a binary container, which is why generated automation ships as
`.bas` text to be pasted, or as VBScript to be run directly.

## See also

- [sketches/01 — A 2D sketch as VBA](../sketches/01-2d-sketch-as-vba.md)
- [sketches/02 — A 3D sketch as VBA](../sketches/02-3d-sketch-as-vba.md)
