---
id: surfacing-05-boundary-surface-from-code
title: Build a Boundary Surface from code, and what can and cannot be changed afterwards
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertNetBlend2, IFeatureManager.SetNetBlendCurveData, IFeatureManager.SetNetBlendDirectionData, IModelDocExtension.SelectByID2, IEntity.Select4, IModelDocExtension.GetLastFeatureAdded, IModelDoc2.GetFeatureCount, IFeature.GetDefinition, IBoundaryBossFeatureData.GetCurvesCount, IBoundaryBossFeatureData.D1Curves, IBoundaryBossFeatureData.D2Curves, IBoundaryBossFeatureData.GetGuideTangencyType, IBoundaryBossFeatureData.AccessSelections, IBoundaryBossFeatureData.ReleaseSelectionAccess, IFeature.ModifyDefinition, IModelDocExtension.DeleteSelection2]
keywords: [boundary surface, InsertNetBlend2, NetBlend, net blend, SetNetBlendCurveData, SetNetBlendDirectionData, Boundary-Surface1, InsertBoundarySurface, selection mark 8193, 8194, 8192, direction 1, direction 2, profiles and guides, returns None, GetLastFeatureAdded, IBoundaryBossFeatureData, D1Curves, D2Curves, add a profile, add a guide, ModifyDefinition caps, capped solid, delete and re-insert, composite profile, mixed piece count, edge as profile, tangency to face, curvature to face, end condition ignored, swBoundaryBossTangencyType_e, influence 32, periodic face, no seam edge, follows reload]
answers: "How do I make a Boundary Surface over imported curves from code, and can I change its curves afterwards?"
---

# Build a Boundary Surface from code

## What this is for

A Boundary Surface over imported curves — sections in one direction, guides in
the other — is the usual way to skin a fuselage or any closed body from
exported points. [surfacing/01](01-boundary-surface-recipe.md) used to say this
step could not be automated and had to be built by hand once. On SolidWorks
2026 it can be built from code, it follows its curves when they are reloaded,
and it takes composite curves and edges as profiles. What cannot be done from
code is change *which* curves an existing one uses.

## The call

There is no `InsertBoundarySurface`: `GetIDsOfNames` refused it on both
`IFeatureManager` and `IModelDoc2`. In the API the Boundary Surface is a **net
blend**, `IFeatureManager.InsertNetBlend2`, found through the 2026 API help's
"Insert Boundary Surface Feature Example (VBA)"
([connect/12](../connect/12-read-the-api-help-offline.md)). Three steps:

1. **Select every curve at an indexed mark**: curve `i` of direction `d` at
   `8192·(i+1) + d`, with `d` 1 for profiles and 2 for guides — the help's
   "8193, 16385, 24577" and "8194, 16386, 24578". Imported curves and
   composites by name as `"REFERENCECURVES"`; an edge, which has no name, by
   `IEntity.Select4` with a `SelectData` carrying the mark.
2. **Set the data per curve and per direction** on `IFeatureManager`:
   `SetNetBlendCurveData(dir, index, tangency, draft, length, applyToAll)` for
   every curve (`dir` 0 or 1 here, not 1 or 2), and
   `SetNetBlendDirectionData(dir, influence, trim, closed, unused)` for each
   direction. Both return `None`.
3. **`InsertNetBlend2`** with type 2 (surface), the two counts and eighteen
   more arguments. **It returns `None` even when it builds**; find the feature
   by `IModelDoc2.GetFeatureCount` either side and
   `IModelDocExtension.GetLastFeatureAdded` ([GOTCHAS §66](../../GOTCHAS.md)).

From the Plane-Builder probe `p9_creases.py` (P9 and P10, 2026-11-03), which
takes curve names or edge objects in either direction:

```python
# InsertNetBlend2 arguments after the counts, as P10a ran them (p10_boundary.NETBLEND_REST).
NETBLEND_REST = (False, 0.0001, False, True, True, True, False, -1.0, -1.0, False, -1, False, False,
                 -1.0, False, -1.0, True, False)
INFLUENCE_GLOBAL = 32
TANGENCY_NONE = 0


def select_data(doc: harness.Doc, mark: int) -> Any:
    sd = call(call(doc.model, "SelectionManager"), "CreateSelectData")
    sd.Mark = mark
    return sd


def insert_netblend(report: harness.Report, doc: harness.Doc, dir1: Sequence[Any], dir2: Sequence[Any],
                    label: str, closed2: bool = False) -> Optional[Any]:
    """A Boundary Surface (``InsertNetBlend2`` Type 2) as P10a made it, but entries may be edges.

    ``dir1``/``dir2``: curve feature names (selected as REFERENCECURVES) or edge objects
    (``IEntity.Select4`` with a SelectData at the mark). Marks ``8192·(i+1) + dir``.
    Returns the feature made (whatever its error) or None.
    """
    doc.activate()
    call(doc.model, "ClearSelection2", True)
    ext = doc.ext
    picks = [(e, 8192 * (i + 1) + 1) for i, e in enumerate(dir1)] + [(e, 8192 * (j + 1) + 2) for j, e in enumerate(dir2)]
    for k, (e, mark) in enumerate(picks):
        if isinstance(e, str):
            ok = call(ext, "SelectByID2", e, "REFERENCECURVES", 0.0, 0.0, 0.0, True, mark, harness.null_dispatch(), 0)
        else:
            ok = call(e, "Select4", True, select_data(doc, mark))
        if not ok:
            report.fact(f"select[{label}]", f"pick {k} ({e if isinstance(e, str) else 'edge'}) at mark {mark} refused")
            call(doc.model, "ClearSelection2", True)
            return None
    report.fact(f"selected[{label}]", doc.selection_count())
    fm = doc.fm
    for i in range(len(dir1)):
        call(fm, "SetNetBlendCurveData", 0, i, TANGENCY_NONE, 0.0, 1.0, True)
    for j in range(len(dir2)):
        call(fm, "SetNetBlendCurveData", 1, j, TANGENCY_NONE, 0.0, 1.0, True)
    call(fm, "SetNetBlendDirectionData", 0, INFLUENCE_GLOBAL, 0, False, False)
    call(fm, "SetNetBlendDirectionData", 1, INFLUENCE_GLOBAL, 0, closed2, False)
    before = doc.feature_count()
    t0 = time.monotonic()
    ok, made = report.attempt(f"InsertNetBlend2[{label}]",
                              lambda: call(fm, "InsertNetBlend2", 2, len(dir1), len(dir2), *NETBLEND_REST))
    report.fact(f"netblend_seconds[{label}]", round(time.monotonic() - t0, 2))
    call(doc.model, "ClearSelection2", True)
    added, last = doc.made_since(before)
    report.fact(f"features_added[{label}]", added)
    if added != 1 or last is None:
        return None
    return last
```

`call` is the late-bound helper of [connect/02](../connect/02-attach-from-python.md)
([GOTCHAS §55](../../GOTCHAS.md)); `harness.null_dispatch()` is
`VARIANT(VT_DISPATCH, None)` ([GOTCHAS §4](../../GOTCHAS.md)). The harness
methods used, copied from the probe's `harness.py`:

```python
def feature_count(self) -> int:
    return int(call(self.model, "GetFeatureCount"))

def last_feature(self) -> Any:
    return call(self.ext, "GetLastFeatureAdded")

def made_since(self, before: int) -> Tuple[int, Any]:
    """How many features appeared since ``before`` was counted, and the last one (GOTCHAS §66)."""
    added = self.feature_count() - before
    return added, (self.last_feature() if added > 0 else None)

def selection_count(self) -> int:
    return int(call(call(self.model, "SelectionManager"), "GetSelectedObjectCount2", -1))
```

### The eighteen arguments after the counts

The probe passed the help's surface example with `CreateSolid` false, and
labelled them in its record (`p10_boundary.py`) as: `HasCenterline` False,
`TessTolFactor` 0.0001, `WantsSolid` False, `MergeBody` True, `FeatureScope`
True, `AutoSelect` True, `Thin` False, `Thickness1` −1, `Thickness2` −1,
`ReverseThickness` False, `ThinSolidType` −1, `UseSharedThickness` False,
`CapEnds` False, `EndThickness` −1, `AutoFillet` False, `FilletRadius` −1,
`ForceNonRational` True, `CreateSolid` False. Only the counts, the type and the
selection were varied. None of the others was changed, so what each does is the
help's word, not something observed here.

### The data calls' arguments

| Argument | Values used | Seen to matter? |
|---|---|---|
| `SetNetBlendCurveData` tangency (`swBoundaryBossTangencyType_e`) | 0 none; 3 tangency to face, 4 curvature to face on an edge profile | Stored, but **no effect on the shape** was seen ([GOTCHAS §88](../../GOTCHAS.md), below) |
| its draft, length, applyToAll | 0.0, 1.0, True (and False) | Not varied otherwise |
| `SetNetBlendDirectionData` influence (`swBoundaryBossCurveInfluenceType_e`) | 32 global; 0 to next curve on direction 2 | **Yes**: direction-2 influence 0 changed the surface (0.45 mm from the analytic body at one place, against 0.17 mm with 32) |
| its trim, closed | 0, False | Not varied |

## What came out

Six closed sections (48 points each, the first row repeated as the last) and
four guides through the sections' own rows:

| | Measured |
|---|---|
| `InsertNetBlend2` | returned `None` in 0.28 s; exactly one feature added; `GetLastFeatureAdded` `Boundary-Surface1`, `GetTypeName2` **`NetBlend`** |
| Rename | `PB_Fuselage`, read back, found again by `FeatureByName` |
| Errors | `GetErrorCode2` 0, warning False; `GetWhatsWrong` empty; `EditRebuild3` True |
| Bodies | 0 solid, **1 sheet**, **1 face** |
| Edges | 2: the end loops. **No seam edge** where every closed section starts and ends: the face is periodic round the section |
| Area | 188,544.53 mm² against the analytic 188,545.41 (−0.0005 %) |
| Deviation, 41 × 96 grid over the whole length | max 0.0239 mm, 100 % within ±0.05 mm |

`IEdge.GetTwoAdjacentFaces2` on one of the end loops returned two items, the
second `None`. Across the line where the closed sections start (no edge there)
the normal angle between points either side halved with the step — 0.2182°,
0.1087°, 0.0543°, 0.0271° at 0.4, 0.2, 0.1, 0.05 mm — which is the surface's
own curvature, not a kink. Why the seam is smooth is
[curves/01](../curves/01-sldcrv-file-format.md#closed-curves): a closed curve
file is drawn as a periodic spline.

### It follows reloaded curves

All ten curves reloaded in place from a different shape, with the tree rolled
back first ([curves/04](../curves/04-reload-curve-in-place.md),
[curves/12](../curves/12-roll-the-tree-back-before-reloading.md)): 3.11 s, one
`EditRebuild3` (True, 0.10 s), the surface's `GetErrorCode2` 0, still one sheet
and one face, and the same feature, never re-picked, then lay on the new shape
to 0.017 mm and 20.6 mm from where it had been.

### Composite profiles, and profiles of different piece counts

- **Composite curves are accepted as profiles**, selected as
  `"REFERENCECURVES"` at the same indexed marks. Six three-piece
  `CompositeCurve` profiles with six guides gave one `NetBlend`, error 0, in
  0.35 s, with **one face per piece** (3), split along the joints. A guide lying
  exactly on a composite's corner was accepted; leaving it out changed the
  surface by under 0.01 mm.
- **Profiles need not have the same number of pieces.** A one-segment closed
  curve first, three-segment composites behind it: built, error 0, 3 faces. So
  did a surface's single closed end edge first (selected by `IEntity.Select4`
  with `SelectData.Mark` 8193). A loft refuses that mismatch
  ([curves/06](../curves/06-composite-curve.md), [GOTCHAS §49](../../GOTCHAS.md));
  the same-piece-count rule is a loft rule.
- **A composite's corner is not the data's corner.** The surface followed each
  composite to 0.05°, but the composite itself turns where its pieces' natural
  ends point: an exported 23.9° chine came out 30.2°. See
  [curves/06](../curves/06-composite-curve.md#a-composites-corner-is-set-by-its-pieces-ends).
- **Nearly tangent joints are merged.** With points packed so tight at a cut
  that the composite's joint turned only 0.30°, the surface merged the faces
  across it (2 faces, not 3), and split the merged loop at a place of its own
  choosing, with 0.000° across that edge. The feature data's
  `MergeTangentFaces` reads True.

### An edge as the first profile

A surface revolve's circular end edge, selected by `IEntity.Select4` at mark
8193, with five closed sections and four guides behind it: `NetBlend`, error 0,
one face. The edge selected as type 51 (`swSelREFEDGES`); a solid revolve's
edge as type 1 (`swSelEDGES`); either built. Plain marks (every profile at 1,
every guide at 2, as the help's edge example has them) also built, and measured
the same across the joint as the indexed marks.

Refused, returning `None` with no feature: the same with **no guides**
(`InsertNetBlend2(2, 6, 0, …)`), and the revolve's **face** selected in place
of its edge (`SelectByID2("", "FACE", point, …, 8193)`, selected as type 2).

## Why it is not obvious

- **The name.** Nothing called "boundary" exists in `IFeatureManager`. The
  feature's type name, `NetBlend`, is the only hint.
- **The marks.** Profiles and guides are told apart by mark alone, and the
  indexed form encodes each curve's position as well. Both the indexed marks and
  plain 1 and 2 built here.
- **`None` from the insert is not failure.** It is all `InsertNetBlend2`
  returns. A refused surface is told only by the feature count not moving.
- **The end condition is stored and was not applied.** See below and
  [GOTCHAS §88](../../GOTCHAS.md).
- **Never commit its feature data.** `IFeature.ModifyDefinition` with the
  Boundary Surface's own data turns it into a capped solid, even with nothing
  changed ([GOTCHAS §87](../../GOTCHAS.md)).

## Reading it back

`IFeature.GetDefinition` on a `NetBlend` **returns an object**,
`IBoundaryBossFeatureData` — unlike a surface loft, which returns `Nothing`
([GOTCHAS §68](../../GOTCHAS.md)). `GetCurvesCount(0)` read 6 and
`GetCurvesCount(1)` 4; `MergeTangentFaces` True; `TrimByD1` False.

**Read the curve data inside `AccessSelections`.** Outside it,
`GetGuideTangencyType(0, 0)` and `GetTangentLength(0, 0)` read 0 and
`D1CurveInfluence` 0 on every feature, whatever was set. Inside
`AccessSelections(doc, typed null)` the same calls read what the setters
stored: tangency 4 (or 3, or 0), length 1.0, `D1CurveInfluence` and
`D2CurveInfluence` 32. `GetTangentInfluence(0, 0)` — the **per-curve**
influence, not the per-direction one — read −1.0 (unset). `AccessSelections`
rolls the tree back, and `ReleaseSelectionAccess()` must be **called**, with
parentheses ([GOTCHAS §55, §59](../../GOTCHAS.md)); after that the tree was
forward again, with the geometry unchanged. From `p11_nose.py`:

```python
def read_in_access(doc: harness.Doc, feature: Any) -> Dict[str, Any]:
    """The end condition read inside AccessSelections (outside it every getter answered 0), then released."""
    data = call(feature, "GetDefinition")
    out: Dict[str, Any] = {"access": repr(call(data, "AccessSelections", doc.model, harness.null_dispatch()))}
    try:
        out["tangency(0,0)"] = call(data, "GetGuideTangencyType", 0, 0)
        out["tangency(0,1)"] = call(data, "GetGuideTangencyType", 0, 1)
        out["length(0,0)"] = call(data, "GetTangentLength", 0, 0)
        out["D1CurveInfluence"] = data.D1CurveInfluence
    finally:
        harness.invoke_method(data, "ReleaseSelectionAccess")   # a Sub: must be invoked (GOTCHAS §55, §59)
    last = call(doc.model, "FeatureByPositionReverse", 0)
    out["rolled_back_after_release"] = bool(call(last, "IsRolledBack")) if last is not None else None
    if out["rolled_back_after_release"]:
        doc.roll_forward()
    return out
```

`harness.invoke_method` invokes the member's dispid with `DISPATCH_METHOD`
([GOTCHAS §39](../../GOTCHAS.md)).

## Adding or removing a curve: delete and insert again

**The feature data could not be edited from Python.** `D1Curves` and
`D2Curves` are documented as get and set. Inside `AccessSelections`, a new
array was put for four edits — add a guide, add a profile, remove a guide,
remove a profile — with the elements as `IFeature` (what the property reads
back as), as `IReferenceCurve` (`GetSpecificFeature2`) and as `IEdge`
(`IReferenceCurve.GetSegments`), each passed three ways: a
`VARIANT(VT_ARRAY | VT_DISPATCH)` set by attribute, a tuple of dispatch
objects, and the VARIANT through `Invoke(DISPATCH_PROPERTYPUT)`. **All 36 puts
raised nothing and read back unchanged**, `GetCurvesCount` stayed [6, 4], and
the `ModifyDefinition` after each returned True and capped the surface
([GOTCHAS §87](../../GOTCHAS.md)). **Not tried**: early-bound access, a VBA
macro, and arrays of objects taken from `ISelectionMgr`. So this is "not from
late-bound Python", not yet "not at all"
([GOTCHAS §14](../../GOTCHAS.md)).

A spare curve already in the tree above the surface cannot be switched in
later either. It has to be in the surface when the surface is made.

**What works is to delete the surface and insert it again**, with the longer or
shorter lists, by the same call: select the feature, `DeleteSelection2(0)`,
`InsertNetBlend2`, rename back. About 0.4 s, error 0, one open sheet. From
`p10c_modify.py`:

```python
def fresh_base(report: harness.Report, doc: harness.Doc, profiles: Sequence[str], guides: Sequence[str],
               tag: str) -> Dict[str, Any]:
    """Delete PB_Fuselage if there is one; insert it again over ``profiles``/``guides``; check it is an open sheet."""
    doc.activate()
    if any(n == SURFACE for n, _ in doc.features()):
        call(doc.model, "ClearSelection2", True)
        call(doc.feature(SURFACE), "Select2", False, 0)
        call(doc.ext, "DeleteSelection2", 0)
    feat = insert_netblend(report, doc, profiles, guides, tag)
    if feat is None:
        raise SolidWorksError(f"{tag}: the base surface was not made")
    doc.rename(feat, SURFACE)
    doc.rebuild()
    bc = body_counts(doc)
    if not (bc["faces"] == 1 and bc["sheet_bodies"] == 1 and bc["solid_bodies"] == 0):
        raise SolidWorksError(f"{tag}: the fresh base is not one open sheet: {bc}")
    return bc
```

| Re-inserted with | Curve counts | Deviation on the new guide's line | On a ring at the new station | Grid max |
|---|---|---|---|---|
| (the base) | [6, 4] | 0.021275 mm | 0.021275 mm | 0.023919 mm |
| a guide where the base sagged most | [6, 5] | **0.000022 mm** | 0.039534 mm | 0.043206 mm |
| that guide and a station between S1 and S2 | [7, 5] | 0.000021 mm | **0.003504 mm** | 0.031521 mm |

The new guide pulled its own line onto the shape and pushed error elsewhere
(the ring went from 0.021 to 0.040 mm until a station was added there): measure
again after every addition.

The cost: anything built on the surface — trims, knits, caps — goes with it
when it is deleted, as for a loft ([GOTCHAS §67](../../GOTCHAS.md)).

## What it does not do

- **Tangency or curvature to face on an edge profile.** Observed, cause not
  established. With a revolve's end edge as profile 0 and tangency 3 or 4, the
  values were stored (read back inside `AccessSelections`) and the geometry was
  identical to six digits to tangency 0: 2.1029° between the two surfaces' normals
  across the joint. Unchanged with `applyToAll` True and False, with indexed and
  plain marks, with the edge picked by `Select4` and by
  `SelectByID2("", "EDGE", point, …)`, with five setter call styles
  (plain late binding, `DISPATCH_METHOD`, `VT_I2`-typed arguments, the help's
  interleaved order, setters before the selection), on a sheet edge and on a
  solid's edge, and with `SetGuideTangencyType`, `SetTangentInfluence(0, 0,
  1.0)` and `SetTangentLength(0, 0, 1.0)` written into the feature data
  afterwards (which also capped it, §87). Denser guide rows near the joint cut
  the kink from 2.10° to 0.87°, the same for all three tangencies, so on this
  evidence the guides set the joint. **Open:** open the PropertyManager by hand
  on one of these parts and see whether it shows the stored condition and
  whether pressing OK applies it. Until then, make the guides carry the joint
  slope, or treat the joint as G0. [GOTCHAS §88](../../GOTCHAS.md).
- `SetNetBlendDirectionData`'s trim and closed flags, thin and solid options,
  draft, and every `InsertNetBlend2` argument beyond the counts were not varied.
- Solid output (`CreateSolid` True) was not tried.
- Only fuselage-like closed sections with 4–6 guides were built.

## Evidence

SolidWorks 2026 (revision 34.0.0), Windows Python 3.13 with pywin32, driven
from WSL, the Plane-Builder probes of 2026-11-03. Result records by filename
(`swbridge/probes/results/` on the Plane-Builder `probes` branch):

- `p10_boundary-20261103-032408`: no `InsertBoundarySurface` on either
  interface; the build, its return, count, name, type, errors, bodies, edges,
  area and grid above; `GetDefinition` an object with `GetCurvesCount` 6 and 4,
  `D1CurveInfluence` 0 outside access; the reload of all ten curves and the
  surface on the new shape to 0.017 mm.
- `p9_creases-20261103-040239`: six three-piece composites with six guides,
  3 faces, error 0, 0.35 s; without the corner guides the same to under
  0.01 mm; a one-segment closed profile first, built.
- `p9_creases-20261103-041135`: a surface's single end edge first, at mark
  8193, built (error 0, 3 faces).
- `p9_creases-20261103-040649` and the inspection in `p9_creases-20261103-043213`:
  faces merged across 0.30° joints.
- `p10c_modify-20261103-042939`: the 36 ignored puts and the delete-and-insert
  numbers. `p10c_modify-20261103-042859`, `-042906`, `-042914`: what caps the
  surface ([GOTCHAS §87](../../GOTCHAS.md)).
- `p11_nose-20261103-035854`: an edge as profile 0, built; the end condition
  reading 0 outside `AccessSelections`.
- `p11_nose-20261103-042911` and `-042950`: values stored inside access;
  plain marks; direction-2 influence 0 changed the shape; the face refused;
  the edge by `SelectByID2`. `p11_nose-20261103-043530`: a solid's edge, the
  same. `p11_nose-20261103-040039`, `-040333`, `-040531`, `-040739`: setter
  styles, edge by point, no guides refused, denser guides.

## See also

- [surfacing/01 — The boundary surface recipe](01-boundary-surface-recipe.md) — the hand-built route this replaces
- [curves/01 — The .sldcrv format](../curves/01-sldcrv-file-format.md#closed-curves) — why a closed section's seam is smooth
- [curves/04 — Reload a curve in place](../curves/04-reload-curve-in-place.md) — the surface follows
- [curves/06 — Composite curves](../curves/06-composite-curve.md) — composite profiles, and their corners
- [curves/12 — Roll the tree back before reloading](../curves/12-roll-the-tree-back-before-reloading.md)
- [features/12 — Insert a guided loft](../features/12-guided-loft.md) — the loft, and where its rules differ
- [surfacing/04 — Cap a refused loft into a solid](04-cap-a-refused-loft-into-a-solid.md) — knitting the sheet into a solid
- [GOTCHAS §14, §49, §55, §59, §66, §67, §68, §87, §88, §89](../../GOTCHAS.md)
