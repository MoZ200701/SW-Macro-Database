using System.Collections.Concurrent;

namespace SwFm.Core.Sw;

/// <summary>
/// Owns a single dedicated STA thread and marshals all work onto it. Every SolidWorks COM
/// call in the app must be scheduled here so the interop objects are only ever touched from
/// the one apartment that created them. See SPEC/00 "Threading model".
/// </summary>
public sealed class StaComRunner : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private volatile bool _disposed;

    public StaComRunner()
    {
        _thread = new Thread(Pump)
        {
            IsBackground = true,
            Name = "SwFm STA COM",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Pump()
    {
        // GetConsumingEnumerable blocks until an item is available and completes once the
        // collection is marked complete for adding (Dispose), after draining what remains.
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            work();
        }
    }

    /// <summary>Schedules <paramref name="work"/> onto the STA thread and returns its result.</summary>
    public Task<T> Run<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(() =>
        {
            try { tcs.SetResult(work()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    /// <summary>Schedules <paramref name="work"/> onto the STA thread.</summary>
    public Task Run(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(() =>
        {
            try { work(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    private void Enqueue(Action action)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StaComRunner));
        _queue.Add(action);
    }

    /// <summary>Drains the queue, then joins the STA thread. COM object release is the
    /// responsibility of the owner (scheduled as the final work item before Dispose).</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        // The STA thread drains any remaining items then exits its pump loop.
        _thread.Join(TimeSpan.FromSeconds(10));
        _queue.Dispose();
    }
}
