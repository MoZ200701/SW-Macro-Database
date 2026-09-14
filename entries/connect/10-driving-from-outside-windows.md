---
id: connect-10-driving-from-outside-windows
title: Driving Windows SolidWorks from WSL or another host
status: verified
verified_on: WSL2 to SolidWorks 2026
language: [shell, typescript, python]
api: [cscript, wslpath]
keywords: [wsl, cross platform, cscript, wslpath, path translation, remote drive, python.exe, windows python from wsl, pywin32 from wsl, ext4, \\wsl.localhost, UNC path, carriage return, tr -d]
answers: "My code runs on Linux or a Mac. How do I still drive SolidWorks?"
---

# Driving Windows SolidWorks from WSL or another host

## The shape of the answer

You cannot call COM from Linux. What you can do is **generate a script on the
Linux side, translate the paths, and run it through the Windows script host.**
The whole automation stays a text file until the last moment.

```
your program (Linux)  ->  write files + a .vbs  ->  wslpath -w  ->  cscript.exe
```

This is why VBScript, and not a compiled binary, is the right target for
generated automation. See [connect/01](01-attach-from-vbscript.md).

## Path translation

Everything you hand SolidWorks must be a Windows path. From WSL:

```bash
wslpath -w /mnt/c/Users/you/Desktop/Planes/exports/trainer40
# C:\Users\you\Desktop\Planes\exports\trainer40
```

In code:

```typescript
export function toWindowsDir(dir: string): string {
  const r = spawnSync("wslpath", ["-w", dir], { encoding: "utf8" });
  if (r.status !== 0) throw new Error(`wslpath failed for ${dir}`);
  const win = r.stdout.trim();
  return win.endsWith("\\") ? win : win + "\\";
}
```

Append the trailing backslash once, at the boundary, so the generated script
can concatenate filenames without thinking about it.

## Running the script

```typescript
const r = spawnSync("cscript.exe", ["//nologo", winScriptPath], {
  encoding: "utf8",
});
```

`//nologo` suppresses the Windows Script Host banner so your parser only sees
your own output. The script's `WScript.Echo` lines come back on stdout and its
`WScript.Quit` code becomes the exit status, which is what makes this composable
with the rest of a build.

## Where files live

Keep the repository on the Linux filesystem and write **exports to the Windows
side**, under `/mnt/c/...`. Two reasons: SolidWorks reads them faster from a
native NTFS path, and a file written to the Linux filesystem is reachable from
Windows only over a network path that some dialogs refuse. A PyInstaller build of a
tool kept this way ran from a copy of the checkout under `%USERPROFILE%`; see
[connect/02](02-attach-from-python.md).

## Running Windows Python from WSL, with the checkout on ext4

The same shape works for Python with pywin32, which cannot run in WSL either.
Call the **Windows** interpreter from the WSL shell, and hand it the script
through the path Windows sees the Linux filesystem by. From a repository that
lives on ext4 (`/home/...`), this is the whole launcher:

```bash
#!/usr/bin/env bash
# Probe the SolidWorks API from WSL, through the Windows Python beside SolidWorks.
#
# The repo stays on ext4; Windows reads it through \\wsl.localhost. Every path
# handed to SolidWorks itself is under C:\, and the results come back through
# the same UNC path as plain file I/O. Carriage returns are stripped so the
# report reads cleanly here.
set -o pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
python.exe "$(wslpath -w "$here/probe.py")" --out "$(wslpath -w "$here/probe/results")" "$@" | tr -d '\r'
exit "${PIPESTATUS[0]}"
```

- `python.exe` is the Windows Python that has pywin32, found on the Windows
  `PATH` through WSL interop. The `.exe` is what makes WSL run the Windows one.
- `wslpath -w` on an ext4 path gives a UNC path,
  `\\wsl.localhost\Ubuntu\home\...`. Windows Python imports the code and writes
  its results there as ordinary file I/O.
- `tr -d '\r'` strips the carriage returns of Windows console output.
  `set -o pipefail` and `exit "${PIPESTATUS[0]}"` keep Python's exit code
  rather than `tr`'s.
- **Every path handed to SolidWorks is under `C:\`.** Files the probe saved
  went to `%USERPROFILE%\Documents\...`, and the probe that saves refuses any
  folder that is not a drive path. Whether SolidWorks will save to a
  `\\wsl.localhost` path was not tested.

The script it runs puts the checkout's `src` on the import path, so nothing is
installed on the Windows side except Python and pywin32:

```python
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "src"))

from gear_generator.probe.__main__ import main

if __name__ == "__main__":
    raise SystemExit(main())
```

And the equivalent for a Windows shell, which works whether the repository is
on `C:` or inside WSL, because `pushd` maps a UNC path to a temporary drive
letter:

```bat
@echo off
REM Probe the SolidWorks API on the running session and write the results into
REM probe\results. SolidWorks must already be open; nothing is launched.
REM
REM pushd maps the \\wsl.localhost path of a WSL checkout to a drive letter, so
REM this works whether the repo sits on C: or inside WSL.
pushd "%~dp0"
python "%~dp0probe.py" --out "%~dp0probe\results" %*
set CODE=%ERRORLEVEL%
popd
exit /b %CODE%
```

**Verified, WSL2 to SolidWorks 2026 (revision 34.0.0).** The run recorded its
own arguments as
`['--out', '\\\\wsl.localhost\\Ubuntu\\home\\<user>\\Gear-Generator\\probe\\results', '--tier', '5', '--timeout', '400']`:
launched from WSL through the script above, it attached to SolidWorks, ran 33
probes (all passed, exit code 0), saved parts under `C:\Users\...\Documents`,
and wrote its report and JSON back into the ext4 checkout through the UNC
path. The `.bat` was not the launcher for that run. See
[connect/11](11-probe-an-api-member-on-a-live-session.md).

## Structuring the output so it can be parsed

The generated script should emit one line per operation with a fixed-width
status token:

```
attach  SldWorks.Application.34
part    trainer40
  ok      S1
  ok      S2
  MISSING S9 - import it by hand, or run with --insert
rebuild True
done    2 refreshed, 1 failed
```

Lowercase for success, uppercase for anything the caller must act on. It reads
fine to a person and parses with a `startsWith`.

## From macOS

There is no equivalent of `wslpath`, and no local Windows to run `cscript`. The
practical options are a shared folder plus running the script by hand on the
Windows machine, or SSH into the Windows box with OpenSSH Server enabled and
`cscript` invoked over that. The generation side is unchanged either way, which
is the point of keeping the automation textual.

## See also

- [connect/01 — Attach from VBScript](01-attach-from-vbscript.md)
- [curves/04 — Reload a curve in place](../curves/04-reload-curve-in-place.md)
- [connect/02 — Attach from Python](02-attach-from-python.md) — what the Windows Python then does
- [connect/11 — Probe an API member on a live session](11-probe-an-api-member-on-a-live-session.md) — the run launched this way
- [documents/02 — Save as, and close](../documents/02-save-as-and-close.md) — saving under `C:\`
