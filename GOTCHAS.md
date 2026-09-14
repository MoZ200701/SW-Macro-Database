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
it ran and worked. See §31.

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
or loft is still a human job. An automatic insert therefore leaves a curve that
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
as `<folder>___EndTag___` after its contents, both typed `FtrFolder`. Nesting
is the depth between the two. Count the end tags or your contents will run on
into whatever follows the folder.

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
cause; it was not established. See
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
