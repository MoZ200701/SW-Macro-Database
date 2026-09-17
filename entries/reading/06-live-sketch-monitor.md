---
id: reading-06-live-sketch-monitor
title: Watch the active sketch while the user works
status: unverified
verified_on: null
language: [csharp, python]
api: [ISldWorks.ActiveDoc, ISketchManager.ActiveSketch, IFeature.ListExternalFileReferences2, IModelDocExtension.ListExternalFileReferences]
keywords: [ActiveSketch, polling, live monitor, external references, in-context, dangling, watch]
answers: "How do I show live information about whatever sketch the user is currently editing?"
---

# Watch the active sketch while the user works

**Status: unverified.** The polling shape is proven — the selection poller in
[reading/04](04-read-the-selection.md) uses it and works — but the external
reference listing has not been run.

## There are no events, so poll

The SolidWorks API has notification interfaces, but wiring them from an
out-of-process client is awkward and fragile. Polling is what actually works
for an external tool.

```
timer every ~500 ms
  -> ISldWorks.ActiveDoc
  -> doc.SketchManager.ActiveSketch
  -> if it changed, do the expensive work
```

Five hundred milliseconds feels instant to a person and costs nothing. The
selection poller runs at roughly 200 ms for the same reason.

## Only do work on a change

`ActiveSketch` returns `Nothing` when no sketch is being edited, which is most
of the time. Compare against what you saw last time and short-circuit:

```csharp
var sketch = await Runner.Run(() =>
{
    var doc = _sw?.IActiveDoc2;
    return doc?.SketchManager?.ActiveSketch;
});

if (SameAsLast(sketch)) return;   // the overwhelmingly common case
```

Identity comparison on a COM object is unreliable. Compare on something stable
instead: the sketch's feature name via `GetFeature().Name`, plus the document
title.

## Reading a sketch's external references

Once the sketch has changed, find its owning feature and ask:

```
IFeature::ListExternalFileReferences2
```

If that proves unreliable for some reference types, the fallback is the
document-level `IModelDocExtension::ListExternalFileReferences`, filtered to the
active sketch's feature name. Less precise, but it degrades to showing slightly
too much rather than to showing nothing.

## Statuses worth distinguishing

| Status | Meaning |
|---|---|
| In context | The reference resolves and is in the same assembly context |
| Out of context | Resolves, but the context is not loaded |
| Dangling | The referenced entity is gone; the reference is broken |
| Broken | The referenced *file* is gone |
| Locked | The reference is deliberately frozen |

These are what make the panel worth looking at. "This sketch references three
files" is not actionable; "one of them is dangling" is.

Use colour: green for in-context, amber for out-of-context, and something
clearly distinct for broken. Do **not** use red if your accent colour is red;
one project here deliberately shifted broken to burnt orange so it could never
be confused with a UI accent.

## Doing this without blocking

Every call above runs on the COM thread. See
[connect/06](../connect/06-one-apartment-thread.md). This matters more here than
anywhere else in this collection: a modal dialog open in SolidWorks blocks the
call for as long as it stays open, and with a 500 ms timer you will queue up
hundreds of them. Skip a tick if the previous one has not returned:

```python
if worker.busy():
    return      # skip this tick rather than queueing another
```

The `busy()` method in the Python worker exists for exactly this.

## An "always on top" toggle earns its keep

The whole point is to look at this panel while working in SolidWorks, which
means SolidWorks has focus and your window does not.

## The acceptance test

Open a part with in-context sketch relations while the tool runs. Edit the
sketch. The panel must list the referenced files, and must agree with what the
FeatureManager shows for that sketch's external references.

## See also

- [reading/04 — Read the selection](04-read-the-selection.md)
- [connect/06 — One apartment thread](../connect/06-one-apartment-thread.md)
- [reading/11 — Cheap change detection](11-cheap-change-detection.md) — what to ask every second so SolidWorks does not stutter
