---
id: documents-02-save-as-and-close
title: Save a document to a new path, and close it
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IModelDocExtension.SaveAs3, IModelDoc2.SaveAs3, IModelDoc2.GetPathName, IModelDoc2.GetTitle, IModelDoc2.GetSaveFlag, ISldWorks.CloseDoc]
keywords: [SaveAs3, save as, overwrite, silent overwrite, swSaveAsOptions_Silent, swSaveAsCurrentVersion, out parameter, tuple, CloseDoc, close without saving, GetSaveFlag, GetPathName, unsaved changes]
answers: "How do I save a new document to a path from code, and close a document without a prompt?"
---

# Save a document to a new path, and close it

## What this is for

A tool that builds a part has to put it somewhere, and a probe or a batch job
has to close what it made without a dialog waiting for someone. Both calls are
short. Both do something you might not want without saying so: `SaveAs3`
replaces an existing file, and `CloseDoc` throws away unsaved changes.

## The call

```python
SAVE_AS_SILENT = 1           # swSaveAsOptions_e.swSaveAsOptions_Silent


def save_as(self, path: str) -> str:
    """Save the build's document to a new path, and report where it went.

    Probe save_as: ``IModelDocExtension.SaveAs3`` returned a plain True and
    silently overwrote an existing file, so an existing path is refused here
    before SolidWorks is asked.
    """
    if os.path.exists(path):
        raise SolidWorksError(f"{path} already exists, and a build never overwrites a file.")
    doc = self._doc()
    saved = call(call(doc, "Extension"), "SaveAs3", path, 0, findings.SAVE_AS_SILENT, _null_dispatch(),
                 _null_dispatch(), _out_long(), _out_long())
    if not saved:
        raise SolidWorksError(f"SolidWorks would not save {call(doc, 'GetTitle')} as {path}.")
    kept = str(call(doc, "GetPathName"))
    if kept.lower() != path.lower():
        raise SolidWorksError(f"SolidWorks saved to {kept!r}, not {path!r}.")
    return kept

def close_document(self, title: str) -> None:
    """``CloseDoc`` closes without asking, modified or not (probe close_document)."""
    call(self._app, "CloseDoc", title)
```

`call`, `_null_dispatch()` and `_out_long()` are from
[connect/02](../connect/02-attach-from-python.md): the late-bound helper, an
absent COM object typed as `VARIANT(VT_DISPATCH, None)`, and a by-reference
integer `VARIANT(VT_BYREF | VT_I4, 0)`.

- `SaveAs3(path, version, options, exportData, advancedSaveAsOptions, out errors, out warnings)`
  on **IModelDocExtension**. `0` is the current version and `1` silent, from
  the probe's constants read out of `swconst.tlb`. The two nulls and two outs
  are the argument types that ran; what the out values held was not read.
- `GetPathName` and `GetTitle` on **IModelDoc2**. After the save the document
  *is* the new file: its title and path both change.
- `CloseDoc(title)` on **ISldWorks**, by the document's title.

The path was always a local drive path (`C:\...`). The tool refuses anything
else before it asks; see "What it does not do".

## Why it is not obvious

**It overwrites without a word.** Saving a second time onto the same path with
the same silent option returned `True`, and the file on disk changed size
(36249 to 35405 bytes). There is no prompt with the silent flag and no
distinct return. If overwriting would be wrong, check the path yourself first,
as `save_as` does.

**The flag came back plain, not as a tuple.** In pywin32, passing
by-reference arguments often makes a call return a tuple of the result and the
outs. `IModelDocExtension.SaveAs3` with two `_out_long()` outs returned a bare
`True` both times. Code that does `result[0]` would raise; code that tests
`bool(result)` is right either way. The probe hedged:

```python
def _flag(result: Any) -> Any:
    """SaveAs3 with by-reference outs comes back as a tuple whose first item is the flag."""
    return result[0] if isinstance(result, tuple) and result else result
```

and recorded `save_as3_result_is_tuple = False`.

**`IModelDoc2.SaveAs3` is a different call with a different answer.** On a
second new part, `call(doc, "SaveAs3", path, 0, 1)` returned `0`, and the file
was on disk. Do not read that `0` as `False`. This entry uses the extension's
form, whose `True` did mean saved.

**`CloseDoc` does not ask.** A part was made, a sketch with one point was
added (`GetSaveFlag` read `True`: modified), and it had never been saved.
`CloseDoc(title)` returned `None`, and the part was gone from the open
documents with no prompt. That is what a probe or a batch wants, and exactly
what an interactive tool must not do to a document the user was editing: close
only what your code made, by the title it had when you made it.

## What it does not do

- **Network and WSL paths are untested.** Every save here went to a local drive
  path under `C:\Users\...\Documents`. Whether `SaveAs3` accepts a
  `\\wsl.localhost\...` UNC path is an open question; settling it takes one
  save to such a path and a check that the file arrived.
- The error and warning out values were never read, so no failure codes are
  recorded here.
- Saving in an older version, or exporting another format, was not tried.
- Only a part was saved through `save_as` itself; the assembly probe saved its
  two parts with the same `IModelDocExtension.SaveAs3` call before inserting
  them.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426. Probes `save_as` and `close_document`, in
[`code/python/gear_generator/probe/`](../../code/python/gear_generator/probe/p3_files.py).

- `SaveAs3(path, current version, silent, null, null, out long, out long)`:
  returned `True`; `save_as3_result_is_tuple = False`; the file was on disk;
  `GetTitle` then read `Probe Save.SLDPRT` and `GetPathName` the new path.
- Onto the same path again: `{'flag': True, 'size_before': 36249, 'size_after': 35405}`.
- `IModelDoc2.SaveAs3` on another new part: `{'returned': 0, 'on_disk': True}`.
- `close_document`: `GetSaveFlag` `True`, `CloseDoc` returned `None`, and the
  title was no longer among the open documents.
- The probe run as a whole compared the open documents before and after all 33
  probes, and every scratch document had been closed this way without leaving
  one behind or closing one that was open before.
- The tool's own build saved its finished gear through `save_as` above.

## See also

- [documents/01 — New part from a template](01-new-part-from-template.md)
- [connect/07 — Find the open document](../connect/07-find-the-open-document.md) — the title `CloseDoc` wants
- [connect/10 — Driving from outside Windows](../connect/10-driving-from-outside-windows.md) — where files should live when the code is in WSL
- [assemblies/01 — New assembly and insert components](../assemblies/01-new-assembly-and-insert-components.md) — parts must be saved before they can be inserted
- [GOTCHAS §34](../../GOTCHAS.md)
- [files/01 — Batch-convert STEP](../files/01-batch-convert-step.md) — `CloseDoc` in a batch, and `IModelDocExtension.SaveAs` with error codes
