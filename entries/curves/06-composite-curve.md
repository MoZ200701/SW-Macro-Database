---
id: curves-06-composite-curve
title: Join several curves into one selectable curve
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [IModelDoc2.InsertCompositeCurve, IModelDocExtension.SelectByID2, IModelDoc2.ClearSelection2, AccessSelections, GetEntitiesToJoin, ReleaseSelectionAccess]
keywords: [InsertCompositeCurve, composite curve, SelectByID2, selection mark, REFERENCECURVES, sharp corner, loft profile, piece count, delete composite deletes loft, rename aside, edit composite, GetEntitiesToJoin, hairpin, select after rebuild, AccessSelections, ReleaseSelectionAccess, rolls the tree back, IsRolledBack, method object never ran]
answers: "How do I join two curves into one thing a loft can select, keeping the corner sharp?"
---

# Join several curves into one selectable curve

## The problem it solves

A Curve Through XYZ Points is **one spline through its points**. So an airfoil
section plus the straight line closing its blunt trailing edge cannot be one of
those without the spline rounding the corner where they meet.

A composite curve joins them instead of refitting them. The corners stay sharp.
And because it is derived, it follows its inputs whenever they are reloaded,
which means it survives a live update loop.

## The call, and the trap

```python
COMPOSITE_SELECT_MARK = 1


def insert_composite_curve(self, sources, name):
    """Join several curves into one selectable curve, and name it."""
    if len(sources) < 2:
        raise SolidWorksError("A composite curve needs at least two curves to join.")

    doc = self._active()
    extension = call(doc, "Extension")
    call(doc, "ClearSelection2", True)
    for position, source in enumerate(sources):
        selected = call(
            extension, "SelectByID2", source, "REFERENCECURVES",
            0.0, 0.0, 0.0, position > 0, COMPOSITE_SELECT_MARK, _null_dispatch(), 0,
        )
        if not selected:
            call(doc, "ClearSelection2", True)
            raise SolidWorksError(f"{source} could not be selected to join.")

    before = set(self.feature_names())
    made = call(doc, "InsertCompositeCurve")
    call(doc, "ClearSelection2", True)
    if not made:
        raise SolidWorksError(
            f"SolidWorks would not join {' and '.join(sources)} into one curve."
        )

    created = [n for n in self.feature_names() if n not in before]
    if len(created) != 1:
        raise SolidWorksError(
            f"Joining added {len(created)} features, so which one it is cannot be told."
        )
    return self.rename_feature(created[0], name)
```

**The selection mark must be 1.** At mark 0, `InsertCompositeCurve` returns
`False` and says nothing about why. This is not documented anywhere obvious and
is pure trial and error to find.

## Reading the `SelectByID2` arguments

```
SelectByID2(name, type, x, y, z, append, mark, callout, selectOption)
```

| Argument | Value here | Why |
|---|---|---|
| `name` | the feature's name | which curve |
| `type` | `"REFERENCECURVES"` | the type string for a reference curve |
| `x, y, z` | `0, 0, 0` | ignored when selecting by name |
| `append` | `position > 0` | first clears, the rest add |
| `mark` | `1` | **the trap above** |
| `callout` | typed null | see [GOTCHAS §4](../../GOTCHAS.md) |
| `selectOption` | `0` | default |

## Clear the selection on every exit path

Including the failures. A leftover selection changes what the *next* call does,
and the resulting bug appears somewhere else entirely.

## Diff to find what you made

`InsertCompositeCurve` returns a boolean, same as `InsertCurveFile`. Same
solution: diff the feature names either side, and refuse to guess if the diff
is not exactly one. See [curves/02](02-insert-curve-from-file.md).

The type name of the result is `CompositeCurve`.

## If a curve was only just made, rebuild and select again

Straight after a push, `SelectByID2` sometimes returned `False` for a curve
that had just been inserted. The Airfoil Converter now selects through a helper
that clears, tries every pick, and on a miss calls `ForceRebuild3(False)` once
and tries again; the full helper is in
[features/12](../features/12-guided-loft.md), and
[`code/python/swcom.py`](../../code/python/swcom.py) uses it for joins and
lofts alike. Observed on SolidWorks 2026.

## A composite will not run through a hairpin

A trimmed outline had a point 0.07 mm past a corner, so the curve ran out and
straight back. SolidWorks 2026 would not join a curve through that. Taking the
out-and-back point out fixed it. Check the pieces for reversals before joining.

## As a loft profile

A composite is selectable as a loft profile with the `REFERENCECURVES` type,
like any other curve ([features/12](../features/12-guided-loft.md)). Four
things were learned on SolidWorks 2026 doing that:

- **Profiles must be cut into the same number of pieces.** A three-piece
  composite lofted only against another three-piece composite. A two-piece
  profile would not loft to a three-piece one. The Airfoil Converter now cuts
  every profile at the same places so the counts always match.
- **It follows its sources.** Reload the curves it joins in place
  ([curves/04](04-reload-curve-in-place.md)) and the composite, and a loft
  built on it, follow. A loft already in the part can be left as it is.
- **Changing *which* curves it joins is not usable.** Editing the composite's
  sources was tried and could not be made to work: the new pieces sit below the
  composite in the tree, out of its reach. How it failed was not written down.
- **Deleting a composite deletes the loft built on it.** So a composite whose
  pieces have changed is neither edited nor deleted. It is renamed out of the
  way, still holding up the old loft, and a new one is made under the old name
  ([GOTCHAS §49](../../GOTCHAS.md)). The loft then has to be pointed at the new
  composite by hand, and the user is told so.

From [`code/python/swlink.py`](../../code/python/swlink.py), `_join`:

```python
if join_as in present:
    # Already there, from an earlier push. Report it anyway, so a record
    # made before it existed still learns the name and stops calling it a
    # stray. A section can change how many pieces it comes in — a nose
    # that has come to a corner is two — so what it joins is checked.
    try:
        same = list(sw.composite_sources(join_as)) == list(sources)
    except SolidWorksError as exc:
        result.failures.append((join_as, str(exc)))
        return
    if same:
        _joined(result, join_as, made=False)
        return
    # What an existing composite joins cannot be changed from here — the
    # new pieces sit below it in the tree, out of its reach — and deleting
    # it takes the loft built on it along. So it is renamed out of the way,
    # still holding up that loft, and made again under its own name.
    absent = [name for name in sources if name not in curve_names]
    if absent:
        result.failures.append((join_as, f"cannot join without {', '.join(absent)}"))
        return
    taken = set(present)
    old = join_as + OLD_JOIN_SUFFIX
    count = 2
    while old in taken:
        old = f"{join_as}{OLD_JOIN_SUFFIX}{count}"
        count += 1
    try:
        kept = sw.rename_feature(join_as, old)
    except SolidWorksError as exc:
        result.failures.append((join_as, str(exc)))
        return
    if kept == join_as:
        result.failures.append((join_as, "SolidWorks would not rename the old one out of the way"))
        return
    result.remade.append((join_as, kept))
```

`OLD_JOIN_SUFFIX` is `"_old"`. After this the function makes the new
composite exactly as `insert_composite_curve` above does.

### Reading what a composite joins, and the two traps in it

`composite_sources` reads the pieces back through the composite's feature
data. As of 2026-09-22 this has been run and watched, and it needed fixing
twice:

```python
def composite_sources(self, name: str) -> List[str]:
    """The curves a composite joins, by name, in the order it holds them."""
    doc = self._active()
    data = call(self._curve_feature(name), "GetDefinition")
    if data is None or not call(data, "AccessSelections", doc, _null_dispatch()):
        raise SolidWorksError(f"The curves {name} joins could not be read.")
    try:
        kinds = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_VARIANT, None)
        entities = call(data, "GetEntitiesToJoin", kinds) or ()
        return [str(call(e, "Name")) for e in entities]
    finally:
        call(data, "ReleaseSelectionAccess")
```

`GetDefinition` is on **IFeature**; `AccessSelections(doc, component)`,
`GetEntitiesToJoin(ByRef types)` and `ReleaseSelectionAccess` are on the
composite curve's feature data (the interface was not named in the source).
On SolidWorks 2026 it returned the three curves the tip composite joins, by
name, in join order, in 3.2–4.5 s.

**`AccessSelections` rolls the model back to just before the feature.** The
help says so, and it is easy to read past. Straight after the call, the last
feature in the tree answered `IsRolledBack` `True`, the loft built on the
composite read **0 faces and 0.000000 mm²**, and the splits and inserts below
it were gone from view. Reading what a curve joins is not a read-only act.

**`ReleaseSelectionAccess` is what puts it back, and through late binding it
never ran.** It is a `Sub` — no return value — so pywin32 hands it over as an
**uncalled method object**, and `call(data, "ReleaseSelectionAccess")` under
the old rule fetched it and dropped it. Watched step by step: after
`AccessSelections` the tree was rolled back; after `call(...)` it was *still*
rolled back; after `data.ReleaseSelectionAccess()` it came forward in 0.9 s.

Every push the tool made while this check existed handed the part back rolled
back. It is the same family as `IAssemblyDoc.FixComponent`
([GOTCHAS §31](../../GOTCHAS.md)); the general rule, and the `call` that now
handles it, are in [connect/02](../connect/02-attach-from-python.md) and
[GOTCHAS §55](../../GOTCHAS.md).

Verified on SolidWorks 2026 SP0.0 (revision 34.0.0), 2026-09-22, experiments
`e21` and `e23`, and the Airfoil Converter commits `8566952` ("Actually
release the selection access a composite read takes") and `a2c1914` ("Call a
late-bound member that comes back uncalled"). A composite **above** the
rollback bar could still be read while the tree was rolled back, and the read
left the bar where it found it.

## See also

- [curves/02 — Insert a curve from a file](02-insert-curve-from-file.md)
- [curves/09 — Curves as loft profiles](09-curves-as-loft-profiles.md)
- [curves/11 — Feature-tree folders](11-feature-tree-folders.md) — putting the composite in a folder with the curves it is built from
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — a composite as a loft profile
- [curves/04 — Reload a curve in place](04-reload-curve-in-place.md) — the composite follows
- [curves/12 — Roll the tree back before reloading](12-roll-the-tree-back-before-reloading.md) — putting a rolled-back tree right, and checking it went
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — why the release had to be called with parentheses
- [GOTCHAS §12, §49, §55, §59](../../GOTCHAS.md)
