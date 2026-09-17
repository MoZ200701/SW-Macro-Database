---
id: features-12-guided-loft
title: Insert a loft through curves along guide curves, as a hand-made loft would be
status: partly-verified
verified_on: SolidWorks 2026 for the solid loft and its settings; the surface fallback's arguments were not examined
language: [python]
api: [IFeatureManager.InsertProtrusionBlend2, IModelDoc2.InsertLoftRefSurface2, IModelDocExtension.SelectByID2, IModelDoc2.ClearSelection2, IModelDoc2.ForceRebuild3, ISketchManager.ActiveSketch]
keywords: [InsertProtrusionBlend2, InsertLoftRefSurface2, loft, boss loft, surface loft, guide curves, guide curve influence, swGuideCurveInfluence_e, To next guide, maintain tangency, selection mark 2, Blend, BlendRefSurface, REFERENCECURVES, composite profile, surface fallback, 18 arguments, guide order]
answers: "How do I make a solid loft through two curves, held by guide curves, from code, with the same settings as one made in the Loft property page?"
---

# Insert a loft through curves along guide curves

## What this is for

Curves already in the part (imported Curve Through XYZ Points features, or
composite curves joining them), and a loft that has to go through some of them
as profiles and follow others as guides. By hand that is the Loft property page:
profiles in one box, guides in another, a few settings. From code it is one
selection with two marks and `IFeatureManager.InsertProtrusionBlend2` with
eighteen arguments. Built this way, the loft came out the same as one the user
made by hand from the same curves.

## The call

From the Airfoil Converter's COM module, copied as it ran
([`code/python/swcom.py`](../../code/python/swcom.py)):

```python
# A loft reads its profiles from selection mark 1 and its guide curves from
# mark 2, as the Loft property page does.
LOFT_PROFILE_MARK = 1
LOFT_GUIDE_MARK = 2
LOFT_TYPE_NAME = "Blend"
LOFT_SURFACE_TYPE_NAME = "BlendRefSurface"

# Values read out of the SolidWorks 2026 constant library (swconst.tlb) rather
# than remembered: swGuideCurveInfluence_e, ...
GUIDE_TO_NEXT_GUIDE = 0
GUIDE_TO_NEXT_SHARP = 1
GUIDE_TO_NEXT_EDGE = 2
GUIDE_GLOBAL = 3
```

```python
def _select_all(self, picks: Sequence[Tuple[str, int]], what: str) -> None:
    """Select curves by name, each at its mark, rebuilding and retrying once.

    Straight after a push SolidWorks sometimes cannot find a curve it has
    just made; a rebuild settles it.
    """
    doc = self._active()
    extension = call(doc, "Extension")
    for attempt in range(2):
        call(doc, "ClearSelection2", True)
        missing = ""
        for position, (curve, mark) in enumerate(picks):
            if not call(
                extension, "SelectByID2", curve, "REFERENCECURVES",
                0.0, 0.0, 0.0, position > 0, mark, _null_dispatch(), 0,
            ):
                missing = curve
                break
        if not missing:
            return
        call(doc, "ClearSelection2", True)
        if attempt == 0:
            call(doc, "ForceRebuild3", False)
    raise SolidWorksError(f"{missing} could not be selected {what}.")
```

```python
def insert_loft(
    self,
    profiles: Sequence[str],
    guides: Sequence[str],
    name: str,
    merge: bool = False,
    keep_tangency: bool = True,
    guide_influence: int = GUIDE_TO_NEXT_GUIDE,
    solid: bool = True,
) -> str:
    """A loft through ``profiles`` in order, held by ``guides``, named ``name``.

    A solid unless ``solid`` is off, when it is a surface: SolidWorks
    refuses some solids whose surface it makes without complaint, and a
    surface is all a measurement needs. A surface loft takes no guide
    influence; it uses SolidWorks' own.
    """
    if len(profiles) < 2:
        raise SolidWorksError("A loft needs at least two profiles.")
    doc = self._active()
    picks = [(p, LOFT_PROFILE_MARK) for p in profiles] + [(g, LOFT_GUIDE_MARK) for g in guides]
    self._select_all(picks, "for the loft")

    before = set(self.feature_names())
    if not solid:
        # Returns nothing either way; whether it worked shows in the tree.
        call(doc, "InsertLoftRefSurface2", False, keep_tangency, False, 1.0, 0, 0)
        made = True
    else:
        made = call(
            call(doc, "FeatureManager"), "InsertProtrusionBlend2",
            False,           # Closed
            keep_tangency,   # KeepTangency: "Maintain tangency" in the page
            False,           # ForceNonRational
            1.0,             # TessToleranceFactor
            0, 0,            # start and end constraints: none
            1.0, 1.0,        # tangent lengths, unused with no constraint
            False, False,    # tangent directions, likewise
            False, 0.0, 0.0, 0,  # not a thin feature
            merge,
            False, True,     # feature scope: every body
            guide_influence,
        )
    call(doc, "ClearSelection2", True)
    created = [n for n in self.feature_names() if n not in before]
    if made is None or made is False or not created:
        kind = "solid" if solid else "surface"
        raise SolidWorksError(
            f"SolidWorks would not make a {kind} loft of {' to '.join(profiles)} "
            f"along {len(guides)} guide curve(s)."
        )
    if len(created) != 1:
        raise SolidWorksError(
            f"The loft added {len(created)} features, so which one it is cannot be told."
        )
    return self.rename_feature(created[0], name)
```

`call`, `_null_dispatch()` and `rename_feature` are the late-bound helper, the
typed null and the rename-and-read-back from
[connect/02](../connect/02-attach-from-python.md) and
[curves/07](../curves/07-rename-a-feature.md). `feature_names()` lists the
tree ([reading/11](../reading/11-cheap-change-detection.md) has the one-call
listing it uses).

Interfaces and units:

- `InsertProtrusionBlend2` is on **IFeatureManager**, from
  `IModelDoc2.FeatureManager`. Eighteen arguments, in the order above; the
  comments are the tool's reading of them. The two thin-wall values (`0.0,
  0.0`) are unused here; like every length in the API they are in metres
  ([GOTCHAS §2](../../GOTCHAS.md)), and nothing in this entry passes a length.
- `InsertLoftRefSurface2` is on **IModelDoc2**, six arguments, and returns
  nothing useful, so success is judged by the feature-name diff.
- `SelectByID2` is on **IModelDocExtension**. Profiles at mark **1**, in loft
  order; guides at mark **2**; the first pick not appended, every later one
  appended. The type string is `"REFERENCECURVES"` for both imported and
  composite curves.
- The tool takes a solid loft's `GetTypeName2` to be `"Blend"` and a surface
  loft's `"BlendRefSurface"`, and uses both to find its lofts again
  (`features_of_type`). Those names are the tool's constants; no run recorded
  reading them back.
- `guide_influence` is `swGuideCurveInfluence_e`: `0` is "To next guide", the
  value the user's hand-made loft carried. `1`, `2`, `3` are as the constants
  above name them, read from `swconst.tlb`; only `0` has been run.

## The settings that reproduce a hand-made loft

The user's own loft, made in the Loft property page and read back, was:
**solid; Maintain tangency on; no start or end constraint; guide curve
influence "To next guide"**. Those are the defaults of `insert_loft` above:
`keep_tangency=True`, both constraints `0`, `guide_influence=0`, not closed,
not thin, `merge=False`.

## Around the call

The tool that drives it
([`code/python/swloft.py`](../../code/python/swloft.py)) adds three rules
worth copying:

**Refuse while a sketch is open.** Selecting from outside while the user is
editing a sketch can crash SolidWorks (see
[reading/04](../reading/04-read-the-selection.md) and GOTCHAS §51). The check:

```python
def editing_sketch(self) -> bool:
    doc = self._active()
    return _try(_try(doc, "SketchManager"), "ActiveSketch") is not None
```

`_try` is `call` with any exception turned into `None`.

**Leave a loft that is already there.** A loft built on curves follows them
when they are reloaded in place ([curves/04](../curves/04-reload-curve-in-place.md)),
so an existing loft of the same name needs nothing, and deleting it would take
anything built on it too. The tool only deletes and remakes a loft when working
on a *copy* of the part.

**Fall back to a surface.** Some lofts that SolidWorks refused as a solid it
made as a surface:

```python
try:
    result.feature = sw.insert_loft(
        plan.profiles, plan.guides, plan.name,
        keep_tangency=plan.keep_tangency, guide_influence=plan.guide_influence,
    )
except SolidWorksError:
    if not surface_fallback:
        raise
    # SolidWorks turns down some solids whose surface it makes
    # without complaint.
    result.feature = sw.insert_loft(
        plan.profiles, plan.guides, plan.name,
        keep_tangency=plan.keep_tangency, solid=False,
    )
    result.surface = True
```

Then one `ForceRebuild3(False)` after all the lofts, not one per loft
([curves/08](../curves/08-rebuild-once-at-the-end.md)).

## Why it is not obvious

- **Guides are a second selection mark.** Profiles and guides go in one
  selection, told apart only by mark 1 and mark 2. Nothing in the call's
  argument list mentions them.
- **The order you pick the guides in can change the shape.** On SolidWorks
  2026 the same loft with its guides picked in a different order differed by up
  to **0.04 mm** on one loft and not at all on another. The tool picks them in a
  fixed order (leading edge, surface guides, trailing edge) so one run compares
  with the next. The tool's own source comment says order does not change the
  shape; the measurement above supersedes it. Profile order is loft order and
  is not optional.
- **A curve just made may not be selectable yet.** Straight after a push,
  `SelectByID2` sometimes returned `False` for a curve that existed; one
  `ForceRebuild3` and a second attempt selected it.
- **Profiles have to be cut into the same number of pieces.** A composite
  profile of three pieces lofted only to another three-piece composite; a
  two-piece to a three-piece was refused. See
  [curves/06](../curves/06-composite-curve.md).
- **Guides very near a sharp corner of a profile break the loft.** See
  [surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md).
- **The call returns the feature or not, but the name is SolidWorks'.** Diff the
  tree and rename, as for curves.

## What it does not do

- **The surface fallback is the thinnest part.** That SolidWorks refused some
  solids and made the same loft as a surface was observed. The six arguments of
  `InsertLoftRefSurface2` are passed as above, and what each means was not
  recorded or varied; a surface loft takes no guide-influence argument, so it
  uses SolidWorks' default influence and may not match the solid it replaces.
- Only `guide_influence=0` and `keep_tangency=True` were run. Start and end
  constraints, closed lofts, thin lofts, `merge=True` and centreline lofts were
  not tried.
- More than two profiles: the call takes any number, but every loft measured
  was two profiles plus guides.
- Changing an existing loft's profiles or guides from code is not covered.
  Replacing a composite profile makes a new composite that the loft does not
  use; see [curves/06](../curves/06-composite-curve.md).
- Profiles here are reference curves. Sketch profiles are
  [features/07](07-loft-cut.md)'s case, for a cut.
- The tool's tests exercise the ordering, the fallback and the keep-existing
  rule against a fake SolidWorks only; the evidence below is what ran live.

## Evidence

SolidWorks 2026, Python over pywin32, the Airfoil Converter (commits of
2026-09-16: "Loft a wing in SolidWorks and write each loft to STEP", "Hold a
lofted wing to 0.05 mm, and loft it from the Wing tab", "Cut every offset
profile at its nose, ...").

- A loft built by `insert_loft` reproduced the user's hand-made loft of the
  same curves exactly, given the same guide order. The hand loft's settings,
  read back off it, are the ones listed above.
- Reordering the guides changed SolidWorks' result by up to 0.04 mm on one loft
  and not at all on another.
- Lofts of a 1000 mm wing, two profiles and up to sixteen surface guides a side
  plus edge guides, were built this way and written to STEP
  ([files/04](../files/04-export-a-body-to-step.md)); their surfaces were
  measured against the intended shape
  ([surfacing/03](../surfacing/03-how-a-loft-fills-between-profiles.md)).
- A profile that was two pieces would not loft to one of three pieces.
- Some solid lofts were refused while the same loft as a surface succeeded.
- Selecting a just-made curve for a join or a loft sometimes failed until a
  rebuild.

## See also

- [surfacing/03 — How a loft fills between two profiles](../surfacing/03-how-a-loft-fills-between-profiles.md) — how close the result came, and how that scaled with the number of guides
- [curves/09 — Curves as loft profiles](../curves/09-curves-as-loft-profiles.md)
- [curves/06 — Composite curves](../curves/06-composite-curve.md) — joined profiles, and what deleting one does to the loft
- [curves/04 — Reload a curve in place](../curves/04-reload-curve-in-place.md) — the loft follows
- [features/07 — Loft cut](07-loft-cut.md) — a lofted cut between sketches
- [files/04 — Export a body to STEP](../files/04-export-a-body-to-step.md)
- [reading/04 — Read the selection](../reading/04-read-the-selection.md) — why selecting mid-sketch is refused
- [curves/08 — Rebuild once, at the end](../curves/08-rebuild-once-at-the-end.md)
- [GOTCHAS §49, §50, §51, §52](../../GOTCHAS.md)
