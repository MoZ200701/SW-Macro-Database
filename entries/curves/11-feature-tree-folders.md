---
id: curves-11-feature-tree-folders
title: Put features in a feature-tree folder
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [IFeatureManager.InsertFeatureTreeFolder2, IFeatureManager.MoveToFolder, IModelDocExtension.DeleteSelection2, IFeature.Select2, IFeature.GetSpecificFeature2, IFeatureFolder.GetFeatureCount, IFeatureFolder.GetFeatures]
keywords: [InsertFeatureTreeFolder2, MoveToFolder, swFeatureTreeFolderType_e, FtrFolder, ___EndTag___, ___EndTag___0, end tag, sentinel, reopen, feature tree folder, group features, ungroup, DeleteSelection2, Select2, BODYFEATURE, loose curves]
answers: "How do I put features into a folder in the feature tree from code?"
---

# Put features in a feature-tree folder

## What this is for

A macro that inserts a dozen curves leaves a dozen rows in the tree, in no
particular order, saying nothing about which belong together. Folders fix that,
and a folder is a row in the tree rather than geometry, so nothing that
references a feature is disturbed when one is made around it.

The API for this is smaller than it looks. There is exactly one call that puts
features in a folder, it works on the current selection, and the call that
looks like it adds a feature to an existing folder does nothing at all.

## The call

`InsertFeatureTreeFolder2` is on **IFeatureManager**, reached from
`IModelDoc2.FeatureManager`. It takes one `swFeatureTreeFolderType_e` value and
acts on whatever is selected. It returns the new folder as an `IFeature`, which
is what you rename it through.

```python
def insert_folder(self, names: Sequence[str], folder: str) -> str:
    """Wrap features in a new folder, and report the name it kept."""
    if not names:
        raise SolidWorksError("A folder has to be made around something.")
    doc = self._active()
    call(doc, "ClearSelection2", True)
    for position, name in enumerate(names):
        if not call(self._curve_feature(name), "Select2", position > 0, 0):
            raise SolidWorksError(f"{name} could not be selected.")
    made = call(call(doc, "FeatureManager"), "InsertFeatureTreeFolder2",
                FOLDER_CONTAINING)
    call(doc, "ClearSelection2", True)
    if made is None or made is False:
        raise SolidWorksError(f"SolidWorks would not make a folder for {folder}.")
    made.Name = folder
    return str(call(made, "Name"))
```

The three constants, and the two helpers, so the code above stands on its own:

```python
FOLDER_TYPE_NAME = "FtrFolder"     # GetTypeName2 of a folder and of its end tag
FOLDER_END_TAG = "___EndTag___"    # usually in the closing sentinel's name; not always
FOLDER_CONTAINING = 2              # swFeatureTreeFolderType_e; see the table below
```

`call(obj, "Member", *args)` is the late-bound helper from
[connect/02](../connect/02-attach-from-python.md), which exists because a
zero-argument COM method is a property get ([GOTCHAS §5](../../GOTCHAS.md)).
`self._curve_feature(name)` walks `FirstFeature`/`GetNextFeature` for a feature
of that name, as in [connect/08](../connect/08-find-a-feature-by-name.md);
despite its name it finds any feature, folders included.

`Select2(append, mark)` is on **IFeature**: the first selection passes `False`,
every one after it `True`, and mark `0`. The folder's name is read back after
setting it for the usual reason — [curves/07](07-rename-a-feature.md).

Select an existing folder along with loose features and the new folder contains
both, nested. That is the only way to nest, and it is how a second group gets
gathered under a parent.

## The enum value is not the one the name suggests

Four values were tried on SolidWorks 2026, each in a fresh part holding three
`CurveInFile` features, all three selected through `Select2`:

| Value | Returned | What appeared in the tree |
|---|---|---|
| 0 | `None` | nothing |
| 1 | the new folder | a folder, with the selection still outside it |
| 2 | the new folder | a folder **containing** the selection |
| 3 | the existing `Surface Bodies` folder | nothing new |

So `2` is the one to use. With nothing selected, `2` returns `None` and makes
nothing: the call needs a selection to wrap.

These numbers were read off a live session. This entry does not claim which
`swFeatureTreeFolderType_e` member name each number belongs to, because the
header was never seen; only the behaviour above was observed.

## `MoveToFolder` does not work

`IFeatureManager.MoveToFolder(folderName, featureName, moveAfter)` is the call
you will reach for to add a feature to a folder that already exists. On
SolidWorks 2026 it returned `False` and changed nothing, every way it was
offered:

- a `CurveInFile` into a folder created with value 2, `moveAfter` both `False` and `True`
- the same, with the feature selected first
- a `RefPlane` (`Front Plane`) into that folder
- a feature into an empty folder made with value 1
- a folder into another folder
- all of the above again after `IModelDoc2.EditRebuild3`

`IFeatureManager.EnableFeatureTree` read `True` throughout, so the tree was not
suppressed. No error was raised and no exception thrown; the return value is
the only signal.

If you find a version or a case where it works, downgrade this section and say
which.

## Deleting a folder keeps everything it held

This is what makes the whole thing workable. Select the folder and delete it,
and only the folder goes:

```python
def delete_folder(self, folder: str) -> None:
    """Remove the folder, leaving everything it held where it was."""
    doc = self._active()
    call(doc, "ClearSelection2", True)
    if not call(self._curve_feature(folder), "Select2", False, 0):
        raise SolidWorksError(f"{folder} could not be selected.")
    deleted = call(call(doc, "Extension"), "DeleteSelection2", 0)
    call(doc, "ClearSelection2", True)
    if not deleted:
        raise SolidWorksError(f"SolidWorks would not remove the folder {folder}.")
```

`DeleteSelection2` is on **IModelDocExtension**. Options `0`, `1`, `2` and `3`
were each tried on a fresh part, on a folder holding two curves: every one
returned `True`, removed the folder, and left both curves in the tree.

## So rebuild the arrangement, do not patch it

Given a create-around-a-selection call and a delete-the-folder-only call, and
no way to add to an existing folder, the workable pattern is:

1. Read the folders and what each holds.
2. A folder whose contents are already exactly the group you want: leave it
   alone, including its name. A user rename then survives.
3. Otherwise delete any folder holding part of the group, then select the whole
   group and wrap it.
4. Do the same one level up, treating the group folders as the members.

Step 2 is what keeps this cheap: a tree that is already right costs a read and
no writes. Step 3 is safe precisely because deleting a folder moves nothing.

When a folder has to be rebuilt, take its name from the folder being replaced
rather than regenerating it, or every rebuild quietly undoes a rename somebody
made in SolidWorks.

## Reading the tree back

A folder is **two** features in the walk: the folder itself, and a closing
sentinel after everything it holds. Both report `GetTypeName2` as `FtrFolder`.
Nesting is the depth between them.

**Do not tell the sentinel by its name.** Until 2026-09-24 this entry said the
sentinel is named `<folder>___EndTag___` and shipped a walk that matched that
suffix. Both halves were wrong, and the walk scrambled a real part:

- The sentinel is named after the name SolidWorks **first** gave the folder,
  `FolderN___EndTag___`, not after what the folder is called now. A folder
  renamed `rib` closes with `Folder1___EndTag___`.
- Once a folder has been renamed, a part closed and opened again hands the
  name `FolderN` out afresh. The new folder's sentinel cannot be
  `FolderN___EndTag___`, which the old folder's sentinel still has, so it is
  `FolderN___EndTag___0`. That no longer *ends* in `___EndTag___`.

A walk matching the suffix reads that sentinel as a folder **opening**, and
everything after it in the tree — lofts included — as inside it. Code that
then rebuilds folders from that reading deletes and renames the wrong ones;
see the evidence below.

What does tell them apart: `IFeature.GetSpecificFeature2` on either feature
returns an `IFeatureFolder`, and **both answer `GetFeatures` with the same
contents**, in tree order. The folder comes before its first item and the
sentinel after it, so a folder feature whose first item the walk has already
passed is the sentinel:

```python
def _closes_folder(feature: Any, name: str, seen: Set[str]) -> bool:
    """Is this folder feature the tag that closes a folder, not one opening?

    Not by its name alone. A tag is named after the folder as SolidWorks first
    made it, ``Folder3___EndTag___``, and once that folder has been renamed a
    part opened again hands ``Folder3`` out afresh; the new folder's tag
    cannot have the name, so it gets ``Folder3___EndTag___0``. Read as a
    folder opening, that tag swallowed everything after it, and arranging the
    tree deleted and renamed the wrong folders (the user's wing part,
    2026-09-24). Both features answer with the same contents, so a folder
    whose first item has already gone past is the closing one.
    """
    held = _try(_try(feature, "GetSpecificFeature2"), "GetFeatures")
    if held:
        first = _try(held[0], "Name")
        if first is not None:
            return str(first) in seen
    # An empty folder has only its name to go by.
    return FOLDER_END_TAG in name
```

```python
def folders(self) -> Dict[str, List[str]]:
    """Every tree folder, and the names it holds, one level deep.

    The tree is read along the top-level chain rather than through
    :meth:`_walk`, which also descends into subfeatures: a sketch absorbed
    into a feature would land in the middle of a folder's contents and the
    nesting would stop adding up.
    """
    out: Dict[str, List[str]] = {}
    stack: List[str] = []
    seen: Set[str] = set()
    feature = call(self._active(), "FirstFeature")
    guard = 0
    while feature is not None and guard < 5000:
        guard += 1
        name = str(call(feature, "Name"))
        try:
            type_name = str(call(feature, "GetTypeName2"))
        except Exception:  # noqa: BLE001 - a feature that will not describe itself
            type_name = "?"
        here, feature = feature, call(feature, "GetNextFeature")

        if type_name != FOLDER_TYPE_NAME:
            seen.add(name)
            if stack:
                out[stack[-1]].append(name)
            continue
        if _closes_folder(here, name, seen):
            if stack:
                stack.pop()
            continue
        seen.add(name)
        if stack:
            out[stack[-1]].append(name)
        out.setdefault(name, [])
        stack.append(name)
    return out
```

`_try(obj, name)` is `call` returning `None` where the member is missing or
raises. An empty folder has no first item, so only its name is left to go by;
this code never makes one.

Walk the top-level `GetNextFeature` chain only. A walk that also descends into
`GetFirstSubFeature` puts absorbed features in the middle of a folder's
contents and the sentinels stop lining up.

`IFeatureFolder.GetFeatures` and `GetFeatureCount` list a nested folder's
sentinel as one of the parent's members: `Airfoil Curves` answered
`['sd7037_275mm', 'Folder3___EndTag___', 'sd7037_136.5mm', 'Folder4___EndTag___']`.
That is why the contents are still read from the walk, and `GetFeatures` is
asked only for its first item.

### A folder that has been given a sentinel's name

The scrambled arrangement renamed new folders to sentinel names:
`IFeature.Name = "Folder4___EndTag___"` on a folder, with that name taken by a
sentinel, left it called `Folder4___EndTag___0`, and the next one
`Folder4___EndTag___1` (inferred from the part it left; the name asked for was
not logged — see [curves/07](07-rename-a-feature.md) for renames that collide).
Nobody chooses such a name, so unlike a rename made in SolidWorks it is not
worth keeping: the arrangement code now renames a kept folder whose name
contains `___EndTag___` back to its group's name, if that name is free.

## What it does not do

- **No adding to an existing folder.** See above. Rebuild instead.
- **No empty folder to fill later.** Value 1 made an empty folder only when
  something was selected, and nothing can be moved into it afterwards, so it is
  of no use.
- **Ordering is only partly addressed.** Folders around features already
  next to each other do not reorder them, and a composite curve stayed after
  the curves it is built from. Features that are *not* next to each other are
  gathered by moving them: three curves inserted at the end of a part, after a
  composite built on an earlier group, and then wrapped in a parent folder with
  that group's folder, came out above the composite, in the parent (E90 below).
  The composite did not depend on them. No case was constructed to test a
  folder that would force a feature before its parent. If you need that
  guarantee, test it.
- **`SelectByID2` was not usable for these features.** With the type string
  `"BODYFEATURE"`, `IModelDocExtension.SelectByID2` returned `False` for a
  `CurveInFile` feature, selecting nothing. `IFeature.Select2` on the feature
  object worked every time and needs no type string, so this entry uses it
  throughout. Passing a bare `None` for the callout argument of `SelectByID2`
  raises `Type mismatch` before any of that — see
  [GOTCHAS §4](../../GOTCHAS.md).

## Evidence

SolidWorks 2026 (revision 34.0.0), driven from Python over pywin32, each
experiment in a scratch part created with `ISldWorks.NewPart` and closed with
`CloseDoc` without saving.

The enum table, the `MoveToFolder` failures and the `DeleteSelection2` options
were each observed as described above, by walking the feature tree before and
after every call and printing it.

The end-to-end pattern was then run against a real part: three curves plus a
composite were gathered into a folder, the folder went into a parent, a second
group of three curves was added on a later pass, and the result was

```
Airfoil Curves
    rib
        rib_airfoil
        rib_airfoil_te
        rib_camber
        rib_airfoil_joined
    rib_2
        rib_airfoil_2
        rib_airfoil_te_2
        rib_camber_2
```

Running the same arrangement a third time with nothing changed made no API
calls at all and reported all three folders kept.

**The sentinel names (2026-09-24, SolidWorks 2026 SP0.0).** A user's wing part
arranged by the suffix-matching walk had curves of two exports outside any
folder, folders named `Folder4___EndTag___0` and `Folder4___EndTag___1`, and
no parent folder. `GetSpecificFeature2` → `GetFeatures` on its 14 `FtrFolder`
features gave identical contents for each folder and its sentinel, which is how
the sentinels were paired: `Folder4___EndTag___1` (ID 615) opened a folder of 109
curves whose sentinel was `Folder6___EndTag___0` (ID 616), and the two features
after that sentinel, a loft and a combine, were not in any folder.

Reproduced in a scratch part (E89–E91): five groups of three `CurveInFile`
features, each arranged into its own folder under a parent, a composite of one
curve between them standing in for a loft — every folder right. Saved, closed
with `CloseDoc`, opened with `OpenDoc6`, and three more groups arranged:

```
[Wing Curves] [Wing] Wing_0 Wing_1 Wing_2 [Folder1___EndTag___] ... [Wing_ci] Wing_ci_0 Wing_ci_1 Wing_ci_2 [Folder9___EndTag___] [Wing_x1] Wing_x1_0 Wing_x1_1 Wing_x1_2 [Folder1___EndTag___0] [Folder2___EndTag___] Wing_loft Wing_ao_loft
```

The first new folder after the reopen was handed `Folder1` again, and closed
with `Folder1___EndTag___0`. Two more groups arranged by the old walk then left
`Wing_x1` holding the next group's curves, `Wing_x1`'s own curves in a folder
named `Folder5___EndTag___0`, and a phantom folder `Folder7___EndTag___0`
"holding" both lofts. The fixed walk read that same part correctly, left every
folder in place, and two more groups added after another close-and-reopen went
into folders under the parent (their sentinels `Folder1___EndTag___0` and
`Folder3___EndTag___0`). A kept folder named `Folder5___EndTag___0` was renamed
to its group's free name through `IFeature.Name` and read back as asked.

## See also

- [curves/02 — Insert a curve from a file](02-insert-curve-from-file.md) — what
  usually creates the features you are about to gather
- [curves/06 — Composite curves](06-composite-curve.md) — the derived curve
  that belongs in the folder with the two it is built from
- [curves/07 — Rename a feature](07-rename-a-feature.md) — read the folder's
  name back after setting it, for the same reason
- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md)
  — how the walk above finds the feature to select
- [reading/04 — Read the selection](../reading/04-read-the-selection.md) — the
  selection this call consumes
- [`code/python/swcom.py`](../../code/python/swcom.py) — the three calls in
  place, and [`code/python/swlink.py`](../../code/python/swlink.py) for the
  rebuild-not-patch arrangement as `arrange`
- [GOTCHAS §18, §19](../../GOTCHAS.md)
- [reading/14 — The journal records API calls](../reading/14-the-journal-records-api-calls.md) — how the folder bug below was narrowed down, and why the journal cannot show what a folder was made around
- [features/01 — Boss extrude](../features/01-boss-extrude.md) — `Select2` judged by the selection count, with a `SelectByID2` fallback
