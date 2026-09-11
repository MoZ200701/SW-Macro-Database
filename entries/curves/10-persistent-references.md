---
id: curves-10-persistent-references
title: Track a feature that might get renamed
status: verified
verified_on: SolidWorks 2026
language: [python]
api: [IModelDocExtension.GetPersistReference3, IModelDocExtension.GetObjectByPersistReference3]
keywords: [GetPersistReference3, GetObjectByPersistReference3, persistent reference, rename, stable handle, VT_BYREF]
answers: "How do I keep a handle on a feature that survives the user renaming it?"
---

# Track a feature that might get renamed

## The problem

Names are the obvious key and they are not stable. A user renames a curve, and
every record your tool holds now points at nothing. Worse, the name may have
been taken by something else.

A persistent reference is a byte handle that survives renaming, and survives
the file being closed and reopened.

## Getting one

```python
def persist_reference(self, name):
    """A handle to a feature that outlives its name."""
    doc = self._active()
    extension = call(doc, "Extension")
    return bytes(call(extension, "GetPersistReference3", self._curve_feature(name)))
```

It comes back as a byte array. Store it base64-encoded in JSON alongside
whatever else you remember about that feature.

## Resolving one

```python
def name_from_persist_reference(self, reference):
    """What that feature is called now, or None if it is gone."""
    doc = self._active()
    extension = call(doc, "Extension")
    try:
        result = call(extension, "GetObjectByPersistReference3", reference, _out_long())
    except Exception:
        # a stale reference is an answer, not a fault
        return None
    found = result[0] if isinstance(result, tuple) else result
    if found is None:
        return None
    try:
        return str(call(found, "Name"))
    except Exception:
        return None
```

## Three things that will trip you

**The out parameter must be typed by reference.** `GetObjectByPersistReference3`
has an error-code out parameter. In Python:

```python
def _out_long():
    """A by-reference integer for an out parameter SolidWorks insists on."""
    return VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
```

Omit it, or pass a plain `0`, and the call fails in a way that does not point at
the argument.

**The return may be a tuple.** With a by-reference argument in play, pywin32
returns the method result together with the updated out parameter. Handle both
shapes: `result[0] if isinstance(result, tuple) else result`.

**A stale reference is a normal answer.** The feature really may have been
deleted. Catch broadly here and return `None` rather than propagating, because
"it's gone" is information, not a fault.

## Reference is per document

A persistent reference is meaningful only within the document it came from.
Store it keyed by the part path, and re-acquire it if the part is saved under a
new name.

## Cost

`GetPersistReference3` is not free, and resolving one requires a document to be
open. For a set of features you touch every push, keeping names as the fast path
and persistent references as the repair mechanism is a reasonable split: look up
by name, and if the name is missing, resolve the references to find out what
things are called now.

## See also

- [curves/07 — Rename a feature](07-rename-a-feature.md)
- [curves/05 — Read curve points back](05-read-curve-points-back.md)
- [GOTCHAS §4](../../GOTCHAS.md)
