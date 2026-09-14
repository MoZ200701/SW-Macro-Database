---
id: connect-02-attach-python
title: Attach to a running SolidWorks from Python
status: verified
verified_on: SolidWorks 2024, 2025 and 2026 side by side
language: [python]
api: [ISldWorks.RevisionNumber, ISldWorks.ActiveDoc]
keywords: [pywin32, pythoncom, running object table, EnumRunning, IDispatch, VARIANT, late binding, SolidWorks_PID, indexed property, property put, DISPATCH_PROPERTYPUT, Invoke, _oleobj_, Equation(i), zero-argument method not invoked, method object, PyInstaller, hidden-import, onefile exe, frozen app]
answers: "How do I connect to SolidWorks from Python without launching a second copy?"
---

# Attach to a running SolidWorks from Python

**Status:** Verified, SolidWorks 2024, 2025 and 2026 side by side
**Language:** Python 3 with `pywin32`

## What this is for

Driving SolidWorks from a Python application: a GUI tool, a batch script, a
pipeline step. This is the richest of the four routes in this collection,
because it enumerates every running session and chooses between them rather
than taking the first thing that answers.

## Install and import

`pywin32` is Windows only. Import it defensively so the rest of your module
still imports on a machine without it, and report the reason separately:

```python
try:
    import pythoncom
    import win32com.client.dynamic as _dynamic
    from win32com.client import VARIANT
    _IMPORT_ERROR = None
except ImportError as exc:
    pythoncom = None
    _dynamic = None
    VARIANT = None
    _IMPORT_ERROR = str(exc)


def is_available() -> bool:
    return pythoncom is not None
```

This is what lets the surrounding code be unit-tested on Linux or a Mac.

## Never use a ProgID here

```python
MONIKER_PREFIX = "solidworks_pid_"
_MONIKER = re.compile(r"^SolidWorks_PID_(\d+)$", re.IGNORECASE)
```

`Dispatch("SldWorks.Application")` would **start** a copy of SolidWorks. The
Running Object Table is the only way in that creates nothing. Every live
session publishes a moniker named `SolidWorks_PID_<pid>`, and that name is the
same across versions, which sidesteps the whole ProgID problem in
[GOTCHAS §1](../../GOTCHAS.md).

## Enumerating every session

```python
def find_candidates(out_unreachable=None):
    """Every reachable session, interrogated. Never launches SolidWorks."""
    if not is_available():
        raise NotAvailable(unavailable_reason())

    table = pythoncom.GetRunningObjectTable()
    context = pythoncom.CreateBindCtx(0)
    found = []
    unreachable = [] if out_unreachable is None else out_unreachable

    for moniker in table.EnumRunning():
        try:
            name = moniker.GetDisplayName(context, None)
        except pythoncom.com_error:
            continue
        if not is_solidworks_moniker(name):
            continue

        # One unresponsive session — sitting on a modal dialog, say — must not
        # sink the whole scan. It is skipped, but never silently.
        try:
            app = _dispatch(table.GetObject(moniker))
            revision = parse_revision(call(app, "RevisionNumber"))
            has_doc = call(app, "ActiveDoc") is not None
        except (pythoncom.com_error, SolidWorksError, AttributeError) as exc:
            unreachable.append((name, str(exc)))
            continue

        found.append(Candidate(
            moniker=name, pid=pid_from_moniker(name),
            revision=revision, has_active_doc=has_doc, app=app,
        ))
    return found
```

Note that only monikers matching the SolidWorks pattern are bound. Unrelated
entries in the table, of which there are many, are never materialised.

## The three quirks that will bite you

**The table hands back an `IUnknown`.** Late binding cannot ask it for type
information until it has been asked for its `IDispatch` face:

```python
def _dispatch(raw):
    return _dynamic.Dispatch(raw.QueryInterface(pythoncom.IID_IDispatch))
```

**A zero-argument method is a property get.** Attribute access already invokes
it. `RevisionNumber` comes back as a string and `ActiveDoc` as a document
object, and *calling* either raises "Member not found". Testing `callable()`
does not help, because a member returning a document is itself callable. The
rule that works is: call only members that take arguments.

```python
def call(obj, name, *args):
    member = getattr(obj, name)
    return member(*args) if args else member
```

Every SolidWorks access in this collection's Python code goes through that
function.

**Two calls need their arguments typed by hand.**

```python
def _null_dispatch():
    """An absent COM object, typed. A bare None is a type mismatch."""
    return VARIANT(pythoncom.VT_DISPATCH, None)


def _out_long():
    """A by-reference integer for an out parameter SolidWorks insists on."""
    return VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
```

`ModifyDefinition` takes a component that a part does not have, and `None`
there is a type mismatch. `GetObjectByPersistReference3` has an out parameter
that must be supplied by reference. Both fail silently-ish, in the sense that
the error you get does not point at the argument.

## Version numbers

```python
MINIMUM_MAJOR = 34          # 2026
HIGHEST_TESTED_MAJOR = 34
_YEAR_OFFSET = 1992         # major + 1992 = marketing year


def parse_revision(text):
    """"34.0.0" -> (34, 0, 0)."""
    parts = str(text).strip().split(".")
    numbers = tuple(int(p) for p in parts if p != "")
    if not numbers:
        raise SolidWorksError(f"Could not read a version out of {text!r}.")
    return numbers
```

The year offset is display only. Getting it wrong costs one wrong word in a
message and nothing else, which is the right amount of trust to put in a
convention.

## Setting an indexed property

Some SolidWorks properties take an index. In VBA, changing an equation in the
Equation Manager is an assignment:

```vb
swEqnMgr.Equation(i) = """Module""= 2.25"
```

Late-bound pywin32 has no way to spell that. `eqm.Equation(i)` is a *get*, and
there is no assignment syntax for a property with an argument. Make the put
through `Invoke` on the underlying `IDispatch`:

```python
def _put_indexed(obj: Any, name: str, index: int, value: Any) -> None:
    """An indexed property put, which late binding cannot spell as an assignment.

    ``eqm.Equation(i) = text`` in VBA; through pywin32's dynamic dispatch the
    name is only ever invoked as a get or a call, so the put goes through
    ``Invoke`` (probe equation_set, SolidWorks 2026).
    """
    oleobj = obj._oleobj_  # noqa: SLF001
    oleobj.Invoke(oleobj.GetIDsOfNames(name), 0, pythoncom.DISPATCH_PROPERTYPUT, False, index, value)
```

`Invoke(dispid, lcid, flags, wantResult, *args)`: the locale is `0`,
`DISPATCH_PROPERTYPUT` makes it a put, `False` asks for no result, and the
index comes before the value, as it does in VBA.

**Verified on SolidWorks 2026 (revision 34.0.0).** With `"Probe Base"= 3` and
`"Probe Twice"= "Probe Base" * 2` in a part, `_put_indexed(eqm, "Equation", i, '"Probe Base"= 5')`
returned `None`, and after `IModelDoc2.ForceRebuild3(False)` the dependent
global read 10.0. The documented-looking alternative,
`IEquationMgr.SetEquationAndConfigurationOption`, returned `-1` and changed
nothing. The same put later drove a sketch dimension, an Equation Driven Curve,
a pattern count and an assembly mate distance through their globals, and
changed a built gear's teeth and module. See
[equations/01](../equations/01-global-variables-from-code.md).

## A zero-argument method that returns nothing is not invoked by `call`

The `call` rule above — attribute access for zero-argument members — held for
every member that returns something. It did **not** run a member that returns
nothing. `call(asm, "FixComponent")` on an assembly, on SolidWorks 2026
(revision 34.0.0), came back as a Python `method` object: the bound method was
fetched and never called. Nothing raised, and a check afterwards passed only
because the component was already fixed.

So a zero-argument *action* needs explicit parentheses, `asm.FixComponent()`,
and a check of its effect. That form was confirmed on a rerun on the same
version (run 20260914-183429): on a free component it returned `None` and
`IsFixed` went from `False` to `True`. If a result's `type(...).__name__` is `'method'`,
nothing ran. That pywin32 treats a zero-argument member with a return value as
a property and one without as a method is the likely reason; it was not
established. See [assemblies/01](../assemblies/01-new-assembly-and-insert-components.md)
and [GOTCHAS §31](../../GOTCHAS.md).

## Bundled into a one-file exe

A tool frozen with PyInstaller attached through the Running Object Table in the
same way, built with pywin32's modules named as hidden imports. The command, from
the tool's `build.bat`:

```bat
python -m PyInstaller --onefile --windowed --name "Gear Generator v0.1" ^
    --paths src ^
    --add-data "src\gear_generator\assets\fonts;assets/fonts" ^
    --hidden-import pythoncom ^
    --hidden-import pywintypes ^
    --hidden-import win32com.client.dynamic ^
    --hidden-import win32timezone ^
    main.py
```

Observed with SolidWorks 2026 (revision 34.0.0), Windows 11, the Microsoft Store
Python 3.13.14 and PyInstaller 6.22.2:

- In `cmd`, `pyinstaller` was "not recognized as an internal or external
  command", with PyInstaller installed for that Python, and the build logged
  above completed. The batch file therefore runs it as `python -m PyInstaller`.
  Its comment gives the reason as the Store Python keeping its Scripts folder off
  `PATH`; that was not checked separately.
- The build ran from a copy of the checkout under `%USERPROFILE%\Documents`, not
  from its `\\wsl.localhost` path. Whether building from the UNC path works is
  not recorded.
- The build log shows pywin32's hooks for `pythoncom`, `pywintypes` and
  `win32com` applied, and `win32timezone` analysed as a hidden import.
- Launched, the exe opened its window, and its SolidWorks panel read "SolidWorks
  2026 (revision 34.0.0)" with the document open in that session: it had
  attached. A `WM_CLOSE` posted to the window closed it, the launched process
  exited 0, and no process of that image was left running.

Not established: that a build without those hidden imports fails to attach (the
batch file says so, but no such build was run), and which process owns the
window of a `--onefile` build.

## Full source

[`code/python/swcom.py`](../../code/python/swcom.py) — the complete module,
around 950 lines. It is the only module in its project that touches COM, and
everything crossing its boundary is plain data, which is what makes the rest of
that application testable without SolidWorks.

## See also

- [connect/05 — Choosing among versions](05-choosing-among-versions.md)
- [connect/06 — One apartment thread](06-one-apartment-thread.md)
- [reading/04 — Read the selection](../reading/04-read-the-selection.md)
- [equations/01 — Global variables from code](../equations/01-global-variables-from-code.md) — the indexed put in use
- [documents/02 — Save as, and close](../documents/02-save-as-and-close.md) — typed nulls and out longs on `SaveAs3`
- [assemblies/01 — New assembly and insert components](../assemblies/01-new-assembly-and-insert-components.md) — the `FixComponent` case
- [connect/11 — Probe an API member on a live session](11-probe-an-api-member-on-a-live-session.md)
- [`code/python/gear_generator/swcom.py`](../../code/python/gear_generator/swcom.py) — a later version of the module, with `_put_indexed`
