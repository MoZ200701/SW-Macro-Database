---
id: curves-08-rebuild-once
title: Suppress rebuilds, change everything, rebuild once
status: verified
verified_on: SolidWorks 2026
language: [vbscript, python]
api: [ISldWorks.CommandInProgress, IModelDoc2.ForceRebuild3, IModelDoc2.EditRebuild3]
keywords: [ForceRebuild3, CommandInProgress, suppress rebuild, batch update, transient error, finally, EditRebuild3, ForceRebuild3 slow, rebuild only what changed, EditRebuild3 returns False, feature in error, 25 seconds rebuild]
answers: "Why does my part show errors halfway through my batch update, and how do I make it fast?"
---

# Suppress rebuilds, change everything, rebuild once

## The two problems with rebuilding as you go

**It shows a real error that is not a real error.** Update twelve curves that a
surface is built on, one at a time with the rebuild live, and partway through
some curves carry the old geometry and some the new. Guide curves genuinely do
not pierce the sections at that moment. The surface genuinely does not close.
SolidWorks is right to complain, and the complaint is meaningless.

**It is slow.** You rebuild the whole model once per curve, twelve times, for a
result you discard eleven times.

## The pattern

```python
def set_rebuild_suppressed(self, suppressed):
    """Hold the rebuild off while several curves are reloaded."""
    self._app.CommandInProgress = bool(suppressed)


def rebuild(self):
    """Rebuild the active document. Called once, after the last curve."""
    return bool(call(self._active(), "ForceRebuild3", False))
```

Used like this:

```python
sw.set_rebuild_suppressed(True)
try:
    for curve in curves:
        sw.reload_curve(curve.name, curve.path)
finally:
    sw.set_rebuild_suppressed(False)
sw.rebuild()
```

## The `finally` is not optional

**A document left suppressed looks completely fine and silently stops
updating.** The user edits a sketch, nothing happens, and there is no
indication why. That is about the worst state to hand back to someone, and it
is exactly what an exception in the middle of the loop produces if the restore
is not in a `finally`.

Restore the flag before you rebuild, not after.

## `ForceRebuild3(False)`

The argument is `topOnly`. `False` rebuilds the whole model including
components; `True` rebuilds only the top level. For a part it makes little
difference. Verified returning `True` on a real part after all twelve curves
had been refreshed.

## On a big part, rebuild what changed, not everything

`IModelDoc2.ForceRebuild3` regenerates **every** feature whether or not
anything under it moved. `IModelDoc2.EditRebuild3` regenerates the features
whose input changed and whatever is built on those. On a part of 397 features —
six lofts through about 33 guide curves each, with splits, inserts and mirrors
on top — after reloading 39 curves:

| Call | Time | Result |
|---|---|---|
| `ForceRebuild3(False)` | 25 s | loft area 433,972.616122 mm² |
| `EditRebuild3` | **0.8 s** | loft area 433,972.616122 mm² |

Identical geometry, to twelve digits, for a thirtieth of the time. The features
a reloaded curve feeds are exactly the ones that have to be redone, so that is
what to ask for. Keep `ForceRebuild3` for the day something turns up that the
tree does not know has moved.

**`EditRebuild3` returns `False` whenever any feature in the part is in
error** — including features that were already in error before you touched
anything. The part measured here carried 13 such features from the start and
`EditRebuild3` answered `False` every single time, while doing exactly what it
was asked. So its return value is not "did not run", and code that treats it
that way will refuse to go on for no reason. Judge a rebuild by the geometry,
not by its answer ([reading/10](../reading/10-mass-properties-as-an-oracle.md)).

One more thing a rebuild is needed for: after a `reload_curve`, a **composite**
curve built on that curve could not be selected by `SelectByID2` until an
`EditRebuild3` had run. That is the rebuild-and-retry in
[curves/06](06-composite-curve.md), and `EditRebuild3` is enough for it.

```python
def rebuild(self, force: bool = False) -> bool:
    """Rebuild the active document. Called once, after the last curve.

    Only what needs it. ``EditRebuild3`` regenerates the features whose
    input moved and whatever is built on them; ``ForceRebuild3``
    regenerates every feature in the part whether or not anything under it
    changed. On a part of 397 features — six lofts through thirty guides
    each, with splits and inserts on top — forcing everything is 25
    seconds and this is 0.8, for geometry that came out identical.
    """
    doc = self._active()
    if force:
        return bool(call(doc, "ForceRebuild3", False))
    return bool(call(doc, "EditRebuild3"))
```

From the Airfoil Converter's `swcom.py`, `Session.rebuild`
([`code/python/swcom.py`](../../code/python/swcom.py)). Verified on SolidWorks
2026 SP0.0 (revision 34.0.0), 2026-09-22, experiments `e5b` and `e8`, and
commit `f63df5d` ("Roll the tree back to the curves before reloading them, and
rebuild only what changed").

## `CommandInProgress` does not make a reload cheap

It suppresses the **rebuild**. It does not stop `ModifyDefinition` being
charged for the tree below the curve: with it set `True`, a single curve reload
on the part above still cost 25 seconds. What makes that cheap is rolling the
tree back below the curves first —
[curves/12](12-roll-the-tree-back-before-reloading.md) — after which the same
reload is half a second. Set the flag anyway, for the reasons above; just do
not expect it to pay for this.

## In VBScript

```vb
' One rebuild at the end. Mid-refresh the guides genuinely do not pierce the
' sections, so the surface is expected to be in error until the last curve lands.
Dim rebuilt
On Error Resume Next
rebuilt = swModel.ForceRebuild3(False)
If Err.Number <> 0 Then
    WScript.Echo "warn    rebuild raised: " & Err.Description
    Err.Clear
Else
    WScript.Echo "rebuild " & CStr(rebuilt)
End If
On Error GoTo 0
```

A rebuild that raises is a warning, not a failure. The changes are committed
either way, and the user can rebuild by hand. Do not exit non-zero for it.

## Write everything first

The full ordering that works:

1. Write **every** file to disk.
2. Suppress rebuilds.
3. Reload **every** curve.
4. Restore the flag, in a `finally`.
5. Rebuild **once**.
6. Report per feature.

Interleaving steps 1 and 3 means a failure partway leaves the part half-updated
against files that are all new, which is the hardest state to reason about
afterwards.

## Also observed with solid features

On SolidWorks 2026 (revision 34.0.0), probe `batched_under_command_in_progress`
(run 20260914-180426): with `CommandInProgress` set `True`, a boss extrude and a
through cut were both made; the flag was restored in a `finally` and read back
`False`; one `ForceRebuild3` then gave the part the expected volume,
12283.627 mm³. A gear build ran its whole batch of globals, sketches and
features under the flag the same way, and a later change of its globals was
also made under it and rebuilt once. See
[features/01](../features/01-boss-extrude.md).

## See also

- [curves/04 — Reload a curve in place](04-reload-curve-in-place.md)
- [curves/12 — Roll the tree back before reloading](12-roll-the-tree-back-before-reloading.md) — what `CommandInProgress` does not pay for
- [GOTCHAS §9, §58](../../GOTCHAS.md)
- [features/01 — Boss extrude](../features/01-boss-extrude.md)
- [equations/01 — Global variables from code](../equations/01-global-variables-from-code.md) — changing many globals, then one rebuild
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — one rebuild after all the lofts
- [files/04 — Export a body to STEP](../files/04-export-a-body-to-step.md) — suppressions restored in a `finally`
