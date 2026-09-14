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
| `GetConfigurationNames(path)` | Configuration names of a **closed** file. The `IModelDoc2` member of the same name reads an open one | Verified, SW 2026 SP1.1 |
| `VersionHistory(path)` | Saved-version list of a **closed** file, `11000[2018/134] \| 14000[2021/85]` | Verified, SW 2026 SP1.1 |
| `GetLatestSupportedFileVersion` | File version this instance writes. Not the `RevisionNumber` major | Verified, SW 2026 SP1.1 |
| `GetDocumentTemplate(type, "", 0, 0, 0)` | Default template path; 1 part, 2 assembly. **Returned the 2025 folder's inch template on a 2026 session** | Verified, SW 2026 (34.0.0) |
| `GetUserPreferenceStringValue(8 / 9)` | Default part / assembly template; same paths as above | Verified, SW 2026 (34.0.0) |
| `NewDocument(template, 0, 0, 0)` | New document from a template; returns it, and it is active | Verified, SW 2026 (34.0.0) |
| `CloseDoc(title)` | Close by title. **Discards unsaved changes without a prompt** | Verified, SW 2026 (34.0.0) |
| `ActivateDoc3(title, False, 0, out long)` | Bring an open document to the front; returned the document, not a tuple | Verified, SW 2026 (34.0.0) |
| `GetUserPreferenceToggle` / `SetUserPreferenceToggle(10, bool)` | `swInputDimValOnCreate`: turn off the modal value dialog around `AddDimension2`, then restore | Verified, SW 2026 (34.0.0) |

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
| `ClearSelection2(all)` | Empty the selection | Verified |
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
| `SaveAs3(path, 0, 1)` | Save as. **Returned `0` while the file was written**; see the extension's form | Verified, SW 2026 (34.0.0) |
| `GetEquationMgr` | The Equation Manager, below | Verified, SW 2026 (34.0.0) |
| `SketchAddConstraints(constant)` | Add a relation to the selected sketch entities, e.g. `"sgTANGENT"` | Verified, SW 2026 (34.0.0) |
| `AddDimension2(x, y, z)` | Dimension the selection, text at a point in metres; returns an `IDisplayDimension` | Verified, SW 2026 (34.0.0) |
| `InsertAxis2(True)` | Reference axis from two selected planes; returns `True`, not the axis | Verified, SW 2026 (34.0.0) |

## IModelDocExtension

| Member | Purpose | Status |
|---|---|---|
| `SelectByID2` | Select a named entity by type string, with a selection mark. `"PLANE"` with a plane's name read from the tree selected it | Verified, SW 2026 |
| `AddDimension2(x, y, z)` | Add a dimension at a placement point, in metres. The `IModelDoc2` member of the same name is the one that has run | Unverified |
| `GetPersistReference3(obj)` | A byte handle to a feature that survives renaming | Verified, SW 2026 |
| `DeleteSelection2(options)` | Delete what is selected. On a folder it removes **only the folder**, leaving its contents in place | Verified, SW 2026 |
| `GetObjectByPersistReference3(ref, out)` | Resolve that handle back. Needs a by-ref out parameter | Verified, SW 2026 |
| `SaveAs(path, version, options, exportData, ByRef err, ByRef warn)` | Save under a new name, with an error and a warning code back. Prefer over `SaveAs3` | Verified, SW 2026 SP1.1 |
| `ListExternalFileReferences` | External references of a document | Unverified |
| `GetUserPreferenceInteger(pref, 0)` | Read a document setting, e.g. 47 `swUnitsLinear` (0 mm, 3 inches) | Verified, SW 2026 (34.0.0) |
| `SetUserPreferenceInteger(263, 0, 5)` | Set the document's unit system to MMGS; returned `True` and took | Verified, SW 2026 (34.0.0) |
| `SaveAs3(path, 0, 1, null, null, out, out)` | Save as, silently. Returned plain `True`, not a tuple. **Overwrites an existing file** | Verified, SW 2026 (34.0.0) |
| `CreateMassProperty` | An `IMassProperty` for the document | Verified, SW 2026 (34.0.0) |

## IConfigurationManager (from `IModelDoc2.ConfigurationManager`)

| Member | Purpose | Status |
|---|---|---|
| `ActiveConfiguration` | The active `IConfiguration`. Read its `Name` rather than trusting `ShowConfiguration2` | Verified, SW 2026 SP1.1 |

## IConfiguration

| Member | Purpose | Status |
|---|---|---|
| `Name` | Configuration name | Verified, SW 2026 SP1.1 |
| `Description` | Its description. Can throw; wrap it | Verified, SW 2026 SP1.1 |
| `GetParent` | Non-null means this configuration is derived from that one | Verified, SW 2026 SP1.1 |

## IPartDoc (a part, cast from IModelDoc2)

| Member | Purpose | Status |
|---|---|---|
| `GetPartBox(useSystemUnits)` | Axis-aligned bounding box, `x1,y1,z1,x2,y2,z2`. **`true` is metres, `false` converts to the document's units** | Verified, SW 2026 SP1.1 |

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
| `EnableFeatureTree` | Whether the tree is live; read `True` while the above failed | Verified, SW 2026 |
| `FeatureExtrusion3` (23 arguments) | Blind boss from the selected sketch, depth in metres; returns the feature | Verified, SW 2026 (34.0.0) |
| `FeatureCut4` (27 arguments) | Cut from the selected sketch. **Through all in the default direction returned `None`**; both directions (end condition 9) cut | Verified, SW 2026 (34.0.0) |
| `FeatureCircularPattern5` (14 arguments) | Pattern the feature at mark 4 about the axis at mark 1; two `"NULL"` strings | Verified, SW 2026 (34.0.0) |
| `FeatureCut3` | Listed by the probe, never called | Unverified |
| `FeatureCircularPattern4` | Listed by the probe, never called | Unverified |

## IFeatureFolder (from `GetSpecificFeature2` on an `FtrFolder`)

| Member | Purpose | Status |
|---|---|---|
| `GetFeatureCount` | How many features the folder holds. **Counts a nested folder's end tag** | Verified, SW 2026 |
| `GetFeatures` | The features it holds, end tag included | Verified, SW 2026 |

## IFeature

| Member | Purpose | Status |
|---|---|---|
| `Name` | Read and write. **Read it back after writing** | Verified |
| `GetNextFeature` | Next sibling in the tree | Verified |
| `GetTypeName2` | Type string, e.g. `"CurveInFile"`, `"CompositeCurve"` | Verified |
| `GetDefinition` | Feature data you can modify | Verified |
| `ModifyDefinition(data, doc, component)` | Commit modified feature data | Verified, SW 2026 |
| `GetSpecificFeature2` | The concrete feature behind a reference plane; on an `FtrFolder`, the `IFeatureFolder`; on a sketch feature, the `ISketch` | Verified |
| `Select2(append, mark)` | Select this feature. Works where `SelectByID2` with `"BODYFEATURE"` returned `False`. Judge by the selection count | Verified, SW 2026 |
| `ListExternalFileReferences2` | External references of one feature | Unverified |
| `GetFirstDisplayDimension` / `GetNextDisplayDimension(prev)` | Walk a feature's dimensions; includes its sketch's | Verified, SW 2026 (34.0.0) |
| `GetFirstSubFeature` / `GetNextSubFeature` | Children, e.g. the mates under `MateGroup`, or the sketch a feature was made from. **Step children with `GetNextSubFeature`**: `GetNextFeature` from a child walked on through the main tree and repeated names | Verified, SW 2026 (34.0.0) |

## Curve feature data (from `GetDefinition` on a CurveInFile)

| Member | Purpose | Status |
|---|---|---|
| `LoadPointsFromFile(path)` | Replace the points from a file, in place | Verified, SW 2026 |
| `PointArray` | The points it holds, flat, in metres | Verified, SW 2026 |

## ISketchManager

| Member | Purpose | Status |
|---|---|---|
| `InsertSketch(rebuild)` | Open a 2D sketch on the current selection; call again to close | Verified, SW 2026 (34.0.0) |
| `Insert3DSketch2(rebuild)` | Open a 3D sketch; call again to close | Unverified |
| `ActiveSketch` | The sketch being edited, or `Nothing` after closing | Verified, SW 2026 (34.0.0) |
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

## ISelectionMgr

| Member | Purpose | Status |
|---|---|---|
| `GetSelectedObjectCount2(mark)` | How many things are selected. `-1` = any mark | Verified |
| `GetSelectedObject6(index, mark)` | The selected object, 1-based | Verified |
| `GetSelectedObjectType3(index, mark)` | A `swSelectType_e` number | Verified |
| `GetSelectedObjectsSketch(index)` | The sketch a selected entity belongs to | Verified |

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

## IMassProperty (from `IModelDocExtension.CreateMassProperty`)

| Member | Purpose | Status |
|---|---|---|
| `Volume` | The part's volume in **cubic metres**; matched π r² w to 16 significant figures | Verified, SW 2026 (34.0.0) |

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
| `AddMate5(type, 2, False, d, d, d, 1, 1, a, a, a, False, False, 0, out long)` | Mate the two entities at mark 1; returned the mate, not a tuple. An angle mate's `D1` (radians) linked to a global was accepted | Verified, SW 2026 (34.0.0) |
| `FixComponent()` | Fix the selected component; returns `None`. A free component's `IsFixed` went `False` → `True`. **Call it with parentheses**: reached by attribute access it came back as a Python `method` and never ran | Verified, SW 2026 (34.0.0), rerun 20260914-183429 |

## IComponent2

| Member | Purpose | Status |
|---|---|---|
| `Name2` | Component name, `<file stem>-1` | Verified, SW 2026 (34.0.0) |
| `IsFixed` | `True` for the first component straight after insertion | Verified, SW 2026 (34.0.0) |
| `Select4(False, null, False)` | Select the component | Verified, SW 2026 (34.0.0) |
| `Transform2` | Its placement; see Geometry interrogation | Verified, SW 2026 (34.0.0) |
| `FeatureByName(name)` | A feature of the component, to select for a mate. Called; whether its result or the `SelectByID2` fallback made the selection was not recorded | Partly verified, SW 2026 (34.0.0) |

## Constants used

Relations, passed to `AddConstraint` as strings:
`sgCOINCIDENT`, `sgMIDPOINT`, `sgHORIZONTAL2D`, `sgVERTICAL2D`, `sgPARALLEL`,
`sgPERPENDICULAR`, `sgTANGENT`, `sgEQUAL`, `sgCONCENTRIC`, `sgCOLINEAR`,
`sgSYMMETRIC`, `sgFIXED`. Verified by geometry on SW 2026 (34.0.0) through
`IModelDoc2.SketchAddConstraints`: `sgCOINCIDENT`, `sgHORIZONTAL2D`,
`sgTANGENT`, `sgSYMMETRIC`, `sgFIXED`, and `sgSAMELENGTH`, which made two
circles' radii equal where **`sgEQUAL` did nothing**. See
[sketches/03](entries/sketches/03-relation-constants.md).

Dimension types (`swDimensionType_e`):
`swDistanceDim`, `swRadiusDim`, `swDiameterDim`, `swAngularDim`.

Selection type strings for `SelectByID2`: `REFERENCECURVES` for a reference
curve; `PLANE` for a reference plane by its tree name.

Feature type names from `GetTypeName2`: `CurveInFile`, `CompositeCurve`,
`FtrFolder`, `RefPlane`. Also seen on SW 2026 (34.0.0): `RefAxis`,
`ProfileFeature` (a sketch), `OriginProfileFeature`, `Extrusion`, `ICE` (a cut),
`CirPattern`, `EqnFolder`, `MaterialFolder`, `MateGroup`, `MateCoincident`,
`MateDistanceDim`, `MatePlanarAngleDim`.

`swFeatureTreeFolderType_e` for `InsertFeatureTreeFolder2`, by observed
behaviour rather than by header: `2` wraps the selection in a new folder, `1`
makes a folder that does not contain it, `3` hands back the existing
`Surface Bodies` folder, `0` does nothing. See
[curves/11](entries/curves/11-feature-tree-folders.md).

A folder in the feature walk is two features: the folder, and a closing
sentinel named `<folder>___EndTag___` after everything it holds.

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
`swConstrainedStatus_e` 2 under, 3 fully, 4 over (4 not observed).
