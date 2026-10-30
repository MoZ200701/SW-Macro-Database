# API ledger

Every SolidWorks API member used anywhere in this collection, what interface it
lives on, and whether it has been observed working. Sorted by interface.

**Verified** = run against a real SolidWorks and the result observed.
**Unverified** = well-formed against the documented surface, never executed.

"SW 2026 (34.0.0)" rows come from one probe run (20260914-180426, 33 of 33
probes passed) whose code and results are in
[`code/python/gear_generator/probe/`](code/python/gear_generator/probe/harness.py).
They were written from the probe's `--ledger` output and then checked against
the probe code: a member a probe listed but never invoked is not marked
verified. `FixComponent` was not invoked in that run; it was verified in the
rerun 20260914-183429 (33 of 33 passed), whose results sit beside the first. See [connect/11](entries/connect/11-probe-an-api-member-on-a-live-session.md).

Rows marked "dev runs 2026-09-15" come from three later development runs of
the same probe on SolidWorks 2026 (revision 34.0.0): 20260915-015506,
20260915-020933 and 20260915-022314, the last passing all 41 probes it ran.
Their excerpts are in
[`probe-log-excerpts-20260915.txt`](code/python/gear_generator/probe/results/probe-log-excerpts-20260915.txt).
As before, a member a probe listed but never reached is left unverified.

Rows marked "helical runs 2026-09-15" come from two full runs of the same
probe on SolidWorks 2026 (revision 34.0.0): 20260915-002812, 43 of 43 probes
passed, after helical and herringbone gears were added, and 20260915-023929,
54 of 54, after internal, crossed and bevel gears. Their reports and JSON sit
beside the earlier results, with the user's documents and profile path
replaced by placeholders, and `latest.json` is now 20260915-023929.

Rows marked "Airfoil Converter 2026-09-16" come from that tool's lofting and
polling work on SolidWorks 2026, from Python over pywin32: a loft built from
code reproduced the user's hand-made loft, its lofts were written to STEP and
measured, and a stutter was traced to the poll. The evidence is in
[features/12](entries/features/12-guided-loft.md),
[surfacing/03](entries/surfacing/03-how-a-loft-fills-between-profiles.md),
[files/04](entries/files/04-export-a-body-to-step.md) and
[reading/11](entries/reading/11-cheap-change-detection.md). A member that is
in that code but whose result nobody recorded stays unverified; its tests run
against a fake SolidWorks only.

Rows marked "Airfoil Converter 2026-09-22" come from a day of experiments on
SolidWorks 2026 SP0.0 (revision 34.0.0), Windows, Python 3.13 with pywin32,
driving a save-as copy of a **real 397-feature part** (six lofts through about
33 guide curves each, plus splits, inserts and mirrors). Fifty-two numbered
experiments, each with its log; the findings are written up in
[curves/12](entries/curves/12-roll-the-tree-back-before-reloading.md),
[surfacing/04](entries/surfacing/04-cap-a-refused-loft-into-a-solid.md),
[reading/12](entries/reading/12-snapshot-suppression-before-you-suppress.md),
[reading/13](entries/reading/13-measure-a-wall-between-two-bodies.md) and the
updates to [curves/06](entries/curves/06-composite-curve.md),
[curves/08](entries/curves/08-rebuild-once-at-the-end.md) and
[surfacing/03](entries/surfacing/03-how-a-loft-fills-between-profiles.md). They
were carried into the Airfoil Converter's v1.6 and v1.7 releases (commits
f63df5d, 0db0d66, 8566952, a2c1914, 1bdec01, 7caf687, 8555d5f). A member that
was tried and refused is recorded as refused rather than left out.

Rows marked "SW 2024 (32.5.0), 2026-10-01" come from one session on SolidWorks 2024 SP5 (revision
32.5.0), Windows Python 3.13 with pywin32 launched from WSL, attached through
the ROT moniker, reading a user's open parts and removing one appearance. The
evidence is in
[appearance/01](entries/appearance/01-read-and-remove-a-part-appearance.md).
A member found to exist with `GetIDsOfNames` but never run is unverified.

## ISldWorks (the application)

| Member | Purpose | Status |
|---|---|---|
| `RevisionNumber` | Version string, e.g. `"34.0.0"`. Major 32 = 2024, 33 = 2025, 34 = 2026 | Verified, SW 2024/2025/2026 |
| `ActiveDoc` | The document on screen, or `Nothing` | Verified |
| `GetFirstDocument` | First of the open documents, walked with `GetNext` | Verified |
| `Visible` | Show or hide a launched instance | Verified |
| `CommandInProgress` | Set `True` to suppress rebuilds during a batch. Features made under it came out right after one rebuild | Verified, SW 2026 |
| `ExitApp` | Close an instance you launched | Unverified |
| `GetDocumentDependencies2` | References of a **closed** file, without opening it | Verified |
| `ReplaceReferencedDocument` | Repoint a reference after a move | Unverified |
| `GetOpenDocumentByName` | Check whether a file is open before touching it | Unverified |
| `LoadFile4(path, argString, importData, ByRef err)` | Import a neutral format. **The working route for STEP**, where `OpenDoc6` fails | Verified, SW 2026 SP1.1 |
| `OpenDoc6` | Opens native files. **Failed every STEP file with 2097152**, whatever the options | Verified not to work on STEP, SW 2026 SP1.1 |
| `GetOpenDocSpec` | Document spec before opening. On a `.step` returns `DocumentType = -1`, `Error = 1024` | Verified, SW 2026 SP1.1 |
| `CloseDoc(title)` | Close one document, discarding changes silently. Unlike `CloseAllDocuments`, raises no save prompt | Verified, SW 2026 SP1.1 |
| `SetUserPreferenceToggle` / `SetUserPreferenceIntegerValue` | Set import and other options from code | Verified, SW 2026 SP1.1 |
| `GetUserPreferenceIntegerValue` | Read an integer option before changing it, so it can be restored. Used by the `convert-step` script | Unverified |
| `GetConfigurationNames(path)` | Configuration names of a **closed** file. The `IModelDoc2` member of the same name reads an open one | Verified, SW 2026 SP1.1 |
| `VersionHistory(path)` | Saved-version list of a **closed** file, `11000[2018/134] \| 14000[2021/85]` | Verified, SW 2026 SP1.1 |
| `GetLatestSupportedFileVersion` | File version this instance writes. Not the `RevisionNumber` major | Verified, SW 2026 SP1.1 |
| `GetDocumentTemplate(type, "", 0, 0, 0)` | Default template path; 1 part, 2 assembly. **Returned the 2025 folder's inch template on a 2026 session** | Verified, SW 2026 (34.0.0) |
| `GetUserPreferenceStringValue(8 / 9)` | Default part / assembly template; same paths as above | Verified, SW 2026 (34.0.0) |
| `NewDocument(template, 0, 0, 0)` | New document from a template; returns it, and it is active | Verified, SW 2026 (34.0.0) |
| `CloseDoc(title)` | Close by title. **Discards unsaved changes without a prompt** | Verified, SW 2026 (34.0.0) |
| `ActivateDoc3(title, False, 0, out long)` | Bring an open document to the front; returned the document, not a tuple | Verified, SW 2026 (34.0.0) |
| `GetUserPreferenceToggle` / `SetUserPreferenceToggle(10, bool)` | `swInputDimValOnCreate`: turn off the modal value dialog around `AddDimension2`, then restore | Verified, SW 2026 (34.0.0) |
| `GetMathUtility` | The `IMathUtility`, below | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `GetDocuments` | Every open document in one call, as a tuple: listed seven parts and an assembly | Verified, SW 2024 (32.5.0), 2026-10-01 |

## IModelDoc2 (a part, assembly or drawing)

| Member | Purpose | Status |
|---|---|---|
| `GetTitle` | Document name, used to match the part you want | Verified |
| `GetNext` | Next open document | Verified |
| `FirstFeature` | Head of the feature tree | Verified |
| `InsertCurveFile(path)` | Import a curve file as a feature. **Lives here, not on IFeatureManager** | Verified, SW 2026 |
| `InsertCurveFileBegin` / `InsertCurveFilePoint` / `InsertCurveFileEnd` | Stream a curve point by point, in metres | Verified, SW 2025 |
| `InsertCompositeCurve` | Join the selected curves into one | Verified, SW 2026 |
| `ForceRebuild3(topOnly)` | Rebuild. Call once, after the last change | Verified |
| `ClearSelection2(all)` | Empty the selection. **Called while the user was picking in a sketch, it crashed SolidWorks 2026** (access violation in `ClearSelectionsNotify`, GOTCHAS §51); never call it on a selection the user is making | Verified; crash observed, Airfoil Converter 2026-09-16 |
| `SelectionManager` | The selection, for reading what was clicked | Verified |
| `SketchManager` | The sketch API | Partly verified |
| `GetType` | Document type of what you actually got. 1 part, 2 assembly. A neutral import decides this for you | Verified, SW 2026 SP1.1 |
| `GetConfigurationNames` | Configuration names of the open document | Verified, SW 2026 SP1.1 |
| `GetConfigurationByName(name)` | One `IConfiguration` | Verified, SW 2026 SP1.1 |
| `ShowConfiguration2(name)` | Activate a configuration. **Returns False when it is already active**; read the name back | Verified, SW 2026 SP1.1 |
| `DeleteConfiguration2(name)` | Delete one. Takes derived configurations with it | Verified, SW 2026 SP1.1 |
| `DeleteDesignTable` | Drop the table. Must precede any configuration deletion | Verified, SW 2026 SP1.1 |
| `GetDesignTable` | Non-null means the configurations are table-driven | Verified, SW 2026 SP1.1 |
| `GetEquationMgr` | `IEquationMgr`, below | Verified, SW 2026 SP1.1 |
| `EditRebuild3` | Rebuild the active configuration. Needed after `ShowConfiguration2` before measuring | Verified, SW 2026 SP1.1 |
| `Save3(options, ByRef err, ByRef warn)` | Save in place, silently | Verified, SW 2026 SP1.1 |
| `ConfigurationManager` | `IConfigurationManager`, below | Verified, SW 2026 SP1.1 |
| `SaveAs3(path, version, options)` | Save under a new name. `0` current version, `2` silent | Unverified |
| `Extension` | `IModelDocExtension`, below | Verified |
| `GetType` | 1 part, 2 assembly | Verified, SW 2026 (34.0.0) |
| `GetPathName` | Full path; empty until saved; follows a `SaveAs3` | Verified, SW 2026 (34.0.0) |
| `GetSaveFlag` | Whether the document has unsaved changes | Verified, SW 2026 (34.0.0) |
| `MaterialPropertyValues` (read) | Nine doubles, `[R, G, B, Ambient, Diffuse, Specular, Shininess, Transparency, Emission]` (order from the API help). Read on a part with no applied appearance and on one with `color.p2m` applied; index 7 matched the appearance's transparency | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `ActiveView` | The `IModelView`, for `DisplayMode` below | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `GetMaterialPropertyName2(config, "")` | **Raised `Type mismatch` (-2147352571)** on every part, reached on the `IModelDoc2` through late binding. The second parameter is an out parameter; a by-reference variant was not tried | Verified not to work with those arguments, SW 2024 (32.5.0), 2026-10-01 |
| `GraphicsRedraw2()` | Redraw. Ran without error after deleting an appearance; whether it was needed was not tested | Ran, SW 2024 (32.5.0), 2026-10-01 |
| `SaveAs3(path, 0, 1)` | Save as. **Returned `0` while the file was written**; see the extension's form | Verified, SW 2026 (34.0.0) |
| `GetEquationMgr` | The Equation Manager, below | Verified, SW 2026 (34.0.0) |
| `SketchAddConstraints(constant)` | Add a relation to the selected sketch entities, e.g. `"sgTANGENT"` | Verified, SW 2026 (34.0.0) |
| `AddDimension2(x, y, z)` | Dimension the selection, text at a point in metres; returns an `IDisplayDimension`. On the Top plane an angle placed at a **model-space** point read the angle (GOTCHAS §43) | Verified, SW 2026 (34.0.0) |
| `GetFeatureCount` | How many features; half of a cheap change key. Its **change** across an insert says how many features the insert made (552 → 553 for one curve, rolled back or not); a different count from a full listing's, which includes sub-features (552 against 559) | Verified, SW 2026, Airfoil Converter 2026-09-16 and 2026-09-23 |
| `GetUpdateStamp` | A number that moves when the model changes and stood still while it was only orbited; with `GetFeatureCount`, under a millisecond | Verified, SW 2026, Airfoil Converter 2026-09-16 |
| `InsertLoftRefSurface2(False, keepTangency, False, 1.0, 0, 0)` | Surface loft through the curves at mark 1 along those at mark 2; returns nothing useful, so judge by the tree. A fallback where a solid was refused; that such a surface was made was observed, the arguments' meanings were not examined | Partly verified, SW 2026, Airfoil Converter 2026-09-16 |
| `EditRebuild3` | Rebuild what changed. **0.8 s against `ForceRebuild3(False)`'s 25 s** on a 397-feature part, geometry identical to twelve digits. **Returns `False` whenever any feature in the part is in error**, including ones that were already in error, so its answer is not "did not run" (GOTCHAS §58) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `FeatureByPositionReverse(0)` | The last feature in the tree. Ask it `IsRolledBack` rather than believe `EditRollback` | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `InsertPlanarRefSurface` | A planar surface across the **selected edges**. **No arguments**; reads the selection, returns a bool, 0.2 s. Refuses a reference or composite curve as a boundary, in 0.0 s. Feature type `PlanarSurface`. Can cap a sliver's loop instead of the end and still answer `True` (GOTCHAS §60) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `InsertLoftRefSurface2(False, keepTangency, False, 1.0, 0, 0)` | The surface loft again: made the loft at every offset where the solid was refused, 10–15 s, and is the first step of a capped solid ([surfacing/04](entries/surfacing/04-cap-a-refused-loft-into-a-solid.md)). The arguments' meanings were still not examined | Partly verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetBodies2(type, visibleOnly)` | Also answers on the document, not only `IPartDoc`: `GetBodies2(0, False)` solid bodies, `GetBodies2(1, False)` sheets. Counting these either side of a knit is what tells a solid from a sheet | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `InsertAxis2(True)` | Reference axis from two selected planes; returns `True`, not the axis. Also from one sketch line selected at mark 0 by `LineN@Sketch`; the axis followed the line's linked angle | Verified, SW 2026 (34.0.0); from a line, dev runs 2026-09-15 |

## IModelDocExtension

| Member | Purpose | Status |
|---|---|---|
| `SelectByID2` | Select a named entity by type string, with a selection mark. `"PLANE"` with a plane's name read from the tree selected it | Verified, SW 2026 |
| `SelectByID2(name, "REFERENCECURVES", 0, 0, 0, append, mark, null, 0)` | An imported or composite curve by name: at mark 1 to join ([curves/06](entries/curves/06-composite-curve.md)), at mark 1 as a loft profile and mark 2 as a loft guide. Straight after an insert it sometimes returned `False` until a `ForceRebuild3` | Verified, SW 2026, Airfoil Converter 2026-09-16 |
| `SelectByID2("LineN@Sketch", "EXTSKETCHSEGMENT", 0, 0, 0, append, mark, null, 0)` | A line of a closed sketch by name; the name from `ISketchSegment.GetName`. At mark 16 as a revolve's axis, at mark 0 for a plane or an axis | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `SelectByID2("", "EXTSKETCHPOINT", x, y, z, append, mark, null, 0)` | A closed sketch's point by its model location, metres | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `SelectByID2("Point1@<origin>@<component>@<assembly>", "EXTSKETCHPOINT", 0, 0, 0, append, 1, null, 0)` | A component's origin, for a mate; selected type 25 | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `SelectByID2(bodyName, "SURFACEBODY", 0, 0, 0, append, 1, null, 0)` | A body by its `IBody2.Name`, at mark 1, which is how a knit's inputs are picked. **The only way that worked**: `IBody2` has no `Select4` and its `Select2` raises through pywin32 | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `AddDimension2(x, y, z)` | Add a dimension at a placement point, in metres. The `IModelDoc2` member of the same name is the one that has run | Unverified |
| `GetPersistReference3(obj)` | A byte handle to a feature that survives renaming | Verified, SW 2026 |
| `DeleteSelection2(options)` | Delete what is selected. On a folder it removes **only the folder**, leaving its contents in place | Verified, SW 2026 |
| `GetObjectByPersistReference3(ref, out)` | Resolve that handle back. Needs a by-ref out parameter | Verified, SW 2026 |
| `SaveAs(path, version, options, exportData, ByRef err, ByRef warn)` | Save under a new name, with an error and a warning code back. Prefer over `SaveAs3` | Verified, SW 2026 SP1.1 |
| `SaveAs(path.STEP, 0, 1 \| 2, null, out, out)` | Export to STEP, silent (1) **as a copy** (2), so the open document keeps its name; format from the extension | Verified, SW 2026, Airfoil Converter 2026-09-16 |
| `ListExternalFileReferences` | External references of a document | Unverified |
| `GetUserPreferenceInteger(pref, 0)` | Read a document setting, e.g. 47 `swUnitsLinear` (0 mm, 3 inches) | Verified, SW 2026 (34.0.0) |
| `SetUserPreferenceInteger(263, 0, 5)` | Set the document's unit system to MMGS; returned `True` and took | Verified, SW 2026 (34.0.0) |
| `SaveAs3(path, 0, 1, null, null, out, out)` | Save as, silently. Returned plain `True`, not a tuple. **Overwrites an existing file** | Verified, SW 2026 (34.0.0) |
| `CreateMassProperty` | An `IMassProperty` for the document | Verified, SW 2026 (34.0.0) |
| `GetLastFeatureAdded` | The feature the last insert made. Named the new curve in 0.010 s after `InsertCurveFile`, with the tree forward and with the bar rolled back (where the new feature is not the last in the tree). With `GetFeatureCount` either side, replaces diffing two listings of the tree ([curves/02](entries/curves/02-insert-curve-from-file.md)) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-23 |
| `GetRenderMaterials2(1, None)` | The appearances applied to the document, as `IRenderMaterial`s; `1` is `swThisDisplayState`. Gave one for a part with a part-level `color.p2m`, none for parts with no appearance | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `GetRenderMaterialsCount2(1, None)` | How many; 1 and 0 for the same parts | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `DeleteDisplayStateSpecificRenderMaterial(ids1, ids2)` | Delete appearances by ID. **Two arrays**, the first IDs and the second IDs, each `VT_ARRAY \| VT_I4`. One combined array `[11, 0]` returned `False`; `[11]`, `[0]` returned `True` and the count went to 0. **Must be invoked with `DISPATCH_METHOD`**: by attribute access it ran with no arguments and gave `'bool' object is not callable` ([GOTCHAS §55, §70](GOTCHAS.md)) | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `RemoveMaterialProperty(Config_opt, Config_names)` | Exists (`GetIDsOfNames`). The help says it is meant for features with a changed material property value. Never run | Unverified |
| `SaveAs(path.png, 0, 3, null, out, out)` on a drawing | A PNG of the sheet as the window shows it; silent copy, the drawing keeps its path. `ViewZoomtofit2` first ([drawings/01](entries/drawings/01-new-drawing-and-place-views.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SelectByID2("<name>@<view>", "NOTE", 0, 0, 0, True, 0, null, 0)` | Select a note or balloon (`DetailItemNN`) for `EditDelete`. Where `IAnnotation.Select3` deleted nothing, this worked (GOTCHAS §80) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SelectByID2("RDn@<view>", "DIMENSION", …)` / `SelectByID2("<view name>", "DRAWINGVIEW", …)` | Select a drawing dimension, or a whole view (for `CreateUnfoldedViewAt3`, `InsertAlternateView`, `AutoBalloon5`, or deleting an alternate position) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SelectByID2("<sketch>@<root component>-N@<view>", "SKETCH", …)` | An assembly sketch through a drawing view; selected object type 9. Used to `UnblankSketch` explode lines (GOTCHAS §85) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `RenameDocument(newName)` | Rename the selected component's file, pending until the assembly is saved with references; returns 0 on success ([files/05](entries/files/05-rename-a-component-file.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `HasRenamedDocuments` | `True` while a rename is pending | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `CustomPropertyManager("")` | The file-level `ICustomPropertyManager` | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## IConfigurationManager (from `IModelDoc2.ConfigurationManager`)

| Member | Purpose | Status |
|---|---|---|
| `ActiveConfiguration` | The active `IConfiguration`. Read its `Name` rather than trusting `ShowConfiguration2` | Verified, SW 2026 SP1.1 |
| `AddConfiguration2(name, comment, "", 0, "", "", True)` | Add an assembly configuration; it became the active one ([assemblies/07](entries/assemblies/07-configurations-that-hold-different-positions.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## IConfiguration

| Member | Purpose | Status |
|---|---|---|
| `Name` | Configuration name | Verified, SW 2026 SP1.1 |
| `Description` | Its description. Can throw; wrap it | Verified, SW 2026 SP1.1 |
| `GetParent` | Non-null means this configuration is derived from that one | Verified, SW 2026 SP1.1 |
| `GetRootComponent3(True)` | The root component; its `GetChildren` are the top-level components | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `AddExplodeStep2(dist, axis, reverse, 0.0, -1, False, False, False, out err)` | Explode the components selected at mark 1 by `dist` **metres** along the assembly's X/Y/Z for `axis` 0/1/2, negative way with `reverse`; `err` 0. Other arguments not varied ([assemblies/08](entries/assemblies/08-exploded-view-from-code.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetNumberOfExplodeSteps` / `GetExplodeStep(i)` / `DeleteExplodeStep(name)` | Read and remove existing steps; names `Explode StepN`, or the user's | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## IPartDoc (a part, cast from IModelDoc2)

| Member | Purpose | Status |
|---|---|---|
| `GetBodies2(0, False)` / `GetBodies2(1, False)` | Solid bodies, and sheet bodies. `swBodyType_e` **0 solid, 1 sheet**. The count either side of a call is what says whether it made a body | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetPartBox(useSystemUnits)` | Axis-aligned bounding box, `x1,y1,z1,x2,y2,z2`. **`true` is metres, `false` converts to the document's units** | Verified, SW 2026 SP1.1 |
| `FeatureByName(name)` | A feature by name in one call: 0.01 s against 2.9 s for a walk of 559 features, and it found a curve inside a feature-tree folder. Reached on the `IModelDoc2` through late binding ([connect/08](entries/connect/08-find-a-feature-by-name.md)) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-23 |

## IEquationMgr (from `IModelDoc2.GetEquationMgr`)

| Member | Purpose | Status |
|---|---|---|
| `GetCount` | How many equations, global variables included | Verified, SW 2026 SP1.1 |
| `Equation[i]` | The raw equation text, in editor order | Verified, SW 2026 SP1.1 |
| `Add2(-1, text, True)` | Append an equation, `"Name"= expression`; returns its index, or `-1` if refused | Verified, SW 2026 (34.0.0) |
| `Add3(-1, text, True, 2, None)` | **Returned `-1` on a one-configuration part** | Verified not to work there, SW 2026 (34.0.0) |
| `GetCount` | How many equations | Verified, SW 2026 (34.0.0) |
| `Equation(i)` | Read the text. **Indexed put** changes it in place; from Python through `Invoke` | Verified, SW 2026 (34.0.0) |
| `Value(i)` | The evaluated value | Verified, SW 2026 (34.0.0) |
| `GlobalVariable(i)` | Whether it is a global variable | Verified, SW 2026 (34.0.0) |
| `Delete(i)` | Remove one; returned `0` | Verified, SW 2026 (34.0.0) |
| `Status` | `-1` after a refused add | Verified, SW 2026 (34.0.0) |
| `AngularEquationUnits` | Read `1` in a part whose trig evaluated in degrees | Verified, SW 2026 (34.0.0) |
| `SetEquationAndConfigurationOption(i, text, 2, None)` | **Returned `-1` and changed nothing** | Verified not to work, SW 2026 (34.0.0) |
| `EvaluateAll` | Listed by the probe, never called | Unverified |

## IDisplayDimension and IDimension (from a feature)

| Member | Purpose | Status |
|---|---|---|
| `IFeature.GetFirstDisplayDimension` / `GetNextDisplayDimension(dd)` | Walk a feature's dimensions. There is no all-dimensions collection | Verified, SW 2026 SP1.1 |
| `IDisplayDimension.GetDimension` | The `IDimension` behind the annotation | Verified, SW 2026 SP1.1 |
| `IDimension.FullName` | Qualified name, as the equation editor spells it | Verified, SW 2026 SP1.1 |
| `IDimension.GetSystemValue2(config)` | Value **in metres**. `""` is the active configuration | Verified, SW 2026 SP1.1 |

## IFeatureManager (from `IModelDoc2.FeatureManager`)

| Member | Purpose | Status |
|---|---|---|
| `InsertFeatureTreeFolder2(type)` | Folder around the current selection. Pass `2`; see the constants below | Verified, SW 2026 |
| `MoveToFolder(folder, feature, moveAfter)` | Would add a feature to an existing folder. **Returned `False` and did nothing, every way it was tried** | Verified not to work, SW 2026 |
| `EnableFeatureTree` | Whether the tree is live; read `True` while the above failed. Set `False` (with `EnableFeatureTreeWindow = False`), a curve reload's `ModifyDefinition` took 12.2 s against 9.7 s with it on: no help there ([curves/12](entries/curves/12-roll-the-tree-back-before-reloading.md)) | Verified, SW 2026; set, SW 2026 SP0.0 2026-09-23 |
| `FeatureExtrusion3` (23 arguments) | Blind boss from the selected sketch, depth in metres; returns the feature | Verified, SW 2026 (34.0.0) |
| `FeatureCut4` (27 arguments) | Cut from the selected sketch. **Through all in the default direction returned `None`**; both directions (end condition 9) cut | Verified, SW 2026 (34.0.0) |
| `FeatureCircularPattern5` (14 arguments) | Pattern the feature at mark 4 about the axis at mark 1; two `"NULL"` strings. On twisted cuts the fifth argument (geometry pattern) `True` made the same solid and rebuilt in about half the time (helical runs 2026-09-15) | Verified, SW 2026 (34.0.0) |
| `FeatureRevolve2` (20 arguments) | Revolve the selected sketch a full turn (radians) about its centreline, or about the line at mark 16; returns a `Revolution` | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `InsertRefPlane(2, 0, 4, 0, 0, 0)` | Plane perpendicular to the line at mark 0 and coincident with the point at mark 1. Its sketch frame kept its signs as the line moved; the plane has no dimension | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `InsertCutBlend` (12 arguments) | Lofted cut through the sketches selected at mark 1, in order; returns a `BlendCut`. Removed the frustum exactly | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `InsertMoveCopyBody2` (12 arguments) | Turned the body selected at mark 1 by 30° about Y; returns a `MoveCopyBody`. **The feature has no dimension**, so no global can drive it (GOTCHAS §44) | Verified, SW 2026 (34.0.0), run 20260915-015506 |
| `InsertRefPlane(8 \| 256, d, 0, 0, 0, 0)` | Plane at distance `d` (metres) from the selected plane. From the Front plane, unflipped is on the +Z side and `256` OR-ed in puts it on −Z; both sketches face +Z. Its one dimension, `D1`, links to a global | Verified, SW 2026 (34.0.0), helical runs 2026-09-15 |
| `InsertProtrusionSwept4` (20 arguments) | Swept boss: profile at mark 1, path at mark 4; third argument twist control (8, constant along path), sixteenth the twist in **radians**. **The twist's sign is ignored** (GOTCHAS §45). Returns a `Sweep` | Verified, SW 2026 (34.0.0), helical runs 2026-09-15 |
| `InsertCutSwept5` (22 arguments) | Swept cut, same marks; third argument twist control, fifteenth the twist in radians. Started ahead of the face it removed A·w and left one body. Returns a `SweepCut` | Verified, SW 2026 (34.0.0), helical runs 2026-09-15 |
| `InsertCutSwept4` (19 arguments) | Listed by the probe as a fallback, never called | Unverified |
| `InsertProtrusionBlend2` (18 arguments) | Solid loft through the curves at mark 1, in order, along the guides at mark 2: `False, keepTangency, False, 1.0, 0, 0, 1.0, 1.0, False, False, False, 0.0, 0.0, 0, merge, False, True, guideInfluence`. With tangency on and influence `0` (To next guide) it **reproduced a hand-made loft exactly**. Some solids were refused where the same surface loft was made | Verified, SW 2026, Airfoil Converter 2026-09-16 |
| `GetFeatures(False)` | Every feature, sub-features included, in one call; about half the time of a `FirstFeature`/`GetNextFeature` walk. Order and folder end tags not checked | Verified, SW 2026, Airfoil Converter 2026-09-16 |
| `InsertMirrorFeature2(True, False, True, False, 0)` | Mirror the body at mark 256 about the plane at mark 2, merged into one `MirrorSolid` body. **With the body at mark 1 it returned `None`** (GOTCHAS §47) | Verified, SW 2026 (34.0.0), helical runs 2026-09-15 |
| `EditRollback(action, featureName)` | Move the rollback bar. `swMoveRollbackBarTo_e` **1 End, 2 PreviousPosition, 3 BeforeFeature, 4 AfterFeature**, read off this machine's `swconst.tlb`. `4` with a name 0.1–0.3 s, `1` with `""` 0.8–25 s. **`2` answers `True` and moves nothing** (GOTCHAS §57); `3` not tried | Verified for 1 and 4, verified not to work for 2, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetRollbackBarPosition` | Where the bar is. **Not reachable through pywin32 late binding**: attribute access raised `AttributeError: <unknown>.GetRollbackBarPosition` | Verified not to work from Python, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `InsertSewRefSurface(True, tryToFormSolid, False, 1e-4, 1e-4)` | Knit the selected surface bodies; tolerances in **metres** (1e-4 m = 0.1 mm). 0.2–8.4 s. Feature type `SewRefSurface`. **With `tryToFormSolid` `True` it can sew a sheet and still make a feature** — count the solid bodies (GOTCHAS §61). Deleting it leaves the sheets it knitted in the tree | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `InsertFillSurface2(3, 0, boundaries, contacts, null, null)` | A filled surface over edges. Capped the same ends as the planar surface. **Needs typed arrays**: `VARIANT(VT_ARRAY\|VT_DISPATCH, edges)` and `VARIANT(VT_ARRAY\|VT_I4, [0]*n)`. Five other argument forms — a `SelectData`, Python tuples, a typed null, "read the selection", and the obsolete `InsertFillSurface` — returned `None` in 3.9–4.5 s each | Verified in that one form, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `InsertProtrusionBlend2` (18 arguments) | Also: **a refused solid returns `Nothing` with no feature, no error and no dialog**, after 7–9.5 s. On one wing it refused at 1.30–1.40 mm of inward offset and built at 1.25 and 1.45–1.60, from the same code; one guide curve was responsible ([surfacing/03](entries/surfacing/03-how-a-loft-fills-between-profiles.md)) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `FeatureCut3` | Listed by the probe, never called | Unverified |
| `FeatureCircularPattern4` | Listed by the probe, never called | Unverified |

## IFeatureFolder (from `GetSpecificFeature2` on an `FtrFolder`)

| Member | Purpose | Status |
|---|---|---|
| `GetFeatureCount` | How many features the folder holds. **Counts a nested folder's end tag** | Verified, SW 2026 |
| `GetFeatures` | The features it holds, a nested folder's end tag included. **A folder and its own end tag answer with the same list**, which is how the tag is told apart: it comes after the list's first item in the walk ([curves/11](entries/curves/11-feature-tree-folders.md)) | Verified, SW 2026 SP0.0, 14 folder features of a real part, 2026-09-24 |

## IFeature

| Member | Purpose | Status |
|---|---|---|
| `Name` | Read and write. **Read it back after writing** | Verified |
| `GetNextFeature` | Next sibling in the tree | Verified |
| `GetTypeName2` | Type string, e.g. `"CurveInFile"`, `"CompositeCurve"` | Verified |
| `GetDefinition` | Feature data you can modify. **Returns `Nothing` on a surface loft (`BlendRefSurface`)**, an object on a solid loft (`Blend`) — so a surface loft's profiles cannot be edited through it (GOTCHAS §68) | Verified; `Nothing` on `BlendRefSurface`, SW 2026 SP0.0, Airfoil Converter 2026-09-27 |
| `ModifyDefinition(data, doc, component)` | Commit modified feature data | Verified, SW 2026 |
| `GetSpecificFeature2` | The concrete feature behind a reference plane; on an `FtrFolder`, the `IFeatureFolder`; on a sketch feature, the `ISketch` | Verified |
| `GetParents` | The features this one is built on. On a composite curve, the curves it joins, in **0.001 s** and without the rollback `AccessSelections` makes (12.9 s); order not promised, so compare as a set ([curves/06](entries/curves/06-composite-curve.md)) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-23 |
| `Select2(append, mark)` | Select this feature. Works where `SelectByID2` with `"BODYFEATURE"` returned `False`. Judge by the selection count | Verified, SW 2026 |
| `ListExternalFileReferences2` | External references of one feature | Unverified |
| `SetSuppression2(action, 1, None)` | Suppress (`0`) or unsuppress (`1`) in this configuration, to export one loft at a time. In the code whose STEP files were measured, but whether it accepted the bare `None` and isolated the body was not recorded | Unverified, Airfoil Converter 2026-09-16 |
| `IsRolledBack` | Whether this feature is below the rollback bar. A genuine `bool` through late binding, and the thing to trust where `EditRollback`'s return cannot be | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `IsSuppressed` | Whether it is suppressed. A property get, answers a `bool` | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `SetSuppression2(action, 1, None)` | Suppress (`0`) or unsuppress (`1`) in this configuration. The bare `None` **is** accepted here. **It cascades**: on one part it suppressed 74 features built on the one asked for, and unsuppressing that one restored none of them (GOTCHAS §62) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetFaces` | The faces a feature made. A property get. The route to a feature's **body**, which a feature does not offer: take a face and ask it | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetErrorCode2(ByRef bool)` | A feature's error code, `0` for clean, with a by-ref warning flag passed as `VARIANT(VT_BYREF \| VT_BOOL, False)`. Read `1` on each of a part's 13 pre-existing failures | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `SetSuppression2(action, 1, None)` on a **mate** | Suppress (`0`) or unsuppress (`1`) one mate under `MateGroup`; `IsSuppressed` read `True` then `False`. No cascade seen on mates ([assemblies/05](entries/assemblies/05-test-motion-with-the-drag-operator.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-06 |
| `GetDefinition` on a **mate** | `IGearMateFeatureData` on `MateGearDim`, `IDistanceMateFeatureData` on distance mates; concentric and coincident data answered `MateAlignment`; `MateCoordinate` answered none of the members tried | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 |
| `GetErrorCode2(ByRef bool)` on a **mate** | `0` on a solved mate; **51** with the warning flag `True` on mates left unsolved after a `Transform2` put and rebuild (GOTCHAS §73) | Verified, SW 2024 SP5 (32.5.0), 2026-10-06 |
| `SetSuppression2(0, 3, names)` on a **mate** | Suppress it in the **listed** configurations (`3`), leaving it live in the rest: one configuration's position mates ([assemblies/07](entries/assemblies/07-configurations-that-hold-different-positions.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IsSuppressed2(3, names)` | One suppression flag per listed configuration, without switching configuration | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetFirstDisplayDimension` / `GetNextDisplayDimension(prev)` | Walk a feature's dimensions; includes its sketch's | Verified, SW 2026 (34.0.0) |
| `GetFirstSubFeature` / `GetNextSubFeature` | Children, e.g. the mates under `MateGroup`, or the sketch a feature was made from. **Step children with `GetNextSubFeature`**: `GetNextFeature` from a child walked on through the main tree and repeated names | Verified, SW 2026 (34.0.0) |

## Curve feature data (from `GetDefinition` on a CurveInFile)

| Member | Purpose | Status |
|---|---|---|
| `LoadPointsFromFile(path)` | Replace the points from a file, in place | Verified, SW 2026 |
| `PointArray` | The points it holds, flat, in metres | Verified, SW 2026 |

## Composite curve feature data (from `IFeature.GetDefinition` on a `CompositeCurve`)

The interface name was not recorded in the source; see [curves/06](entries/curves/06-composite-curve.md).

| Member | Purpose | Status |
|---|---|---|
| `AccessSelections(doc, null)` | Open the definition's selections for reading. Returned `True` in 0.1 s — and **rolled the model back to just before the feature**, which the help documents and which is easy to read past (GOTCHAS §59) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetEntitiesToJoin(ByRef types)` | The curves the composite joins, as objects with `Name`, in join order; `types` passed as `VARIANT(VT_BYREF \| VT_VARIANT, None)`. Three curves back off a three-piece composite, whole read 3.2–4.5 s | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `ReleaseSelectionAccess` | Close them again, in a `finally`; rolls the tree forward again in 0.9 s. **A `Sub`, so late binding hands it over as an uncalled method object** — call it with parentheses (GOTCHAS §55) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |

## ISweepFeatureData (from `IFeature.GetDefinition` on a `Sweep` or `SweepCut`)

| Member | Purpose | Status |
|---|---|---|
| `TwistControlType` | Read `8` on a sweep made with constant twist along path | Verified, SW 2026 (34.0.0), helical runs 2026-09-15 |
| `GetTwistAngle` | The twist in **radians**; read 1.5707963267948966 for a quarter turn | Verified, SW 2026 (34.0.0), helical runs 2026-09-15 |
| `AccessSelections(doc, component)` | Called before changing the definition, component a typed null in a part; returned `True` | Verified, SW 2026 (34.0.0), helical runs 2026-09-15 |
| `D1ReverseTwistDir` | Read `False` by default; set `True` and committed with `IFeature.ModifyDefinition(data, doc, null)`, which returned `True`, it turned the twist the other way. The only way found to get the other hand (GOTCHAS §45). The tool applies it to a `SweepCut` | Verified, SW 2026 (34.0.0), helical runs 2026-09-15 |

## ISketchManager

| Member | Purpose | Status |
|---|---|---|
| `InsertSketch(rebuild)` | Open a 2D sketch on the current selection; call again to close | Verified, SW 2026 (34.0.0) |
| `Insert3DSketch2(rebuild)` | Open a 3D sketch; call again to close | Unverified |
| `ActiveSketch` | The sketch being edited, or `Nothing` after closing. The Airfoil Converter refuses to select anything while it is not `None` (GOTCHAS §51) | Verified, SW 2026 (34.0.0) |
| `AddToDB` / `DisplayWhenAdded` | Set `True` / `False` while drawing, restored after; `AddToDB` read back | Verified, SW 2026 (34.0.0) |
| `CreatePoint(x, y, z)` | A sketch point, in metres | Verified, SW 2026 (34.0.0) |
| `CreateLine(x1, y1, z1, x2, y2, z2)` | A line, in metres | Verified, SW 2026 (34.0.0) |
| `CreateLine2(x1, y1, z1, x2, y2, z2)` | A line, in metres | Unverified |
| `CreateCenterLine(x1, y1, z1, x2, y2, z2)` | A construction line | Verified, SW 2026 (34.0.0) |
| `CreateArc(cx, cy, cz, sx, sy, sz, ex, ey, ez, dir)` | An arc. `1` counter-clockwise from the start, `-1` clockwise | Verified, SW 2026 (34.0.0) |
| `Create3PointArc(x1, y1, z1, x2, y2, z2, x3, y3, z3)` | An arc through three points | Verified, SW 2026 (34.0.0) |
| `CreateCircleByRadius(cx, cy, cz, r)` | A circle | Verified, SW 2026 (34.0.0) |
| `CreateCircle(cx, cy, cz, px, py, pz)` | A circle through a point | Verified, SW 2026 (34.0.0) |
| `CreateSpline(pointArray)` | A spline through a flat array of triples | Unverified |
| `CreateEquationSpline2(x, y, "", t1, t2, False, 0, 0, 0, True, True)` | Equation Driven Curve. Document units, **radian trig**; `*180/pi` refused (`None`) | Verified, SW 2026 (34.0.0) |
| `AddConstraint(name)` | Add a relation to the current selection. `IModelDoc2.SketchAddConstraints` is the form that has run | Unverified |

## ISketch (from `ActiveSketch`, or `GetSpecificFeature2` on a sketch feature)

| Member | Purpose | Status |
|---|---|---|
| `GetConstrainedStatus` | 2 under-defined, 3 fully defined (4 over, from the type library, not observed) | Verified, SW 2026 (34.0.0) |
| `GetSketchSegments` | The segments, still there after closing | Verified, SW 2026 (34.0.0) |
| `GetSketchPoints2` | The sketch points, e.g. a curve's ends, or the origin's on `OriginProfileFeature` | Verified, SW 2026 (34.0.0) |

## ISketchSegment / ISketchPoint

| Member | Purpose | Status |
|---|---|---|
| `GetStartPoint2` / `GetEndPoint2` | A segment's endpoints, as selectable objects | Verified, SW 2026 (34.0.0) |
| `GetCenterPoint2` | An arc or circle centre | Verified, SW 2026 (34.0.0) |
| `ConstructionGeometry` | Toggle construction. Verified on a line; availability on a bare point varies by version | Verified on a line, SW 2026 (34.0.0) |
| `Select4(append, data)` | Add to the selection. From Python, `data` a typed null dispatch. Failed once on a new sketch point, not reproduced in 14 trials (GOTCHAS §36) | Verified, SW 2026 (34.0.0) |
| `X` / `Y` / `Z` | A sketch point's coordinates, in **sketch** space | Verified |
| `GetType` | 0 line, 1 arc (a circle reads 1), 3 spline | Verified, SW 2026 (34.0.0) |
| `GetLength` | Length in metres | Verified, SW 2026 (34.0.0) |
| `GetRadius` (ISketchArc) | Radius in metres, arcs and circles | Verified, SW 2026 (34.0.0) |
| `GetPoints2` (ISketchSpline) | The spline's points as sketch point objects, not doubles | Verified, SW 2026 (34.0.0) |
| `GetName` (ISketchSegment) | The segment's name, e.g. `'Line2'`, for `SelectByID2` after the sketch closes | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |

## ISelectionMgr

| Member | Purpose | Status |
|---|---|---|
| `GetSelectedObjectCount2(mark)` | How many things are selected. `-1` = any mark | Verified |
| `GetSelectedObject6(index, mark)` | The selected object, 1-based | Verified |
| `GetSelectedObjectType3(index, mark)` | A `swSelectType_e` number | Verified |
| `GetSelectedObjectsSketch(index)` | The sketch a selected entity belongs to | Verified |
| `CreateSelectData` | A selection data object; set `Mark` on it before `IBody2.Select2` | Verified, SW 2026 (34.0.0), run 20260915-015506 and helical runs 2026-09-15 |

## Geometry interrogation

| Member | On | Purpose | Status |
|---|---|---|---|
| `GetPoint` | IVertex, IRefPoint | A point's coordinates, in metres | Verified |
| `GetCurve` | IEdge, ISketchSegment | The underlying curve | Verified |
| `IsLine` | ICurve | Whether it is straight | Verified |
| `GetStartVertex` / `GetEndVertex` | IEdge | An edge's ends | Verified |
| `GetSurface` | IFace2 | The underlying surface | Verified |
| `IsPlane` | ISurface | Whether it is planar | Verified |
| `PlaneParams` | ISurface | Normal first, then a root point | Verified |
| `Transform` → `ArrayData` | IRefPlane feature | Sixteen doubles: rotation **by columns**, translation, scale | Verified |
| `ModelToSketchTransform` → `Inverse` → `ArrayData` | ISketch | Sketch space to model space | Verified |
| `Transform2` → `ArrayData` | IComponent2 | Items 9–11 are the component's translation, in metres | Verified, SW 2026 (34.0.0) |
| `GetRefAxisParams` | IRefAxis (from `GetSpecificFeature2` on a `RefAxis`) | Two points on the axis, six doubles; their difference is its direction | Verified, SW 2026 (34.0.0), run 20260915-015506 |
| `GetBody` | IFace2 | The body a face belongs to. A feature does not offer its body; a face of it does | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetArea` | IFace2 | The face's area, in **square metres**. What tells a cap that spans an end from one that spans a sliver | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetEdges` | IBody2 | The body's edges. **Comes back as an uncalled method object** through pywin32 (GOTCHAS §55) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetCurveParams2` | IEdge | Items 0–2 the start point, 3–5 the end point, **in metres**, 6–7 the parameter range. Both ends in the plane of an end profile is what marks an end-loop edge. `GetCurve` is called first, because SolidWorks does not keep the underlying curve on the edge until asked | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `Identity` | ICurve | The curve type; **3005 for a spline** | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `Evaluate2(t, 0)` | ICurve | The point at parameter `t`, in metres, in its first three items. Used to sample two fitted splines either side of a crease | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `Select4(append, selectData)` | IEntity (an IEdge here) | Add an edge to the selection; `selectData` from `ISelectionMgr.CreateSelectData`, no mark needed. **The only route for an edge**, which has no name for `SelectByID2` | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetTessTriangles(True)` | IFace2 | The face's tessellation, a flat array of doubles, three per vertex, **in metres** with `True`. **Comes back as an uncalled method object** (GOTCHAS §55) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetClosestPointOn(x, y, z)` | IFace2 | The nearest point of that face to a point, **metres in and out**, first three items. Per face, so a distance to a body is the minimum over its faces. Checked by measuring a point back to its own face: 0.000000 mm | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetFirstSegment` / `GetNextSegment` | IReferenceCurve (from `GetSpecificFeature2` on a `CurveInFile` or `CompositeCurve`) | The curve's edges in order: one for an imported curve, one per piece for a composite. `GetCurveParams2` on each gave its ends; a composite's pieces met end to end to 0.0 mm | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-27 |
| `GetTessPts(chordTol, lengthTol, start, end)` | ICurve | Points along the curve SolidWorks drew, **metres**, three per point; tolerances in metres (1e-7 and 1e-8 used), `start`/`end` from `GetCurveParams2`. How a drawn section was checked flat (5–9e-7 mm off its plane at six decimals) | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-27 |
| `EnableGraphicsUpdate` | IModelView (from `IModelDoc2.ActiveView`) | Set `False` around a curve reload: it did not make `ModifyDefinition` faster (14.8 s against 9.7 s); put back `True` | Set, SW 2026 SP0.0, Airfoil Converter 2026-09-23 |

## IMassProperty (from `IModelDocExtension.CreateMassProperty`)

| Member | Purpose | Status |
|---|---|---|
| `Volume` | The part's volume in **cubic metres**; matched π r² w to 16 significant figures | Verified, SW 2026 (34.0.0) |
| `CenterOfMass` | Centre of mass, three doubles in **metres**, after `ForceRebuild3`; put a revolved ring's on its axis, and told which way a twisted sweep turned | Verified, SW 2026 (34.0.0), dev runs and helical runs 2026-09-15 |
| `AddBodies(bodies)` | Would restrict the measurement to some bodies. **Reached by attribute access it answered a `bool` — it had already run, with no bodies — and then could not be called with them: `'bool' object is not callable`** (GOTCHAS §55). `IBody2.GetMassProperties` is what was used instead | Verified not to work from Python this way, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |

## IMathUtility (from `ISldWorks.GetMathUtility`)

| Member | Purpose | Status |
|---|---|---|
| `CreateTransform(double[16])` | An `IMathTransform` from sixteen doubles, rotation by columns. **Raised `DISP_E_MEMBERNOTFOUND` through late binding** with a list or a `VT_ARRAY\|VT_R8`; worked through `Invoke(..., DISPATCH_METHOD, True, VARIANT(VT_ARRAY\|VT_R8, data))` | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |

## IBody2 (from `IPartDoc.GetBodies2`)

| Member | Purpose | Status |
|---|---|---|
| `IPartDoc.GetBodies2(0, True)` | The part's solid bodies; `len` of it counted one body after a cut | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `Select2(append, selectData)` | Select the body, at the mark set on `ISelectionMgr.CreateSelectData` | Verified at mark 1, SW 2026 (34.0.0), run 20260915-015506; at mark 256, appended, for a mirror, helical runs 2026-09-15 |
| `Name` | The body's own name, which is how `SelectByID2` picks it as a `"SURFACEBODY"` | Verified, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `GetMassProperties(1.0)` | Twelve doubles; **item 3 is the volume, in cubic metres**. The route to one body's volume, where `IMassProperty.AddBodies` could not be called. Item 4 read once as an area in square metres; the rest were not identified | Verified for item 3, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `Select2(append, 0)` and `Select2(append, null)` | **Both raised on a sheet body**: `TypeError: The Python instance can not be converted to a COM object`. A real `SelectData` (which is what made the row above work on a solid body) was **not** retried here; the route taken instead was `SelectByID2` on the body's `Name` as a `"SURFACEBODY"`. `IBody2` has no `Select4` at all | Verified not to work with those two arguments, SW 2026 SP0.0, Airfoil Converter 2026-09-22 |
| `MaterialPropertyValues2` (read) | The body's own nine appearance values. **`None`** on all nine bodies of a part whose only appearance was on the part as a whole | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `Visible` (read) | `True` on solid bodies, `False` on a hidden sheet body | Verified, SW 2024 (32.5.0), 2026-10-01 |

## IRenderMaterial (from `IModelDocExtension.GetRenderMaterials2`)

| Member | Purpose | Status |
|---|---|---|
| `FileName` | The `.p2m` the appearance came from, a full path under the install's `data\graphics\materials` | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `Specular`, `Reflectivity`, `Transparency` | Read as doubles: 0.5, 0.15000000596046448, 0.75 on a `color.p2m` | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `GetEntitiesCount` | Read 1 for an appearance applied to the whole part | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `GetMaterialIds(out id1, out id2)` | The two IDs `DeleteDisplayStateSpecificRenderMaterial` wants, through two `VT_BYREF \| VT_I4` variants: gave 11 and 0 | Verified, SW 2024 (32.5.0), 2026-10-01 |
| `MaterialID` | Read `1` on the same appearance. What it identifies is not known; it was not used | Read, SW 2024 (32.5.0), 2026-10-01 |

## IModelView (from `IModelDoc2.ActiveView`)

| Member | Purpose | Status |
|---|---|---|
| `DisplayMode` (read) | The view's display style as `swViewDisplayMode_e`. Read `5`, `swViewDisplayMode_ShadedWithEdges` | Verified, SW 2024 (32.5.0), 2026-10-01 |

## IDimension / IDisplayDimension

| Member | Purpose | Status |
|---|---|---|
| `Dimension` | The `IDimension` behind a display dimension | Unverified |
| `GetDimension2(0)` | The `IDimension` behind a display dimension | Verified, SW 2026 (34.0.0) |
| `Dimension.Name` | Rename it, e.g. `"D1"`. Read back after setting | Verified, SW 2026 (34.0.0), through `GetDimension2` |
| `Dimension.SystemValue` | Set the value in **metres**, or radians for an angle. A circle's measures its diameter, an arc's its radius | Verified, SW 2026 (34.0.0), through `GetDimension2` |
| `FullName` | `Name@Owner@Document`, e.g. `'Width@Blank@Part233.Part'` | Verified, SW 2026 (34.0.0) |

## IAssemblyDoc (an assembly's document, late-bound)

| Member | Purpose | Status |
|---|---|---|
| `AddComponent5(path, 0, "", False, "", x, y, z)` | Insert a saved, open part; position in metres; returns the component | Verified, SW 2026 (34.0.0) |
| `AddMate5(type, 2, flip, d, d, d, 1, 1, a, a, a, False, False, 0, out long)` | Mate the two entities at mark 1; returned the mate, not a tuple. An angle mate's `D1` (radians) linked to a global was accepted. Also an angle between two axes (`MatePlanarAngleDim`), and a point's distance from a plane, **measured along the plane's normal**: `flip` `True` for a point on the negative side (GOTCHAS §42) | Verified, SW 2026 (34.0.0); axes and flip, dev runs 2026-09-15 |
| `FixComponent()` | Fix the selected component; returns `None`. A free component's `IsFixed` went `False` → `True`. **Call it with parentheses**: reached by attribute access it came back as a Python `method` and never ran | Verified, SW 2026 (34.0.0), rerun 20260914-183429 |
| `AddMate5(0, 2, False, 0, …)` / `AddMate5(5, 2, False, d, d, d, …)` on 2024 | Coincident and distance mates. The out code read **1** when the mate solved and **5** when the assembly came out over-defined, with mates then at `GetErrorCode2` 51 (GOTCHAS §83) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetExplodedViewNames2(config)` | The exploded views in a configuration, e.g. `Exploded View1` | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ShowExploded2(True, name)` | Show that exploded view; returned `True` | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## IComponent2

| Member | Purpose | Status |
|---|---|---|
| `Name2` | Component name, `<file stem>-1` | Verified, SW 2026 (34.0.0) |
| `IsFixed` | `True` for the first component straight after insertion | Verified, SW 2026 (34.0.0) |
| `Select4(False, null, False)` | Select the component | Verified, SW 2026 (34.0.0) |
| `Transform2` | Its placement; see Geometry interrogation | Verified, SW 2026 (34.0.0) |
| `FeatureByName(name)` | A feature of the component, to select for a mate. Called; whether its result or the `SelectByID2` fallback made the selection was not recorded | Partly verified, SW 2026 (34.0.0) |
| `Transform2 = transform` (put) | Set the component's whole frame from an `IMathTransform`; returned `None`, read back to 2.22e-16 and 0 mm | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `GetModelDoc2` | The component's part, to read its origin feature's name | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `SetTransformAndSolve2` | Listed by the probe as a fallback to the `Transform2` put, never reached | Unverified |
| `GetCorresponding` | Listed by the probe as a fallback route to a component's origin, never reached | Unverified |

## IDragOperator (from `IAssemblyDoc.GetDragOperator`)

| Member | Purpose | Status |
|---|---|---|
| `IAssemblyDoc.GetDragOperator` | A drag operator for the assembly; reached by attribute access through `call` | Verified, SW 2024 SP5 (32.5.0), 2026-10-06 |
| `AddComponent(component, False)` | The component to drag | Verified, SW 2024 SP5 (32.5.0), 2026-10-06 |
| `TransformType = 0`, `UseAbsoluteTransform = False` | The one recipe run: each `Drag` takes a **relative** transform. Other values never run | Verified for these values only, SW 2024 SP5 (32.5.0), 2026-10-06 |
| `BeginDrag` / `Drag(xform)` / `EndDrag` | Drag in steps; `Drag` returned `True` each step. A free component turned the full amount, a fixed one 0°, and gear mates carried the motion (GOTCHAS §73). Dragging a driving gear did **not** turn its driven gear; open ([assemblies/05](entries/assemblies/05-test-motion-with-the-drag-operator.md)) | Partly verified, SW 2024 SP5 (32.5.0), 2026-10-06 |
| `DragMode`, `DragAsUI` | Never run | Unverified |

## IMate2 (from `IFeature.GetSpecificFeature2` on a mate)

| Member | Purpose | Status |
|---|---|---|
| `Type` | `swMateType_e`: 0 coincident, 1 concentric, 5 distance **and** limit distance, 10 gear, 20 coordinate were seen ([assemblies/06](entries/assemblies/06-read-every-mate.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 |
| `GetMateEntityCount` / `MateEntity(i)` | The entities the mate joins, as `IMateEntity2`. On a gear mate, `MateEntity(1)` owns the ratio's numerator (GOTCHAS §75) | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 |
| `MaximumVariation` / `MinimumVariation` | On a limit distance mate, metres either side of its `Distance`: one side read 0 and the other the travel. Read as the range by inference from a handful of mates | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 |
| `Alignment`, `Flipped` | Read as integers / `bool`; meanings not checked | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 (read only) |

## IGearMateFeatureData (from `IFeature.GetDefinition` on a `MateGearDim`)

| Member | Purpose | Status |
|---|---|---|
| `GearRatioNumerator` / `GearRatioDenominator` | The ratio as typed, as diameters in metres. The numerator belongs to `MateEntity(1)`, and neither is the picked geometry's size (GOTCHAS §75) | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 |
| `Reverse` | `bool`, the mate's Reverse box. What it does to direction was not checked | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 |

## IDistanceMateFeatureData (from `IFeature.GetDefinition` on a distance mate)

| Member | Purpose | Status |
|---|---|---|
| `Distance` | Metres, on both `MateDistanceDim` and `MateLimitDistanceDim` | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 |
| `MateAlignment` | Read **7602281** on a plain distance mate whose `IMate2.Alignment` read 2; do not trust it there | Verified, SW 2024 SP5 (32.5.0), 2026-10-28 (value suspect) |
| `MaximumDistance` / `MinimumDistance` | Listed in the interop assembly; never read | Unverified |

## IMateEntity2 (from `IMate2.MateEntity(i)`)

| Member | Purpose | Status |
|---|---|---|
| `ReferenceComponent` | The component the entity is on; `None` for the assembly's own | Verified, SW 2024 SP5 (32.5.0), 2026-10-06 |
| `ReferenceType2` | 1 edge, 2 face, 24 sketch point were seen. Also 11 (`swSelSKETCHPOINTS`, the assembly's own origin) and 25 (`swSelEXTSKETCHPOINTS`, a sub-assembly's) on coordinate mates; values are `swSelectType_e` ([assemblies/06](entries/assemblies/06-read-every-mate.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-06 and 2026-10-28 |
| `EntityParams` | In **assembly** coordinates, metres: items 0–2 a point on the axis, 3–5 the direction, 6 the radius (0 for a sketch point). Matched `CylinderParams` of the same face carried through the component's `Transform2` | Verified, SW 2024 SP5 (32.5.0), 2026-10-06 |

## IInterferenceDetectionMgr (from `IAssemblyDoc.InterferenceDetectionManager`)

| Member | Purpose | Status |
|---|---|---|
| `IAssemblyDoc.InterferenceDetectionManager` | The manager, a property get | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `TreatCoincidenceAsInterference`, `TreatSubAssembliesAsComponents`, `IncludeMultibodyPartInterferences`, `MakeInterferingPartsTransparent`, `CreateFastenersFolder`, `IgnoreHiddenBodies`, `ShowIgnoredInterferences`, `UseTransform` | Options, each put before counting (False, True, True, False, False, True, False, False); each put returned `None`. Their effects were not varied | Put verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `GetInterferenceCount` | How many interferences. **A property get** under late binding, despite the name | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `GetInterferences` | The interferences, a tuple. **A property get** | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `Done` | Release the manager. **A method**: call `manager.Done()`; attribute access fetches it without running it | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |
| `IInterference.Volume` | The interference's volume in **cubic metres**, a property get; matched a lens of two circles to 1e-3 | Verified, SW 2026 (34.0.0), dev runs 2026-09-15 |

## IDrawingDoc (a drawing, late-bound)

| Member | Purpose | Status |
|---|---|---|
| `ISldWorks.NewDocument(template.drwdot, 0, 0, 0)` | A drawing from a template; the template's sheet and format kept | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `Create3rdAngleViews2(modelPath)` | Front, top and right views of a saved model; `True`, view `Type` 7, 4, 4 | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `CreateDrawViewFromModelView3(path, "*Front", x, y, 0)` | One named model view at a sheet point, metres. **The first one rescales the sheet** (GOTCHAS §77) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `CreateUnfoldedViewAt3(x, y, 0, False)` | A projected view of the **selected** view toward the point; follows the sheet's projection angle | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetFirstView` / `IView.GetNextView` | The sheet first (`Type` 1), then the views | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetCurrentSheet` | The `ISheet` | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `EditTemplate` / `EditSheet` | Into and out of the sheet format, to change title-block notes; `EditSheet` before `InsertNote` makes a sheet note (GOTCHAS §79) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `CreateAutoBalloonOptions` / `AutoBalloon5(options)` | Balloon the selected view; returns the balloon notes. **Skips parts with no visible edge** (GOTCHAS §81) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `InsertCenterMark3(2, False, False)` | A centre mark on the selected circular edge; the mark, or `None` | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `InsertCenterLine2` | A centre line on the selected cylindrical face's axis | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IModelDoc2.InsertNote(text)` | A note, `"\n"` for line breaks. **Goes into the active view** (GOTCHAS §79) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IModelDoc2.UnblankSketch` | Show the selected sketch; returns nothing, so call it ([drawings/07](entries/drawings/07-explode-lines-in-a-drawing-view.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IModelDoc2.EditDelete` | Delete the selection: notes, dimensions, centre marks, an alternate position view | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## ISheet (from `IDrawingDoc.GetCurrentSheet`)

| Member | Purpose | Status |
|---|---|---|
| `GetProperties2` | Item 2 and 3 the scale, 4 first-angle (`False` = third angle), 5 and 6 the size in metres | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SetScale(num, den, False, False)` | Set the sheet scale. Call it again after the first model view | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetTemplateName` | The sheet format file | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## IView (a drawing view)

| Member | Purpose | Status |
|---|---|---|
| `GetName2`, `Type`, `ScaleDecimal` | Name (`Drawing ViewN`, `Alternate PositionN`), type (1 sheet, 4 projected, 7 standard base, 10 alternate position were seen), scale | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `UseSheetScale = True` | Follow the sheet's scale | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetOutline` / `Position` (get and put) | Outline `[xmin, ymin, xmax, ymax]` and position `[x, y]`, sheet metres. `Position` is the outline's centre, not the model's (GOTCHAS §86) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ModelToViewTransform` | `ArrayData` items 9 and 10: the sheet position of the model origin, metres. Aligns two views by model | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SetDisplayMode4(False, 1, False, True, False)` | Hidden lines drawn dashed. Only these values run | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ReferencedConfiguration` (get and put) | The view's configuration. **The view can draw another configuration's pose** (GOTCHAS §82) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `InsertAlternateView(config)` | On the selected base view: an `Alternate PositionN` view in phantom lines, `Type` 10 ([drawings/06](entries/drawings/06-alternate-position-views.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ShowExploded(True)` / `IsExploded` | Show the assembly's exploded state in the view; `True` / `True` | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `InsertBomTable5(False, x, y, 2, 2, "", template, False, 0, False, False)` | A top-level BOM placed by its top-right corner. **Empty until `SetConfigurations`** (GOTCHAS §78) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetTableAnnotations` | Tables on the sheet (`Type` 2 = BOM); a property get | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetVisibleEntities2(null, 1)` / `(null, 3)` | The view's visible edges / faces | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SelectEntity(entity, False)` | Select an edge or face of the view, for a centre mark or line | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetFirstNote` / `INote.GetNext`, `GetNotes` | The view's notes (balloons included) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetFirstDisplayDimension5` / `IDisplayDimension.GetNext5` | The view's dimensions | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetFirstCenterMark` / `ICenterMark.GetNext` | The view's centre marks | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `RootDrawingComponent` | Its `Name` is the root component with instance suffix, for selecting through the view | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## INote, IAnnotation and ICenterMark (drawing annotations)

| Member | Purpose | Status |
|---|---|---|
| `INote.GetText` / `SetText(text)` | Read (line breaks as `"\r\n"`) and replace a note's text, title-block notes included | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `INote.GetName` | `DetailItemNN`, for `SelectByID2` | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `INote.GetExtent` / `GetTextPoint2` | Its box `[xmin, ymin, z, xmax, ymax, z]` / text point, sheet metres | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `INote.IsBomBalloon` | `True` for a BOM balloon; its text is the item number | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IAnnotation.SetPosition2(x, y, 0)` / `GetPosition` | Move a note or balloon, sheet metres (top-left for a note) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IAnnotation.GetLeaderCount` | 1 for a note with a leader, 0 without | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IAnnotation.Select3(True, null)` | **Selected nothing that `EditDelete` removed**, on nine balloons (GOTCHAS §80) | Verified not to work, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ICenterMark.GetPosition(0)` | Its centre in **model** metres, not sheet | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ICenterMark.Select(True, null)` | Select it; `EditDelete` then removed it | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## IDisplayDimension and IDimensionTolerance (on a drawing)

| Member | Purpose | Status |
|---|---|---|
| `GetDimension2(0)` → `IDimension.SystemValue` | The value, metres | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IDimension.Tolerance.Type` | 2 bilateral, 4 symmetric, 8 fit with tolerance were seen, each matching what the sheet showed | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IDimensionTolerance.GetHoleFitValue` / `GetShaftFitValue` | `'H9'`, `'h6'`, `''` for the side not set | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IDimensionTolerance.GetMaxValue` / `GetMinValue` | ± and +/− tolerance limits, metres | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ShowParenthesis` | `True` on a reference dimension | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetText(1)` / `GetText(2)` | Prefix / suffix (`'2X <MOD-DIAM>'`, `' THRU'`) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SetText(1, text)` | Replace the prefix; read back | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SetPrecision3(primary, dual, tol, dualTol)` | Decimal places, `-1` to leave a field; returned 0 and the display changed | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `Type2` | 6 on a diameter dimension SolidWorks put on an arc | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `IDimensionTolerance.SetType`, `SetFitValues`; `IDisplayDimension.Diametric` | Written, never reached ([drawings/05](entries/drawings/05-read-and-tidy-dimensions.md)) | Unverified |

## IBomFeature and the table (from `IBomTableAnnotation.BomFeature`)

| Member | Purpose | Status |
|---|---|---|
| `GetConfigurationCount(visibleOnly)` | 0 visible, 1 available straight after `InsertBomTable5` | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `GetConfigurations(False, out visible)` | Names and visible flags | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `SetConfigurations(True, bool[], names[])` | Show those configurations; the table filled (GOTCHAS §78) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `FollowAssemblyOrder2 = True` | Item numbers in tree order; balloons renumber with it | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `TableType` | 2 | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ITableAnnotation.RowCount` / `ColumnCount` / `Text(r, c)` | Read the table | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## ICustomPropertyManager (from `IModelDocExtension.CustomPropertyManager`)

| Member | Purpose | Status |
|---|---|---|
| `Add3("Description", 30, text, 2)` | Add or replace a text property; returned 0. A BOM's DESCRIPTION column showed it after the part was saved and the drawing rebuilt | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## Explode lines (assembly)

| Member | Purpose | Status |
|---|---|---|
| `ISketchManager.InsertExplodeLineSketch` | Open (and close) the explode-line sketch for editing. **Segments deleted inside it came back on rebuild** (GOTCHAS §84) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ISldWorks.RunCommand(3307, "")` | Tried to dissolve smart explode lines; returned `False`, nothing changed. The ID's meaning not established | Verified not to work, SW 2024 SP5 (32.5.0), 2026-10-30 |

## Body faces (from `IPartDoc.GetBodies2` and `IBody2.GetFaces`)

| Member | Purpose | Status |
|---|---|---|
| `IFace2.GetBox` | The face's box, metres; **a property get** (`f.GetBox()` raised `'tuple' object is not callable`) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ISurface.PlaneParams` | Normal (0–2), a point (3–5), metres; an axis-aligned face's offset is item `3 + axis` ([reading/15](entries/reading/15-measure-a-part-that-has-no-dimensions.md)) | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |
| `ISurface.Identity` | 4004 and 4005 on fillet corner blends | Verified, SW 2024 SP5 (32.5.0), 2026-10-30 |

## Constants used

Relations, passed to `AddConstraint` as strings:
`sgCOINCIDENT`, `sgMIDPOINT`, `sgHORIZONTAL2D`, `sgVERTICAL2D`, `sgPARALLEL`,
`sgPERPENDICULAR`, `sgTANGENT`, `sgEQUAL`, `sgCONCENTRIC`, `sgCOLINEAR`,
`sgSYMMETRIC`, `sgFIXED`. Verified by geometry on SW 2026 (34.0.0) through
`IModelDoc2.SketchAddConstraints`: `sgCOINCIDENT`, `sgHORIZONTAL2D`,
`sgTANGENT`, `sgSYMMETRIC`, `sgFIXED`, `sgVERTICAL2D`, and `sgSAMELENGTH`, which made two
circles' radii equal where **`sgEQUAL` did nothing**. See
[sketches/03](entries/sketches/03-relation-constants.md).

Dimension types (`swDimensionType_e`):
`swDistanceDim`, `swRadiusDim`, `swDiameterDim`, `swAngularDim`.

Selection type strings for `SelectByID2`: `REFERENCECURVES` for a reference
curve; `PLANE` for a reference plane by its tree name; `AXIS` for a reference
axis; `EXTSKETCHSEGMENT` for a closed sketch's line as `LineN@Sketch`;
`EXTSKETCHPOINT` for a sketch point by location, or a component's origin as
`Point1@Origin@<component>@<assembly>`; `SURFACEBODY` for a body by its own `IBody2.Name`,
which is how a knit's inputs are picked (Airfoil Converter 2026-09-22).

Selection marks that decided a feature, SW 2026: loft profiles 1 and loft
guides 2 (Airfoil Converter 2026-09-16); and on 34.0.0: revolve axis line
16; `InsertRefPlane` references 0 and 1 in order; loft sections all 1;
mates 1; circular pattern axis 1 and seed 4; sweep profile 1 and path 4;
mirror body 256 and plane 2 (body at mark 1 mirrored nothing).

Feature type names from `GetTypeName2`: `CurveInFile`, `CompositeCurve`,
`FtrFolder`, `RefPlane`. From the 2026-09-22 work: `SewRefSurface` (a knit),
`PlanarSurface` (a planar cap), `FillRefSurface` (a filled surface), and, on
the real part walked there, `Split`, `CombineBodies`, `NetBlend` and `ICE`.
Also seen on SW 2026 (34.0.0): `RefAxis`,
`ProfileFeature` (a sketch), `OriginProfileFeature`, `Extrusion`, `ICE` (a cut),
`CirPattern`, `EqnFolder`, `MaterialFolder`, `MateGroup`, `MateCoincident`,
`MateDistanceDim`, `MatePlanarAngleDim`, and from the dev runs of 2026-09-15
`Revolution`, `BlendCut`, `MoveCopyBody`, and from the helical runs `Sweep`,
`SweepCut`, `MirrorSolid`. Mates on SW 2024 SP5 (32.5.0): `MateConcentric`,
`MateCoincident`, **`MateGearDim`** (a gear mate), `MateCoordinate`,
`MateLimitDistanceDim`, and `FtrFolder` for folder markers under `MateGroup`.

`swMateType_e` (read from `swconst.dll`): `0` coincident, `1` concentric,
`5` distance, `10` gear, `20` coordinate. `swSelectType_e`: `1` edges, `2`
faces, `11` sketch points, `25` external sketch points.

`swGuideCurveInfluence_e`: `0` to next guide (run, and the hand-made loft's
value), `1` to next sharp, `2` to next edge, `3` global (read only).
`swFeatureSuppressionAction_e`: `0` suppress, `1` unsuppress.
`swMoveRollbackBarTo_e`, read off this machine's `swconst.tlb` on 2026-09-22:
`1` End, `2` PreviousPosition (answers `True` and moves nothing on SW 2026),
`3` BeforeFeature, `4` AfterFeature. `swBodyType_e`: `0` solid, `1` sheet.
`swInConfigurationOpts_e`: `1` this configuration. `swSaveAsOptions_e`: `1`
silent, `2` copy. All read from `swconst.tlb` on SW 2026 by the Airfoil
Converter; only the influence `0` and the save-as options are behind a
recorded run.

Loft type names the Airfoil Converter uses to find its lofts: `Blend` (solid)
and `BlendRefSurface` (surface). They are the tool's constants; no recorded run
printed them.

`swTwistControlType_e`: `8` constant twist along path. `swRefPlaneReferenceConstraints_e`:
`8` distance, `256` its flip option, `2` perpendicular, `4` coincident. All read from
`swconst.tlb` and used in calls that ran on SW 2026 (34.0.0).

`swFeatureTreeFolderType_e` for `InsertFeatureTreeFolder2`, by observed
behaviour rather than by header: `2` wraps the selection in a new folder, `1`
makes a folder that does not contain it, `3` hands back the existing
`Surface Bodies` folder, `0` does nothing. See
[curves/11](entries/curves/11-feature-tree-folders.md).

A folder in the feature walk is two features: the folder, and a closing
sentinel after everything it holds. The sentinel is usually named
`FolderN___EndTag___` after the folder's first automatic name, but after a part
is reopened it can be `FolderN___EndTag___0`; do not match it by name
([curves/11](entries/curves/11-feature-tree-folders.md)).

Selection type numbers seen from `GetSelectedObjectType3`: 1 edge, 2 face,
3 vertex, 4 plane, 5 axis, 6 reference point, 9 sketch, 24 sketch segment,
25 sketch point.

Neutral-format errors seen on a `.step`: `2097152` `swFileRequiresRepairError`
from `OpenDoc6`, `1024` `swInvalidFileTypeError` from `GetOpenDocSpec`. See
[files/01](entries/files/01-batch-convert-step.md).

`swDocumentTypes_e` from `IModelDoc2.GetType`: `1` part and `2` assembly, both
observed. The enum also defines a drawing value; it was not seen here.

File versions, from `VersionHistory` and `GetLatestSupportedFileVersion`:
`11000` is 2018, `14000` is 2021, `19000` is 2026. These are **not** the
`RevisionNumber` majors above, where 34 is 2026. Two schemes for one release;
do not compare across them.

Numbers read from `swconst.tlb` on SW 2026 with makepy and used in calls that
ran (the behaviour is verified, the member names are the type library's):
`swDocumentTypes_e` 1 part, 2 assembly; `swUserPreferenceIntegerValue_e`
47 `swUnitsLinear`, 263 `swUnitSystem`; `swLengthUnit_e` 0 mm, 3 inches;
`swUnitSystem_e` 5 MMGS; `swUserPreferenceToggle_e` 10 `swInputDimValOnCreate`;
`swEndConditions_e` 0 blind, 1 through all, 9 through all both;
`swSaveAsVersion_e` 0 current; `swSaveAsOptions_e` 1 silent;
`swMateType_e` 0 coincident, 5 distance, 6 angle; `swMateAlign_e` 2 closest;
`swConstrainedStatus_e` 2 under, 3 fully, 4 over (4 not observed);
`swRefPlaneReferenceConstraints_e` 2 perpendicular, 4 coincident.

Read from `swconst.chm` installed with SW 2024 ([connect/12](entries/connect/12-read-the-api-help-offline.md))
and used in calls that ran: `swDisplayStateOpts_e` 1 `swThisDisplayState`
(2 all, 3 specified, not used); `swViewDisplayMode_e` 5
`swViewDisplayMode_ShadedWithEdges`, observed (the enum runs 2 to 13).
