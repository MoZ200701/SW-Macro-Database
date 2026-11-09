---
id: assemblies-09-keep-an-edited-toolbox-copy
title: Use an edited copy of a Toolbox part in an assembly without it reverting to the library part
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IModelDocExtension.ToolboxPartType, IModelDoc2.Save3, IComponent2.Select4, IAssemblyDoc.ReplaceComponents2, IComponent2.GetPathName, IComponent2.ReferencedConfiguration, ISldWorks.OpenDoc6, ISldWorks.CloseDoc]
keywords: [Toolbox, toolbox part, edited toolbox part, toolbox copy, sldsetdocprop, Toolbox status, ToolboxPartType, swToolBoxPartType_e, swToolboxStandardPart, swToolboxCopiedPart, swNotAToolboxPart, reverts to library, edits disappeared, SOLIDWORKS Data browser, ReplaceComponents2, replace component, mates lost after reopen, error 51, read-only toolbox part]
answers: "I edited a Toolbox part (or a copy of one) and on reopening the assembly my edits are gone and the mates fail — why, and how do I make the edited copy stick?"
---

# Keep an edited copy of a Toolbox part in an assembly

## What this is for

A Toolbox part (a gear, a fastener) inserted into an assembly, then changed by
hand — a bore cut to a non-standard size, a recess added — and saved as a copy.
The assembly appears to use the copy, but after closing and reopening it the
components point at the **library** parts again, the hand-made features are
gone, and every mate on them fails. This entry is why, and the order of steps
that made the copy stay.

## What the assembly does with a Toolbox-flagged component

Each part carries a Toolbox status, readable on
`IModelDocExtension.ToolboxPartType` (`swToolBoxPartType_e`: 0 not a Toolbox
part, 1 standard Toolbox part, 2 copied part). A **Save As copy keeps 1**.

While the copies still read 1:

1. `IAssemblyDoc.ReplaceComponents2` swapped each library component for its
   copy, every mate solved, and the assembly was saved.
2. On reopening, all three components pointed back at the library files under
   the Toolbox folder (`…\SOLIDWORKS Data\browser\…`), with new instance
   numbers. The load warning was 2 + 32 (read-only, needs rebuild). The mates
   read `GetErrorCode2` 0 **only because the assembly had not been rebuilt**;
   the library parts lacked the bores the mates referenced, so a rebuild would
   have failed them, as it had before (error 51).

So an assembly finds a Toolbox-flagged component through the Toolbox library,
not through the path it was saved with.

## The order that worked

1. **Clear the flag on the copy and save it.** The put is a property put
   through `Invoke`:

   ```python
   ext=call(d,"Extension"); ole=ext._oleobj_; dispid=ole.GetIDsOfNames("ToolboxPartType")
   before=call(ext,"ToolboxPartType")
   ole.Invoke(dispid,0,pythoncom.DISPATCH_PROPERTYPUT,False,0)
   after=call(ext,"ToolboxPartType")
   ```

   then `IModelDoc2.Save3(1, err, warn)`. Read back 0. (The type library
   lists `ToolboxPartType` with both a get, `invkind` 2, and a put, 4.)
   Toolbox's own `sldsetdocprop.exe` (Property state: No) did the same from
   outside, on closed files; use the one from the **same SolidWorks version**
   as the files.
2. **Then replace the components** with the now-unflagged copies, one at a
   time, and save the assembly. **Re-saving the assembly is not enough**:
   with the copies already unflagged and in the assembly, a save, close and
   reopen still redirected both to the library. Only a replace after the flag
   was cleared stuck.

   ```python
   T(asm,"ClearSelection2",True)
   ok = comp.Select4(False, VARIANT(pythoncom.VT_DISPATCH,None), False)
   n = T(T(asm,"SelectionManager"),"GetSelectedObjectCount2",-1)
   r = asm.ReplaceComponents2(path, cfg, False, 1, True)
   ```

   `ReplaceComponents2(fileName, configName, replaceAllInstances,
   configChoice, reattachMates)`: `False` replaced only the selected instance —
   needed when two components share one library file but should get different
   copies — `1` (`swReplaceComponentsConfiguration_ManuallySelect`) used the
   configuration named, and `True` re-attached the mates. The replaced
   component was found again by its path.
3. **Prove it by reopening.** Close the assembly *and* the copies (otherwise
   reopening uses the documents in memory), reopen with `OpenDoc6`, rebuild,
   and read each component's `GetPathName`, `ToolboxPartType` and the mates'
   `GetErrorCode2`.

## Why it is not obvious

- Everything looks right until the assembly is reopened: paths, geometry and
  mates are all correct in the session that made the change.
- The library files had been modified by the session that made the edits
  (larger, newer timestamps), but the library parts as loaded later did not
  contain the added features. Whether they were never saved there or were
  removed on load was not established. Edit a copy, not the library part.
- After the redirect, mates read clean until a rebuild. Rebuild before trusting
  an error count ([GOTCHAS §90](../../GOTCHAS.md)).

## What it does not do

- **Pack and Go** with flagged and unflagged copies was not tested.
- **Status 2 (copied part)** was never seen; the copies read 1 until cleared.
- **Open:** whether the redirect depends on the Toolbox add-in being loaded.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), two assemblies each with
several gears copied from Toolbox and edited.

- Copies saved with status 1, swapped in with `ReplaceComponents2` (each `True`,
  mates 0 after rebuild), assembly saved: on reopen every copy was replaced by
  its library part, warning 34.
- Flag cleared with `sldsetdocprop` (2024 build), assembly reopened: still the
  library parts. `ReplaceComponents2` onto the unflagged copies (status read 0),
  rebuild, save, reopen: load warning 0, paths on the copies, all 12 mates 0
  after `ForceRebuild3`.
- Second assembly: flag cleared over COM with the property put above (1 → 0,
  `Save3` `True`), assembly re-saved, copies closed, reopened: redirected to
  the library. Replaced, saved, copies closed, reopened: on the copies, status 0,
  11 mates 0.

## See also

- [assemblies/01 — New assembly and insert components](01-new-assembly-and-insert-components.md)
- [assemblies/06 — Read every mate](06-read-every-mate.md) — `GetErrorCode2` on every mate
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — the property put through `Invoke`
- [GOTCHAS §90](../../GOTCHAS.md)
