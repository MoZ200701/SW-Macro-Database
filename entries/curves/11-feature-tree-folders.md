---
id: curves-11-feature-tree-folders
title: Put features in a feature-tree folder
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [IFeatureManager.InsertFeatureTreeFolder2, IFeatureManager.MoveToFolder, IModelDocExtension.DeleteSelection2, IFeature.Select2, IFeature.GetSpecificFeature2, IFeatureFolder.GetFeatureCount, IFeatureFolder.GetFeatures]
keywords: [InsertFeatureTreeFolder2, MoveToFolder, swFeatureTreeFolderType_e, FtrFolder, ___EndTag___, feature tree folder, group features, ungroup, DeleteSelection2, Select2, BODYFEATURE]
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
FOLDER_END_TAG = "___EndTag___"    # the suffix on the closing sentinel
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
sentinel named `<folder>___EndTag___`, after everything it holds. Both report
`GetTypeName2` as `FtrFolder`. Nesting is the depth between them:

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
    feature = call(self._active(), "FirstFeature")
    guard = 0
    while feature is not None and guard < 5000:
        guard += 1
        name = str(call(feature, "Name"))
        try:
            type_name = str(call(feature, "GetTypeName2"))
        except Exception:  # noqa: BLE001 - a feature that will not describe itself
            type_name = "?"
        feature = call(feature, "GetNextFeature")

        if type_name != FOLDER_TYPE_NAME:
            if stack:
                out[stack[-1]].append(name)
            continue
        if name.endswith(FOLDER_END_TAG):
            if stack:
                stack.pop()
            continue
        if stack:
            out[stack[-1]].append(name)
        out.setdefault(name, [])
        stack.append(name)
    return out
```

Walk the top-level `GetNextFeature` chain only. A walk that also descends into
`GetFirstSubFeature` puts absorbed features in the middle of a folder's
contents and the end tags stop lining up.

`IFeature.GetSpecificFeature2` on a folder returns an `IFeatureFolder`, whose
`GetFeatureCount` and `GetFeatures` also list the contents. It is usable, with
one wrinkle: for a folder containing another folder it counted the nested
folder's `___EndTag___` as a member, reporting 2 for
`['n0012_150mm', 'Folder1___EndTag___']`. The walk above was used instead for
that reason.

## What it does not do

- **No adding to an existing folder.** See above. Rebuild instead.
- **No empty folder to fill later.** Value 1 made an empty folder only when
  something was selected, and nothing can be moved into it afterwards, so it is
  of no use.
- **Ordering is not addressed here.** Folders were observed not to reorder the
  features they gather, and a composite curve stayed after the curves it is
  built from, but no case was constructed to test a folder that would force a
  feature before its parent. If you need that guarantee, test it.
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
- [features/01 — Boss extrude](../features/01-boss-extrude.md) — `Select2` judged by the selection count, with a `SelectByID2` fallback
