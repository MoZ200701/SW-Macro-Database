---
id: files-05-rename-a-component-file
title: Rename a component's part file from inside its assembly, keeping references
status: verified
verified_on: SolidWorks 2024 SP5 (32.5.0)
language: [python]
api: [IModelDocExtension.RenameDocument, IModelDocExtension.HasRenamedDocuments, IComponent2.Select4, IComponent2.Name2, IComponent2.GetPathName, IModelDoc2.Save3, IConfiguration.GetRootComponent3, IComponent2.GetChildren]
keywords: [rename part, rename component, rename file, RenameDocument, HasRenamedDocuments, swRenameDocumentError_e, keep references, rename without breaking assembly, Save3 save referenced, swSaveAsOptions_SaveReferenced, case-only rename, Pack and Go alternative, BOM part number]
answers: "How do I rename the part files of an assembly's components from code without breaking the assembly's references?"
---

# Rename a component's part file from inside its assembly

## What this is for

Part files named during modelling ("Part7", "Bracket old (simplified)") that
should carry their proper names, for a bill of materials or a hand-in. Renaming
in Windows breaks the assembly; Pack and Go copies. SolidWorks' own rename, from
the assembly, renames the file and updates every open reference to it.

Attach as in [assemblies/06](../assemblies/06-read-every-mate.md#attaching). The
assembly must be the active document, with its components resolved.

## The call

Select the component, `IModelDocExtension.RenameDocument(newName)`, and save the
assembly **with its references**:

```python
ERR = {0: "None", 2: "InvalidSelection", 5: "ComponentNotResolved", 6: "LightWeightComponent", 8: "FileAlreadyExists",
       9: "InvalidCharactersInName", 12: "DocumentNameInUse", 13: "PendingNameAlreadyInUse", 14: "ReadOnlyDocument", 15: "DocumentNotSaved"}
def comps():
    return {call(c, "Name2").rsplit("-", 1)[0]: c for c in call(call(call(doc, "GetActiveConfiguration"), "GetRootComponent3", True), "GetChildren")}
before = {k: call(c, "GetPathName") for k, c in comps().items()}
renamed = 0
for old, new in pairs:
    c = comps().get(old)
    if c is None: print(f"MISSING component {old}"); continue
    call(doc, "ClearSelection2", True)
    sel = c.Select4(False, nul, False)
    r = ext.RenameDocument(new)
    print(f"rename  {old!r} -> {new!r}: select={sel} result={r} {ERR.get(r, '')}")
    if r == 0: renamed += 1
call(doc, "ClearSelection2", True)
print("HasRenamedDocuments", call(ext, "HasRenamedDocuments"))
if renamed:
    err = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0); warn = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
    ok = doc.Save3(1 | 4, err, warn)
    print(f"save    ok={ok} err={err.value} warn={warn.value}")
```

- `newName` is the file name **without** extension or folder; the file stays in
  its folder.
- `RenameDocument` returns `swRenameDocumentError_e`; 0 is success. The table
  above is that enum's values as read from the API help; only 0 was returned here.
- **Nothing changes on disk until the save.** `RenameDocument` is pending
  (`HasRenamedDocuments` reads `True`); `Save3(1 | 4, …)` — silent, save
  referenced documents — writes the renamed files and the assembly.
- Re-read the components after each rename: `comps()` is called again inside
  the loop because a renamed component's `Name2` changes.
- Do not run this while the user has unsaved edits in the assembly or its parts:
  the save writes those too.

## What it does not do

- **Drawings and other assemblies that are not open** were not checked. Open
  them before renaming so their references update, or check them afterwards.
- **Moving a file to another folder** is not this call; see
  [reading/03](../reading/03-repair-references-after-a-move.md).
- **Part numbers set separately from the file name** were not met: these parts
  used the file name as their part number, so a BOM showed the new names.

## Evidence

SolidWorks 2024 SP5 (`RevisionNumber` 32.5.0), 2026-10-30, a nine-part assembly
with parts in two folders. Eight renames over three runs (one a case-only change,
`pin` → `Pin`): every `Select4` `True`, every `RenameDocument` 0,
`HasRenamedDocuments` `True`, `Save3(1 | 4)` `True` with err 0 and warn 0. After
each save every component's `GetPathName` pointed at the new name, the new files
existed, the old files no longer existed, and later work on the assembly in the same
session found its mates intact; an assembly drawing made afterwards listed the new names in its BOM.
Names are placeholders here.

## See also

- [reading/03 — Repair references after a move](../reading/03-repair-references-after-a-move.md)
- [curves/07 — Rename a feature](../curves/07-rename-a-feature.md) — renaming features, not files
- [drawings/02 — BOM table and balloons](../drawings/02-bom-table-and-balloons.md)
- [documents/02 — Save as and close](../documents/02-save-as-and-close.md)
- [GOTCHAS §8, §16](../../GOTCHAS.md)
