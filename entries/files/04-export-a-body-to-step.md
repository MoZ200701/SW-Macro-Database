---
id: files-04-export-a-body-to-step
title: Write one body of a part to its own STEP file
status: partly-verified
verified_on: SolidWorks 2026 for the STEP export; the suppression that isolates one body was run but its effect on the file was not recorded
language: [python]
api: [IModelDocExtension.SaveAs, IFeature.SetSuppression2, IModelDoc2.ForceRebuild3]
keywords: [STEP, export STEP, SaveAs, swSaveAsOptions_Copy, save as copy, neutral format export, one body, suppress, SetSuppression2, swFeatureSuppressionAction_e, swInConfigurationOpts_e, measure loft, out parameter]
answers: "How do I write a part, or just one of its lofts, to a STEP file from code without the open document changing name?"
---

# Write one body of a part to its own STEP file

## What this is for

Getting SolidWorks' geometry out to be measured or handed on. A STEP file of
the part is one call. A STEP file of *one* feature's body, when the part holds
several, needs the others suppressed while it is written and put back
afterwards, whatever goes wrong in between. The Airfoil Converter did this to
measure each loft it built against the shape it meant
([surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md)).

Importing STEP is a different problem with a different trap:
[files/01](01-batch-convert-step.md).

## The call

From [`code/python/swcom.py`](../../code/python/swcom.py), as it ran:

```python
# Values read out of the SolidWorks 2026 constant library (swconst.tlb) rather
# than remembered: swGuideCurveInfluence_e, swFeatureSuppressionAction_e,
# swInConfigurationOpts_e, swOpenDocOptions_e, swSaveAsOptions_e,
# swDocumentTypes_e.
SUPPRESS = 0
UNSUPPRESS = 1
THIS_CONFIGURATION = 1
SAVE_SILENT = 1
SAVE_AS_COPY = 2


def export_step(self, path: str) -> None:
    """Write every visible body of the active part to a STEP file, as a copy.

    The part stays the open document under its own name.
    """
    doc = self._active()
    path = os.path.abspath(path)
    errors, warnings = _out_long(), _out_long()
    saved = call(
        call(doc, "Extension"), "SaveAs", path, 0, SAVE_SILENT | SAVE_AS_COPY,
        _null_dispatch(), errors, warnings,
    )
    if not saved or not os.path.exists(path):
        raise SolidWorksError(f"SolidWorks would not write {path} (error {errors.value}).")


def set_suppressed(self, name: str, suppressed: bool) -> None:
    action = SUPPRESS if suppressed else UNSUPPRESS
    feature = self._curve_feature(name)
    if not call(feature, "SetSuppression2", action, THIS_CONFIGURATION, None):
        raise SolidWorksError(
            f"SolidWorks would not {'suppress' if suppressed else 'unsuppress'} {name}."
        )
```

- `SaveAs(path, version, options, exportData, ByRef errors, ByRef warnings)`
  on **IModelDocExtension**. The format comes from the extension, `.STEP` here.
  `0` is the current version; options `1 | 2` are silent and **save as copy**.
  `_out_long()` is `VARIANT(VT_BYREF | VT_I4, 0)` and `_null_dispatch()` a
  typed null ([connect/02](../connect/02-attach-from-python.md)).
- **Copy is what keeps the document itself.** Without it, a save-as makes the
  open document *become* the new file ([documents/02](../documents/02-save-as-and-close.md)),
  and the next save of "the part" would go to the STEP path.
- The tool checks the file is on disk as well as the return value.
- `SetSuppression2(action, configOption, configNames)` on **IFeature**. `0`
  suppress, `1` unsuppress (`swFeatureSuppressionAction_e`); `1` this
  configuration (`swInConfigurationOpts_e`). The third argument is passed as a
  bare `None`; compare [GOTCHAS §4](../../GOTCHAS.md), where a bare `None` was a
  type mismatch for a different call. Whether it was accepted here is covered
  under "What it does not do".
- `_curve_feature(name)` is the name walk from
  [connect/08](../connect/08-find-a-feature-by-name.md).

## One body at a time

From [`code/python/swloft.py`](../../code/python/swloft.py):

```python
bodies = sw.features_of_type(*BODY_FEATURE_TYPES)
for result in results:
    if not result.ok:
        continue
    others = [name for name in bodies if name != result.feature]
    hidden: List[str] = []
    try:
        for name in others:
            sw.set_suppressed(name, True)
            hidden.append(name)
        sw.rebuild()
        path = os.path.join(step_folder, result.plan.name + ".STEP")
        sw.export_step(path)
        result.step = path
    except SolidWorksError as exc:
        result.error = str(exc)
    finally:
        errors = []
        for name in hidden:
            try:
                sw.set_suppressed(name, False)
            except SolidWorksError as exc:
                errors.append(str(exc))
        sw.rebuild()
        if errors and result.ok:
            result.error = "; ".join(errors)
```

`BODY_FEATURE_TYPES` is `("Blend", "BlendRefSurface")`, the solid and surface
loft type names ([features/12](../features/12-guided-loft.md)). Only what was
actually suppressed is put back, and it is put back in a `finally`, for the same
reason as the rebuild flag in [curves/08](../curves/08-rebuild-once-at-the-end.md):
a part left with features suppressed looks fine and is wrong.

The tool does this on a **copy** of the part (`--copy-to`), saved as
`<name> lofted.SLDPRT` because SolidWorks opens one document per file name and
the original is probably open.

## What it does not do

- **Whether suppression isolated the body was not recorded.** The STEP files
  were written and measured; that each file held only its own loft, and that
  `SetSuppression2` accepted the bare `None`, is what the code expects and what
  nobody wrote down. Check the first file's body count before trusting a
  batch.
- Only the default STEP export options were used; the AP version and the
  export settings under *Tools > Options > Export* were not examined or set
  from code.
- The `errors` and `warnings` out values were never seen non-zero.
- Units in the STEP file: the measurements were made in millimetres and agreed
  with the curves to 0.0007 mm ([curves/01](../curves/01-sldcrv-file-format.md)),
  so the file's unit was read correctly, but which unit it declares was not
  recorded.
- Its tests run only against a fake SolidWorks.

## Evidence

SolidWorks 2026, Python over pywin32, the Airfoil Converter's
`python -m airfoil_converter.swloft <part> --copy-to <folder>` (commit "Loft a
wing in SolidWorks and write each loft to STEP", 2026-09-16). Its STEP files of
SolidWorks' lofts were read back and measured: the lofted surfaces against the
intended wing ([surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md)),
and the profile spline against the tool's model of it, to under 0.001 mm.

## See also

- [files/01 — Batch-convert STEP](01-batch-convert-step.md) — STEP in, and the same `SaveAs` with error codes
- [documents/02 — Save as, and close](../documents/02-save-as-and-close.md) — a save-as without copy renames the document
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — the lofts exported
- [surfacing/03 — How a loft fills between two profiles](../surfacing/03-how-a-loft-fills-between-profiles.md) — what the files were measured for
- [curves/08 — Rebuild once, at the end](../curves/08-rebuild-once-at-the-end.md) — restore in a `finally`
