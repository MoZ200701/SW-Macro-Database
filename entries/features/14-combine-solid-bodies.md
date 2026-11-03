---
id: features-14-combine-solid-bodies
title: Join two solid bodies into one, with Combine Add or a loft that merges
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [IFeatureManager.InsertCombineFeature, IModelDocExtension.SelectByID2, IFeatureManager.InsertProtrusionBlend2, IPartDoc.GetBodies2, IBody2.GetMassProperties]
keywords: [InsertCombineFeature, combine, Combine Add, SWBODYADD, 15903, swBodyOperationType_e, SOLIDBODY, selection mark 2, CombineBodies, merge result, Merge True, multibody, junction, fin on fuselage, one body, GetFaces empty]
answers: "How do I add one solid body to another from code, so the part ends up as one body?"
---

# Join two solid bodies into one

## What this is for

Two solids built separately — a fin lofted on its own, a fuselage knitted into a
solid — that should be one body, with the junction edge where they meet and
the hidden parts of each gone.

## The call: Combine Add

Select every body as `"SOLIDBODY"` by its `IBody2.Name` **at mark 2**, then
`IFeatureManager.InsertCombineFeature(15903, null, null)`. 15903 is
`swBodyOperationType_e.SWBODYADD` (from `swconst`); the two nulls are the
main body and the tool bodies of the subtract and common operations, unused for
add. From the Plane-Builder probe `junction_kit.py` (`call` and
`harness.null_dispatch()` as in [connect/02](../connect/02-attach-from-python.md)
and [GOTCHAS §4](../../GOTCHAS.md)):

```python
def select_bodies(doc: harness.Doc, names: Sequence[str], kind: str, mark: int, append_first: bool = False) -> int:
    if not append_first:
        call(doc.model, "ClearSelection2", True)
    for i, n in enumerate(names):
        if not call(doc.ext, "SelectByID2", n, kind, 0.0, 0.0, 0.0, append_first or i > 0, mark, harness.null_dispatch(), 0):
            raise SolidWorksError(f"body {n!r} would not select as {kind} at mark {mark}")
    return doc.selection_count()


def combine_add(report: harness.Report, doc: harness.Doc, body_names: Sequence[str], name: str) -> Optional[Any]:
    """``InsertCombineFeature(SWBODYADD, null, null)`` with the bodies selected as SOLIDBODY at mark 2 (the help's example)."""
    doc.activate()
    select_bodies(doc, body_names, "SOLIDBODY", 2)
    report.fact(f"combine_selected[{name}]", doc.selection_count())
    before = doc.feature_count()
    report.attempt(f"InsertCombineFeature[{name}] (15903, null, null)",
                   lambda: call(doc.fm, "InsertCombineFeature", SWBODYADD, harness.null_dispatch(), harness.null_dispatch()))
    call(doc.model, "ClearSelection2", True)
    f = last_made(report, doc, before, name)
    if f is None:
        return None
    checked(report, doc, f, name)
    rename(report, doc, f, name)
    return f
```

(One space was added to the log label, after `[{name}]`, so this repo's link
checker does not read it as a link; nothing else was changed.) `last_made` is the feature count and `GetLastFeatureAdded`
([GOTCHAS §66](../../GOTCHAS.md)); `checked` reads `GetErrorCode2` and the
feature's faces. Body names come from `IPartDoc.GetBodies2(0, False)`.

It returned the feature, `GetTypeName2` **`CombineBodies`**, error 0, and the
part went from 2 solid bodies to 1.

**The combine feature's own `GetFaces` is empty** (0 faces) although it made a
body. Read the result from the bodies, not the feature
([GOTCHAS §99](../../GOTCHAS.md)).

## The other way: let the loft merge

The second body can be made merged in the first place. A solid loft
([features/12](12-guided-loft.md)) with its merge argument — the
fifteenth of `InsertProtrusionBlend2` — **`True`** merged straight into the
existing knit fuselage solid: one body, the same volume as the combine to
0.1 mm³. With it `False` the loft is its own body, ready to combine.

```python
args = (False, True, False, 1.0, 0, 0, 1.0, 1.0, False, False, False, 0.0, 0.0, 0, merge, False, True, 0)
report.attempt(f"InsertProtrusionBlend2[{name}]{args}", lambda: call(doc.fm, "InsertProtrusionBlend2", *args))
```

## What came out

A NACA 0010 fin lofted from code, standing on a fuselage made by a Boundary
Surface, two planar caps and a knit into a solid
([surfacing/05](../surfacing/05-boundary-surface-from-code.md),
[surfacing/04](../surfacing/04-cap-a-refused-loft-into-a-solid.md)), its root
section 10 mm inside the skin:

| Route | Bodies | Volume against the oracle (fuselage + fin − overlap, 5,479,047.1 mm³) |
|---|---|---|
| Combine Add | 1 solid, 0 sheet | 5,479,315.1 mm³, +0.005 % |
| Loft with Merge True | 1 solid | 5,479,316.6 mm³ |

The junction was **one closed edge**, 313.29 mm long, lying on the analytic
fuselage to 0.0021 mm and on the analytic fin to 0.0007 mm. The fuselage's
skin face had three loops: its two ends and the hole under the fin. On a second
pair of shapes, combine gave 5,079,642.3 against 5,079,207.1 (+0.009 %), again
one closed junction edge.

The same body also came from a third route, trimming the two sheets against
each other and knitting them with caps
([surfacing/06](../surfacing/06-trim-a-surface-from-code.md)): 5,479,353.6 mm³,
the same junction edge.

## Why it is not obvious

- **Mark 2**, as the help's example has it, and `"SOLIDBODY"` as the type.
- **`CombineBodies` reports no faces**; `CombineBodies` and `GetFaces` are not
  how you check it.
- The same curves built from scratch twice did not give identical volumes:
  5,479,315.146 and 5,479,316.598 mm³ for the same combine. Compare a result
  with a fresh build to a few parts in a million, not to 1e-9
  ([GOTCHAS §100](../../GOTCHAS.md)).

## What it does not do

- Subtract and common were not tried, nor more than two bodies.
- Whether a combined part follows its curves when they are reloaded depends on
  the loft: it did when the fin's loft had leading- and trailing-edge guides, and
  did not reliably without them ([GOTCHAS §100](../../GOTCHAS.md)).

## Evidence

SolidWorks 2026 (revision 34.0.0), Windows Python 3.13 with pywin32 from WSL,
Plane-Builder probes, 2026-11-03:

- `p12_junction-20261103-043842` (run of record): combine and merge routes,
  volumes, junction edge, skin loops, `CombineBodies` with 0 faces.
- `p12_junction-20261103-043955`: the second pair of shapes.
- `p12_junction-20261103-041351` and `-043842`: 5,479,316.598 and
  5,479,315.146 mm³ from the same files.

## See also

- [features/12 — Insert a guided loft](12-guided-loft.md) — the loft, and its merge argument
- [features/15 — Cut a body with a plane](15-cut-a-body-with-a-plane.md) — trimming one body before combining
- [surfacing/06 — Trim a surface from code](../surfacing/06-trim-a-surface-from-code.md) — the sheet route to the same body
- [surfacing/04 — Cap a refused loft into a solid](../surfacing/04-cap-a-refused-loft-into-a-solid.md) — making the solids
- [features/11 — Mirror a body](11-mirror-body.md) — another feature that merges bodies
- [reading/17 — The bodies a feature made](../reading/17-bodies-of-a-feature-and-their-tessellation.md)
- [GOTCHAS §66, §99, §100](../../GOTCHAS.md)
