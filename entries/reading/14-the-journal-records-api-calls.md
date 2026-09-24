---
id: reading-14-the-journal-records-api-calls
title: Read what a program did to a part from the SolidWorks journal
status: verified
verified_on: SolidWorks 2026 SP0.0 (revision 34.0.0)
language: [n/a]
api: []
keywords: [swxJRNL.swj, swxJRNL.BAK, journal, macro journal, what did the macro do, post-mortem, audit, InsertCurveFileBegin, InsertCurveFilePoint, SelectByID2, InsertFeatureTreeFolder2, InsertProtrusionBlend, Save3, SaveAs, rename not recorded, Select2 not recorded, diagnose after the fact]
answers: "How can I find out afterwards which API calls a program made on a part, and in what order, when it kept no log?"
---

# Read what a program did to a part from the SolidWorks journal

## What this is for

Something went wrong in a part hours ago — curves left out of their folders,
a feature in the wrong place — and the program that did it kept no log of its
calls. SolidWorks did: its journal records API calls made **from another
process** over COM, not only what the user clicks, as a VBA macro. It is often
the only record of what happened, in what order, and which of it was the
user.

It is also incomplete and in places rewritten. This entry says what was seen
in it and what was not.

## Where it is

```
%APPDATA%\SolidWorks\SOLIDWORKS 2026\swxJRNL.swj    this session, written as it goes
%APPDATA%\SolidWorks\SOLIDWORKS 2026\swxJRNL.BAK    the session before
```

Plain text (CRLF), a VBA macro with a header naming the date and the user:

```vb
' ******************************************************************************
' C:\Users\M0obo\AppData\Roaming\SolidWorks\SOLIDWORKS 2026\swxJRNL.swj - journal recorded on 09/24/26 by M0obo
' *****************************************************************************
Dim swApp As Object

Dim Part As Object
Dim boolstatus As Boolean
Dim longstatus As Long, longwarnings As Long

Sub main()
```

**Only two sessions are kept.** Each start of SolidWorks moves `.swj` to
`.BAK`, overwriting the older one. A crash, a restart and a second restart
lose the crash session's journal. Copy both files away before restarting
SolidWorks if you will want them.

There are **no timestamps**. Order is all you get; tie it to the clock through
something else that was written at the time (file modification times, a
record the program saved).

## What was recorded, and how

From two sessions in which an external Python program (pywin32, late binding)
inserted about 1,000 curves, joined them, foldered them and lofted them:

| The program called | The journal shows |
|---|---|
| `IModelDoc2.InsertCurveFile(path)` | `Part.InsertCurveFileBegin`, one `Part.InsertCurveFilePoint(x, y, z)` per point **in metres**, `Part.InsertCurveFileEnd()`. The file's path is not recorded |
| `IModelDocExtension.SelectByID2(name, "REFERENCECURVES", …)` | The same call, with its arguments |
| `IModelDoc2.InsertCompositeCurve` | `Part.InsertCompositeCurve` |
| `IFeatureManager.InsertFeatureTreeFolder2(2)` | `Part.FeatureManager.InsertFeatureTreeFolder2(swFeatureTreeFolderType_e.swFeatureTreeFolder_Containing)` — the enum **name** for the value 2 |
| `IFeatureManager.InsertProtrusionBlend2(…)` (18 arguments) | `Part.FeatureManager.InsertProtrusionBlend …` with 17 arguments |
| `IModelDoc2.EditRebuild3` | `Part.EditRebuild3()` |
| `ISldWorks.NewDocument(template, …)` | The same, with the template path |
| `ISldWorks.CloseDoc(title)` | The same |
| `IModelDocExtension.SaveAs(path.STEP, 0, silent + copy, …)` | **`Part.Save3(1, swErrors, swWarnings)`** — see below |

Real lines, from one export of a wing: the composite for one section, then the
folders made after the last composite, then the loft's guide selections and
the loft:

```vb
boolstatus = Part.Extension.SelectByID2("Wing_cut_outer_s22_lower", "REFERENCECURVES", 0, 0, 0, False, 0, Nothing, 0)
boolstatus = Part.Extension.SelectByID2("Wing_cut_outer_s22_upper", "REFERENCECURVES", 0, 0, 0, True, 0, Nothing, 0)
boolstatus = Part.Extension.SelectByID2("Wing_cut_outer_s22_te", "REFERENCECURVES", 0, 0, 0, True, 0, Nothing, 0)
Part.InsertCompositeCurve
Part.ClearSelection2 True
Set myFeature = Part.FeatureManager.InsertFeatureTreeFolder2(swFeatureTreeFolderType_e.swFeatureTreeFolder_Containing)
Part.ClearSelection2 True
Set myFeature = Part.FeatureManager.InsertFeatureTreeFolder2(swFeatureTreeFolderType_e.swFeatureTreeFolder_Containing)
Part.ClearSelection2 True
Set myFeature = Part.FeatureManager.InsertFeatureTreeFolder2(swFeatureTreeFolderType_e.swFeatureTreeFolder_Containing)
Part.ClearSelection2 True
```

```vb
boolstatus = Part.Extension.SelectByID2("Wing_cut_outer_te_upper", "REFERENCECURVES", 0, 0, 0, True, 2, Nothing, 0)
boolstatus = Part.Extension.SelectByID2("Wing_cut_outer_te_lower", "REFERENCECURVES", 0, 0, 0, True, 2, Nothing, 0)
Part.FeatureManager.InsertProtrusionBlend False, True, False, 1, 0, 0, 1, 1, False, False, False, 0, 0, 0, False, True, True
Part.ClearSelection2 True
boolstatus = Part.EditRebuild3()
```

What the user did in the same session is in the same stream, in the same form:
a rollback, a folder made by hand and renamed in the tree:

```vb
boolstatus = Part.FeatureManager.EditRollback(swMoveRollbackBarTo_e.swMoveRollbackBarToBeforeFeature, "")
Set myFeature = Part.FeatureManager.InsertFeatureTreeFolder2(swFeatureTreeFolderType_e.swFeatureTreeFolder_Containing)
boolstatus = Part.SelectedFeatureProperties(0, 0, 0, 0, 0, 0, 0, 1, 0, "Attachment Inner Curve")
```

Nothing marks which lines were the user's. Tell them apart by what the program
is known to call — this program never calls `SelectedFeatureProperties` — and
by selections of bodies and rays that only a mouse makes.

## What it leaves out or rewrites

- **`IFeature.Select2` is not recorded.** The program selected features for
  its folders through `IFeature.Select2`; the three `InsertFeatureTreeFolder2`
  lines above have no selection before them. Selections through
  `SelectByID2` are recorded. So the journal cannot say what a folder was made
  around.
- **Renames through `IFeature.Name` are not recorded.** Not one of hundreds
  appears. A rename made in the tree is recorded, as
  `SelectedFeatureProperties(…, "new name")`.
- **Reads are not recorded**: tree walks, `GetMassProperties`,
  `FeatureByName`, `GetBodies2`.
- **A save-as-copy is written as a save.** `IModelDocExtension.SaveAs` to a
  `.STEP` file, with the copy option, appears as
  `Part.Save3(1, swErrors, swWarnings)` under a `' Save` comment. Five new
  parts that were never saved as parts — `Part3` to `Part7`, each exported to
  STEP and closed — each show that `Save3` just before their `CloseDoc`; a
  real `Save3` on a part with no file would have asked for a name, and nobody
  was there to answer. A `SaveAs` to a new `.SLDPRT` appears the same way. Do not read a `Save3` in the journal as
  proof the part on disk was written.
- **Arguments can be translated.** `InsertProtrusionBlend2`'s 18 arguments
  came out as `InsertProtrusionBlend` with 17; the value `2` for
  `InsertFeatureTreeFolder2` came out as an enum name. Treat the journal as a
  description of what happened, not as a macro that will replay it.
- **Deletions: not established.** No `DeleteSelection2` line appeared in a
  session where the program's folder arrangement may have deleted folders; it
  was not confirmed that it did, so whether deletions are recorded is open.

## Why it was worth reading

In a part where two exports' curves were left outside their folders, the
journal showed that one of the two had been foldered by hand (the
`SelectedFeatureProperties` rename above), that the user had rolled the tree
back before the program's next export — which is why that export's curves sat
above the lofts — and exactly which folder calls the program had made after
each export. That narrowed a tree-reading bug down to the folder code, which
was then reproduced in a scratch part
([curves/11](../curves/11-feature-tree-folders.md)).

## Evidence

SolidWorks 2026 SP0.0 (revision 34.0.0), 2026-09-24. `swxJRNL.BAK` (3.6 MB,
33,621 lines) covered a session in which the Airfoil Converter inserted 189
curves, 41 composites, folders and two lofts into a user's part; `swxJRNL.swj`
(19.5 MB) covered a session in which scripted experiments made 7 new parts,
849 curve inserts, 182 composites, 36 folders and 10 lofts. Counted in the
`.swj`: `InsertCurveFileBegin` and `InsertCurveFileEnd` 849 each,
`InsertCompositeCurve` 182, `InsertFeatureTreeFolder2` 36,
`InsertProtrusionBlend` 10, `NewDocument` 7, and no line setting a feature's
`Name` although every insert was renamed. The `Save3` lines and their
unsaved parts are as described above; for example:

```vb
boolstatus = Part.EditRebuild3()

' Save
boolstatus = Part.Save3(1, swErrors, swWarnings)

' Close Document
Set Part = Nothing
swApp.CloseDoc "Part3"
```

## See also

- [reading/07 — Report the feature type on failure](07-report-feature-type-on-failure.md)
  — the log a program should have kept in the first place
- [curves/11 — Feature-tree folders](../curves/11-feature-tree-folders.md) —
  the bug this was used to find
- [files/04 — Export a body to STEP](../files/04-export-a-body-to-step.md) —
  the `SaveAs` the journal shows as `Save3`
- [GOTCHAS §69](../../GOTCHAS.md)
