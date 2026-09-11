---
id: connect-03-attach-csharp
title: Attach to a running SolidWorks from C# on .NET 8
status: partly-verified
verified_on: SolidWorks 2026 for attach and dependency read
language: [csharp]
api: [GetActiveObject, GetRunningObjectTable, CreateBindCtx, CLSIDFromProgID]
keywords: [dotnet 8, Marshal.GetActiveObject, P/Invoke, ole32, oleaut32, interop, EmbedInteropTypes, wpf]
answers: "Marshal.GetActiveObject is gone on .NET 8. How do I attach to SolidWorks?"
---

# Attach to a running SolidWorks from C# on .NET 8

**Status:** Verified as compiling and connecting; the dependency read on top of
it is verified against SolidWorks
**Language:** C#, .NET 8, `net8.0-windows`

## The problem

`Marshal.GetActiveObject` was dropped after .NET Framework. On modern .NET
there is no in-box way to attach to a running COM server, so you P/Invoke it
yourself.

There are two routes and you want both, in order.

## Route one: the ProgID's class id

Fast, and correct whenever the generic ProgID resolves to the version that is
actually running.

```csharp
[DllImport("ole32.dll")]
private static extern int CLSIDFromProgID(
    [MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);

[DllImport("oleaut32.dll")]
private static extern int GetActiveObject(ref Guid clsid, IntPtr reserved,
    [MarshalAs(UnmanagedType.IUnknown)] out object obj);

public static object? GetActiveObjectOrNull(string progId)
{
    if (CLSIDFromProgID(progId, out var clsid) != 0) return null;
    return GetActiveObject(ref clsid, IntPtr.Zero, out var obj) == 0 ? obj : null;
}
```

## Route two: walk the Running Object Table

Needed when several major versions are installed, because then the generic
ProgID resolves to one version's class id while the running session registers
under another. Matching on the moniker's display name is version-independent.

```csharp
[DllImport("ole32.dll")]
private static extern int GetRunningObjectTable(uint reserved,
    out COMTypes.IRunningObjectTable rot);

[DllImport("ole32.dll")]
private static extern int CreateBindCtx(uint reserved, out COMTypes.IBindCtx ctx);

public static object? FindInRunningObjectTable(Func<string, bool> nameMatches)
{
    if (GetRunningObjectTable(0, out var rot) != 0) return null;
    CreateBindCtx(0, out var ctx);
    rot.EnumRunning(out var enumMoniker);
    var monikers = new COMTypes.IMoniker[1];
    try
    {
        while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
        {
            var moniker = monikers[0];
            try
            {
                string displayName;
                try { moniker.GetDisplayName(ctx, null, out displayName); }
                catch (COMException) { continue; }

                if (!nameMatches(displayName)) continue;

                object? obj = null;
                try
                {
                    if (rot.GetObject(moniker, out obj) == 0 && obj is not null)
                    {
                        var found = obj;
                        obj = null;   // hand ownership to the caller
                        return found;
                    }
                }
                catch (COMException) { /* transient entry; keep scanning */ }
                finally { if (obj is not null) Marshal.ReleaseComObject(obj); }
            }
            finally { Marshal.ReleaseComObject(moniker); }
        }
    }
    finally
    {
        Marshal.ReleaseComObject(enumMoniker);
        Marshal.ReleaseComObject(ctx);
        Marshal.ReleaseComObject(rot);
    }
    return null;
}
```

Only matching monikers are bound. Everything else is released. The one object
handed to the caller has its release deliberately skipped.

## Putting them together

```csharp
private const string ProgId = "SldWorks.Application";
private const string RotMonikerPrefix = "SolidWorks_PID_";

var attached = ComActivation.GetActiveObjectOrNull(ProgId)
    ?? ComActivation.FindInRunningObjectTable(
        name => name.StartsWith(RotMonikerPrefix, StringComparison.OrdinalIgnoreCase));

if (attached is ISldWorks running)
{
    _sw = running;
    ConnectionState = State.AttachedToRunning;
    return OpResult.Success;
}
```

## Project setup

Reference the interop assemblies from the SolidWorks install:

```
C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\
    SolidWorks.Interop.sldworks.dll
    SolidWorks.Interop.swconst.dll
```

with `EmbedInteropTypes=false` and `CopyLocal=true`:

```xml
<ItemGroup>
  <Reference Include="SolidWorks.Interop.sldworks">
    <HintPath>C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll</HintPath>
    <EmbedInteropTypes>false</EmbedInteropTypes>
    <Private>true</Private>
  </Reference>
  <Reference Include="SolidWorks.Interop.swconst">
    <HintPath>C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll</HintPath>
    <EmbedInteropTypes>false</EmbedInteropTypes>
    <Private>true</Private>
  </Reference>
</ItemGroup>
```

Target `net8.0-windows`. For a WPF front end add `<UseWPF>true</UseWPF>`.

## Full source

- [`code/csharp/ComActivation.cs`](../../code/csharp/ComActivation.cs) — both routes
- [`code/csharp/SwConnector.cs`](../../code/csharp/SwConnector.cs) — connect, read dependencies, dispose
- [`code/csharp/StaComRunner.cs`](../../code/csharp/StaComRunner.cs) — the thread every call runs on

## See also

- [connect/06 — One apartment thread](06-one-apartment-thread.md)
- [connect/09 — Launch versus attach](09-launch-versus-attach.md)
- [reading/01 — Dependencies of a closed file](../reading/01-dependencies-of-a-closed-file.md)
