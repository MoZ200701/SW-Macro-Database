---
id: reading-02-walk-reference-tree
title: Walk a full reference tree with a cycle guard
status: verified
verified_on: SolidWorks 2026
language: [csharp]
api: [ISldWorks.GetDocumentDependencies2]
keywords: [reference tree, recursion, cycle, depth first, dependency tree, ancestors]
answers: "How do I build the whole dependency tree from the one-level primitive?"
---

# Walk a full reference tree with a cycle guard

## The primitive and the walk

[reading/01](01-dependencies-of-a-closed-file.md) gives you direct children.
The tree is a depth-first recursion on top of it. The only subtlety is the
cycle guard.

```csharp
static async Task PrintTree(SwConnector connector, string file, int depth,
                            HashSet<string> ancestors)
{
    foreach (var (name, path) in await connector.GetDependenciesAsync(file))
    {
        PrintRow(name, path, depth);
        if (File.Exists(path) && ancestors.Add(path))
        {
            await PrintTree(connector, path, depth + 1, ancestors);
            ancestors.Remove(path);
        }
    }
}
```

## `ancestors` holds the current branch, not everything seen

That distinction is the whole design. `ancestors.Add(path)` returns `false` if
the path is already on the branch you are descending, which stops a cycle. The
`ancestors.Remove(path)` on the way back up is what lets **the same file appear
under different parents**, which is normal and correct: a common part legitimately
appears under every assembly that uses it.

Using a global "seen" set instead would show each part exactly once and produce a
tree that is wrong in a way nobody notices until they rely on it.

## Only recurse into files that exist

`File.Exists(path)` before descending. A missing file has no dependencies to
read, and asking SolidWorks about it costs a round trip to get an empty answer.

## Reporting

```csharp
static void PrintRow(string name, string path, int depth)
{
    var indent = new string(' ', depth * 2);
    var state = File.Exists(path) ? "EXISTS" : "MISSING";
    Console.WriteLine($"{indent}{name}\t{path}\t{state}");
}
```

Two spaces per level, tab-separated fields, so the output is both readable and
parseable.

## A complete command-line tool

```
swfm-smoke [--deep] <file.(sldprt|sldasm|slddrw)>
```

Exit codes: 0 fine, 2 cannot connect to SolidWorks, 3 input file not found,
4 COM error. Distinguishing 2 from 4 matters: the first means "start SolidWorks",
the second means "something went wrong once you were connected".

Full source in the SW File Manager smoke tool; the recursion above is the whole
of it.

## Verify against SolidWorks itself

The check that this is right: run it on a real assembly and compare against
**File → Find References** for the same file. They must agree.

Do this before building anything on top. A dependency reader that is subtly
wrong produces a broken-reference report that is subtly wrong, and you will
believe it.

## Scale

For a library of a few thousand files, do not walk trees on demand. Scan once,
one level per file, store the edges, and assemble trees from the table. Rescan
only files whose modified time or size changed. See
[reading/01](01-dependencies-of-a-closed-file.md).

## See also

- [reading/01 — Dependencies of a closed file](01-dependencies-of-a-closed-file.md)
