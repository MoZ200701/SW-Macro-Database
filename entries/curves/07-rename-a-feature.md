---
id: curves-07-rename-a-feature
title: Rename a feature, and read the name back
status: verified
verified_on: SolidWorks 2026
language: [vbscript, python]
api: [IFeature.Name]
keywords: [rename feature, feature name, collision, silent rename, read back]
answers: "How do I name a feature I just created, and why did my name not stick?"
---

# Rename a feature, and read the name back

## The call

```python
feature.Name = "S9"
```

That is all there is to it. Renaming from the API works, on curves and on
everything else with a name.

Note that in Python this is a genuine attribute **set**, not a `call`. The
`call` helper in [connect/02](../connect/02-attach-from-python.md) is for
reading members; setting goes through normal attribute assignment.

## The part that will catch you

**SolidWorks quietly keeps its own name on a collision.** Set `Name = "S9"`
when an `S9` already exists and you get something else — `S91`, or the original
generated name — with no error, no exception, no `False` return.

If you then record `"S9"` in your own state file, you have a record pointing at
a feature that does not exist, and nothing later can detect it. The next refresh
reports MISSING for a curve that is sitting right there under another name.

## So always read it back

```python
def rename_feature(self, current, new):
    """Rename, and report the name SolidWorks actually kept."""
    feature = self._curve_feature(current)
    feature.Name = new
    return str(call(feature, "Name"))
```

Return the name it kept, and store *that*. Every caller in this collection uses
the returned value, never the requested one.

## In VBScript

```vb
On Error Resume Next
feat.Name = featName
If Err.Number <> 0 Then
    WScript.Echo "  WARN    inserted as " & feat.Name & ", could not rename to " & featName
    Err.Clear
    Exit Function
End If
On Error GoTo 0
```

## Naming conventions worth adopting

If a program owns a set of features, give them names it can recognise later:
a fixed prefix, and no spaces. `S1`, `S2`, `G_keel`, `G_crown`. Short, because
they show in the tree, and predictable, because your refresh loop looks them up
by name.

Do not use a name a person might reasonably type themselves, or you will
collide with their work and hit the silent-rename behaviour above.

## When names are not enough

A user can rename anything at any time. If a mapping has to survive that, use a
persistent reference instead: [curves/10](10-persistent-references.md).

## See also

- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md)
- [curves/11 — Feature-tree folders](11-feature-tree-folders.md) — a folder is renamed the same way, and read back for the same reason
- [GOTCHAS §8](../../GOTCHAS.md)
