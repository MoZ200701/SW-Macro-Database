---
id: reading-11-cheap-change-detection
title: Notice that the part changed without making SolidWorks stutter
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [IModelDoc2.GetFeatureCount, IModelDoc2.GetUpdateStamp, IFeatureManager.GetFeatures]
keywords: [polling, poll, change detection, GetUpdateStamp, GetFeatureCount, GetFeatures, stutter, lag, orbit, feature tree walk, UI thread, slow, every second, watch the part]
answers: "My tool polls SolidWorks every second and orbiting the model stutters. What can I ask that is cheap and still tells me the part changed?"
---

# Notice that the part changed without making SolidWorks stutter

## What this is for

An outside tool that shows what is in the part has to poll: there are no
events worth wiring from out of process ([reading/06](06-live-sketch-monitor.md)).
The obvious poll, walking the feature tree, is expensive in a way that does not
show in your own process. **Every API call is served on SolidWorks' own thread,
the one that draws the view, between frames.** A poll that takes 650 ms of that
thread every second makes rotating the model stutter for as long as your tool is
open.

## The call

Ask two numbers instead. From
[`code/python/swcom.py`](../../code/python/swcom.py):

```python
def change_key(self) -> Tuple[int, int]:
    """Two numbers that move when the active part does: its feature count and
    its update stamp.

    Asked every second, so it has to be cheap: two calls, under a
    millisecond. Walking the tree instead took 650 ms of SolidWorks' own
    thread on a part of 120 features — every call is served there, between
    frames — and orbiting the model stuttered while the app was open. The
    stamp stands still while the model is only looked at.
    """
    doc = self._active()
    return int(call(doc, "GetFeatureCount")), int(call(doc, "GetUpdateStamp"))
```

Both are on **IModelDoc2**, and neither takes an argument, so under late
binding both are reached by attribute access ([GOTCHAS §5](../../GOTCHAS.md)).

The key the tool compares each second adds which session and which document
(from the Airfoil Converter's `gui.py`):

```python
def _read_key(session) -> tuple:
    """The cheapest question worth asking every second."""
    document = session.active_document()
    return (
        session.pid,
        document.title if document else "",
        document.path if document else "",
        session.change_key() if document and document.is_part else (0, 0),
    )
```

`active_document()` is `ISldWorks.ActiveDoc` described by `GetTitle`,
`GetPathName` and `GetType` ([connect/07](../connect/07-find-the-open-document.md));
`session.pid` is the process id read from the Running Object Table moniker
([connect/02](../connect/02-attach-from-python.md)). A different session, a
different active document, the same document saved to a new path, or a changed
feature count or update stamp: any of them means read properly.

## The full read, when it is needed

Only when the key moves, when the tool's window gets the focus back, or every
fifth tick while it has the focus, does the tool list the features. It lists
them in one call:

```python
def features(self) -> List[FeatureInfo]:
    """Every feature in the active document, subfeatures included.

    Listed in one call, then asked two questions each; walking the tree
    asks four, and takes nearly twice as long.
    """
    doc = self._active()
    try:
        listed = call(call(doc, "FeatureManager"), "GetFeatures", False)
    except Exception:  # noqa: BLE001 - the walk below always works
        listed = None
    if not listed:
        return self._walk(call(doc, "FirstFeature"))
    out: List[FeatureInfo] = []
    for feature in listed:
        try:
            type_name = str(call(feature, "GetTypeName2"))
        except Exception:  # noqa: BLE001 - a feature that will not describe itself
            type_name = "?"
        out.append(FeatureInfo(name=str(call(feature, "Name")), type_name=type_name))
    return out
```

`GetFeatures(topOnly)` is on **IFeatureManager**; `False` asks for
sub-features too. The walk it falls back to is
[connect/08](../connect/08-find-a-feature-by-name.md)'s. Per feature the list
costs `Name` and `GetTypeName2`; the walk also costs `GetFirstSubFeature` and
`GetNextFeature`.

The timing in the tool: the key every 1000 ms, the full read at most every
fifth tick and only while the tool's window has the focus, and during an
interactive pick only the selection read, every 200 ms.

## Why it is not obvious

- **The cost lands in SolidWorks, not in your tool.** Your process sees a call
  that returns in a fraction of a second. The user sees the model stutter while
  they orbit it, and nothing connects the two.
- **The update stamp does not move when the model is only looked at.** While
  the user orbited the model it stood still, so the poll did not turn into a
  full read every second.
- **A change may move neither number.** Whether a rename does was not tested.
  The tool also re-reads when its window gets the focus back, which is when a
  rename made in SolidWorks is freshest.

## What it does not do

- Which operations advance `GetUpdateStamp` was not enumerated. Rebuilds and
  added features were what the tool relied on; a rename, a suppression, and a
  change to a curve's points without a rebuild were not tested one by one.
- `GetFeatures(False)` and the walk were compared for speed ("nearly twice as
  long"), but whether they list exactly the same features in the same order
  was not checked; the tool only uses the names and types as a set.
- The ordering of `GetFeatures` relative to the tree, and whether it includes a
  folder's `___EndTag___` ([curves/11](../curves/11-feature-tree-folders.md)),
  is not recorded. Use the top-level walk for folder structure.

## Evidence

SolidWorks 2026, Python over pywin32, the Airfoil Converter (commit "Stop the
once-a-second poll from stalling SolidWorks' view", 2026-09-16):

- On a part of 120 features, the once-a-second tree walk held SolidWorks'
  drawing thread for about 650 ms of every second, and a full read every fifth
  second took about another second. Orbiting the model stuttered while the
  tool was open.
- `GetFeatureCount` and `GetUpdateStamp` together took under a millisecond,
  and the stutter went. The stamp stood still while the model was only looked
  at.
- Listing the features with `GetFeatures(False)` took about half as long as
  the walk.

## See also

- [reading/06 — Live sketch monitor](06-live-sketch-monitor.md) — the polling pattern
- [reading/04 — Read the selection](04-read-the-selection.md) — the other thing a tool polls
- [connect/06 — One apartment thread](../connect/06-one-apartment-thread.md) — where the calls are made from
- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md) — the walk this replaces in the poll
- [GOTCHAS §54](../../GOTCHAS.md)
