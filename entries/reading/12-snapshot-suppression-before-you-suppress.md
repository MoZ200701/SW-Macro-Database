---
id: reading-12-snapshot-suppression-before-you-suppress
title: Snapshot every feature's suppression before you suppress one, because suppression cascades
status: verified
verified_on: SolidWorks 2026 SP0.0 (revision 34.0.0)
language: [python]
api: [IFeature.SetSuppression2, IFeature.IsSuppressed, IModelDoc2.FirstFeature, IFeature.GetNextFeature, IFeature.GetFirstSubFeature]
keywords: [SetSuppression2, IsSuppressed, suppression cascade, suppressed children, unsuppress does not restore, swFeatureSuppressionAction_e, swInConfigurationOpts_e, 74 features, Sketch9<3>, feature name not found, restore by object, parents first, export one body]
answers: "Why did suppressing one feature suppress 74 others, and how do I put the part back exactly as it was?"
---

# Snapshot suppression before you suppress

## What this is for

Writing one body of a multi-body part to STEP, or measuring one loft on its
own, means suppressing the other body features while you do it and putting them
back afterwards ([files/04](../files/04-export-a-body-to-step.md)). The obvious
shape — remember what you suppressed, unsuppress exactly that in a `finally` —
**does not put the part back**.

Suppressing one body feature suppressed **74 other features**: the splits and
inserts built on that body, the folders holding them, and the planes and
sketches under those. Unsuppressing the one feature brought back **none** of
them. The part was handed back with a quarter of its tree switched off, looking
entirely normal.

## What happens

On a real 397-feature part, suppressing a single loft body feature so its
neighbour could be exported alone:

- solid bodies fell from 16 to 6;
- 74 features read `IsSuppressed` `True` that had not before, of types
  `FtrFolder`, `Split`, `CombineBodies`, `RefPlane`, `ProfileFeature`, `ICE`,
  `NetBlend`, `RefAxis`, `FillRefSurface`;
- unsuppressing the feature that was suppressed restored none of them.

And one of the 74 is named **`Sketch9<3>`**. A walk that finds features by name
and then acts on them cannot find that one again — the name walk raises "No
feature called 'Sketch9<3>' is in ..." — so a restore keyed on names is
missing a feature before it starts.

## The fix: hold the feature objects, in tree order

Write down every feature's state **as objects** before, and restore from that
after. Parents come first because the tree is in order, and a child cannot be
unsuppressed before the thing it is built on.

From the Airfoil Converter's `swcom.py`, `Session.suppression_state` and
`Session.restore_suppression`, copied as they ran
([`code/python/swcom.py`](../../code/python/swcom.py)):

```python
@dataclass
class Suppressed:
    """What one feature's suppression was, and the feature itself.

    The feature rather than its name: a name is not always enough to find it
    again, and this is what a restore has to work through.
    """

    name: str
    feature: Any = field(repr=False)
    suppressed: bool = False


def suppression_state(self) -> List["Suppressed"]:
    """Every feature in the tree, in tree order, and whether it is suppressed."""
    return [
        Suppressed(name=str(call(feature, "Name")), feature=feature,
                   suppressed=bool(call(feature, "IsSuppressed")))
        for feature in self._walk_objects(call(self._active(), "FirstFeature"))
    ]


def restore_suppression(self, state: Sequence["Suppressed"]) -> List[str]:
    """Put back every feature whose suppression has changed since ``state``.

    In the order the tree holds them, so that a parent is unsuppressed
    before whatever was built on it. Returns the names of any that would
    not go back, because a part left with features suppressed is worth
    saying out loud.
    """
    left: List[str] = []
    for item in state:
        try:
            now = bool(call(item.feature, "IsSuppressed"))
        except Exception:  # noqa: BLE001 - one feature that will not answer
            left.append(item.name)
            continue
        if now == item.suppressed:
            continue
        action = SUPPRESS if item.suppressed else UNSUPPRESS
        try:
            put_back = call(item.feature, "SetSuppression2", action,
                            THIS_CONFIGURATION, None)
        except Exception:  # noqa: BLE001 - and one that will not move
            put_back = False
        if not put_back:
            left.append(item.name)
    return left
```

Used like this, around whatever needs one body alone:

```python
state = sw.suppression_state()
try:
    for name in others:
        sw.set_suppressed(name, True)
    sw.rebuild()
    sw.export_step(path)
finally:
    left = sw.restore_suppression(state)
    sw.rebuild()
    if left:
        result.error = "the part was left with " + ", ".join(left) + " suppressed"
```

Interfaces and constants:

- `IFeature.SetSuppression2(action, configOption, configNames)`. `action` is
  `swFeatureSuppressionAction_e`: **0 suppress, 1 unsuppress**. `configOption`
  is `swInConfigurationOpts_e` **1**, this configuration. The third argument is
  a bare `None` and was accepted — unlike `ModifyDefinition`'s component, which
  needs a typed null ([GOTCHAS §4](../../GOTCHAS.md)).
- `IFeature.IsSuppressed` is a property get through late binding and answers a
  plain `bool`.
- `_walk_objects` walks `IModelDoc2.FirstFeature` / `IFeature.GetNextFeature`,
  stepping into children with `GetFirstSubFeature` / `GetNextSubFeature`
  ([connect/08](../connect/08-find-a-feature-by-name.md)), and yields the
  feature **objects**, not names. Keeping the object is the whole point: it
  outlives a name nothing can look up.
- No lengths cross this boundary, so no unit question.

## What it costs

A walk of 397 features is tens of seconds of COM round trips — about 10 s here
— and this does two of them per body exported. On the part measured, adding the
snapshot took a six-loft export from about **5 minutes to about 9**.

That is a price a diagnostic export can pay and an interactive loop probably
cannot. If you need it faster, the thing to try is walking once and reusing the
snapshot across every body of one run; that was not tried.

## What it does not do

- **Why it cascades was not established.** That SolidWorks suppresses what is
  built on a suppressed feature is reasonable; that unsuppressing the parent
  does not undo it is what costs the time, and no setting was found that
  changes it.
- Only one configuration was ever involved (`swInConfigurationOpts_e` 1).
  Whether a cascade behaves the same across configurations was not tried.
- The restore was only ever checked by counting: 0 features left suppressed
  where 74 had been, and the solid-body count back to 16. Whether every
  feature's *rebuild* state is identical afterwards was not checked, only its
  suppression.
- A feature that will not answer, or will not move, is reported by name rather
  than retried.

## Evidence

SolidWorks 2026 SP0.0 (revision 34.0.0), Windows, Python 3.13 with pywin32, on
a save-as copy of a real 397-feature part. Experiments `e49`, `e50` and `e52` of
2026-09-22, and the Airfoil Converter commit `8555d5f` ("Check that a cap spans
its end and a knit makes a solid, and put a suppression back") on main.

- `e49`: after `loft_and_export` had suppressed the other body features and
  unsuppressed them again its own way, the part held **6 solid bodies where it
  had 16**, and the check crashed on a feature called `Notes` that the name
  walk could not find.
- `e50`: the part was left with **74 features suppressed**, listed by name and
  type — `Inserts` (FtrFolder), `Split1` (Split), `Inserts cut profile`
  (CombineBodies), seven `wing_tip*` planes, `Aileron Rotation Axis` (RefAxis),
  the aileron `FillRefSurface` and `NetBlend` features, thirteen `Wing insert`
  and `Wing Split` splits, and `Sketch9<3>` (ProfileFeature), which could not be
  unsuppressed because it could not be found by name. Restoring by object took
  the part back to **17 solid bodies**.
- `e52`: with `suppression_state`/`restore_suppression` in place,
  `loft_and_export` on the same part came back with **0 features suppressed**
  (`suppressed count back: True (0 vs 0)`), and the run took **537.6 s** where
  the earlier one without the snapshot took 296.3 s.

## See also

- [files/04 — Export a body to STEP](../files/04-export-a-body-to-step.md) — what this protects, and where it is used
- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md) — the walk, and why a name is not a handle
- [curves/10 — Persistent references](../curves/10-persistent-references.md) — the other way to hold a feature that a name cannot
- [curves/08 — Rebuild once, at the end](../curves/08-rebuild-once-at-the-end.md) — the same `finally` discipline for the rebuild flag
- [reading/11 — Cheap change detection](11-cheap-change-detection.md) — what a tree walk costs, and how to avoid one
- [surfacing/04 — Cap a refused loft into a solid](../surfacing/04-cap-a-refused-loft-into-a-solid.md) — the bodies being exported one at a time
- [GOTCHAS §62](../../GOTCHAS.md)
