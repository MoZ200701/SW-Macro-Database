# Gotchas

Things that cost somebody an afternoon. Read this before writing a new macro.

## 1. The unversioned ProgID can point at the wrong SolidWorks

`GetObject(, "SldWorks.Application")` failed with error **429, ActiveX
component can't create object**, on a machine with SolidWorks visibly open.

It was not a permissions problem and not a sandboxing problem. Several major
versions were installed, and the registry looked like this:

| ProgID | CLSID | Version |
|---|---|---|
| `SldWorks.Application` | `{afbec3b2-…}` | 32 (SW 2024) |
| `SldWorks.Application.32` | `{afbec3b2-…}` | 32 (SW 2024) |
| `SldWorks.Application.34` | `{666aaee2-…}` | 34 (SW 2026) |

The unversioned ProgID resolved to the 2024 class id, so the Running Object
Table lookup searched for a class that was not running, while the live 2026
session sat registered under the other one.

**Two fixes, both in this repo.** Ask the registry which
`SldWorks.Application.*` ProgIDs exist and try them newest first, or skip
ProgIDs entirely and walk the Running Object Table matching on the moniker
name `SolidWorks_PID_<pid>`, which is the same across versions. See
[connect/01](entries/connect/01-attach-from-vbscript.md) and
[connect/02](entries/connect/02-attach-from-python.md).

**The general lesson:** a COM failure that looks like an environment or
elevation problem can just be a ProgID resolving to the wrong install.

## 2. The API is in metres, always

The file format takes a `mm` suffix and honours it. The API does not take
units at all: everything is metres, whatever the document is set to.

Recording the import dialog proved both halves at once. A file written as
`-50.000000mm` came back as:

```vb
boolstatus = Part.InsertCurveFilePoint(0, 0, -0.05)
```

Radians for angles, on the same principle. See
[sketches/05](entries/sketches/05-units-and-number-format.md).

## 3. `InsertCurveFile` is on the document, not the feature manager

Calling it on `IFeatureManager` raises **error 438, object doesn't support
this property or method**, which reads like the method does not exist. It
does. It is on `IModelDoc2`.

## 4. A bare `None` is a type mismatch for an absent COM object

`ModifyDefinition(data, doc, component)` takes a component, which a part does
not have. In Python, passing `None` fails. It needs a typed null:

```python
VARIANT(pythoncom.VT_DISPATCH, None)
```

`GetObjectByPersistReference3` has the same shape of problem in the other
direction: an out parameter that must be supplied as a by-reference integer,
`VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)`. Both are silent until they
are not. See [connect/02](entries/connect/02-attach-from-python.md).

## 5. In late-bound COM, a zero-argument method is a property get

In Python with `win32com.client.dynamic`, `RevisionNumber` comes back as a
string just by attribute access, and `ActiveDoc` comes back as a document.
*Invoking* either raises "Member not found". Testing `callable()` does not save
you, because a member returning a document is itself callable.

The rule that works: call only members that take arguments.

```python
def call(obj, name, *args):
    member = getattr(obj, name)
    return member(*args) if args else member
```

**It has an exception.** A zero-argument member that *returns nothing* is not
invoked by attribute access: `IAssemblyDoc.FixComponent` came back as a Python
`method` object and never ran. Called with parentheses, `asm.FixComponent()`,
it ran and worked. So does a member returning an **array**: `IBody2.GetEdges`
and `IFace2.GetTessTriangles` both come back as uncalled methods. See §31 for
the first case and **§55 for the rule that covers all of them**.

## 6. The Running Object Table hands back an `IUnknown`

Late binding cannot ask it for type information until it has been asked for
its `IDispatch` face:

```python
_dynamic.Dispatch(raw.QueryInterface(pythoncom.IID_IDispatch))
```

## 7. `Marshal.GetActiveObject` does not exist on modern .NET

It was dropped after .NET Framework. On .NET 8 you P/Invoke `GetActiveObject`
from `oleaut32.dll` yourself, or walk the Running Object Table via
`GetRunningObjectTable` and `CreateBindCtx` from `ole32.dll`. Both are in
[`code/csharp/ComActivation.cs`](code/csharp/ComActivation.cs).

## 8. SolidWorks quietly renames on collision

Set `feature.Name = "S9"` when an `S9` already exists and you get `S91` or
similar, with no error. A record holding a name that does not exist is a
silent failure. **Always read the name back:**

```python
feature.Name = new
return str(call(feature, "Name"))
```

## 9. Rebuilding per change is both slow and wrong

Update twelve curves with the rebuild live and you get a real but transient
error partway through, because mid-refresh some curves carry the old geometry
and some the new, so a surface over them genuinely does not close. Suppress,
change everything, restore, rebuild once.

**Restore the flag in a `finally`.** A document left suppressed looks fine and
silently stops updating, which is about the worst state to hand back to
someone. See [curves/08](entries/curves/08-rebuild-once-at-the-end.md).

## 10. A transform's rotation is stored by columns

`IMathTransform.ArrayData` is sixteen doubles: nine of rotation, three of
translation, then a scale. The first three are where the local X axis ends up,
the next three the local Y, the next three the local Z.

Reading them as rows transposes the rotation. This is **silent on the Front
plane**, whose transform is the identity, and wrong everywhere else. The
symptom was the Top plane reporting a normal of −Y and the Right plane −X. See
[reading/05](entries/reading/05-sketch-to-model-transform.md).

## 11. `InsertCurveFile` tells you True or False and nothing else

It does not hand back the feature it made. Diff the feature names either side
of the call. If the diff is not exactly one name, stop rather than guess.

## 12. Composite Curve reads its inputs from selection mark 1

At mark 0, `InsertCompositeCurve` returns `False` and says nothing about why.
See [curves/06](entries/curves/06-composite-curve.md).

## 13. A point-streamed curve has no file association

If you created a curve with `InsertCurveFileBegin`/`Point`/`End` rather than
from a file, it carries no path, so the 2025 Reload button has nothing to
reload from. It can still be refreshed with `LoadPointsFromFile` afterwards,
which was tested and works, but the dialog route is closed to it.

## 14. Inserting a curve does not add it to your surface

`InsertCurveFile` creates the curve and stops. Adding it to a boundary surface
or loft is still a human job. (Making a *new* loft over curves from code is
not: [features/12](entries/features/12-guided-loft.md), since 2026-09-16.
Adding a curve to a surface or loft that already exists still is.) An automatic insert therefore leaves a curve that
looks connected and is not, which is a quieter failure than a missing curve.
This is why insertion should be opt-in.

## 15. Never text-merge SolidWorks binaries

Part, assembly and drawing files are opaque binaries. In git, mark them binary
and track them with LFS. Conflicts get prevented with file locks, not resolved.

**LFS is not free, and generated parts blow through it.** Writing one file per
configuration turns a 9-part folder into 315 files, and a whole library into a
few thousand. That is ordinary storage but an expensive LFS quota. A nested
`.gitattributes` in the generated folder can opt those files back out to plain
blobs while the hand-built masters above it stay in LFS.
As of 2026 the container format also cannot be parsed for references directly,
and the Document Manager API that would do it is behind a subscription licence.
The regular API is the way in. See
[reading/01](entries/reading/01-dependencies-of-a-closed-file.md).

## 16. `ReplaceReferencedDocument` needs the referencing documents closed

Check with `GetOpenDocumentByName` and ask the user to close first, rather than
failing halfway through a batch.

## 17. `check-ignore` and other verifications that read what you control

Not a SolidWorks issue, but it bites automation work generally: a verification
that reads a surface the thing under test controls is not a verification. When
checking what is committed, read the committed object, not the working tree.

## 18. `MoveToFolder` does not move anything

`IFeatureManager.MoveToFolder` returned `False` and changed nothing on
SolidWorks 2026: on a curve, on a plane, on a folder, into an empty folder and
into a full one, with and without the feature selected first, before and after
a rebuild. There is no error and no exception, only the return value.

The only call that puts features in a folder is
`InsertFeatureTreeFolder2`, and it wraps the current selection in a **new**
folder. So an arrangement is rebuilt rather than patched. This is affordable
because deleting a folder deletes only the folder: everything it held stays
where it was. See [curves/11](entries/curves/11-feature-tree-folders.md).

## 19. A folder is two features in the tree

Walking `FirstFeature`/`GetNextFeature`, a folder appears as itself and again
as a closing tag after its contents, both typed `FtrFolder`. Nesting is the
depth between the two. Count the end tags or your contents will run on into
whatever follows the folder.

**Do not find the end tag by its name.** It is named after the folder's first,
automatic name — `Folder3___EndTag___`, whatever the folder is called now — and
after a renamed folder's part is reopened, SolidWorks reuses `Folder3` and names
the new tag `Folder3___EndTag___0`. A walk matching the `___EndTag___` suffix
(this file said to, until 2026-09-24) reads that tag as a folder opening, and
everything after it as inside. Both features answer `IFeatureFolder.GetFeatures`
with the same contents: the one that comes after its first item is the tag.
SolidWorks 2026 SP0.0.

Walk the top-level chain only. A walk that also descends into
`GetFirstSubFeature` drops absorbed features into the middle of a folder's
contents and the tags stop lining up.

## 20. `OpenDoc6` cannot open a STEP file that `LoadFile4` opens fine

On a vendor's STEP library, `ISldWorks.OpenDoc6` failed every file with error
**2097152, `swFileRequiresRepairError`**. It kept failing with import
diagnostics off, full entity check off, and several combinations of open
options. `ISldWorks.LoadFile4(path, "r", Nothing, err)` imported the same files
with `err = 0`.

`OpenDoc7` is not a way out: it rejects neutral formats before it starts.
`GetOpenDocSpec` on a `.step` returns `DocumentType = -1` and `Error = 1024`,
`swInvalidFileTypeError`. Renaming to `.stp` changes nothing.

The error number reads like the file is damaged. The files were fine.

This is about neutral formats. On native parts and assemblies `OpenDoc6` works
normally and is the call to use. See
[files/01](entries/files/01-batch-convert-step.md).

## 21. A late-bound host cannot call an API with a `ByRef Long` out-parameter

`OpenDoc6` and `LoadFile4` both take one. VBScript and PowerShell are
late-bound, cannot produce `VT_BYREF|VT_I4`, and fail with **"Type mismatch"
before the method runs** — so the failure looks like a bad argument list rather
than a host limitation, and no amount of rearranging the call fixes it.

VBA inside SolidWorks works, because it is in-process. C# compiled against the
interop assemblies works, because it is early-bound. Python gets there with an
explicit typed variant, `VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)`, as
in §4.

This is the reason a job may have to be a compiled exe rather than the `.vbs`
that everything else in this repo uses.

## 22. Opening an older file in a newer SolidWorks makes closing it dangerous

The version upgrade alone marks the document modified, so closing prompts
*"Save changes to ...?"* — under a **"SOLIDWORKS CAM Warning"** title, which
gives no clue what is at stake. Answering **yes rewrites the file in the new
format**, in place. For a read-only pass over someone's library that is data
loss with a friendly dialog in front of it.

`CloseAllDocuments` raises this prompt. `ISldWorks.CloseDoc(title)` discards
silently, which is what a batch wants.

## 23. `ShowConfiguration2` returns False when the configuration is already active

Activating a configuration that happens to be the active one returns **False**
having done nothing wrong. In a loop over every configuration in a part, that is
exactly one false failure per part: whichever configuration the file was last
saved in.

Read the name back instead of trusting the return:

```vb
swModel.ShowConfiguration2 cfg
If swModel.ConfigurationManager.ActiveConfiguration.Name <> cfg Then
```

Same shape as §8. A SolidWorks call's return value tells you less than the state
does.

Two more from the same area, both silent:

- **A design table owns its configurations.** With the table still in place,
  `DeleteConfiguration2` either fails or the configuration comes back on the
  next edit. `DeleteDesignTable` first.
- **Configurations delete in a cascade.** A derived configuration goes when its
  parent does, so a pass over a name list captured up front tries to delete
  names that are already gone. Re-read `GetConfigurationNames` each pass, and
  assert on what is left before saving.

See [files/02](entries/files/02-explode-configurations.md).

## 24. `GetPartBox` is the exception to §2

The API is in metres everywhere, except that `IPartDoc.GetPartBox` takes a
boolean that decides. `GetPartBox(true)` is metres. `GetPartBox(false)` converts
to **the document's own unit system**, so the same call returns different
numbers for two parts depending on how each file was set up.

Pass `true` and convert yourself, particularly when the result is a check. A
verification that silently depends on a setting in the file being verified is
not a verification — which is §17, arriving from a different direction.

See [reading/09](entries/reading/09-bounding-box.md).

## 25. The default template can be another year's, and in inches

On a SolidWorks 2026 session, `ISldWorks.GetDocumentTemplate(1, "", 0, 0, 0)`
returned `C:\ProgramData\SolidWorks\SOLIDWORKS 2025\templates\Part.prtdot`, and
a part made from it was in **inches** (`swUnitsLinear` 3). The assembly
template was the 2025 one too, also inches. The 2026 folder's own template was
millimetres.

It matters beyond dimensions typed by hand: **equations are in document
units.** A global of 30 linked to a dimension is 30 mm in a millimetre part and
30 inches in an inch one, and nothing errors either way. Read the new
document's units and set them:
`IModelDocExtension.SetUserPreferenceInteger(263, 0, 5)` (swUnitSystem, MMGS)
worked. See [documents/01](entries/documents/01-new-part-from-template.md).

## 26. The Equation Manager says no with -1, not an exception

Three calls returned `-1` on SolidWorks 2026 and did nothing:

- `IEquationMgr.Add3` on a part with one configuration, as its help warns. Use
  `Add2(-1, text, True)`.
- `IEquationMgr.SetEquationAndConfigurationOption` changing a global. An
  indexed property put of `Equation(i)` works; from Python it needs
  `Invoke` ([connect/02](entries/connect/02-attach-from-python.md)).
- `Add2` of a malformed equation, of one naming a global that does not exist,
  and of a dimension link added before the dimension exists. `Status` read `-1`
  after the first two (it was not read after the third); no dialog, nothing
  raised.

Check every return. See [equations/01](entries/equations/01-global-variables-from-code.md).

## 27. `sqr ( 0 )` is refused, and `IIF` does not guard it

`"x"= sqr ( 0 )` returned `-1`, and so did `IIF ( 1 > 1 , sqr ( 0 ) , 0 )`,
where the square root is on the branch that is never taken. So a formula like
`sqr ( ( a / b ) ^ 2 - 1 )` is refused whenever `a` equals `b`, however it is
wrapped. `0 ^ 0.5` evaluated to 0. Write square roots as `( ... ) ^ 0.5`. See
[equations/01](entries/equations/01-global-variables-from-code.md).

## 28. An unbracketed comparison binds before the arithmetic

`IIF ( 3 >= 1 + 5 , 1 , 0 )` evaluated to **1**; `IIF ( 3 >= ( 1 + 5 ) , 1 , 0 )`
to 0. The first is what `( 3 >= 1 ) + 5` gives. Bracket both sides of every
comparison in an equation. Unary minus is also looser than `^`: `-2 ^ 2` is -4.

## 29. An Equation Driven Curve's trig is radians, and a degree conversion is refused

`ISketchManager.CreateEquationSpline2` evaluates `cos ( t )` in radians, even
in a part whose Equation Manager evaluates `sin ( 90 )` as 1. The natural fix,
`cos ( t * 180 / pi )`, was refused in every spelling tried: the call returned
`None` and made no curve, with no error. Keep curve expressions in radians and
convert degree-valued globals in their own equations. Curve expressions are in
document units. See [sketches/07](entries/sketches/07-equation-driven-curve.md).

## 30. `sgEQUAL` did nothing to two circles

Two circles of radius 4 and 7 mm, both selected, `sgEQUAL`: no error, and the
radii were still 4 and 7. `sgSAMELENGTH` on the same selection made them both
4. A relation that silently does nothing leaves a sketch that looks related and
is not; check relations by where the geometry went. See
[sketches/03](entries/sketches/03-relation-constants.md).

## 31. A zero-argument method that returns nothing is not called by `call`

The late-bound rule in §5 — reach a zero-argument member by attribute access —
fetched `IAssemblyDoc.FixComponent` as a Python `method` object and never ran
it. Nothing raised. The check after it passed anyway, because the first
component of an assembly is already fixed when it is inserted, which is how
this went unnoticed in a passing probe. Call such members with explicit
parentheses and check their effect: on a rerun, `asm.FixComponent()` on a free
second component returned `None` and its `IsFixed` went from `False` to `True`
(SolidWorks 2026, revision 34.0.0, run 20260914-183429; see
[assemblies/01](entries/assemblies/01-new-assembly-and-insert-components.md)). That pywin32 exposes a zero-argument member
with a return value as a property and one without as a method is the likely
cause; it was not established. Two more of the family, and a test that catches
them without knowing the member, are in **§55**. See
[connect/02](entries/connect/02-attach-from-python.md).

## 32. A through-all cut in the default direction made nothing

`IFeatureManager.FeatureCut4`, through all, single direction, default
direction, from a sketch on the plane the boss was extruded from: returned
`None`, removed nothing. Flipped, or through all in both directions (end
condition 9), it cut the hole. Measure the volume before and after rather than
trusting that a cut cut. See [features/02](entries/features/02-cut-extrude.md)
and [reading/10](entries/reading/10-mass-properties-as-an-oracle.md).

## 33. Find a feature's dimension by its value, not by name or position

A circular pattern's dimensions came back as `[D3 = 2π, D1 = count]`: the
count second, the angle first. An extrusion's came back as its own depth `D1`
**and** its sketch's `OD`. Code that takes the first dimension, or assumes
`D1` is the one it wants, relies on an order and a naming nothing promises.
Match the value you asked for, refuse if more than one matches, then rename it.
See [features/04](entries/features/04-read-a-features-dimensions.md).

## 34. `SaveAs3` overwrites an existing file without a word

`IModelDocExtension.SaveAs3(path, 0, 1, null, null, out, out)` onto an existing
path returned `True` and replaced the file. With by-reference outs it returned a
plain `True`, not a tuple. `IModelDoc2.SaveAs3(path, 0, 1)` returned `0` while
the file was written. If overwriting would be wrong, check the path first. See
[documents/02](entries/documents/02-save-as-and-close.md).

## 35. An entity drawn starting on another's end shares that point

A line created starting exactly on a line's end, an arc's end, or an Equation
Driven Curve's end does not get a point of its own: there is one sketch point
there, and the line's `GetStartPoint2` compares equal to it. A coincident
relation between the two is already true, and trying to add it selects the same
point twice, leaving one object selected. Skip it. A line 0.1 mm away gets its
own point.

That second pick is where a careful selection helper goes wrong: one that judges
a selection by `GetSelectedObjectCount2` going up sees the count stay at 1 and
reports that the second entity could not be selected. Compare the two point
objects first (`a == b` was `True` for the shared point) and skip the relation
when they are the same. See [sketches/01](entries/sketches/01-2d-sketch-as-vba.md) and
[sketches/07](entries/sketches/07-equation-driven-curve.md).

## 36. A new sketch point once could not be selected, and it did not happen again

Observed once, cause unknown: an observation, not a rule. In a gear build on
SolidWorks 2026 (revision 34.0.0), the first relation in a freshly made part, a
circle's centre point to the origin, stopped the build with

```
od.centre could not be selected by Select4 or SelectByID2 (in Part249, with Part249 active and 0 selected).
```

By then the code had tried `ISketchPoint.Select4(False, null)` four times,
0.25 s apart, each judged by `ISelectionMgr.GetSelectedObjectCount2(-1)`, and
then `IModelDocExtension.SelectByID2("", "SKETCHPOINT", x, y, 0, ...)` at the
point's location. The part being built was the active document, and nothing was
selected. The session was shared with a person's own work: a user document that
was open when that build started had been closed by the time the next build
started. The next build, with the same code in the same session, went through.

Fourteen controlled trials did not reproduce it. Each made a new part, opened a
sketch on the first plane, drew a circle and selected its centre point: 8 in a
console process, rebuilds suppressed in every other trial, and 6 inside a Tk GUI
process through its COM worker thread, with rebuilds suppressed and the window
in front for three and withdrawn for three. Every one selected on the first
`Select4`, in 3 to 9 ms. A development run of a probe had earlier failed once
the same way on its first relation ("Entity 1 of 2 could not be selected by any
route"); the selection code it ran then is not recorded.

What held up: judge a selection by the count, retry briefly, fall back to a
second route, and when every route fails stop with a message naming the
document, the active document and the selection count. That message is what
ruled out the wrong document being active. The console trial script is
[`code/python/gear_generator/diagnostics/select_timing.py`](code/python/gear_generator/diagnostics/select_timing.py).
See [sketches/03](entries/sketches/03-relation-constants.md).

## 37. From a sub-feature, `GetNextFeature` walks on through the main tree

A feature walk that descends with `IFeature.GetFirstSubFeature` and then steps
the children with `GetNextFeature` does not stay among the children. On
SolidWorks 2026 (revision 34.0.0) such a walk over a gear part listed a sketch
and the extrusion made from it five times over, then repeated the later features.
Step children with `IFeature.GetNextSubFeature`. Even then each consumed sketch
appears twice: in the top-level chain just before its feature, and as that
feature's sub-feature. See [connect/08](entries/connect/08-find-a-feature-by-name.md).

## 38. With two centrelines in a sketch, the revolve axis has to be selected, at mark 16

`IFeatureManager.FeatureRevolve2` with only the sketch selected revolved about
the sketch's one centreline. A bevel blank's sketch has two construction lines,
and there the axis line was selected as well: `SelectByID2("Line2@<sketch>",
"EXTSKETCHSEGMENT", 0, 0, 0, True, 16, null, 0)` after the sketch, and the ring
weighed exactly 2π r A about the meant line. Mark 16 was the first mark tried
and worked; mark 4 and "sketch only" with two centrelines were never tried. A
revolve about the wrong line still returns a feature, so weigh it. SolidWorks
2026 (revision 34.0.0). See [features/05](entries/features/05-revolve.md).

## 39. `IMathUtility.CreateTransform` is "Member not found" to late binding

From pywin32's dynamic dispatch, `CreateTransform(data)` raised
**`DISP_E_MEMBERNOTFOUND`** with a Python list and with a
`VARIANT(VT_ARRAY | VT_R8, data)` alike, which reads as though the member does
not exist. Invoking the same dispid with `pythoncom.DISPATCH_METHOD` and the
typed array made the transform:

```python
oleobj = utility._oleobj_
raw = oleobj.Invoke(oleobj.GetIDsOfNames("CreateTransform"), 0, pythoncom.DISPATCH_METHOD, True,
                    VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_R8, data))
```

The probe's reading is that late binding asked for a property get with an
argument; that was not isolated. Put the result on `IComponent2.Transform2` by
plain assignment, with the rotation **by columns** (§10), and read it back.
SolidWorks 2026 (revision 34.0.0). See
[assemblies/04](entries/assemblies/04-mates-between-non-parallel-axes.md).

## 40. Interference detection: three `Get` members are properties, `Done` is a method

On `IInterferenceDetectionMgr` through late binding, `GetInterferenceCount` and
`GetInterferences` came back as their values by attribute access, and so did
`IInterference.Volume` (cubic metres). `Done` came back as a Python `method`
and runs only as `manager.Done()` — the §31 trap again, on a member whose name
gives no hint. Also run the check on something that must fail: an
interference check that never reports anything looks exactly like a good fit.
SolidWorks 2026 (revision 34.0.0). See
[assemblies/03](entries/assemblies/03-interference-detection.md).

## 41. `AddComponent5` does not put the part's origin at the point you give

A 10 mm thick cylinder inserted at (60, 0, 0) mm read a translation of
(60.0, 0.0, −5.0) mm. Left where it landed and used as the reference for an
origin-to-origin mate, it put the mated component's origin 5.0 mm from where
its frame said, with the rotation exact. The probe's reading is that the
part's middle goes at the point; only that one part thickness was tried, so
the rule is not established. What worked either way: set **every**
component's frame with `Transform2`, the first included, before mating.
SolidWorks 2026 (revision 34.0.0). See
[assemblies/04](entries/assemblies/04-mates-between-non-parallel-axes.md) and
[assemblies/01](entries/assemblies/01-new-assembly-and-insert-components.md).

## 42. A point-to-plane distance mate is measured along the plane's normal

A component origin placed 8 mm below another component's Top plane (y = −8 mm),
mated to that plane with an 8 mm distance and `AddMate5`'s `Flip` false, moved
to **y = +8 mm** on rebuild: 16 mm from where it was placed, with nothing
reported and the mate's `D1` reading 0.008 either way. With `Flip` (the third
argument) `True` it stayed at −8 mm. Pass the distance positive and flip it
when the point is on the plane's negative side. SolidWorks 2026 (revision
34.0.0). See [assemblies/04](entries/assemblies/04-mates-between-non-parallel-axes.md).

## 43. An angle dimension on the Top plane: give its placement point in model space

The Top plane's sketch x is model +X and its sketch **y is model −Z**
(`top_sketch_axes_in_model = {'x': [1.0, 0.0, 0.0], 'y': [0.0, 0.0, -1.0]}`).
An angle dimension between two lines on it, placed with
`IModelDoc2.AddDimension2` at a point inside the angle given in **model**
coordinates, read the angle, 29.999999999999996° for 30°. The probe's own
findings record that placed at that point's **sketch** coordinates, which lie
mirrored across X and outside the angle, it had read the supplement; that
earlier run's log is not among the evidence kept here, so that half is
unverified in this repo. Either way the dimension is created without complaint,
and a supplement linked to a global drives the geometry to the wrong angle.
SolidWorks 2026 (revision 34.0.0). See
[sketches/04](entries/sketches/04-dimensions.md).

## 44. A Move/Copy Body turn records no dimension, so no global can drive it

`IFeatureManager.InsertMoveCopyBody2(0, 0, 0, 0, 0, 0, 0, 0, radians(30), 0,
False, 1)`, with the body selected at mark 1 through `IBody2.Select2`, made a
`MoveCopyBody` feature (`Body-Move/Copy1`) and the body's centre of mass turned
30° about Y as expected, (19.82050807568877, ~0, −5.6698729810778055) mm from
(20, 0, 5). But the feature's dimensions were `[]`: the turn is not a dimension,
and an equation cannot change it. A part that has to follow a global angle has
to be built in its final orientation instead (planes and sketches that carry
the angle as dimensions: [features/06](entries/features/06-reference-plane-normal-to-a-line.md),
[features/03](entries/features/03-circular-pattern.md) for an axis from a line).
SolidWorks 2026 (revision 34.0.0), run 20260915-015506, probe
`move_body_rotate`, in
[`p2_bevel.py`](code/python/gear_generator/probe/p2_bevel.py).

## 45. A sweep's twist sign is ignored: the other hand is `D1ReverseTwistDir`

`IFeatureManager.InsertProtrusionSwept4` with constant twist along path (8) and
a twist of **−π/2** made exactly the same solid as +π/2: the same volume,
565.6302 mm³, and the same centre of mass, (7.6397, +7.6400, 9.9998) mm, to
every printed digit. Both were a right-hand helix, turning counter-clockwise
about +Z as z increases, and so was a sweep whose path ran toward −Z. Nothing
reports that the sign was dropped. The direction is a flag on the definition:
`IFeature.GetDefinition`, `ISweepFeatureData.AccessSelections(doc, null)`,
`D1ReverseTwistDir = True`, then `IFeature.ModifyDefinition(data, doc, null)`,
which returned `True` and moved the centre to y −7.6401. The tool reverses its
`InsertCutSwept5` tooth spaces the same way, and a left-hand space came out
mirrored in y. SolidWorks 2026 (revision 34.0.0), runs 20260915-002812 and
20260915-023929. See [features/09](entries/features/09-twisted-sweep.md) and
[features/10](entries/features/10-swept-cut.md).

## 46. `arctan` is refused; the inverse tangent is `atn`

In the Equation Manager, `"Probe Arctan"= arctan ( 1 )` made `IEquationMgr.Add2`
return **-1**, while `"Probe Atn"= atn ( 1 )` was accepted and read 45 in a part
whose trig is degrees. `arcsin ( 0.5 )` and `arccos ( 0.5 )` were accepted and
read 30 and 60: inverse trig answers in the document's angle unit, as `sin`
takes it, and only the tangent has the short name. Nested,
`atn ( tan ( "Probe Alpha" ) / cos ( "Probe Beta" ) )` with 20 and 15 read
20.646896487046472 against 20.64689648704647 worked by hand. Not tried inside
an Equation Driven Curve, whose trig is radians (§29). SolidWorks 2026
(revision 34.0.0), runs 20260915-002812 and 20260915-023929. See
[equations/01](entries/equations/01-global-variables-from-code.md).

## 47. Mirroring a body: the body goes at mark 256, and a wrong mark returns `None`

`IFeatureManager.InsertMirrorFeature2(True, False, True, False, 0)` with the
plane at mark 2 and the body at mark 1 returned `None` and made nothing; so did
body and plane both at mark 1. With the body at **mark 256** and the plane at
mark 2 it made one `MirrorSolid` body of twice the volume, centred on the plane.
The body is selected through `IBody2.Select2(True, data)` with the mark set on
an `ISelectionMgr.CreateSelectData` object, so there is no mark argument to
notice. SolidWorks 2026 (revision 34.0.0), runs 20260915-002812 and
20260915-023929. See [features/11](entries/features/11-mirror-body.md).

## 48. Twisted and patterned spline cuts weigh close to the arithmetic, not exactly

Extrusions and straight cuts matched hand arithmetic to sixteen significant
figures ([reading/10](entries/reading/10-mass-properties-as-an-oracle.md)).
Features with spline surfaces did not. A quarter-turn sweep of A·L =
565.487 mm³ measured 565.630 (2.5e-4 out). A twisted cut of 282.743 mm³
measured 282.743 when it started ahead of the face and 282.701 when it started
on it. Six patterned twisted cuts left 10869.773 mm³ against 10869.911. A ring
gear's single tooth-space cut weighed 157.494 mm³ on its own against 157.345
per space when sixty were patterned, which put the ring 2.8e-4 off "blank less
sixty spaces". None of these is an error in the feature. Compare such parts
with a relative tolerance (the Gear Generator uses 5e-4), or like with like: an
updated ring and a fresh build of the same ring agreed to 1e-13
(34284.14386641376 and 34284.143866418235 mm³). SolidWorks 2026 (revision
34.0.0), run 20260915-023929. See [features/09](entries/features/09-twisted-sweep.md),
[features/10](entries/features/10-swept-cut.md).

## 49. Deleting a composite curve deletes the loft built on it

A loft whose profile is a composite curve goes with the composite: delete the
composite and the loft is deleted too, not left with a dangling reference.
Changing which curves an existing composite joins was no way out either;
editing its sources could not be made to work. So when a composite's pieces
change, rename the old one aside (it keeps holding up the loft), make a new one
under the old name, and tell the user to re-pick it in the loft. Profiles must
also be cut into the same number of pieces: a three-piece composite lofted only
to another three-piece composite. SolidWorks 2026. See
[curves/06](entries/curves/06-composite-curve.md).

## 50. The order guides are picked in can change the loft

Two lofts identical except for the order their guide curves were selected
(all at mark 2) came out different by up to **0.04 mm** on one loft; on
another, not at all. Nothing reports it. When comparing one run with the next,
pick the guides in a fixed order. SolidWorks 2026. See
[features/12](entries/features/12-guided-loft.md) and
[surfacing/03](entries/surfacing/03-how-a-loft-fills-between-profiles.md).

## 51. Clearing the selection during a user's pick crashed SolidWorks

A tool polling the selection and clearing it after each read
(`IModelDoc2.ClearSelection2(True)`), so that each click was an event, crashed
SolidWorks 2026 twice on 2026-09-16: an access violation in SolidWorks' own
`ClearSelectionsNotify`, while the user was in a sketch with the Point property
page open on the point just clicked. The read and the clear were the last API
calls both times. Read the selection only, tell clicks apart by the selection
changing, and refuse to start anything that selects while
`ISketchManager.ActiveSketch` is not `None`. See
[reading/04](entries/reading/04-read-the-selection.md), which recommended the
clear until this.

## 52. Guides too close to a sharp corner break a loft, sometimes silently

On an offset profile that came to a sharp corner at its nose, surface guides
starting at 1 to 1.5 % of the chord made the loft **fail**; starting at 0.5 %
it built, as a garbage dome, with no error; starting at 2 % it was right.
Check the shape, not just that the feature built. SolidWorks 2026. See
[surfacing/03](entries/surfacing/03-how-a-loft-fills-between-profiles.md).

## 53. A curve file with near-duplicate points is refused

SolidWorks 2026 refused Curve Through XYZ Points files in which two
neighbouring points were all but coincident, as an offset or a trim leaves
them. Dropping every point within **0.01 mm** of the one kept before it
(keeping both ends) fixed it. A closed curve's deliberate repeat of its first
point as its last is fine. See
[curves/01](entries/curves/01-sldcrv-file-format.md).

## 54. Walking the feature tree every second makes SolidWorks stutter

API calls are served on SolidWorks' own thread, the one that draws the view.
A once-a-second `FirstFeature`/`GetNextFeature` walk of a 120-feature part took
about 650 ms of that thread each second, and orbiting the model stuttered for
as long as the tool was open; nothing in the tool's own process showed it.
Poll `IModelDoc2.GetFeatureCount` and `GetUpdateStamp` instead (under a
millisecond together) and read the tree only when they move. SolidWorks 2026.
See [reading/11](entries/reading/11-cheap-change-detection.md).

## 55. A zero-argument member that returns nothing *or an array* comes back uncalled

§5's rule — reach a zero-argument member by attribute access — and §31's
exception for `FixComponent` are the same fact seen twice. On SolidWorks 2026
it has a third and fourth case, and together they give the rule that works:

| Member | Attribute access gives | Symptom |
|---|---|---|
| composite feature data `ReleaseSelectionAccess` | an uncalled `method` | the tree silently stays rolled back |
| `IBody2.GetEdges` | an uncalled `method` | `TypeError: 'method' object is not iterable` |
| `IFace2.GetTessTriangles(True)` | an uncalled `method` | the same |
| `IAssemblyDoc.FixComponent` | an uncalled `method` | nothing happens (§31) |

A member that returns **nothing or an array** is handed over as a bound method;
a member that returns a value is invoked for you. A *value* never comes back
looking like a method object, so testing what came back is safe where testing
the member's name is not:

```python
member = getattr(obj, name)
if args:
    return member(*args)
if type(member).__name__ == "method":
    return member()
return member
```

Members observed **not** to need this on the same session: `GetFaces`,
`GetBody`, `GetCurve`, `GetCurveParams2`, `GetMassProperties`, `IsRolledBack`,
`Name`, `GetTypeName2`, `GetNextFeature`, `EditRebuild3` and
`InsertPlanarRefSurface` — the last two of which *do* something and still come
back as their result.

One case this does not cover: `IMassProperty.AddBodies` reached by `getattr`
answered a `bool` — it had already run, with no bodies — and then could not be
called with its bodies at all (`'bool' object is not callable`). Where a member
both takes arguments and might be exposed as a property, check the effect.
SolidWorks 2026 SP0.0. See [connect/02](entries/connect/02-attach-from-python.md).

## 56. Reloading a curve is charged for the whole tree below it

`IFeature.ModifyDefinition` after `LoadPointsFromFile` cost **25 seconds per
curve** on a 397-feature part with the tree rolled forward — 39 curves, sixteen
and a half minutes, which was the whole of a seventeen-minute export. With or
without `ISldWorks.CommandInProgress`, which suppresses the *rebuild* and not
this.

Put the rollback bar just after the last curve first
(`IFeatureManager.EditRollback(4, name)`) and the same reload is **0.5
seconds**, because everything built on the curve is rolled back and has nothing
to say. SolidWorks 2026 SP0.0. See
[curves/12](entries/curves/12-roll-the-tree-back-before-reloading.md).

## 57. `EditRollback` to the previous position answers True and moves nothing

`IFeatureManager.EditRollback(2, "")` —
`swMoveRollbackBarToPreviousPosition` — returned `True` in 0.1 s and left the
bar where it was. The part stayed rolled back, its lofts reading 0 faces and
0 mm², the splits and inserts under them gone from view, and two whole pushes
handed it back that way before it was noticed.

Roll to **End** (`1`) instead, and then ask
`IModelDoc2.FeatureByPositionReverse(0)` whether it `IsRolledBack` — that is a
genuine bool. `IFeatureManager.GetRollbackBarPosition`, which would have made
this visible, is not reachable through pywin32 late binding at all.
SolidWorks 2026 SP0.0. See
[curves/12](entries/curves/12-roll-the-tree-back-before-reloading.md).

## 58. `EditRebuild3` returns False whenever anything in the part is in error

Including features that were in error before you touched the part. A part
carrying 13 such features answered `False` to every `EditRebuild3` while doing
exactly what it was asked, geometry and all. The return is not "did not run",
and code that treats it as one stops for no reason.

While you are there: `EditRebuild3` was **0.8 s** where `ForceRebuild3(False)`
was **25 s** on that part, for geometry identical to twelve digits. Rebuild
what changed. SolidWorks 2026 SP0.0. See
[curves/08](entries/curves/08-rebuild-once-at-the-end.md).

## 59. `AccessSelections` rolls the tree back, and the release that undoes it never ran

Reading what a composite curve joins is not a read-only act:
`AccessSelections` on its feature data rolls the model back to just before the
feature (the help says so, and it is easy to read past). Afterwards the loft
built on that composite read 0 faces and the splits below it were gone.

`ReleaseSelectionAccess` puts it back — in 0.9 s — but it is a `Sub`, so late
binding hands it over as an uncalled method object and reaching it through a
helper that only fetches zero-argument members did nothing at all (§55). Every
push the tool made while that check existed left the part rolled back.
SolidWorks 2026 SP0.0. See [curves/06](entries/curves/06-composite-curve.md).

## 60. A planar cap can span a sliver instead of the end, and answer True

`IModelDoc2.InsertPlanarRefSurface` across the four edges of a loft's end loop
returned `True` and made a `PlanarSurface` feature — of **0.078 mm²**, where
the section it was meant to close encloses **4,046.8 mm²**. It had capped a
sliver face's own little loop instead of the wing.

Compare the cap's `IFace2.GetArea` with the area the profile encloses, and
refuse a cap that is a fraction of it. Better still, refuse before capping:
count the edges of the end loop against the number of pieces the profile is
cut into — one edge per piece is what an end of a two-profile loft is.
SolidWorks 2026 SP0.0. See
[surfacing/04](entries/surfacing/04-cap-a-refused-loft-into-a-solid.md).

## 61. A knit asked for a solid can sew a sheet and still make a feature

`IFeatureManager.InsertSewRefSurface(True, True, False, 1e-4, 1e-4)` — with
`TryToFormSolid` `True` — took three sheet bodies to one sheet body, made a
`SewRefSurface` feature, and answered as though it had done what was asked.
The part gained **no** solid body, and the resulting body's
`GetMassProperties` volume slot read 439,803,896.6 mm³ against a real
2,551,504.3: a meaningless number off an unclosed shell.

Count `IPartDoc.GetBodies2(0, False)` before and after, and delete the knit if
the count did not go up. SolidWorks 2026 SP0.0. See
[surfacing/04](entries/surfacing/04-cap-a-refused-loft-into-a-solid.md).

## 62. Suppressing one feature suppresses what is built on it, and unsuppressing does not undo it

`IFeature.SetSuppression2(0, 1, None)` on one body feature — to write another
body alone to STEP — suppressed **74 other features**: the splits and inserts
built on that body, their folders, and the planes and sketches under those. The
part went from 16 solid bodies to 6. Unsuppressing the feature that had been
suppressed brought back **none** of them.

Restoring by name does not work either: one of the 74 is called `Sketch9<3>`,
which a name walk cannot find. Snapshot every feature's `IsSuppressed` with the
feature **objects**, in tree order, before; restore from that, parents first,
after. SolidWorks 2026 SP0.0. See
[reading/12](entries/reading/12-snapshot-suppression-before-you-suppress.md).

## 63. A six-decimal curve file can make a flat loft end "3D", and the solid loft is refused

Written to six decimals of a millimetre, a planar section stands up to 5e-7 mm
off its plane, and SolidWorks' test for a flat loft end is stricter than that.
On one wing 9 of 22 flat sections were refused as the end of a solid loft —
at random-looking places, neighbours of the same shape passing — and
`InsertProtrusionBlend2` said so only by returning `Nothing`. The Loft page
says it outright: *"A 3D section that does not bound a face or surface cannot
be used as an end section."* Written to ten decimals, every one lofted as a
solid. A surface loft never asks, so it builds either way and hides the cause.
SolidWorks 2026 SP0.0. See
[curves/01](entries/curves/01-sldcrv-file-format.md).

## 64. Guides that land on each section's nearest point zigzag, and two of them refuse the loft

Through many sections close together, a guide pinned to each section's point
*nearest* its chord fraction wanders chordwise by up to half the point
spacing between neighbours a tenth of a millimetre apart. Through 22 such
sections any two surface guides made SolidWorks refuse the loft — even as a
surface — while one alone built. Give each section a point at exactly the
guide's place, or have the guide follow the same numbered point of every
section. SolidWorks 2026 SP0.0. See
[surfacing/03](entries/surfacing/03-how-a-loft-fills-between-profiles.md).

## 65. A round nose cut into two curves comes out a wedge

Each half of a Curve Through XYZ Points is a natural spline with zero
curvature at its ends, so a smooth nose split into upper and lower curves
flattens where they meet: a loft through such sections was 0.36 mm off at the
nose and 0.5 % light. Draw a smooth nose as one curve; cut only where there is
a real corner. SolidWorks 2026 SP0.0. See
[surfacing/03](entries/surfacing/03-how-a-loft-fills-between-profiles.md).

## 66. Diffing the tree around every insert costs two full listings

Finding what `InsertCurveFile` (or any insert that returns a boolean) made by
listing the tree before and after cost 3.7 s a listing on a 560-feature part,
plus 2.9 s to walk to it again by name: ten seconds of bookkeeping around one
second of work, per curve. `IModelDoc2.GetFeatureCount` either side and
`IModelDocExtension.GetLastFeatureAdded` answer the same question in a
hundredth of a second, with the tree rolled back or not, and
`IPartDoc.FeatureByName` finds a feature in one call. SolidWorks 2026 SP0.0.
See [curves/02](entries/curves/02-insert-curve-from-file.md),
[connect/08](entries/connect/08-find-a-feature-by-name.md).

## 67. Deleting a surface loft deletes the caps built on its edges

Planar surfaces made across a surface loft's end edges go when the loft goes.
Code that deleted the loft and then asked for each cap by name failed on the
first (`No feature called ...`) after it had already deleted the knit, leaving
no loft at all. Delete in the reverse of building — knit, caps, surface —
checking each is still there. SolidWorks 2026 SP0.0. See
[surfacing/04](entries/surfacing/04-cap-a-refused-loft-into-a-solid.md).

## 68. A surface loft has no feature data

`IFeature.GetDefinition` on a surface loft (`BlendRefSurface`) returns
`Nothing`; on a solid loft (`Blend`) it returns an object. A surface loft's
profiles and guides cannot be changed through its definition — keep the
profile count and names constant and reload the curves instead, which a loft
follows. SolidWorks 2026 SP0.0. See
[features/12](entries/features/12-guided-loft.md).

## 69. The journal records a program's calls, but not all of them, and not always as made

`%APPDATA%\SolidWorks\SOLIDWORKS 2026\swxJRNL.swj` records API calls an
external program makes over COM, next to what the user clicks, and is often
the only record of what a program did. But `IFeature.Select2` and renames
through `IFeature.Name` do not appear at all, a STEP export through
`IModelDocExtension.SaveAs` with the copy option appears as `Part.Save3`, and
only the current and previous sessions are kept. SolidWorks 2026 SP0.0. See
[reading/14](entries/reading/14-the-journal-records-api-calls.md).
