---
id: files-02-explode-configurations
title: Write one file per configuration out of a configurable part
status: verified
verified_on: SolidWorks 2026 SP1.1
language: [vba, csharp]
api: [ISldWorks.GetConfigurationNames, IModelDoc2.ShowConfiguration2, IModelDoc2.DeleteDesignTable, IModelDoc2.DeleteConfiguration2, IModelDoc2.GetConfigurationNames, IModelDoc2.Save3, IConfigurationManager.ActiveConfiguration]
keywords: [configuration, design table, ShowConfiguration2, DeleteConfiguration2, DeleteDesignTable, derived configuration, save configuration as part, one file per length, explode]
answers: "How do I turn a part with many configurations into one standalone file per configuration?"
---

# Write one file per configuration out of a configurable part

## What this is for

A library ships each cut-to-length metal as one part carrying a configuration
per length, driven by a design table. Some workflows cannot use that: a bill of
materials wanting a row per part number, a drag-and-drop parts folder, an
exporter that ignores configurations. You want a real file per length, and the
master left alone.

## There is no API for this

That is the finding. SolidWorks has no "save this configuration as its own
part" call, and `Save As` on a configured part copies **every** configuration
into the output. So the file has to be reduced to the one you want, in a copy:

1. Copy the master to the destination name.
2. Open the copy.
3. Activate the wanted configuration.
4. Delete the design table.
5. Delete every other configuration.
6. Rebuild, and save.

Only the copy is ever touched. The master keeps its table and all its
configurations.

## The sequence

```vb
fso.CopyFile masterPath, destPath, True
fso.GetFile(destPath).Attributes = 0      ' master may be read-only

' 1 = swDocPART, 1 = swOpenDocOptions_Silent
Set swModel = swApp.OpenDoc6(destPath, 1, 1, "", nErr, nWarn)

' ShowConfiguration2 returns False when the configuration is already the
' active one - which is exactly the case for whichever config the master
' was last saved in ("35" for the C-Channels). Only a name mismatch
' afterwards is a real failure.
swModel.ShowConfiguration2 cfg
If swModel.ConfigurationManager.ActiveConfiguration.Name <> cfg Then
    WriteLog "  CFGFAIL  cannot activate '" & cfg & "' in " & baseName
    GoTo Failed
End If

' Must go before the deletions. Not every configurable part has a design
' table, so a failure here is fine.
On Error Resume Next
swModel.DeleteDesignTable
On Error GoTo Failed

' Re-read the list each pass: deleting a parent configuration takes its
' derived children with it, so a single pass can try to delete a name
' that is already gone.
For pass = 1 To 4
    names = swModel.GetConfigurationNames
    If UBound(names) < 1 Then Exit For
    For j = 0 To UBound(names)
        If CStr(names(j)) <> cfg Then
            On Error Resume Next
            swModel.DeleteConfiguration2 CStr(names(j))
            On Error GoTo Failed
        End If
    Next j
Next pass

names = swModel.GetConfigurationNames
If UBound(names) <> 0 Or CStr(names(0)) <> cfg Then
    WriteLog "  PRUNEFAIL " & cfg & " " & baseName & " - left " & (UBound(names) + 1) & " configs"
    GoTo Failed
End If

swModel.EditRebuild3

' 1 = swSaveAsOptions_Silent
If Not swModel.Save3(1, nErr, nWarn) Then
```

`ShowConfiguration2`, `DeleteDesignTable`, `DeleteConfiguration2`,
`GetConfigurationNames` and `Save3` are all on `IModelDoc2`.
`ActiveConfiguration` is on `IConfigurationManager`, reached through
`IModelDoc2.ConfigurationManager`.

## Why it is not obvious

Three traps, all of which produce a wrong file rather than an error.

**`ShowConfiguration2` returns False when the configuration is already
active.** Treat that return as failure and the run breaks on exactly one
configuration per part: whichever one the master was last saved in. Read
`ActiveConfiguration.Name` back instead and compare. This is the same shape of
problem as reading a feature name back after setting it
([GOTCHAS §8](../../GOTCHAS.md)).

**The design table owns the configurations.** Leave it in place and the
deletions either fail outright or come back on the next edit, so the output
still has all of them. Delete the table first. Wrap it loosely: a part that is
configured by hand has no table, and that is not an error.

**Configurations delete in a cascade.** A derived configuration goes when its
parent goes, so a single pass over a name list captured up front will try to
delete names that no longer exist. Re-read `GetConfigurationNames` on each pass
and loop until one name is left. Then assert that the one left is the one you
wanted, before saving.

## Which configurations exist, without opening anything

`ISldWorks.GetConfigurationNames(path)` takes a **file path** and reads the
names straight out of the closed file. The same name on `IModelDoc2` reads them
from an open document. Use the application one to plan the work and the
document one to check it. See
[reading/01](../reading/01-dependencies-of-a-closed-file.md).

## Check the geometry afterwards

A file that saved cleanly can still hold the wrong solid. Where the
configuration name implies a size, measure the output and compare. That check
is [reading/09](../reading/09-bounding-box.md), and it is what caught the defect
described below.

## What it does not do

- **It does not scale.** One file per configuration means disk. A 35-length
  part is 35 files and several hundred megabytes, at roughly 5 seconds each.
  A part configured in two dimensions, such as a plate named `width x length`,
  can carry a few hundred configurations on its own.
- **Only the copy is single-configuration.** Anything that referenced the
  master still references the master. This is a good thing: an assembly built
  on the master can be re-lengthed without swapping a reference, and the
  exploded files cannot.
- Open question: whether four passes is always enough for the cascade. Four was
  sufficient for every part tried, and the assertion afterwards catches the case
  where it is not, but the depth at which it would fail is unknown. Settling it
  needs a part with deeper derived-configuration nesting than this library has.

## Evidence

SolidWorks 2026 SP1.1. `SwExplodeConfigs.exe` wrote **315 files** from 9
masters: 175 from 5 C-Channel masters and 140 from 4 Angle masters, each
carrying configurations `1` through `35`.

Observed:

- `ShowConfiguration2` returned False on the master's last-saved configuration
  and the activation had nonetheless happened, confirmed by reading
  `ActiveConfiguration.Name`.
- After `DeleteDesignTable` and four delete passes, `GetConfigurationNames` on
  the saved copy returned exactly one name in all 315 files, checked again on a
  re-open pass.
- Every output's bounding box was measured against the length its name implies.
  **Two came back wrong**, and the cause was upstream: configuration `33` of
  `Aluminum 2x2 Angle` and `Steel 2x2 Angle` is 16.990 in in the master where
  33 holes at 0.5 in is 16.500 in. `32` is 16.000 and `34` is 17.000, so only
  `33` is off. A bad value in the original design table, faithfully copied.

That last point is the argument for the check. The conversion was correct and
the output was still wrong, and nothing in the save path could have told you.

The VBA macro is the same sequence and was written from the same run, but the
315 files were produced by the C# tool. Both are in `code/`.

## Full source

- [`code/vba/ExplodeConfigsToFiles.bas`](../../code/vba/ExplodeConfigsToFiles.bas)
  — the macro. Set `LIB_ROOT`, list the folders to process in
  `TargetFolders()`, press F5.
- [`code/csharp/SwExplodeConfigs.cs`](../../code/csharp/SwExplodeConfigs.cs) —
  the tool that produced the evidence, with `--dry-run`, `--verify` and
  `--check` modes.

## See also

- [files/01 — Batch-convert STEP](01-batch-convert-step.md) — the same batch
  scaffolding, and why a neutral format has no configurations to explode
- [files/03 — Upgrade a file version](03-upgrade-file-version.md)
- [reading/01 — Dependencies of a closed file](../reading/01-dependencies-of-a-closed-file.md)
- [reading/09 — Bounding box as a check](../reading/09-bounding-box.md)
- [GOTCHAS §23, §24](../../GOTCHAS.md)
