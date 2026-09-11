# API ledger

Every SolidWorks API member used anywhere in this collection, what interface it
lives on, and whether it has been observed working. Sorted by interface.

**Verified** = run against a real SolidWorks and the result observed.
**Unverified** = well-formed against the documented surface, never executed.

## ISldWorks (the application)

| Member | Purpose | Status |
|---|---|---|
| `RevisionNumber` | Version string, e.g. `"34.0.0"`. Major 32 = 2024, 33 = 2025, 34 = 2026 | Verified, SW 2024/2025/2026 |
| `ActiveDoc` | The document on screen, or `Nothing` | Verified |
| `GetFirstDocument` | First of the open documents, walked with `GetNext` | Verified |
| `Visible` | Show or hide a launched instance | Verified |
| `CommandInProgress` | Set `True` to suppress rebuilds during a batch | Verified, SW 2026 |
| `ExitApp` | Close an instance you launched | Unverified |
| `GetDocumentDependencies2` | References of a **closed** file, without opening it | Verified |
| `ReplaceReferencedDocument` | Repoint a reference after a move | Unverified |
| `GetOpenDocumentByName` | Check whether a file is open before touching it | Unverified |

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
| `Extension` | `IModelDocExtension`, below | Verified |

## IModelDocExtension

| Member | Purpose | Status |
|---|---|---|
| `SelectByID2` | Select a named entity by type string, with a selection mark | Verified, SW 2026 |
| `AddDimension2(x, y, z)` | Add a dimension at a placement point, in metres | Unverified |
| `GetPersistReference3(obj)` | A byte handle to a feature that survives renaming | Verified, SW 2026 |
| `GetObjectByPersistReference3(ref, out)` | Resolve that handle back. Needs a by-ref out parameter | Verified, SW 2026 |
| `ListExternalFileReferences` | External references of a document | Unverified |

## IFeature

| Member | Purpose | Status |
|---|---|---|
| `Name` | Read and write. **Read it back after writing** | Verified |
| `GetNextFeature` | Next sibling in the tree | Verified |
| `GetTypeName2` | Type string, e.g. `"CurveInFile"`, `"CompositeCurve"` | Verified |
| `GetDefinition` | Feature data you can modify | Verified |
| `ModifyDefinition(data, doc, component)` | Commit modified feature data | Verified, SW 2026 |
| `GetSpecificFeature2` | The concrete feature behind a reference plane | Verified |
| `ListExternalFileReferences2` | External references of one feature | Unverified |

## Curve feature data (from `GetDefinition` on a CurveInFile)

| Member | Purpose | Status |
|---|---|---|
| `LoadPointsFromFile(path)` | Replace the points from a file, in place | Verified, SW 2026 |
| `PointArray` | The points it holds, flat, in metres | Verified, SW 2026 |

## ISketchManager

| Member | Purpose | Status |
|---|---|---|
| `InsertSketch(rebuild)` | Open a 2D sketch on the current selection; call again to close | Unverified |
| `Insert3DSketch2(rebuild)` | Open a 3D sketch; call again to close | Unverified |
| `ActiveSketch` | The sketch being edited, or `Nothing` | Partly verified |
| `CreatePoint(x, y, z)` | A sketch point, in metres | Unverified |
| `CreateLine2(x1, y1, z1, x2, y2, z2)` | A line, in metres | Unverified |
| `CreateArc(cx, cy, cz, sx, sy, sz, ex, ey, ez, dir)` | An arc | Unverified |
| `CreateCircleByRadius(cx, cy, cz, r)` | A circle | Unverified |
| `CreateSpline(pointArray)` | A spline through a flat array of triples | Unverified |
| `AddConstraint(name)` | Add a relation to the current selection | Unverified |

## ISketchSegment / ISketchPoint

| Member | Purpose | Status |
|---|---|---|
| `GetStartPoint2` / `GetEndPoint2` | A segment's endpoints, as selectable objects | Unverified |
| `GetCenterPoint2` | An arc or circle centre | Unverified |
| `ConstructionGeometry` | Toggle construction. Availability on a bare point varies by version | Unverified |
| `Select4(append, data)` | Add to the selection | Unverified |
| `X` / `Y` / `Z` | A sketch point's coordinates, in **sketch** space | Verified |

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

## IDimension / IDisplayDimension

| Member | Purpose | Status |
|---|---|---|
| `Dimension` | The `IDimension` behind a display dimension | Unverified |
| `Dimension.Name` | Rename it, e.g. `"D1"` | Unverified |
| `Dimension.SystemValue` | Set the value in **metres**, or radians for an angle | Unverified |

## Constants used

Relations, passed to `AddConstraint` as strings:
`sgCOINCIDENT`, `sgMIDPOINT`, `sgHORIZONTAL2D`, `sgVERTICAL2D`, `sgPARALLEL`,
`sgPERPENDICULAR`, `sgTANGENT`, `sgEQUAL`, `sgCONCENTRIC`, `sgCOLINEAR`,
`sgSYMMETRIC`, `sgFIXED`.

Dimension types (`swDimensionType_e`):
`swDistanceDim`, `swRadiusDim`, `swDiameterDim`, `swAngularDim`.

Selection type strings for `SelectByID2`: `REFERENCECURVES` for a reference curve.

Feature type names from `GetTypeName2`: `CurveInFile`, `CompositeCurve`.

Selection type numbers seen from `GetSelectedObjectType3`: 1 edge, 2 face,
3 vertex, 4 plane, 5 axis, 6 reference point, 9 sketch, 24 sketch segment,
25 sketch point.
