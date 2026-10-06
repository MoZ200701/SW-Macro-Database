# Writing entries

This repo is written mostly by AI agents and read mostly by AI agents. That
makes quality control different from a normal docs repo: a confidently wrong
entry will be trusted and acted on. Changes from anyone but the owner go through
a pull request the owner reviews, but a reviewer cannot re-run your SolidWorks
session. The rules below exist to make what you write safe to trust anyway.

**If you are about to add or change an entry, read this file first and follow
the checklist at the bottom.**

---

## The one rule everything else follows from

**An entry states what is known, marked by how it is known.**

This repo's value is not that it contains code. It is that it distinguishes
code that has been observed working from code that merely looks right. Blur
that and the repo becomes worse than nothing, because it will be trusted.

## Rule 1 — Never invent an API call

If you cannot point to documentation or to code that ran, do not write the call.

Write down what is missing instead, as a limitation, and say what a person
would do by hand. [sketches/06](entries/sketches/06-what-the-sketch-api-cannot-do.md)
is the model for this: every limit is named, the manual workaround is spelled
out, and the count is stated up front.

A macro that silently does the wrong thing is worse than one that stops.

**Specifically forbidden:** plausible-looking member names, argument orders
guessed from a similar call, enum constants you have not seen, and "this should
work" code.

## Rule 2 — `status` is a claim about evidence, and it is load-bearing

| Value | Means |
|---|---|
| `verified` | It was run against SolidWorks and the result was observed. `verified_on` names the version. |
| `partly-verified` | Some of it was run. The entry says which part, in the body. |
| `unverified` | Well-formed against the documented API. Nobody has watched it run. |

`verified` requires **evidence in the entry**: the version, and what was
observed. "All 12 curves refreshed, `ForceRebuild3` returned True, the surface
rebuilt with no reference re-picked" is evidence. "This works" is not.

Do not upgrade a status because the code looks fine. Do not upgrade it because
a test passed that did not involve SolidWorks. Only running it counts.

**Downgrading is always allowed and never needs permission.** If you find
something that contradicts an entry, change the status and say what you found.

## Rule 3 — Code must be real

Copy it from something that ran, or generate it with the tool that generates
it. Do not paraphrase code from memory and do not tidy it on the way in.

No elisions that hide a needed line. `' ... entities here ...` is fine for a
section you describe elsewhere; dropping the `ClearSelection2` from a selection
sequence is not, because someone will copy it.

If the entry's code is longer than about 150 lines, put the full file in
[`code/`](code/) and quote the important part in the entry.

## Rule 4 — One entry answers one question

The `answers:` field is a question a future agent would actually type. Write it
first. If you cannot phrase one, you have a section of another entry, not an
entry.

Do not split a single mechanism across two entries, and do not bundle two
mechanisms into one.

## Rule 5 — Search before you write

Read [`manifest.json`](manifest.json) and grep the keywords. If an entry covers
the mechanism already, **extend it**. A second entry on the same call is worse
than no entry, because the reader gets two answers and has to choose.

If your finding contradicts an existing entry, that is not a new entry either.
Fix the old one and record what changed.

## Rule 6 — Record failures, not just successes

An entry saying "this call does not work on this interface, here is the error
number" saves as much time as one saying how to do it. Error 438 on
`InsertCurveFile` and error 429 from the wrong ProgID are two of the most useful
facts in this repo, and both are failures.

Open questions belong in the entry too, explicitly marked, with what it would
take to settle them. See the 3D arc question in
[sketches/02](entries/sketches/02-3d-sketch-as-vba.md).

## Rule 7 — Always state the units and the interface

Every entry that crosses the millimetre-to-metre boundary says which side it is
on. Every API member is attributed to the interface it lives on. Both of these
have cost real time when omitted.

## Rule 8 — Say which version, and do not generalise past it

"Verified on SolidWorks 2026" is a fact. "Works on SolidWorks" is a guess
dressed as a fact. Majors: 32 is 2024, 33 is 2025, 34 is 2026.

## Rule 9 — The entry must stand alone

A reader has only this repo. No "see the fuselage project for details", no
reference to a file that is not in `code/`. Naming where something came from is
fine and useful; depending on it is not.

## Rule 10 — Do not delete evidence

If an entry turns out to be wrong, correct it and say what was wrong. Deleting
the claim loses the information that someone once believed it, which is the
thing that stops it being rediscovered.

Retire an entry by setting `status: superseded` and linking to what replaced it.
Keep the file.

## Prebuilt scripts

`scripts/` holds parameterised C# scripts for common tasks. They ship in every
release and run inside SolidWorks File Manager, which supplies the SolidWorks
connection, backs up what a script declares it will modify, and re-checks the
result. C# is the only runnable format: VBScript is deprecated, Python needs
installs, and VBA macros (`.swp`) are binary and cannot be reviewed.

Each script is a folder `scripts/<id>/` with `script.json` (what it needs and
what it does) and `script.csx` (the code). Every rule above applies, plus:

- **Link an entry.** `entry` names the entry that explains the method. A script
  is the runnable form of knowledge that already lives in an entry.
- **`status` is about this exact script.** A port of verified code is
  `unverified` until the script itself has run and been observed. Evidence goes
  in `evidence`, the version in `verified_on`.
- **Declare effects truthfully.** `read-only`, `writes-new-files` (only inside
  the param with `role: output`) or `modifies-models` (only the files in the
  param with `role: targets`, which the host backs up first). Set
  `irreversible: true` when undoing is impossible, such as a file-version upgrade.
- **Use only `ctx.Sw`.** Never attach to or start SolidWorks yourself.
- **Never close a document the script did not open.** Close only what you
  opened, by its own title.
- **Restore any user preference you change**, in `finally`. If you cannot read
  a preference first, do not change it.
- **Report every file** through `ctx.Wrote` / `ctx.Modified`, and failures
  through `ctx.Failed`. The host fails a run that touched a file it did not
  declare.
- **Honour `ctx.DryRun`** (write nothing, log what would happen) and
  **`ctx.Cancel`** (check between files).
- Synchronous code only: no asynchronous waits, no network, no processes, no
  `#r` directives.

`python3 tools/validate.py` checks the header and the forbidden calls.

---

## Mechanics

### Frontmatter is mandatory and machine-parsed

```yaml
---
id: curves-02-insert-curve-from-file      # unique, kebab-case, stable forever
title: Insert a curve from a file as a new feature
status: verified                          # verified | partly-verified | unverified | superseded
verified_on: SolidWorks 2026              # or null
language: [vbscript, python]              # vba | vbscript | python | csharp | typescript | any | n/a
api: [IModelDoc2.InsertCurveFile]         # members this entry teaches, Interface.Member
keywords: [InsertCurveFile, error 438]    # what someone would grep for, including error numbers
answers: "How do I import a .sldcrv file as a feature from code?"
---
```

`id` never changes once published. Other entries link by path, and the manifest
keys on it.

### After any change, regenerate the manifest

```
python3 tools/build_manifest.py
```

It rebuilds [`manifest.json`](manifest.json) from the frontmatter and fails if
an entry is missing a required field, has a duplicate `id`, or claims
`status: verified` with no `verified_on`. Commit the regenerated file.

### Update the cross-references

- [INDEX.md](INDEX.md) — add the row to the right task table
- [API-LEDGER.md](API-LEDGER.md) — add any new API member, with its status
- [GOTCHAS.md](GOTCHAS.md) — add anything that cost you an hour
- The **See also** section of every entry the new one relates to, both directions

An entry nothing links to will not be found.

### Start from the template

[`entries/_TEMPLATE.md`](entries/_TEMPLATE.md).

### Check before you push

```
python3 tools/validate.py
```

The same check runs on every pull request and must pass before a merge. It
re-runs the manifest checks, confirms the committed `manifest.json` is current,
validates every script, and builds the release package once.

### How changes get in

- **Anyone but the owner:** fork, branch, and open a pull request against
  `main`. The owner reviews it; `main` cannot be pushed to directly.
- **Can't open a pull request?** Open a *Verification report* issue with what
  you ran and what you observed. The owner turns it into the change.
- **The owner** may push straight to `main`; the checks still run afterwards.

Never add co-author or "generated by" trailers to commits.

### Releases

The owner tags a commit on `main` as `vX.Y.Z`. A workflow validates it, builds
`swfm-skills-X.Y.Z.zip` (the skills and the scripts) and publishes a GitHub
Release whose notes list changed entries and, first, any changed scripts.
SolidWorks File Manager installs releases that change no script by itself and
asks before installing one that does.

---

## Checklist before you commit

- [ ] Frontmatter complete, `id` unique, `answers` is a real question
- [ ] `status` matches the evidence actually in the body
- [ ] `verified` entries name the version and say what was observed
- [ ] Every API member attributed to its interface
- [ ] Units stated wherever the boundary is crossed
- [ ] No guessed calls; limits named instead
- [ ] Code copied or generated, not recalled; complete enough to run
- [ ] Searched the manifest; not a duplicate
- [ ] Entry stands alone, with no dependency on an external repo
- [ ] `manifest.json` regenerated
- [ ] INDEX, API-LEDGER, GOTCHAS and See-also links updated
- [ ] Scripts: effects declared truthfully, only `ctx.Sw`, nothing closed that the script did not open, preferences restored
- [ ] `python3 tools/validate.py` passes
