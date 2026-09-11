using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace SwFm.Core.Sw;

/// <summary>
/// Owns the single connection to SolidWorks and the STA thread all COM calls run on.
/// Either attaches to a running session or launches a hidden background instance.
/// See SPEC/01.
/// </summary>
public sealed class SwConnector : IDisposable
{
    public enum State { Disconnected, AttachedToRunning, LaunchedHidden }

    private const string ProgId = "SldWorks.Application";

    /// <summary>
    /// SolidWorks registers each live session in the Running Object Table under a moniker named
    /// <c>SolidWorks_PID_&lt;pid&gt;</c>. This prefix is stable across major versions, so matching on
    /// it attaches to the user's running session regardless of which SolidWorks CLSID the generic
    /// <c>SldWorks.Application</c> ProgID happens to resolve to on this machine.
    /// </summary>
    private const string RotMonikerPrefix = "SolidWorks_PID_";

    private ISldWorks? _sw;

    public State ConnectionState { get; private set; } = State.Disconnected;

    /// <summary>The single STA thread every COM call is marshalled onto.</summary>
    public StaComRunner Runner { get; } = new();

    /// <summary>
    /// Attaches to a running SolidWorks if present, otherwise launches one hidden.
    /// Never sets <c>UserControl = true</c>; a launched instance stays invisible.
    /// </summary>
    public Task<OpResult> ConnectAsync() => Runner.Run(() =>
    {
        try
        {
            // First try the generic ProgID's CLSID (works when it resolves to the running version),
            // then fall back to the version-independent ROT moniker so a session registered under a
            // different SolidWorks CLSID than the generic ProgID is still found.
            var attached = ComActivation.GetActiveObjectOrNull(ProgId)
                ?? ComActivation.FindInRunningObjectTable(
                    name => name.StartsWith(RotMonikerPrefix, StringComparison.OrdinalIgnoreCase));
            if (attached is ISldWorks running)
            {
                _sw = running;
                ConnectionState = State.AttachedToRunning;
                return OpResult.Success;
            }

            var type = Type.GetTypeFromProgID(ProgId);
            if (type is null)
                return OpResult.Fail($"SolidWorks is not installed (ProgID '{ProgId}' not found).");

            if (Activator.CreateInstance(type) is not ISldWorks app)
                return OpResult.Fail("Failed to create a SolidWorks application instance.");

            app.Visible = false;
            _sw = app;
            ConnectionState = State.LaunchedHidden;
            return OpResult.Success;
        }
        catch (COMException ex)
        {
            ConnectionState = State.Disconnected;
            return OpResult.Fail($"COM error connecting to SolidWorks: 0x{ex.HResult:X8} {ex.Message}");
        }
    });

    /// <summary>
    /// Direct (immediate-child) dependencies of the given file, read without opening it.
    /// This is the primitive; callers build the full tree by recursing on each child path.
    /// Virtual components (name contains '^') are skipped. Returns empty on any COM failure.
    /// </summary>
    public Task<IReadOnlyList<(string Name, string Path)>> GetDependenciesAsync(string filePath) =>
        Runner.Run<IReadOnlyList<(string, string)>>(() =>
        {
            var sw = _sw;
            if (sw is null) return Array.Empty<(string, string)>();

            try
            {
                // GetDocumentDependencies2(document, traverseFlag: false = direct children only,
                //   searchFlag: false = use stored paths (no search rules), addReadOnlyInfo: false).
                // Returns a flat array alternating name, path, name, path, ...
                var raw = sw.GetDocumentDependencies2(filePath, false, false, false);

                IReadOnlyList<string>? flat = raw switch
                {
                    string[] s => s,
                    object[] o => Array.ConvertAll(o, x => x?.ToString() ?? string.Empty),
                    _ => null,
                };
                if (flat is null) return Array.Empty<(string, string)>();

                var deps = new List<(string, string)>(flat.Count / 2);
                for (int i = 0; i + 1 < flat.Count; i += 2)
                {
                    var name = flat[i];
                    var path = flat[i + 1];
                    if (name.Contains('^')) continue; // virtual component — never an edge
                    deps.Add((name, path));
                }
                return deps;
            }
            catch (COMException)
            {
                return Array.Empty<(string, string)>();
            }
        });

    /// <summary>
    /// Releases the COM object on the STA thread — exiting the app only if we launched it —
    /// then disposes the runner. Attaching to a user's session never closes their SolidWorks.
    /// </summary>
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
}
