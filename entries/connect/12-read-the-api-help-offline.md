---
id: connect-12-read-the-api-help-offline
title: Read the SolidWorks API help offline, from the installed .chm files
status: verified
verified_on: SolidWorks 2024 SP5 install, Windows 11, driven from WSL2
language: [shell, powershell]
api: []
keywords: [API help, documentation, chm, sldworksapi.chm, swconst.chm, apihelp.chm, hh.exe, decompile, help.solidworks.com, empty page, JavaScript, enum values, swconst, signature, Remarks, offline docs, spaces in path, Start-Process, silent failure]
answers: "The online API help comes back empty to my fetch tool. How do I read a member's signature, remarks or an enum's values from the docs installed with SolidWorks?"
---

# Read the SolidWorks API help offline, from the installed .chm files

## What this is for

[CONTRIBUTING](../../CONTRIBUTING.md) Rule 1 says never to write a call you
cannot point to documentation for. The documentation is at
`help.solidworks.com`, but a fetch of a member's page there came back as the
site's navigation and footer only, with no signature or remarks, apparently
because the page builds its content with script. Every SolidWorks install
carries the same help as compiled HTML Help files. Unpacked, each member is a
plain HTML file that `grep` and `sed` can read.

## Where the files are

In the install's `api` folder,
`C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS (2)\api\` on the machine this
was found on (the `(2)` is that machine's 2024 folder; check which folder
holds which version). The two that matter:

| File | Holds | Unpacked |
|---|---|---|
| `sldworksapi.chm` (32 MB) | Every interface and member: signature, parameters, return value, Remarks, and the examples | 17,392 files |
| `swconst.chm` | Every enum and its values (`swDisplayStateOpts_e`, `swViewDisplayMode_e`, ...) | 1,331 files |

## Unpacking with `hh.exe`

Windows' own help viewer decompiles a `.chm`. What worked, from WSL, was to
**copy the `.chm` to a path with no spaces first**:

```bash
L=/mnt/c/Users/<user>/AppData/Local/Temp/swapi_claude; mkdir -p $L; cp "/mnt/c/Program Files/SOLIDWORKS Corp/SOLIDWORKS (2)/api/sldworksapi.chm" /mnt/c/Users/<user>/AppData/Local/Temp/swapi.chm; timeout 300 powershell.exe -NoProfile -Command "Start-Process -FilePath hh.exe -ArgumentList '-decompile','C:\Users\<user>\AppData\Local\Temp\swapi_claude','C:\Users\<user>\AppData\Local\Temp\swapi.chm' -Wait" 2>&1 | tr -d '\r'; find "$L" -type f | wc -l; ls $L | head
```

The Windows user name in those paths is replaced by `<user>`; the rest is as
it ran. The output folder must already exist. Delete both the copy and the folder
when done; they are a few hundred megabytes of nothing you need to keep.

## Finding a page

Pages are named after the interop assembly, so a member and an enum are found
by name:

```
SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDocExtension~DeleteDisplayStateSpecificRenderMaterial.html
SolidWorks.Interop.swconst~SolidWorks.Interop.swconst.swDisplayStateOpts_e.html
```

Examples are separate `.htm` files, such as
`Add_and_Delete_Materials_from_Specific_Display_States_Example_VB.htm`; find
the ones that use a member with `grep -l MemberName *.htm`.

Stripping the tags is enough to read a page:

```bash
strip(){ sed -e 's/<[^>]*>//g' -e 's/&nbsp;/ /g;s/&amp;/\&/g;s/&lt;/</g;s/&gt;/>/g' "$1" | tr -d '\r' | tr -s ' \t' | grep -v '^\s*$'; }
```

An enum page's values come out with:

```bash
sed -e 's/<[^>]*>/ /g' -e 's/&nbsp;/ /g' "$C/SolidWorks.Interop.swconst~SolidWorks.Interop.swconst.$e.html" | tr -d '\r' | tr -s ' \t\n' ' ' | grep -oE "sw[A-Za-z_]+ ?[0-9]+[^s]{0,80}"
```

which gave, for example, `swThisDisplayState 1`, `swAllDisplayState 2`,
`swSpecifyDisplayState 3`, and `swViewDisplayMode_ShadedWithEdges 5`.

## Why it is not obvious

**`hh.exe -decompile` fails silently.** No error, no exit code, no files.
Three attempts on `sldworksapi.chm` in place produced **zero files**:

1. `hh.exe` run straight from WSL, output to a `\\wsl.localhost\...` folder.
2. The same, output to `C:\Users\...\AppData\Local\Temp\swapi_claude`.
3. Through `Start-Process ... -Wait`, the `.chm` path quoted.

The fourth, with the `.chm` copied to `%TEMP%\swapi.chm`, gave 17,392 files.
The one thing that changed between the third and fourth was the `.chm`'s
path, which in place is `C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS (2)\...`,
with spaces and brackets. Whether a `\\wsl.localhost` output folder works once
the input has no spaces was not tested, and neither was running `hh.exe`
directly (without `Start-Process`) on the copied file.

Always count the files afterwards. An empty folder is the only sign it failed.

## What it does not do

- The help describes what a member is documented to do, not what it does on
  your version. [connect/11](11-probe-an-api-member-on-a-live-session.md) is
  how to find that out.
- The `.chm` belongs to the installed version. On a machine with several
  versions, read the one matching the session you are driving.

## Evidence

Windows 11 with SolidWorks 2024 SP5 installed, from WSL2, on 2026-10-01. The
three failed attempts each left 0 files in the output folder. The copied
`sldworksapi.chm` unpacked to 17,392 files and `swconst.chm`, copied the same
way, to 1,331. The pages read gave
`DeleteDisplayStateSpecificRenderMaterial(PWMaterialId1, PWMaterialId2)`,
"Array of the first IDs of the appearances to delete" and "Array of the second
IDs", which is what made that call work after it had returned `False`; see
[appearance/01](../appearance/01-read-and-remove-a-part-appearance.md).

## See also

- [connect/11 — Probe an API member on a live session](11-probe-an-api-member-on-a-live-session.md) — the documentation says what exists; a probe says what works
- [connect/10 — Driving from outside Windows](10-driving-from-outside-windows.md)
- [appearance/01 — Read and remove a part's appearance](../appearance/01-read-and-remove-a-part-appearance.md)
- [GOTCHAS §72](../../GOTCHAS.md)
