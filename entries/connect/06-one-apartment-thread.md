---
id: connect-06-one-apartment-thread
title: Put every COM call on one apartment thread
status: verified
verified_on: SolidWorks 2026
language: [csharp, python]
api: [CoInitializeEx, Marshal.FinalReleaseComObject]
keywords: [STA, apartment, thread, marshalling, BlockingCollection, CoInitializeEx, COINIT_APARTMENTTHREADED, modal dialog, blocking]
answers: "How do I stop COM calls blowing up or freezing my UI?"
---

# Put every COM call on one apartment thread

**Status:** Verified in both languages
**Language:** C# and Python

## Why

A COM object belongs to the apartment that created it. Touch it from another
thread and the runtime has to marshal the interface across, which at best is
slow and at worst does not work. SolidWorks interop is single-threaded
apartment, so the rule is simple and absolute: **one dedicated STA thread owns
the connection, and every call is scheduled onto it.**

There is a second reason that matters just as much in a GUI. A modal dialog
open in SolidWorks blocks any call for as long as it stays open, and there is
no safe way to cancel one. With the calls on their own thread, that freezes a
worker, not your interface.

The corollary: **nothing COM-shaped may cross back over the queue.** Return
plain data. That is also what makes the code around it testable without
SolidWorks.

## C#

```csharp
public sealed class StaComRunner : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private volatile bool _disposed;

    public StaComRunner()
    {
        _thread = new Thread(Pump) { IsBackground = true, Name = "SwFm STA COM" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Pump()
    {
        // Blocks until an item is available; completes once the collection is
        // marked complete for adding, after draining what remains.
        foreach (var work in _queue.GetConsumingEnumerable()) work();
    }

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

    private void Enqueue(Action action)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(StaComRunner));
        _queue.Add(action);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(10));
        _queue.Dispose();
    }
}
```

`SetApartmentState(ApartmentState.STA)` must be called before `Start()`. Using
it turns every SolidWorks call into an awaitable:

```csharp
public Task<OpResult> ConnectAsync() => Runner.Run(() => { /* COM here */ });
```

Full file: [`code/csharp/StaComRunner.cs`](../../code/csharp/StaComRunner.cs).

## Python

Same shape, plus the explicit apartment initialisation `pywin32` needs:

```python
class Worker:
    """One dedicated apartment-threaded thread, and everything COM on it."""

    def __init__(self, connect_fn=connect):
        self._connect = connect_fn
        self._jobs = queue.Queue()
        self._session = None
        self._busy = threading.Event()
        self._thread = threading.Thread(target=self._pump, name="solidworks", daemon=True)
        self._thread.start()

    def _pump(self):
        if pythoncom is not None:
            pythoncom.CoInitializeEx(pythoncom.COINIT_APARTMENTTHREADED)
        try:
            while True:
                job = self._jobs.get()
                if job is None:
                    return
                work, outbox = job
                self._busy.set()
                try:
                    if self._session is None:
                        self._session = self._connect()
                    outbox.put((work(self._session), None))
                except BaseException as exc:
                    self._session = None          # a failed call invalidates it
                    outbox.put((None, exc))
                finally:
                    self._busy.clear()
        finally:
            self._session = None
            if pythoncom is not None:
                pythoncom.CoUninitialize()

    def submit(self, work):
        outbox = queue.Queue(maxsize=1)
        self._jobs.put((work, outbox))
        return Call(outbox)
```

Two details worth copying. `CoInitializeEx(COINIT_APARTMENTTHREADED)` runs on
the worker thread itself, not on the thread that created it. And a failed call
drops the cached session, so the next call reattaches rather than repeatedly
using a connection to a SolidWorks that has since closed.

## Collecting the result without blocking a GUI

```python
class Call:
    def poll(self):
        """None means not finished. Otherwise (result, exception)."""
        if self._result is None:
            try:
                self._result = self._outbox.get_nowait()
            except queue.Empty:
                return None
        return self._result

    def wait(self, timeout=None):
        if self._result is None:
            self._result = self._outbox.get(timeout=timeout)
        return self._result
```

`poll` is what a tkinter `after` loop or a WPF timer calls. `wait` is for
scripts.

## Disposing safely

Release the COM object **on the thread that made it**, as the last item of
work, before shutting the thread down:

```csharp
Runner.Run(() =>
{
    try { if (launched) sw.ExitApp(); }
    catch (COMException) { /* best-effort shutdown */ }
    Marshal.FinalReleaseComObject(sw);
}).GetAwaiter().GetResult();

Runner.Dispose();
```

## See also

- [connect/09 — Launch versus attach](09-launch-versus-attach.md)
- [files/01 — Batch-convert STEP](../files/01-batch-convert-step.md) — the
  same rule from a compiled exe, with `[STAThread]`
- [`code/python/swcom.py`](../../code/python/swcom.py)
- [connect/11 — Probe an API member on a live session](11-probe-an-api-member-on-a-live-session.md) — one job per probe on the COM thread, with a timeout
