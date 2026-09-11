---
id: files-03-upgrade-file-version
title: Re-save a library into the current file version
status: verified
verified_on: SolidWorks 2026 SP1.1
language: [csharp]
api: [ISldWorks.VersionHistory, ISldWorks.GetLatestSupportedFileVersion, ISldWorks.OpenDoc6, IModelDocExtension.SaveAs]
keywords: [file version, upgrade, VersionHistory, GetLatestSupportedFileVersion, 19000, 14000, backwards compatible, downgrade, re-save, read-only attribute]
answers: "How do I re-save a whole folder of parts in the current SolidWorks file format?"
---

# Re-save a library into the current file version

## What this is for

A library written by an older SolidWorks opens in a newer one, but every open
pays a conversion cost and every close offers to save. Re-saving the whole tree
once in the current format settles it. The same job comes up when merging two
libraries written years apart so that one version stamp covers everything.

**Read the next section before running anything.**

## This is a one-way door

SolidWorks cannot open a file written by a newer release, and there is no
downgrade. Re-saving a library in 2026 means nobody on a 2025 or earlier seat
can open those files again, at all. Where the library is shared, that decision
is not yours to make quietly.

Keep the original tree, or make the upgrade a copy. This is also the reason the
run should skip files that are already current rather than rewriting everything
it touches.

## Ask the file what wrote it, without opening it

`ISldWorks.VersionHistory(path)` returns the saved-version list straight off a
closed file. `ISldWorks.GetLatestSupportedFileVersion()` is what the running
SolidWorks writes. Compare, and skip.

```csharp
// "11000[2018/134] | 14000[2021/85]" -> 14000
static bool IsCurrent(string path)
{
    try
    {
        string[] vh = (string[])sw.VersionHistory(path);
        if (vh == null || vh.Length == 0) return false;
        string last = vh[vh.Length - 1];
        int br = last.IndexOf('[');
        if (br > 0) last = last.Substring(0, br);
        int v;
        return int.TryParse(last.Trim(), out v) && v >= latestVersion;
    }
    catch { return false; }
}
```

Each entry is a file-version number with the release and build in brackets, and
the last entry is the version the file was last written by. Observed values:
`11000` is 2018, `14000` is 2021, `19000` is 2026. These are file versions, not
the major numbers in `RevisionNumber`, where 34 is 2026. Two different
numbering schemes for the same release, so do not compare across them.

## Parts before assemblies, always

Opening an assembly pulls in its components, and saving it can rewrite them
too. Convert every part first, then assemblies, then drawings, so each part is
upgraded exactly once, by itself, rather than as a side effect of something
that references it.

```csharp
Run(parts, (int)swDocumentTypes_e.swDocPART,     root, dry);
Run(asms,  (int)swDocumentTypes_e.swDocASSEMBLY, root, dry);
Run(drws,  (int)swDocumentTypes_e.swDocDRAWING,  root, dry);
```

## Save over the same path, not `Save`

```csharp
File.SetAttributes(f, FileAttributes.Normal);
int e = 0, w = 0;
doc = (ModelDoc2)sw.OpenDoc6(f, docType,
        (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
if (doc == null) throw new Exception("open failed err=" + e + " warn=" + w);

// SaveAs over the same path rewrites in the running version even
// when the model itself is unchanged; a plain Save can no-op.
int se = 0, sw2 = 0;
bool ok = doc.Extension.SaveAs(f, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref se, ref sw2);
```

Two details in there that cost time if missed. A plain `Save` can decide there
is nothing to write, because the model itself has not changed. And a library
part is often **read-only on disk**, which fails the save with no useful
message, so clear the attribute first.

Note that `OpenDoc6` is the right call here. Its failure on STEP
([files/01](01-batch-convert-step.md)) is specific to neutral formats; on native
files it is what you want.

## Make it resumable and dry-runnable

A run over a few thousand parts will be interrupted. Log one line per file and
read the log back at startup, skipping anything already recorded as done. Offer
`--dry-run`, which prints what it would rewrite and touches nothing, because on
a one-way operation the first run should always be a dry one.

## What it does not do

- It does not check that the upgraded file is still correct. Nothing here opens
  the result and compares geometry. For a library where the names carry an
  expected size, add the check in [reading/09](../reading/09-bounding-box.md).
- It does not handle references that live outside the tree. An assembly whose
  components sit elsewhere will pull those in and may rewrite them, outside the
  ordering guarantee above.
- Open question: whether the version stamp in `VersionHistory` can ever lag the
  actual format after a save, which would make the skip logic skip a file that
  still needs work. It matched on every file tried, but it has not been tested
  against a file saved by a service pack newer than the one reading it.

## Evidence

SolidWorks 2026 SP1.1. `SwUpgradeVersion.exe` re-saved a library originally at
file version `14000` (2021) so that every file reads `19000` (2026), running
parts before assemblies before drawings.

Observed:

- `VersionHistory` returned pipe-separated entries of the form
  `11000[2018/134] | 14000[2021/85]` on closed files, without opening them.
- `GetLatestSupportedFileVersion()` returned the 2026 file version on the
  running instance, and files already at it were skipped.
- `IModelDocExtension.SaveAs` over the source path rewrote files whose model
  was otherwise unchanged.

## Full source

[`code/csharp/SwUpgradeVersion.cs`](../../code/csharp/SwUpgradeVersion.cs).
Run as `SwUpgradeVersion <root> <logFile> [--dry-run]` with SolidWorks already
open.

## See also

- [files/01 — Batch-convert STEP](01-batch-convert-step.md) — the batch
  scaffolding, and why this attaches rather than launching
- [files/02 — Explode configurations](02-explode-configurations.md)
- [reading/01 — Dependencies of a closed file](../reading/01-dependencies-of-a-closed-file.md)
  — the other things readable without opening a file
- [GOTCHAS §22](../../GOTCHAS.md) — the save prompt this exists to stop
  happening by accident
