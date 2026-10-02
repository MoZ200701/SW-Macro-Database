---
id: appearance-01-read-and-remove-a-part-appearance
title: Read a part's appearance, and remove one applied at the part level
status: verified
verified_on: SolidWorks 2024 SP5 (revision 32.5.0)
language: [python]
api: [IModelDoc2.MaterialPropertyValues, IModelDocExtension.GetRenderMaterials2, IModelDocExtension.GetRenderMaterialsCount2, IRenderMaterial.FileName, IRenderMaterial.Specular, IRenderMaterial.Reflectivity, IRenderMaterial.Transparency, IRenderMaterial.GetEntitiesCount, IRenderMaterial.GetMaterialIds, IRenderMaterial.MaterialID, IModelDocExtension.DeleteDisplayStateSpecificRenderMaterial, IBody2.MaterialPropertyValues2, IModelView.DisplayMode]
keywords: [appearance, transparent, transparency, see-through, glossy, shiny, reflective, reflectivity, specular, color.p2m, p2m, render material, RenderMaterial, MaterialPropertyValues, remove appearance, delete appearance, DisplayManager, display state, swDisplayStateOpts_e, swThisDisplayState, swViewDisplayMode_e, ShadedWithEdges, GetMaterialIds, DeleteDisplayStateSpecificRenderMaterial, two arrays, returns False, bool object is not callable]
answers: "Why does my whole part look transparent or glossy, and how do I find and remove the appearance doing it from code?"
---

# Read a part's appearance, and remove one applied at the part level

## What this is for

A part whose every body draws transparent, or glossier than the user's other
parts, while the display style is right and no body or feature has an
appearance of its own. The cause found here was an appearance applied to the
**part as a whole**, which every body inherits. This entry is how to see that
from code, compare it with a part that looks normal, and delete it.

## Reading it

Two different things answer "what does this part look like", and they are
worth reading together:

| Member | Interface | Gives |
|---|---|---|
| `MaterialPropertyValues` | `IModelDoc2` | Nine doubles, `[R, G, B, Ambient, Diffuse, Specular, Shininess, Transparency, Emission]`, each 0 to 1. The order is from the API help's Remarks. Read it whether or not an appearance is applied |
| `GetRenderMaterials2(1, None)` | `IModelDocExtension` | The appearances **applied** to the document, as `IRenderMaterial` objects. `1` is `swThisDisplayState` in `swDisplayStateOpts_e` (2 all display states, 3 the named ones) |
| `GetRenderMaterialsCount2(1, None)` | `IModelDocExtension` | How many there are |
| `MaterialPropertyValues2` | `IBody2` | The body's own nine values. **Came back `None`** for every body that had no appearance of its own |

On an `IRenderMaterial`: `FileName` (the `.p2m` it came from), `Specular`,
`Reflectivity`, `Transparency` and `GetEntitiesCount`, all read as plain
attributes. From
[`code/python/appearance/read_appearance.py`](../../code/python/appearance/read_appearance.py),
which walks every open part:

```python
    tryp("MaterialPropertyValues (RGB,amb,diff,spec,shin,transp,emis)", lambda: tuple(round(v,4) for v in doc.MaterialPropertyValues))
    tryp("SW material", lambda: doc.GetMaterialPropertyName2(cfg, ""))
    ext = doc.Extension
    tryp("render materials count", lambda: ext.GetRenderMaterialsCount2(1, None))
    def rms():
        out = []
        for rm in (ext.GetRenderMaterials2(1, None) or ()):
            out.append((rm.FileName, "spec", rm.Specular, "refl", rm.Reflectivity, "transp", rm.Transparency, "ents", rm.GetEntitiesCount))
        return out
    tryp("render materials", rms)
```

The `GetMaterialPropertyName2` line **failed**: `(-2147352571, 'Type
mismatch.')` on every part. Its second parameter is an out parameter, and a
plain `""` is not a by-reference argument (compare [GOTCHAS §4](../../GOTCHAS.md)).
It was not retried, so whether the parts had a SolidWorks material assigned
was not read.

## Removing it

```python
id1 = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0); id2 = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
rms[0].GetMaterialIds(id1, id2)
ids = [id1.value, id2.value]
dispid = ext._oleobj_.GetIDsOfNames("DeleteDisplayStateSpecificRenderMaterial")
ok = ext._oleobj_.Invoke(dispid, 0, pythoncom.DISPATCH_METHOD, True, VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_I4, [ids[0]]), VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_I4, [ids[1]]))
```

The whole script, with the guard that refuses to act unless the part carries
exactly one appearance and it is `color.p2m`, is
[`code/python/appearance/remove_appearance.py`](../../code/python/appearance/remove_appearance.py).
It does not save.

- `IRenderMaterial.GetMaterialIds(out id1, out id2)` gives the appearance's two
  IDs through two by-reference integers.
- `IModelDocExtension.DeleteDisplayStateSpecificRenderMaterial(ids1, ids2)`
  takes **two arrays**: the first IDs of every appearance to delete, and their
  second IDs. The API help's VBA example builds `materialID1_ToDelete(0)` and
  `materialID2_ToDelete(0)` as one-element `Long` arrays. Per the help it
  deletes them "from the active configuration".

## Why it is not obvious

**Zeroing the transparency does not bring back the normal look.** The user
set the part's transparency to 0 in SolidWorks and the part still looked
glossy and unlike their other parts. The parts that looked normal had **no
applied appearance at all**; the odd one had `color.p2m` applied with
reflectivity 0.15. Editing the appearance keeps it applied. Deleting it is
what made the part read the same as the others.

**The removal call failed twice, silently, before it worked.** Both are worth
knowing:

1. Reached through pywin32's dynamic dispatch,
   `ext.DeleteDisplayStateSpecificRenderMaterial(...)` raised
   `TypeError: 'bool' object is not callable`. Attribute access had already
   **run it once with no arguments**, as a property get, and handed back its
   `bool`. That run changed nothing: the appearance was still there when read
   back. The fix is to invoke it as a method through `IDispatch`, as above;
   see [connect/02](../connect/02-attach-from-python.md).
2. Invoked properly but with **one** array, `[11, 0]`, it returned `False`
   and deleted nothing. The two IDs go in two arrays.

**`IModelDocExtension.RemoveMaterialProperty(Config_opt, Config_names)`
exists, and was not used.** Its help says it "is intended to be used on
features that have a changed material property value", so it was not the
call for an appearance on the whole part. It was never run here.

## What it does not do

- It was run on one part with one part-level appearance. A part with several
  appearances, or ones on faces, bodies or features, was not tried. Whether
  `GetRenderMaterials2` lists those too, and what `GetEntitiesCount` would
  say, is open. Settling it needs a scratch part with an appearance on a face.
- `swThisDisplayState` only. A part with several display states was not tried.
- Whether **reflectivity** alone is what made the part look glossy was not
  isolated. Only the whole appearance was removed.
- Whether **Ctrl+Z** undoes the API delete was not tested. The script does
  not save, so the change stays in the open session until the user saves.
- What `IRenderMaterial.MaterialID` means is not known. It read `1` while
  `GetMaterialIds` gave `11, 0`, and only the latter pair was used.

## Evidence

SolidWorks 2024 SP5 (revision 32.5.0), from Windows Python 3.13 with pywin32,
launched from WSL (see [connect/10](../connect/10-driving-from-outside-windows.md)),
attached through the Running Object Table moniker `SolidWorks_PID_58424`.
On 2026-10-01:

- `v0 Master Geometry.SLDPRT`: `MaterialPropertyValues`
  `(0.7922, 0.8196, 0.9333, 1.0, 1.0, 0.5, 0.3125, 0.75, 0.0)`, transparency
  0.75. `GetRenderMaterials2(1, None)` gave one appearance,
  `...\SOLIDWORKS (2)\data\graphics\materials\color.p2m`, `Specular` 0.5,
  `Reflectivity` 0.15000000596046448, `Transparency` 0.75,
  `GetEntitiesCount` 1. All 9 solid bodies: `MaterialPropertyValues2` `None`.
- The six gear parts open in the same session, which the user pointed to as
  looking the way a part should: the same nine values with transparency 0.0,
  and `GetRenderMaterialsCount2` 0.
- `IModelView.DisplayMode` (from `IModelDoc2.ActiveView`) read `5`,
  `swViewDisplayMode_ShadedWithEdges`, so the display style was not the cause.
- `GetMaterialIds` gave `11, 0`. Deleting with `[11]` and `[0]` returned
  `True`. Read back afterwards: `GetRenderMaterials2` gave 0 appearances and
  `MaterialPropertyValues` gave
  `(0.792, 0.82, 0.933, 1.0, 1.0, 0.5, 0.312, 0.0, 0.0)`, the same as the gear
  parts. How the part looked on screen afterwards was not reported back.

## See also

- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — the ROT attach, and invoking a member that attribute access has already run
- [connect/12 — Read the API help offline](../connect/12-read-the-api-help-offline.md) — how the two-array signature was found
- [connect/10 — Driving from outside Windows](../connect/10-driving-from-outside-windows.md)
- [GOTCHAS §55, §70 and §71](../../GOTCHAS.md)
