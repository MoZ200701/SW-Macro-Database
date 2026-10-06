---
name: sw-macro-database
description: Proven SolidWorks automation recipes (COM/API from VBA, VBScript, Python and C#), each marked verified or unverified against a real SolidWorks. Use before writing, running or debugging any SolidWorks automation, and when a SolidWorks API call fails or behaves oddly.
---

# SolidWorks macro database (v{{VERSION}})

This skill answers one question: **has this SolidWorks automation problem already
been solved, and is the solution trustworthy?** Check here before writing any
SolidWorks code, so a fresh session does not rediscover what is already known.

## Looking something up

1. Scan the index below. Every entry answers one question and carries its status.
   For keywords and API members, read `manifest.json` in this folder.
2. If an entry matches, read it in full. Entries are self-contained; their code
   lives under `code/`, linked from the entry.
3. To check whether one API member is proven, search `API-LEDGER.md`.
4. Before writing any SolidWorks automation, read `GOTCHAS.md`. It is short, and
   most of it is failure modes that are silent.
5. For a common task, check the prebuilt scripts below before writing code.

## How to read `status`

| Value | What you may do with it |
|---|---|
| `verified` | Use it. It ran against the named SolidWorks version. |
| `partly-verified` | Use it, and check the entry for which part is proven. |
| `unverified` | A good starting point. Test it before relying on it, and say it is untested. |
| `superseded` | Do not use. Follow the link in the entry. |

Do not trust an entry more than its `status` says just because the code reads
well. That distinction is this collection's whole value. A `verified_on` naming a
different SolidWorks major version than the one running is weaker evidence: say so.

**Never invent an API call.** If the database does not cover something and you
cannot point to documentation, say what is missing instead of guessing member
names, argument orders or enum values.

## If the answer is not here

Say so plainly rather than improvising something that looks like an entry. If
you then solve it against a real SolidWorks, or prove an `unverified` entry or
script works, use the `sw-macro-contribute` skill to write it back.

## Prebuilt scripts

Parameterised C# scripts for common tasks. In SolidWorks File Manager, list them
with the `scripts_list` tool and run them with `sw_run_script`. Outside it, the
scripts are in the release package's top-level `scripts/` folder, next to
`skills/`, and the linked entry explains the method. Status works as for entries:
an `unverified` script has not run in this form yet.

{{SCRIPTS}}

## Index

{{INDEX}}
