using System.Runtime.InteropServices;
using COMTypes = System.Runtime.InteropServices.ComTypes;

namespace SwFm.Core.Sw;

/// <summary>
/// Attaches to an already-running COM server. .NET 8 dropped <c>Marshal.GetActiveObject</c>,
/// so we P/Invoke the underlying APIs.
///
/// <para><b>Why the ROT walk exists:</b> when more than one SolidWorks major version has been
/// installed on a machine, the generic <c>SldWorks.Application</c> ProgID resolves to <i>one</i>
/// version's CLSID (here SW 2024's), while the actually-running session (SW 2026) registers under
/// a different, version-specific CLSID. <see cref="GetActiveObjectOrNull"/> asks for the generic
/// CLSID and therefore misses it. <see cref="FindInRunningObjectTable"/> instead walks the Running
/// Object Table and matches on the moniker's display name, which is version-independent.</para>
/// </summary>
internal static class ComActivation
{
    [DllImport("ole32.dll")]
    private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid clsid, IntPtr reserved,
        [MarshalAs(UnmanagedType.IUnknown)] out object obj);

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(uint reserved, out COMTypes.IRunningObjectTable rot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved, out COMTypes.IBindCtx ctx);

    /// <summary>Attach via the generic ProgID's CLSID. Fast path; misses a running session whose
    /// version-specific CLSID differs from the generic one (see class remarks).</summary>
    public static object? GetActiveObjectOrNull(string progId)
    {
        if (CLSIDFromProgID(progId, out var clsid) != 0) return null;
        return GetActiveObject(ref clsid, IntPtr.Zero, out var obj) == 0 ? obj : null;
    }

    /// <summary>
    /// Walks the Running Object Table and returns the first running object whose moniker display
    /// name satisfies <paramref name="nameMatches"/>, or null if none. Only matching monikers are
    /// bound (unrelated entries — OneDrive, shell handlers — are never materialised). The returned
    /// object is left live for the caller to own; everything else is released.
    /// </summary>
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
                            obj = null; // hand ownership to the caller; skip the release below
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
}
