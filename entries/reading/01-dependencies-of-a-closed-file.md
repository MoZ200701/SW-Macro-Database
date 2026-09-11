---
id: reading-01-dependencies-closed-file
title: Read a closed file's references without opening it
status: verified
verified_on: SolidWorks 2026
language: [csharp]
api: [ISldWorks.GetDocumentDependencies2]
keywords: [GetDocumentDependencies2, references, dependencies, closed file, virtual component, document manager, opaque format]
answers: "How do I find out what a part or assembly references without opening it?"
---

# Read a closed file's references without opening it

## Why this is the only way

As of SolidWorks 2026 the file format is an opaque container that cannot be
parsed for references directly. The Document Manager API, which is the
purpose-built tool for this, is behind a subscription licence that a student
plan does not include.

What works is the **regular API**: `GetDocumentDependencies2` reads the full
reference tree of a *closed* file while SolidWorks runs in the background. The
file is never opened, so scanning a few thousand of them is feasible.

## The call

```csharp
// GetDocumentDependencies2(document, traverseFlag, searchFlag, addReadOnlyInfo)
var raw = sw.GetDocumentDependencies2(filePath, false, false, false);
```

| Argument | Value | Meaning |
|---|---|---|
| `traverseFlag` | `false` | Direct children only, not the whole tree |
| `searchFlag` | `false` | Use the stored paths; do not apply search rules |
| `addReadOnlyInfo` | `false` | Omit the read-only flags |

`traverseFlag: false` is deliberate. One level per file keeps rescans
incremental: store the edges, and assemble trees from the edge table. With
`true` you re-read the whole subtree every time any file in it changes.

`searchFlag: false` is what makes broken references *visible*. With search rules
on, SolidWorks helpfully finds a moved file somewhere else and reports the path
it found, which is precisely the information you are trying to detect.

## The return shape

A flat array alternating name, path, name, path. Not pairs, not a jagged array.

```csharp
IReadOnlyList<string>? flat = raw switch
{
    string[] s => s,
    object[] o => Array.ConvertAll(o, x => x?.ToString() ?? string.Empty),
    _ => null,
};
if (flat is null) return Array.Empty<(string, string)>();

var deps = new List<(string, string)>(flat.Count / 2);
for (int i = 0; i + 1 < flat.Count; i += 2)
{
    var name = flat[i];
    var path = flat[i + 1];
    if (name.Contains('^')) continue; // virtual component — never an edge
    deps.Add((name, path));
}
return deps;
```

Handle both `string[]` and `object[]`. Which one you get depends on the interop
marshalling, and assuming one produces a cast exception on some machines and not
others.

## Skip virtual components

A name containing `^` is a virtual component: a part that lives inside its
parent assembly rather than as a file on disk. It has no independent existence,
so it is never an edge in a file-level reference graph. Including them produces
phantom "missing file" reports.

## Full method

```csharp
public Task<IReadOnlyList<(string Name, string Path)>> GetDependenciesAsync(string filePath) =>
    Runner.Run<IReadOnlyList<(string, string)>>(() =>
    {
        var sw = _sw;
        if (sw is null) return Array.Empty<(string, string)>();
        try
        {
            var raw = sw.GetDocumentDependencies2(filePath, false, false, false);
            // ... unpacking as above ...
        }
        catch (COMException)
        {
            return Array.Empty<(string, string)>();
        }
    });
```

Note it runs on the STA thread — see
[connect/06](../connect/06-one-apartment-thread.md) — and that a COM failure
returns empty rather than throwing. On a scan of thousands of files, one
unreadable file must not abort the run. Log it separately.

## What you do with the edges

Store them. `files(path, mtime, size, type)` and `refs(parent, child)` in
SQLite is enough for everything downstream:

- **Broken references** — an edge whose child path does not exist on disk. If a
  file of the same name exists elsewhere in the index, that is a candidate fix
  worth offering.
- **Where-used** — a reverse query on the edge table.
- **Incremental rescan** — re-read only files whose modified time or size moved.

Skip files whose name starts with `~$`. Those are SolidWorks' own temporary
lock files.

## Full source

[`code/csharp/SwConnector.cs`](../../code/csharp/SwConnector.cs)

## See also

- [reading/02 — Walk a reference tree](02-walk-a-reference-tree.md)
- [reading/03 — Repair references after a move](03-repair-references-after-a-move.md)
