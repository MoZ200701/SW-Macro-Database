---
id: reading-03-repair-references
title: Rename or move a file and repair every reference to it
status: unverified
verified_on: null
language: [csharp]
api: [ISldWorks.ReplaceReferencedDocument, ISldWorks.GetOpenDocumentByName]
keywords: [ReplaceReferencedDocument, rename, move, broken reference, repair, where used, preflight]
answers: "How do I move a part on disk without breaking the assemblies that use it?"
---

# Rename or move a file and repair every reference to it

**Status: unverified.** The design below is worked out and the call is
documented, but the flow has not been run end to end. Test on copies.

## The call

```
ISldWorks::ReplaceReferencedDocument(referencingPath, oldPath, newPath)
```

You call it **once per referencing document**, not once for the file you moved.
So you need to know who refers to it, which is what the reverse index from
[reading/01](01-dependencies-of-a-closed-file.md) is for.

## The precondition that will stop you

**Every referencing document must be closed.** Check with
`GetOpenDocumentByName` and ask the user to close them before starting, rather
than discovering it halfway through a batch and leaving half the references
repaired.

## The sequence

1. **Preflight.** From the where-used index, list every document that refers to
   the target. Check none is open. Check the destination path is free. Check
   every referencing document is writable.
2. **Confirm.** Show the user the complete list of files that will be modified.
   Not a count, the list. This is a destructive operation on files they did not
   name.
3. **Back up.** Copy every file about to be modified, plus the file being moved.
4. **Act.** Move or rename on disk first, then call
   `ReplaceReferencedDocument(referencingPath, oldPath, newPath)` for each
   referencing document.
5. **Verify.** Re-read the dependencies of every referencing document and check
   the new path is present and the old one is not.
6. **Report.** Per file, succeeded or failed, and what to do about the failures.

Do not collapse steps 5 and 6 into "no exception was thrown". The call returning
without error is not evidence the reference moved.

## Update your own index

After a successful repair, update the edge table too. The alternative is a
rescan of everything the move touched, which is slower and easy to forget.

## The acceptance test

On **copies** of real files: rename a part used by a test assembly through your
tool, then open the assembly in SolidWorks. It must resolve with no "unable to
locate" prompt.

That prompt appearing is the whole failure mode, and it is the only test that
detects it. A dependency read that reports the new path can still be wrong if
SolidWorks resolves through a search rule rather than the stored path.

## Detecting what needs repair in the first place

An edge whose child path does not exist on disk is a broken reference. If a file
of the same name exists elsewhere in your index, offer it as a candidate fix,
but do not apply it automatically: two parts with the same name in different
folders is common and picking the wrong one is silent.

## See also

- [reading/01 — Dependencies of a closed file](01-dependencies-of-a-closed-file.md)
- [GOTCHAS §16](../../GOTCHAS.md)
