# Index by task

What you are trying to do, and where the answer is. An agent wanting the whole
set in one read should use [manifest.json](manifest.json) instead.

## Get a connection

| I want to… | Entry |
|---|---|
| Attach to an open SolidWorks from a `.vbs` file | [connect/01 — Attach from VBScript](entries/connect/01-attach-from-vbscript.md) |
| Attach from Python | [connect/02 — Attach from Python](entries/connect/02-attach-from-python.md) |
| Attach from C# on modern .NET | [connect/03 — Attach from C#](entries/connect/03-attach-from-csharp.md) |
| Attach from a VBA macro inside SolidWorks | [connect/04 — Attach from VBA](entries/connect/04-attach-from-vba.md) |
| Pick between several installed versions | [connect/05 — Choosing among versions](entries/connect/05-choosing-among-versions.md) |
| Stop COM blowing up under threads | [connect/06 — One apartment thread](entries/connect/06-one-apartment-thread.md) |
| Find the part I want among the open documents | [connect/07 — Find the open document](entries/connect/07-find-the-open-document.md) |
| Find a feature by its name | [connect/08 — Find a feature by name](entries/connect/08-find-a-feature-by-name.md) |
| Start SolidWorks if it isn't running | [connect/09 — Launch versus attach](entries/connect/09-launch-versus-attach.md) |
| Drive Windows SolidWorks from WSL or a Mac | [connect/10 — Driving from outside Windows](entries/connect/10-driving-from-outside-windows.md) |

## Get geometry in

| I want to… | Entry |
|---|---|
| Write a point file SolidWorks will read | [curves/01 — The .sldcrv format](entries/curves/01-sldcrv-file-format.md) |
| Import a curve file as a feature | [curves/02 — Insert a curve from a file](entries/curves/02-insert-curve-from-file.md) |
| Create a curve without a file at all | [curves/03 — Stream curve points](entries/curves/03-stream-curve-points.md) |
| Update an existing curve without breaking what uses it | [curves/04 — Reload a curve in place](entries/curves/04-reload-curve-in-place.md) |
| Read a curve's points back out | [curves/05 — Read curve points back](entries/curves/05-read-curve-points-back.md) |
| Join several curves into one selectable curve | [curves/06 — Composite curves](entries/curves/06-composite-curve.md) |
| Rename a feature I just made | [curves/07 — Rename a feature](entries/curves/07-rename-a-feature.md) |
| Avoid spurious errors while updating many curves | [curves/08 — Rebuild once, at the end](entries/curves/08-rebuild-once-at-the-end.md) |
| Track a feature that might get renamed | [curves/10 — Persistent references](entries/curves/10-persistent-references.md) |
| Know whether I must wrap curves in sketches before lofting | [curves/09 — Curves as loft profiles](entries/curves/09-curves-as-loft-profiles.md) |

## Draw a sketch from code

| I want to… | Entry |
|---|---|
| Generate a 2D sketch as a VBA macro | [sketches/01 — A 2D sketch as VBA](entries/sketches/01-2d-sketch-as-vba.md) |
| Generate a 3D sketch as a VBA macro | [sketches/02 — A 3D sketch as VBA](entries/sketches/02-3d-sketch-as-vba.md) |
| Look up the constant for a sketch relation | [sketches/03 — Relation constants](entries/sketches/03-relation-constants.md) |
| Add and set a dimension | [sketches/04 — Dimensions](entries/sketches/04-dimensions.md) |
| Get the units and number formatting right | [sketches/05 — Units and number format](entries/sketches/05-units-and-number-format.md) |
| Know what I will have to do by hand | [sketches/06 — What the sketch API cannot do](entries/sketches/06-what-the-sketch-api-cannot-do.md) |

## Read an existing model

| I want to… | Entry |
|---|---|
| List what a closed file references | [reading/01 — Dependencies of a closed file](entries/reading/01-dependencies-of-a-closed-file.md) |
| Build a full reference tree | [reading/02 — Walk a reference tree](entries/reading/02-walk-a-reference-tree.md) |
| Rename or move a file and fix references | [reading/03 — Repair references after a move](entries/reading/03-repair-references-after-a-move.md) |
| Find out what the user just clicked | [reading/04 — Read the selection](entries/reading/04-read-the-selection.md) |
| Convert sketch coordinates to model coordinates | [reading/05 — Sketch to model transform](entries/reading/05-sketch-to-model-transform.md) |
| Watch the active sketch while the user works | [reading/06 — Live sketch monitor](entries/reading/06-live-sketch-monitor.md) |
| Make a failing call tell me why | [reading/07 — Report the feature type on failure](entries/reading/07-report-feature-type-on-failure.md) |

## Build a surface, and get the orientation right

| I want to… | Entry |
|---|---|
| Turn imported curves into a surface | [surfacing/01 — The boundary surface recipe](entries/surfacing/01-boundary-surface-recipe.md) |
| Land geometry the right way up | [surfacing/02 — Axis conventions](entries/surfacing/02-axis-conventions.md) |

## Index by language

- **VBScript** — connect/01, connect/07, connect/08, connect/09, curves/02, curves/04, curves/07, curves/08, reading/07
- **VBA** — connect/04, sketches/01 through 06, curves/03
- **Python (pywin32)** — connect/02, connect/05, connect/06, curves/02, curves/04, curves/05, curves/06, curves/10, reading/04, reading/05
- **C# (.NET 8)** — connect/03, connect/06, connect/09, reading/01, reading/02, reading/03, reading/06

## The thing you probably came here for

If you are about to write a macro that drives a feature this collection does
not cover — an extrude, a revolve, a fillet — you still want
[connect/01](entries/connect/01-attach-from-vbscript.md) for the attach,
[connect/07](entries/connect/07-find-the-open-document.md) for finding the
part, [connect/08](entries/connect/08-find-a-feature-by-name.md) for finding
what to build on, [sketches/05](entries/sketches/05-units-and-number-format.md)
for the units, [curves/07](entries/curves/07-rename-a-feature.md) for naming
the result, and [reading/07](entries/reading/07-report-feature-type-on-failure.md)
for the error handling. That is the whole scaffold. Only the one call in the
middle is new.
