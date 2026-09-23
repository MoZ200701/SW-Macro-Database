---
id: curves-12-roll-the-tree-back-before-reloading
title: Roll the tree back before reloading curves, because a reload is charged for the tree below it
status: verified
verified_on: SolidWorks 2026 SP0.0 (revision 34.0.0)
language: [python]
api: [IFeatureManager.EditRollback, IModelDoc2.FeatureByPositionReverse, IFeature.IsRolledBack, IFeature.ModifyDefinition, IModelDoc2.EditRebuild3]
keywords: [EditRollback, swMoveRollbackBarTo_e, rollback bar, roll back, roll forward, previous position, slow reload, LoadPointsFromFile slow, ModifyDefinition slow, 25 seconds per curve, IsRolledBack, FeatureByPositionReverse, GetRollbackBarPosition, rolled back part, loft with no faces, insert at the bar]
answers: "Why does reloading each curve take 25 seconds on a big part, and how do I make it half a second?"
---

# Roll the tree back before reloading curves

## What this is for

[curves/04](04-reload-curve-in-place.md) replaces a curve's points without
breaking what refers to it. On a small part that is instant. On a part with
six lofts, splits, inserts and mirrors built on those curves it is not:
committing the new points makes SolidWorks reconsider everything below the
curve, once per curve, and the reload of one curve cost **25 seconds**.

Thirty-nine curves is sixteen and a half minutes, which was the whole of a
seventeen-minute export. Putting the rollback bar just below the curves first
takes it to half a second each, because everything that would have been
reconsidered is rolled back and has nothing to say yet.

## What it costs, measured

On a real 397-feature part (six lofts through about 33 guide curves each, plus
splits, inserts and mirrors), SolidWorks 2026 SP0.0:

| With the tree | Per reload | 39 curves, end to end |
|---|---|---|
| rolled forward | 25.2 / 25.5 / 25.5 s | about 16.5 minutes |
| rolled back to just after the last curve | 0.50 s | **29.4 s**, bar and rebuild included |

The 29.4 s breaks down as: roll back 0.1 s, 39 reloads 19.6 s, roll forward
9.0 s, one `EditRebuild3` 0.7 s.

`ISldWorks.CommandInProgress` makes no difference to this. The 25-second
reloads above were measured with it set `True` — it suppresses the *rebuild*,
not the commit ([curves/08](08-rebuild-once-at-the-end.md)). Set it anyway,
for the reason that entry gives, but do not expect it to pay for this.

## The two calls

```python
# swMoveRollbackBarTo_e, read off this machine's swconst.tlb rather than
# remembered.
ROLLBACK_TO_END = 1
ROLLBACK_TO_PREVIOUS = 2
ROLLBACK_BEFORE_FEATURE = 3
ROLLBACK_AFTER_FEATURE = 4


def roll_back_to(self, name: str) -> None:
    """Put the rollback bar just after the feature ``name``."""
    manager = call(self._active(), "FeatureManager")
    if not call(manager, "EditRollback", ROLLBACK_AFTER_FEATURE, name):
        raise SolidWorksError(f"The tree could not be rolled back to {name}.")


def roll_forward(self) -> None:
    """Put the bar at the end of the tree, and make sure it went there."""
    doc = self._active()
    call(call(doc, "FeatureManager"), "EditRollback", ROLLBACK_TO_END, "")
    if self._rolled_back():
        raise SolidWorksError("The tree could not be rolled forward again.")


def _rolled_back(self) -> bool:
    """Is any of the tree rolled back? The last feature is the one to ask."""
    last = call(self._active(), "FeatureByPositionReverse", 0)
    return bool(last is not None and call(last, "IsRolledBack"))
```

From the Airfoil Converter's `swcom.py`, `Session.roll_back_to`,
`Session.roll_forward` and `Session._rolled_back`, copied as they ran
([`code/python/swcom.py`](../../code/python/swcom.py)).

- `EditRollback(action, featureName)` is on **IFeatureManager**, from
  `IModelDoc2.FeatureManager`. The action is `swMoveRollbackBarTo_e`; the name
  is the feature to move relative to, and `""` for the actions that need none.
  It returns a boolean. No lengths, so no units question.
- `FeatureByPositionReverse(0)` is on **IModelDoc2** and gives the last feature
  in the tree. `IsRolledBack` is on **IFeature** and comes back as a genuine
  Python `bool` through late binding — `type(...) is bool`, checked — so it is
  worth trusting where the `EditRollback` return is not.

## "Previous position" answers True and moves nothing

`EditRollback(2, "")` — `swMoveRollbackBarToPreviousPosition` — returned
`True` in 0.1 s and left the bar exactly where it was. The part stayed rolled
back: its lofts read **0 faces, 0 mm² of area**, and the splits and inserts
under them were gone from view. A second call, `EditRollback(1, "")` to the
end, returned `True` in 0.8 s and the lofts came back to 4 faces and
433,972.6 mm².

That is worse than a call that fails, because the obvious repair — "put the bar
back where the user had it" — is the one that does nothing. Two whole pushes
handed the part back rolled back before this was found.

So: roll to **End (1)**, which is where the bar stands for anyone who has not
moved it, and then ask the last feature whether it is still rolled back. Do not
believe the return value on its own.

`IFeatureManager.GetRollbackBarPosition`, which would have made this obvious,
is not reachable through pywin32 late binding at all: attribute access raised
`AttributeError: <unknown>.GetRollbackBarPosition`.

## New features land at the bar (not observed here)

A feature made while the tree is rolled back is created **at the bar**, not at
the end of the tree. That is what you want here — a new curve lands beside the
curves it belongs with, above the lofts that will use it — and it is why the
Airfoil Converter inserts as well as reloads inside the rolled-back block.

**Nothing in this campaign actually inserted a curve while rolled back**: every
run had all 39 curves already in the part, and reported `inserted: 0`. So this
is how the tool is written and what the bar means, not something watched
happening. If you rely on it, check where the feature landed.

## The shape that works

1. Find the last feature of the set you are about to reload, in tree order.
2. `EditRollback(4, that_name)`.
3. Set `CommandInProgress`, reload every curve, restore it — in a `finally`.
4. `EditRollback(1, "")`, then check `IsRolledBack` on the last feature.
5. One `EditRebuild3` ([curves/08](08-rebuild-once-at-the-end.md)).

Steps 2 and 4 belong in a `try`/`finally` around step 3 for the same reason the
rebuild flag does: **a part left rolled back looks like a part with half its
features deleted.** The Airfoil Converter's `swlink._apply` puts the roll
forward in the `finally` and the flag restore inside that, so that whichever
fails, the other still happens.

A bar that will not move is a cost, not a correctness problem: the push goes
ahead on the live tree the slow way rather than refusing.

## What it does not do

- Only `swMoveRollbackBarToAfterFeature` (4) and `swMoveRollbackBarToEnd` (1)
  were run. `BeforeFeature` (3) was not tried; `PreviousPosition` (2) was tried
  and does nothing, above.
- The 25 s figure is this part. It is a function of what is built below the
  curves, not of the curve, so read it as "the tree below you, once per
  commit", not as a constant.
- Why the reload is charged for the tree below it when `CommandInProgress` is
  set was not established. Only that it is.
- Rolling back and forward is not free either: forward cost 9 s here, and up to
  25 s on a part that had to regenerate everything. It pays from about the
  second curve.
- Whether a rolled-back tree changes what `SelectByID2` can find below the bar
  was not tested. A composite curve above the bar read back fine.
- **Where a newly inserted feature lands was not observed**, above. Every run
  reloaded existing curves.
- Only one document was ever rolled back at a time, and only a part.

## Evidence

SolidWorks 2026 SP0.0 (revision 34.0.0), Windows, Python 3.13 with pywin32,
driving a save-as copy of a real 397-feature part. Experiment scripts and logs
`e8`, `e17`, `e18`, `e20`, `e24` of 2026-09-22, and the Airfoil Converter
commits `f63df5d` ("Roll the tree back to the curves before reloading them, and
rebuild only what changed") and `0db0d66` ("Roll the bar to the end and check it
went, since 'previous position' lies") on main, released as v1.6.

- `e8`: three reloads with the tree forward, 25.2 / 25.5 / 25.5 s each, under
  `CommandInProgress`. Then roll back 0.1 s, 39 reloads 19.6 s (0.50 s each),
  roll forward 9.0 s, `EditRebuild3` 0.7 s, total 29.4 s. The loft's area
  changed from 433,971.771401 to 433,972.616122 mm², so the new points did
  reach the geometry.
- `e17`: after a push, `EditRollback(2, "")` → `True` in 0.1 s, loft still
  0 faces / 0.000000 mm²; `EditRollback(1, "")` → `True` in 0.8 s, loft 4 faces
  / 433,972.616122 mm². `GetRollbackBarPosition` raised.
- `e20`: the push before the fix left `'200mm Inserts'.IsRolledBack` `True` and
  the loft at 0 faces, twice running. `e24`, after it: `False` and 4 faces,
  area 433,972.616122.
- `e18`: five offsets pushed in turn, each 64–67 s, each followed by a roll to
  end of 0.8 s where the previous-position call had been.

## See also

- [curves/04 — Reload a curve in place](04-reload-curve-in-place.md) — the reload this makes affordable
- [curves/08 — Rebuild once, at the end](08-rebuild-once-at-the-end.md) — `CommandInProgress`, and which rebuild to use afterwards
- [curves/06 — Composite curves](06-composite-curve.md) — another call that moves the bar, without being asked to
- [surfacing/04 — Cap a refused loft into a solid](../surfacing/04-cap-a-refused-loft-into-a-solid.md) — built with the tree rolled back to the curves
- [reading/11 — Cheap change detection](../reading/11-cheap-change-detection.md) — what a tree walk of 397 features costs
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — `call` and the typed null
- [GOTCHAS §56, §57](../../GOTCHAS.md)
