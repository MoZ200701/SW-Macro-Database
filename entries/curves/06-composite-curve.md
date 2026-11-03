---
id: curves-06-composite-curve
title: Join several curves into one selectable curve
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [IModelDoc2.InsertCompositeCurve, IModelDocExtension.SelectByID2, IModelDoc2.ClearSelection2, AccessSelections, GetEntitiesToJoin, ReleaseSelectionAccess, IFeature.GetParents]
keywords: [InsertCompositeCurve, composite curve, boundary surface profile, composite corner angle, natural end lean, kink at cut, pack points at cut, SelectByID2, selection mark, REFERENCECURVES, sharp corner, loft profile, piece count, delete composite deletes loft, rename aside, edit composite, GetEntitiesToJoin, hairpin, select after rebuild, AccessSelections, ReleaseSelectionAccess, rolls the tree back, IsRolledBack, method object never ran, GetParents, which curves a composite joins, slow composite read]
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

*Since 2026-09-23 the Airfoil Converter finds what this made by the feature count and `GetLastFeatureAdded` instead of listing the tree before and after — the same check, a hundredth of a second instead of seconds on a big part; the copy in [`code/python/swcom.py`](../../code/python/swcom.py) is that version. See [curves/02](../curves/02-insert-curve-from-file.md).*

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
solution: find exactly one new feature, and refuse to guess if there is not
exactly one — by the feature count and `GetLastFeatureAdded`, which is a
hundredth of a second, rather than by diffing two listings of the tree, which
is seconds on a big part. See [curves/02](02-insert-curve-from-file.md).

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
  every profile at the same places so the counts always match. A Boundary
  Surface does not ask this (below).
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

### As a Boundary Surface profile

A composite is also a Boundary Surface profile, selected the same way at the
Boundary Surface's indexed marks ([surfacing/05](../surfacing/05-boundary-surface-from-code.md)).
On SolidWorks 2026 (34.0.0), six three-piece composites and six guides made one
`NetBlend`, error 0, with one face per piece, split along the joints (run
`p9_creases-20261103-040239`). **The same-number-of-pieces rule above is a loft
rule**: a Boundary Surface took a one-piece closed curve as its first profile
against three-piece composites (same run), and a surface's single end edge
likewise (`p9_creases-20261103-041135`).

A composite that **has to be cut at a corner** of a closed section is now
known to be the only way to keep that corner at all: a closed curve file is a
periodic spline and rounds any corner at its seam away
([curves/01](01-sldcrv-file-format.md#closed-curves)). Two open halves of a
V-keel section, cut at the keel and joined, kept corners of 16–102° to 0.11°
(at the 26 of 30 stations the probe read),
and a Boundary Surface over them held the corner (45.72° across the keel at
every step down to 0.05 mm, against 45.89° in the data). Those V sides were
**straight**; see the next section for curved ones. Run
`p9_creases-20261103-040014`.

### A composite's corner is set by its pieces' ends

A composite joins its pieces without refitting them, so its corner is sharp.
But **it is the corner between the two pieces' end tangents, and each piece
ends as a natural spline**, with zero curvature and a tangent that leans in
([GOTCHAS §65](../../GOTCHAS.md)). Where the sides of the corner are straight,
a natural end does not lean, and the corner comes out right. Where they are
curved, it does not:

| Cut at | Data's turn there | Composite's turn | Run |
|---|---|---|---|
| a 23.9° chine, curved sides, 48 points per section | 23.904–23.949° | **30.198–30.244°** | `p9_creases-20261103-040239` |
| a smooth keel (no corner in the data) | 0.002° | 2.655–2.676° | same |
| a smooth bilge, radius 15–22 mm, neighbours 9 / 4.4 / 0.54 / 0.135 mm away | 0° | **23.0° / 10.3° / 1.21° / 0.30°** | `p9_creases-20261103-040649` |
| a V keel with straight sides | 15.9–101.6° | within 0.11° (26 of 30 stations read) | `p9_creases-20261103-040014` |

A natural cubic through the same points predicted the 30.198° exactly, so the
error is in the curve, not in anything downstream: a Boundary Surface over
those composites followed them to 0.05°. The kink at a cut through smooth data
falls roughly linearly with the distance to the neighbouring points, so **pack
points toward every cut on both sides**: at 0.135 mm spacing a cut on a 15 mm
radius turned 0.30° and the deviation near it was down to the uncut curve's
(0.0012 mm). The curvature at the joint stays exactly 0 however close the
points; packing narrows the flat spot but does not remove it. SolidWorks 2026
(34.0.0), 2026-11-03.

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

### Checking which curves a composite joins, without the rollback

`composite_sources` above rolls the whole part back and forward to read one
composite, and on a copy of a 560-feature part that was **10–13 s a read**. A
push that checked each of 19 section composites against the curves it should
join spent **190 s** doing it — more than any other step but the reloads.

If what you need is *which* curves a composite joins — not in what order —
`IFeature.GetParents` on the composite's feature answers in a millisecond and
touches nothing:

```python
def composite_parents(self, name: str) -> List[str]:
    """The curves a composite joins, by name, in no particular order."""
    parents = call(self._curve_feature(name), "GetParents") or ()
    return [str(call(parent, "Name")) for parent in parents]
```

On SolidWorks 2026 SP0.0, 2026-09-23 (`e59`), on a composite of three curves:
`GetParents` took **0.001 s** and named the three curves
(`v57_s05_lower`, `v57_s05_upper`, `v57_s05_te`); `composite_sources` took
**12.9 s** and named the same three, and the tree was left rolled forward
after both. The two orders happened to agree on that one composite; the help
promises no order for `GetParents`, so the Airfoil Converter compares the two
lists as **sets**, and keeps `composite_sources` for the one place it needs
the pieces in order (reading a profile's pieces for an end cap,
[surfacing/04](../surfacing/04-cap-a-refused-loft-into-a-solid.md)). Pushes of
48 to 99 curves with 15 to 22 existing composites then checked every join in
under a second in total.

## See also

- [curves/02 — Insert a curve from a file](02-insert-curve-from-file.md)
- [curves/09 — Curves as loft profiles](09-curves-as-loft-profiles.md)
- [curves/11 — Feature-tree folders](11-feature-tree-folders.md) — putting the composite in a folder with the curves it is built from
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — a composite as a loft profile
- [curves/04 — Reload a curve in place](04-reload-curve-in-place.md) — the composite follows
- [curves/12 — Roll the tree back before reloading](12-roll-the-tree-back-before-reloading.md) — putting a rolled-back tree right, and checking it went
- [connect/02 — Attach from Python](../connect/02-attach-from-python.md) — why the release had to be called with parentheses
- [surfacing/05 — Build a Boundary Surface from code](../surfacing/05-boundary-surface-from-code.md) — composites as Boundary Surface profiles, where piece counts need not match
- [curves/01 — The .sldcrv format](01-sldcrv-file-format.md#closed-curves) — why a corner on a closed section has to be a cut
- [GOTCHAS §12, §49, §55, §59, §65, §66](../../GOTCHAS.md)
