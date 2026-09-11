---
id: files-01-batch-convert-step
title: Batch-convert a tree of STEP files to SolidWorks parts
status: partly-verified
verified_on: SolidWorks 2026 SP1.1 for the C# converter; the VBA macro is the same sequence with a different save call and was not run end to end
language: [vba, csharp]
api: [ISldWorks.LoadFile4, ISldWorks.CloseDoc, ISldWorks.SetUserPreferenceToggle, IModelDoc2.GetType, IModelDoc2.SaveAs3, IModelDocExtension.SaveAs]
keywords: [STEP, stp, neutral format, import, batch convert, LoadFile4, OpenDoc6, error 2097152, swFileRequiresRepairError, swInvalidFileTypeError, 1024, task scheduler, SLDPRT, resumable]
answers: "How do I import a folder of STEP files and save each one as a SolidWorks part?"
---

# Batch-convert a tree of STEP files to SolidWorks parts

## What this is for

A vendor ships a library as STEP and you want it as `.SLDPRT`, keeping the
folder structure. Hundreds of files, so it has to run unattended, survive being
stopped, and tell you afterwards which ones failed. The interesting part is not
the walk — it is that the obvious call to open the file is the wrong one.

If you have SolidWorks Professional or Premium, **try Task Scheduler first**:
*Convert Files*, add the source folder with subfolders included, set the output
folder and the target type. It runs SolidWorks in the background and needs no
code. It does not mirror the subfolder structure in every version, so if the
output comes out flat, come back here.

## Use `LoadFile4`, not `OpenDoc6`

This is the finding that cost the time.

```vb
' ISldWorks.LoadFile4(fileName, argString, importData, ByRef error)
Set swModel = swApp.LoadFile4(stepPath, "r", Nothing, nErr)
```

`"r"` is the argument string; `Nothing` is the import-data object; the last
argument is a by-reference error out-parameter. On success `nErr` is 0 and the
return is an `IModelDoc2`.

`ISldWorks.OpenDoc6` fails on the same files with error **2097152**
(`swFileRequiresRepairError`) for every combination of open options, and it
still fails after turning off import diagnostics and full entity check:

```csharp
SetToggle(swUserPreferenceToggle_e.swImportAutoRunImportDiagnostics, false);
SetToggle(swUserPreferenceToggle_e.swImportAutoRunImportDiagnosticsPersist, false);
SetToggle(swUserPreferenceToggle_e.swImportNeutralRunDiagnostics, false);
SetToggle(swUserPreferenceToggle_e.swForceEnableImportDiagnosis, false);
SetInt(swUserPreferenceIntegerValue_e.swImportCheckAndRepair, 0);
```

`OpenDoc7` is not an alternative either: it rejects neutral formats outright.
`GetOpenDocSpec` on a `.step` returns `DocumentType = -1` and `spec.Error = 1024`
(`swInvalidFileTypeError`). Renaming the file to `.stp` changes nothing.

## Let SolidWorks decide part or assembly

A neutral file becomes whatever SolidWorks makes of it, regardless of what you
asked for. Read the type back and pick the extension from it, or the save
silently produces a file whose contents do not match its name.

```vb
' IModelDoc2.GetType: 1 = part (swDocPART), 2 = assembly (swDocASSEMBLY)
If swModel.GetType = 2 Then
    outPath = outDir & "\" & baseName & ".SLDASM"
End If

ok = swModel.SaveAs3(outPath, 0, 2)   ' 0 = current version, 2 = silent
```

The run that produced the evidence below used the extension form instead, which
is the one to prefer in new code because it hands back an error and a warning
code:

```csharp
ok = m.Extension.SaveAs(dst, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                        (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                        null, ref e2, ref w2);
```

Both take a full path. Neither creates the directory: make it yourself first.

## Close with `CloseDoc`, never `CloseAllDocuments`

`ISldWorks.CloseDoc(title)` discards silently. `CloseAllDocuments` raises the
save prompt, and in a batch that is a modal dialog nobody is there to answer.

This matters beyond the batch. Opening an older `.SLDPRT` in a newer SolidWorks
marks it modified by the version upgrade alone, so closing it offers to save,
under a **"SOLIDWORKS CAM Warning"** title that gives no hint that answering yes
**rewrites the file in the new format**. See [GOTCHAS §22](../../GOTCHAS.md).

## Set the import options first

Before the run, in *Tools > Options > System Options > Import*, with
*STEP/IGES/ACIS* selected:

| Setting | Value | Why |
|---|---|---|
| Import as | Solid/Surface bodies | The alternative runs feature recognition on every file, which is enormously slower and on imported geometry usually produces a worse tree than none. |
| Enable surface/solid entity import | checked | This is what brings the geometry in. |
| Perform full entity check and repair errors | unchecked | Roughly doubles import time. |
| Import multiple bodies as parts | unchecked | Keeps a multi-body part as one file instead of exploding it into an assembly plus loose parts. |
| Map configuration data | unchecked | Nothing in a neutral file uses it. |

If FeatureWorks is installed, make sure it is not set to run automatically on
import. Automatic feature recognition across a few hundred imported parts takes
hours and buys nothing.

The same settings are reachable from code as
`ISldWorks.SetUserPreferenceToggle`, as in the block above.

## Make it resumable

Skip anything that already has an output, and the run becomes stoppable:

```vb
If fso.FileExists(outPath) Or fso.FileExists(outDir & "\" & baseName & ".SLDASM") Then
    nSkipped = nSkipped + 1
    Exit Sub
End If
```

Log one line per file and count the outcomes. A file that fails is logged and
skipped rather than stopping the run; open the stragglers by hand afterwards.

## Why this exists as a VBA macro and as a compiled exe

`LoadFile4` and `OpenDoc6` both take `ByRef Long` out-parameters. **A late-bound
host cannot call them at all.** VBScript and PowerShell cannot produce
`VT_BYREF|VT_I4` and fail with "Type mismatch" *before the method runs*. VBA
inside SolidWorks is in-process, and C# compiled against the interop assemblies
is early-bound, so both marshal correctly. See [GOTCHAS §21](../../GOTCHAS.md).

That rules out the usual `.vbs` route from
[connect/01](../connect/01-attach-from-vbscript.md) for this particular job.

## Attach, do not launch

For a batch this inverts the advice you might expect. A COM-created instance
starts hidden, and any modal dialog then blocks every API call indefinitely with
nothing on screen to dismiss — the process sits at a few hundred megabytes,
reported as "Responding", doing nothing. Start `SLDWORKS.exe` normally, let it
finish loading, then attach. See
[connect/09](../connect/09-launch-versus-attach.md).

The C# converter also needs `[STAThread]`; SolidWorks is an STA server and
calling it from an MTA thread deadlocks
([connect/06](../connect/06-one-apartment-thread.md)). It attaches with the
versioned ProgID `SldWorks.Application.34`, because the unversioned one resolves
to a different install ([GOTCHAS §1](../../GOTCHAS.md)).

## What you get, and what it does not do

A converted part is a **dumb solid**. This is a property of STEP, not of the
conversion: no feature tree, no sketches, nothing parametric, no mate
references, no custom appearances.

**No configurations, in particular.** If the source library stored length
variants as configurations inside one file, the STEP export is one fixed file
per length, and the conversion cannot put them back. Where a part exists both
as a native file and as STEP, keep the native one.

Open questions, both unresolved:

- Whether `OpenDoc6` fails this way on STEP generally or only on this vendor's
  exports. Settling it needs STEP files from an unrelated source.
- Whether `IModelDoc2.SaveAs3` behaves identically to `IModelDocExtension.SaveAs`
  here. The VBA macro uses the former and was not run to completion; the
  evidence below is from the latter.

## Evidence

SolidWorks 2026 SP1.1, Windows. The C# converter in
[`code/csharp/SwConvert.cs`](../../code/csharp/SwConvert.cs) ran over a
108-part STEP tree and produced the `.SLDPRT` library, attaching to an
already-running SolidWorks with `Marshal.GetActiveObject`.

Observed in that run:

- `OpenDoc6` returned error 2097152 on every file tried, with import
  diagnostics and check-and-repair both off, and with several combinations of
  open options.
- `LoadFile4` with an argument string of `"r"` returned `err = 0` on the same
  files.
- `GetOpenDocSpec` on a `.step` gave `DocumentType = -1` and `Error = 1024`.
- VBScript and PowerShell raised "Type mismatch" on the out-parameter before
  either open call executed.

Not verified: the VBA macro as a whole, and `IModelDoc2.SaveAs3` specifically.
The macro is the same sequence and was corrected to `LoadFile4` after the
finding above, but it has not been run end to end. Treat its save call as
unverified until someone does.

To decode a SolidWorks error number rather than guessing at it, reflect over
the interop enum:

```powershell
[void][Reflection.Assembly]::LoadFrom("$SWDIR\SolidWorks.Interop.swconst.dll")
[Enum]::GetNames([SolidWorks.Interop.swconst.swFileLoadError_e])
```

## Full source

- [`code/vba/ConvertStepToSldprt.bas`](../../code/vba/ConvertStepToSldprt.bas) —
  the macro. Set `SRC_ROOT` and `OUT_ROOT` at the top, paste into
  *Tools > Macro > New...*, press F5.
- [`code/csharp/SwConvert.cs`](../../code/csharp/SwConvert.cs) — the compiled
  converter that produced the evidence. Build with `csc.exe` referencing
  `SolidWorks.Interop.sldworks.dll` and `SolidWorks.Interop.swconst.dll`; run as
  `SwConvert <stepFolder> <outFolder> <logFile>` with SolidWorks already open.

## See also

- [connect/01 — Attach from VBScript](../connect/01-attach-from-vbscript.md) —
  and why it cannot be used for this
- [connect/06 — One apartment thread](../connect/06-one-apartment-thread.md)
- [connect/09 — Launch versus attach](../connect/09-launch-versus-attach.md)
- [reading/01 — Dependencies of a closed file](../reading/01-dependencies-of-a-closed-file.md)
- [GOTCHAS §20, §21, §22](../../GOTCHAS.md)
