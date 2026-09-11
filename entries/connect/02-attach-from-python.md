---
id: connect-02-attach-python
title: Attach to a running SolidWorks from Python
status: verified
verified_on: SolidWorks 2024, 2025 and 2026 side by side
language: [python]
api: [ISldWorks.RevisionNumber, ISldWorks.ActiveDoc]
keywords: [pywin32, pythoncom, running object table, EnumRunning, IDispatch, VARIANT, late binding, SolidWorks_PID]
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

## Full source

[`code/python/swcom.py`](../../code/python/swcom.py) — the complete module,
around 950 lines. It is the only module in its project that touches COM, and
everything crossing its boundary is plain data, which is what makes the rest of
that application testable without SolidWorks.

## See also

- [connect/05 — Choosing among versions](05-choosing-among-versions.md)
- [connect/06 — One apartment thread](06-one-apartment-thread.md)
- [reading/04 — Read the selection](../reading/04-read-the-selection.md)
