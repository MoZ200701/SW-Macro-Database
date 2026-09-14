---
id: documents-01-new-part-from-template
title: Make a new part or assembly from a template, in millimetres
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [ISldWorks.GetDocumentTemplate, ISldWorks.GetUserPreferenceStringValue, ISldWorks.NewDocument, IModelDoc2.GetType, IModelDocExtension.GetUserPreferenceInteger, IModelDocExtension.SetUserPreferenceInteger]
keywords: [NewDocument, GetDocumentTemplate, NewPart, new part, new assembly, prtdot, asmdot, template, inch template, wrong year template, MMGS, swUnitSystem, swUnitsLinear, 263, 47, document units, millimetres]
answers: "How do I create a new part or assembly from code, and make sure it is in millimetres?"
---

# Make a new part or assembly from a template, in millimetres

## What this is for

Anything that builds geometry from nothing starts with a fresh document. Two
things decide whether what follows is right: which template it came from, and
what length unit that template left the document in. The API itself is always
metres, but **equations are in document units**
([equations/02](../equations/02-link-a-dimension-to-a-global.md)), so a global
of `30` is 30 mm only in a millimetre document. A part from the wrong template
builds without error and is 25.4 times the size.

## The call

Three members, all real code from a tool whose build ran end to end on
SolidWorks 2026. `call(obj, "Member", *args)` is the late-bound helper from
[connect/02](../connect/02-attach-from-python.md).

```python
# swDocumentTypes_e, as GetType reports it.
DOC_PART = 1
DOC_ASSEMBLY = 2

UNIT_SYSTEM = 263            # swUserPreferenceIntegerValue_e.swUnitSystem
UNIT_SYSTEM_MMGS = 5         # swUnitSystem_e.swUnitSystem_MMGS
UNITS_LINEAR = 47            # swUserPreferenceIntegerValue_e.swUnitsLinear
LENGTH_MM = 0                # swLengthUnit_e.swMM


def default_template(self, kind: str = "part") -> str:
    """``GetDocumentTemplate`` for a part or an assembly (probe: templates)."""
    doc_type = DOC_PART if kind == "part" else DOC_ASSEMBLY
    return str(call(self._app, "GetDocumentTemplate", doc_type, "", 0, 0.0, 0.0) or "")

def _new_document(self, template: str, doc_type: int, noun: str) -> DocInfo:
    template = template or self.default_template("part" if doc_type == DOC_PART else "assembly")
    if not template or not os.path.isfile(template):
        raise SolidWorksError(f"There is no {noun} template at {template!r}.")
    doc = call(self._app, "NewDocument", template, 0, 0.0, 0.0)
    if doc is None:
        raise SolidWorksError(f"SolidWorks would not make a new {noun} from {template}.")
    info = self._describe(doc)
    if info.doc_type != doc_type:
        raise SolidWorksError(f"{template} made a document of type {info.doc_type}, not a {noun}.")
    self._build_doc = doc
    self._set_millimetres(doc, info.title)
    return info

@staticmethod
def _set_millimetres(doc: Any, title: str) -> None:
    """Equations are in document units, and the default templates here are inches.

    Probe document_units: a part from the default template read
    swUnitsLinear 3; setting swUnitSystem to MMGS made it 0, and a global of
    30 then drove a dimension to 0.030 m.
    """
    extension = call(doc, "Extension")
    if int(call(extension, "GetUserPreferenceInteger", findings.UNITS_LINEAR, 0)) == findings.LENGTH_MM:
        return
    call(extension, "SetUserPreferenceInteger", findings.UNIT_SYSTEM, 0, findings.UNIT_SYSTEM_MMGS)
    if int(call(extension, "GetUserPreferenceInteger", findings.UNITS_LINEAR, 0)) != findings.LENGTH_MM:
        raise SolidWorksError(f"{title} could not be set to millimetres, so its equations would be wrong.")
```

The constants block is from the same tool's `findings.py`; the enum values were
read from `swconst.tlb` on SolidWorks 2026 with makepy. `self._describe(doc)`
reads `IModelDoc2.GetTitle`, `GetPathName` and `GetType`.

Interfaces and units:

- `GetDocumentTemplate(docType, "", 0, 0.0, 0.0)` and `NewDocument(template, 0, 0.0, 0.0)`
  are on **ISldWorks**. `NewDocument` returns the new document, which is also
  the active one.
- `GetUserPreferenceInteger(preference, 0)` and
  `SetUserPreferenceInteger(preference, 0, value)` are on
  **IModelDocExtension**, reached from `IModelDoc2.Extension`. They act on
  that one document, not on the application's defaults. The second argument is
  `0` throughout (`swDetailingNoOptionSpecified` in the probe's constants).
- `GetType` on **IModelDoc2**: 1 part, 2 assembly.

## Why it is not obvious

**The default template was a different year's, and it was in inches.** On a
SolidWorks 2026 session, `GetDocumentTemplate(1, "", 0, 0, 0)` returned

```
C:\ProgramData\SolidWorks\SOLIDWORKS 2025\templates\Part.prtdot
```

and `GetUserPreferenceStringValue(8)` returned the same path. A part made from
it read `swUnitsLinear` **3** (inches) and `swUnitSystem` 3. The 2026 folder's
own template, `C:\ProgramData\SOLIDWORKS\SOLIDWORKS 2026\templates\Part.PRTDOT`,
read `swUnitsLinear` 0 and `swUnitSystem` 5 (MMGS). The assembly template had
the same story: `GetDocumentTemplate(2, ...)` gave the 2025 folder's
`Assembly.asmdot`, and assemblies made from it read `swUnitsLinear` 3.

The default template is whatever the user's options point at, which on a
machine that has carried several versions can be an older install's. Nothing
errors. So **do not trust the template's units: read them and set them** on
the document you just made, as above.

**Setting the unit system is one call and it took.**
`SetUserPreferenceInteger(263, 0, 5)` on the new part's extension returned
`True`, and `swUnitsLinear` then read 0.

**Find the reference planes by tree order, not by name.** A new part's
top-level features came back as, in order:

```
Comments, Favorites, History, Selection Sets, Sensors, Design Binder,
Annotations, Surface Bodies, Solid Bodies, Lights and Cameras, Markups,
Equations, Material <not specified>, Front Plane, Top Plane, Right Plane, Origin
```

with the three planes typed `RefPlane` and the origin `OriginProfileFeature`.
Taking the Nth `RefPlane` by `GetTypeName2` works in a template of any
language; `"Front Plane"` does not.

## What it does not do

- **Angular units are not set.** The Equation Manager's trig was degrees in
  these documents; see [equations/01](../equations/01-global-variables-from-code.md).
- **No material.** A part from these templates reads `Material <not specified>`.
- Only the argument values shown were tried. What the last three arguments of
  `GetDocumentTemplate` and `NewDocument` do was not explored.
- The meaning of `swUnitSystem` 3 is not claimed here; only that such a part
  was in inches by `swUnitsLinear`.
- The template path found on another machine will differ. The rule (read the
  units back, set MMGS when they are not millimetres) does not depend on it.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, probe run
20260914-180426 (33 of 33 probes passed). Probes `templates`, `new_document`
and `document_units`; the records are in
[`code/python/gear_generator/probe/results/probe-results-20260914-180426.json`](../../code/python/gear_generator/probe/results/probe-results-20260914-180426.json).

- `GetDocumentTemplate(1, '', 0, 0, 0)` and `GetUserPreferenceStringValue(8)`
  both returned the 2025 folder's `Part.prtdot`; `(2, ...)` and `(9)` both the
  2025 folder's `Assembly.asmdot`.
- `NewDocument` from it made `Part210`, `GetType` 1, the active document,
  `GetPathName` empty, three `RefPlane`s in the order Front, Top, Right.
- Default template: `{'linear': 3, 'system': 3}`. After
  `SetUserPreferenceInteger(swUnitSystem, 0, swUnitSystem_MMGS)`: returned
  `True`, `linear_after` 0. The 2026 folder's template: `{'linear': 0, 'system': 5}`.
- In a millimetre part a global of 30 linked to a dimension made it 0.030 m
  (probe `dimension_link`).
- The tool's own build (probe `end_to_end`) made its part through
  `_new_document` above from the default template, set it to millimetres, and
  every one of its 24 globals then matched the gear maths.

## See also

- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md) — why the units matter
- [documents/02 — Save as, and close](02-save-as-and-close.md) — the other end of a document's life
- [assemblies/01 — New assembly and insert components](../assemblies/01-new-assembly-and-insert-components.md)
- [sketches/05 — Units and number format](../sketches/05-units-and-number-format.md) — the API side of the unit boundary
- [connect/11 — Probe an API member on a live session](../connect/11-probe-an-api-member-on-a-live-session.md) — how these facts were established
- [GOTCHAS §25](../../GOTCHAS.md)
