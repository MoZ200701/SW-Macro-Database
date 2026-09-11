---
id: connect-09-launch-versus-attach
title: Launch versus attach, and never closing someone's session
status: partly-verified
verified_on: SolidWorks 2026 for attach; launch path compiles and runs, ExitApp untested
language: [csharp, vbscript]
api: [ISldWorks.Visible, ISldWorks.ExitApp, Activator.CreateInstance, Type.GetTypeFromProgID]
keywords: [launch solidworks, hidden instance, CreateObject, ExitApp, FinalReleaseComObject, headless]
answers: "Should my tool start SolidWorks if none is running, and how do I clean up?"
---

# Launch versus attach, and never closing someone's session

## The decision

| Situation | Do this |
|---|---|
| A tool that operates on the part the user is looking at | Attach only. Fail if nothing is running. |
| A batch job on a build machine | Launch hidden. |
| A tool that could be either | Attach, and make launching an explicit flag. |

The reason to default against launching is that `CreateObject` succeeds when
`GetObject` fails, and if the reason `GetObject` failed was a ProgID resolving
to the wrong install ([GOTCHAS §1](../../GOTCHAS.md)), you have just started a
**second** SolidWorks while the first sits on screen. You then silently edit an
invisible document.

## Launching hidden, in C#

```csharp
var type = Type.GetTypeFromProgID(ProgId);
if (type is null)
    return OpResult.Fail($"SolidWorks is not installed (ProgID '{ProgId}' not found).");

if (Activator.CreateInstance(type) is not ISldWorks app)
    return OpResult.Fail("Failed to create a SolidWorks application instance.");

app.Visible = false;
_sw = app;
ConnectionState = State.LaunchedHidden;
return OpResult.Success;
```

Never set `UserControl = true` on an instance you launched. That flag tells
SolidWorks the instance belongs to a person, and it will then outlive your
process.

## Track which one you did

This is the whole point of the state enum:

```csharp
public enum State { Disconnected, AttachedToRunning, LaunchedHidden }
```

Because it decides what happens on the way out.

## Disposing

```csharp
public void Dispose()
{
    var sw = _sw;
    if (sw is not null)
    {
        var launched = ConnectionState == State.LaunchedHidden;
        try
        {
            Runner.Run(() =>
            {
                try { if (launched) sw.ExitApp(); }
                catch (COMException) { /* best-effort shutdown */ }
                Marshal.FinalReleaseComObject(sw);
            }).GetAwaiter().GetResult();
        }
        catch (COMException) { /* connection already gone */ }
        _sw = null;
    }

    Runner.Dispose();
    ConnectionState = State.Disconnected;
}
```

Three things here, all deliberate:

- **`ExitApp` only if you launched it.** Closing a session you attached to
  throws away the user's unsaved work. This is the single worst thing an
  automation tool can do.
- **Release on the thread that made it.** See
  [connect/06](06-one-apartment-thread.md). `FinalReleaseComObject` rather than
  `ReleaseComObject`, because you are done with it entirely.
- **Swallow COM exceptions on the way out.** By the time you are disposing, the
  connection may already be gone, and throwing from a dispose obscures whatever
  the real problem was.

## Startup cost

Launching is slow, on the order of tens of seconds on a cold start. Attach if
you possibly can, and if your tool runs repeatedly, keep the connection alive
between operations rather than reconnecting each time. The Python worker in
[connect/06](06-one-apartment-thread.md) caches its session for exactly this
reason and drops it only when a call fails.

## Student licences

A single-seat licence generally allows one instance. If SolidWorks is already
open, launching a second may fail on licensing rather than on COM. Attach.

## See also

- [connect/01 — Attach from VBScript](01-attach-from-vbscript.md)
- [connect/03 — Attach from C#](03-attach-from-csharp.md)
