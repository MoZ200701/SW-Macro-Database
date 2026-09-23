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
| Walk every feature, sketches under features included | [connect/08 — Find a feature by name](entries/connect/08-find-a-feature-by-name.md) |
| Start SolidWorks if it isn't running | [connect/09 — Launch versus attach](entries/connect/09-launch-versus-attach.md) |
| Drive Windows SolidWorks from WSL or a Mac | [connect/10 — Driving from outside Windows](entries/connect/10-driving-from-outside-windows.md) |
| Find out whether an API call really works before depending on it | [connect/11 — Probe an API member on a live session](entries/connect/11-probe-an-api-member-on-a-live-session.md) |

## Make, save and close documents

| I want to… | Entry |
|---|---|
| Make a new part or assembly, in millimetres | [documents/01 — New part from a template](entries/documents/01-new-part-from-template.md) |
| Save to a new path, and close without a prompt | [documents/02 — Save as, and close](entries/documents/02-save-as-and-close.md) |

## Make a part parametric

| I want to… | Entry |
|---|---|
| Add, read and change global variables | [equations/01 — Global variables from code](entries/equations/01-global-variables-from-code.md) |
| Make a dimension follow a global | [equations/02 — Link a dimension to a global](entries/equations/02-link-a-dimension-to-a-global.md) |
| Use inverse trig in a global (`atn`, not `arctan`) | [equations/01 — Global variables from code](entries/equations/01-global-variables-from-code.md) |

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
| Stop each curve reload costing 25 seconds on a big part | [curves/12 — Roll the tree back before reloading](entries/curves/12-roll-the-tree-back-before-reloading.md) |
| Put the rollback bar back, and know it went | [curves/12 — Roll the tree back before reloading](entries/curves/12-roll-the-tree-back-before-reloading.md) |
| Rebuild only what changed, on a part too big to force | [curves/08 — Rebuild once, at the end](entries/curves/08-rebuild-once-at-the-end.md) |
| Read back which curves a composite joins, without leaving the part rolled back | [curves/06 — Composite curves](entries/curves/06-composite-curve.md) |
| Track a feature that might get renamed | [curves/10 — Persistent references](entries/curves/10-persistent-references.md) |
| Know whether I must wrap curves in sketches before lofting | [curves/09 — Curves as loft profiles](entries/curves/09-curves-as-loft-profiles.md) |
| Group features into a folder in the tree | [curves/11 — Feature-tree folders](entries/curves/11-feature-tree-folders.md) |
| Use a composite curve as a loft profile, or change what one joins without losing the loft | [curves/06 — Composite curves](entries/curves/06-composite-curve.md) |
| Know what curve SolidWorks draws through my points, and why it refused my file | [curves/01 — The .sldcrv format](entries/curves/01-sldcrv-file-format.md) |

## Draw a sketch from code

| I want to… | Entry |
|---|---|
| Generate a 2D sketch as a VBA macro | [sketches/01 — A 2D sketch as VBA](entries/sketches/01-2d-sketch-as-vba.md) |
| Generate a 3D sketch as a VBA macro | [sketches/02 — A 3D sketch as VBA](entries/sketches/02-3d-sketch-as-vba.md) |
| Look up the constant for a sketch relation | [sketches/03 — Relation constants](entries/sketches/03-relation-constants.md) |
| Add and set a dimension | [sketches/04 — Dimensions](entries/sketches/04-dimensions.md) |
| Get the units and number formatting right | [sketches/05 — Units and number format](entries/sketches/05-units-and-number-format.md) |
| Know what I will have to do by hand | [sketches/06 — What the sketch API cannot do](entries/sketches/06-what-the-sketch-api-cannot-do.md) |
| Draw a curve from x(t) and y(t), driven by globals | [sketches/07 — Equation driven curve](entries/sketches/07-equation-driven-curve.md) |
| Check that a sketch is fully defined | [sketches/10 — Is the sketch fully defined](entries/sketches/10-is-the-sketch-fully-defined.md) |

## Convert and batch-process files

| I want to… | Entry |
|---|---|
| Turn a folder of STEP files into SolidWorks parts | [files/01 — Batch-convert STEP](entries/files/01-batch-convert-step.md) |
| Write one file per configuration out of a configurable part | [files/02 — Explode configurations](entries/files/02-explode-configurations.md) |
| Re-save a whole library in the current file format | [files/03 — Upgrade a file version](entries/files/03-upgrade-file-version.md) |
| Write a part, or one loft of it, to a STEP file without renaming the document | [files/04 — Export a body to STEP](entries/files/04-export-a-body-to-step.md) |

## Build solid features

| I want to… | Entry |
|---|---|
| Extrude a closed sketch into a boss | [features/01 — Boss extrude](entries/features/01-boss-extrude.md) |
| Cut a sketch through the part | [features/02 — Cut extrude](entries/features/02-cut-extrude.md) |
| Repeat a feature around an axis | [features/03 — Circular pattern](entries/features/03-circular-pattern.md) |
| Find an extrusion's depth or a pattern's count to name or link | [features/04 — Read a feature's dimensions](entries/features/04-read-a-features-dimensions.md) |
| Revolve a profile about a centreline, choosing the axis line | [features/05 — Revolve](entries/features/05-revolve.md) |
| Make a plane square to a sketch line at its end, and know which way its sketch faces | [features/06 — Reference plane normal to a line](entries/features/06-reference-plane-normal-to-a-line.md) |
| Cut a loft between two sketched sections | [features/07 — Loft cut](entries/features/07-loft-cut.md) |
| Make a plane at a distance from another, on the side I choose, driven by a global | [features/08 — Offset reference plane](entries/features/08-offset-reference-plane.md) |
| Sweep a profile along a line with a twist, and choose its hand | [features/09 — Twisted sweep](entries/features/09-twisted-sweep.md) |
| Cut along a path with a twist (a helical tooth space), and pattern it | [features/10 — Swept cut](entries/features/10-swept-cut.md) |
| Mirror a body about a plane into one body (a herringbone) | [features/11 — Mirror a body](entries/features/11-mirror-body.md) |
| Loft through curves along guide curves, with a hand-made loft's settings | [features/12 — Insert a guided loft](entries/features/12-guided-loft.md) |
| Make a reference axis along a sketch line | [features/03 — Circular pattern](entries/features/03-circular-pattern.md) |

## Put parts together

| I want to… | Entry |
|---|---|
| Make an assembly and insert saved parts | [assemblies/01 — New assembly and insert components](entries/assemblies/01-new-assembly-and-insert-components.md) |
| Mate components, and drive a mate from a global | [assemblies/02 — Mates from code](entries/assemblies/02-mates-from-code.md) |
| Check an assembly for interference, and get the volumes | [assemblies/03 — Interference detection](entries/assemblies/03-interference-detection.md) |
| Set a component's rotation, and mate parts on axes at an angle (bevel, crossed) | [assemblies/04 — Mates between non-parallel axes](entries/assemblies/04-mates-between-non-parallel-axes.md) |

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
| Read the dimensions and equations of an existing part | [reading/08 — Dimensions and equations](entries/reading/08-dimensions-and-equations.md) |
| Check that a generated file is the size it should be | [reading/09 — Bounding box as a check](entries/reading/09-bounding-box.md) |
| Check that a feature changed the solid the way I meant | [reading/10 — Mass properties as an oracle](entries/reading/10-mass-properties-as-an-oracle.md) |
| Poll for changes every second without making SolidWorks stutter | [reading/11 — Cheap change detection](entries/reading/11-cheap-change-detection.md) |
| Suppress one body feature without losing everything built on it | [reading/12 — Snapshot suppression before you suppress](entries/reading/12-snapshot-suppression-before-you-suppress.md) |
| Measure the wall between two surfaces without exporting anything | [reading/13 — Measure a wall between two bodies](entries/reading/13-measure-a-wall-between-two-bodies.md) |
| Get one body's volume, where `IMassProperty` will not take bodies | [reading/13 — Measure a wall between two bodies](entries/reading/13-measure-a-wall-between-two-bodies.md) |
| Read a pick without crashing SolidWorks | [reading/04 — Read the selection](entries/reading/04-read-the-selection.md) |

## Build a surface or loft, and get the orientation right

| I want to… | Entry |
|---|---|
| Turn imported curves into a surface | [surfacing/01 — The boundary surface recipe](entries/surfacing/01-boundary-surface-recipe.md) |
| Land geometry the right way up | [surfacing/02 — Axis conventions](entries/surfacing/02-axis-conventions.md) |
| See how a loft's accuracy scaled with the number of guides, in one measured case | [surfacing/03 — How a loft fills between two profiles](entries/surfacing/03-how-a-loft-fills-between-profiles.md) |
| Get a solid out of a loft SolidWorks refuses as a solid | [surfacing/04 — Cap a refused loft into a solid](entries/surfacing/04-cap-a-refused-loft-into-a-solid.md) |
| Put a flat face across the end of a surface, and knit sheets into a solid | [surfacing/04 — Cap a refused loft into a solid](entries/surfacing/04-cap-a-refused-loft-into-a-solid.md) |
| Find out why a solid loft is refused while its surface builds | [surfacing/03 — How a loft fills between two profiles](entries/surfacing/03-how-a-loft-fills-between-profiles.md) |

## Index by language

- **VBScript** — connect/01, connect/07, connect/08, connect/09, curves/02, curves/04, curves/07, curves/08, reading/07
- **Python (pywin32)** — connect/02, connect/05, connect/06, connect/10, connect/11, curves/02, curves/04, curves/05, curves/06, curves/10, curves/11, curves/12, documents/01, documents/02, equations/01, equations/02, sketches/03, sketches/04, sketches/07, sketches/10, features/01 through 12, assemblies/01 through 04, reading/04, reading/05, reading/10, reading/11, reading/12, reading/13, surfacing/04, files/04
- **VBA** — connect/04, sketches/01 through 06, curves/03, files/01, files/02
- **C# (.NET 8)** — connect/03, connect/06, connect/09, reading/01, reading/02, reading/03, reading/06, reading/08, reading/09, files/01, files/02, files/03

## The thing you probably came here for

If you are about to write a macro that drives a feature this collection does
not cover — a fillet, a shell, a sweep — you still want
[connect/01](entries/connect/01-attach-from-vbscript.md) for the attach,
[connect/07](entries/connect/07-find-the-open-document.md) for finding the
part, [connect/08](entries/connect/08-find-a-feature-by-name.md) for finding
what to build on, [sketches/05](entries/sketches/05-units-and-number-format.md)
for the units, [curves/07](entries/curves/07-rename-a-feature.md) for naming
the result, and [reading/07](entries/reading/07-report-feature-type-on-failure.md)
for the error handling. That is the whole scaffold. Only the one call in the
middle is new — and [connect/11](entries/connect/11-probe-an-api-member-on-a-live-session.md)
is how to find out whether that call works before you depend on it, with
[reading/10](entries/reading/10-mass-properties-as-an-oracle.md) as the check.
Extrude, cut, revolve, loft cut, guided loft, reference planes and circular pattern are in
[features/](entries/features/), and what to do when SolidWorks refuses one of
them without saying so is
[surfacing/04](entries/surfacing/04-cap-a-refused-loft-into-a-solid.md).
