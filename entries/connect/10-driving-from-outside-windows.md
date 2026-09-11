---
id: connect-10-driving-from-outside-windows
title: Driving Windows SolidWorks from WSL or another host
status: verified
verified_on: WSL2 to SolidWorks 2026
language: [shell, typescript]
api: [cscript, wslpath]
keywords: [wsl, cross platform, cscript, wslpath, path translation, remote drive]
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
Windows only over a network path that some dialogs refuse.

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
