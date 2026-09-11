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
