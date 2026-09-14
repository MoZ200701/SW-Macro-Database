---
id: connect-11-probe-an-api-member-on-a-live-session
title: Probe an API member on a live session before relying on it
status: verified
verified_on: SolidWorks 2026 (revision 34.0.0)
language: [python]
api: [ISldWorks.NewDocument, ISldWorks.CloseDoc, ISldWorks.GetFirstDocument, IModelDoc2.GetNext, ISelectionMgr.GetSelectedObjectCount2]
keywords: [probe, harness, verify API, test against SolidWorks, scratch document, timeout, modal dialog, hang, HRESULT, argerror, com_error, evidence, latest.json, API ledger, skip cascade, cleanup]
answers: "How do I find out whether a SolidWorks API call really works before my tool depends on it?"
---

# Probe an API member on a live session before relying on it

## What this is for

The API help is a list of what members exist. It is not evidence that a call
does what its name says on the version you have: in one run on SolidWorks 2026,
`IEquationMgr.Add3` returned -1, `sgEQUAL` left two circles unequal,
`FeatureCut4` in its default direction made nothing, and an Equation Driven
Curve refused `*180/pi`, all without an error. A probe is a small script that
calls the member on a live session, on a document it made itself, and records
what actually happened, so the tool is written from observations. This entry is
how to build one that is safe to run against a session somebody is using.

## The shape

A registry of small probe functions, each asserting through a context object,
run in order on the one COM thread, each on a timeout:

```python
@probe("axis", needs=("new_document",), tier=2, members=("IModelDoc2.InsertAxis2",))
def axis(px: Px) -> None:
    """An axis from the second and third planes, which is the normal of the first."""
    with sc.scratch(px) as doc:
        px.require(sc.select_plane(doc, 2) and sc.select_plane(doc, 3, append=True), "planes 2 and 3 select together")
        before = sc.feature_names(doc)
        made = call(doc, "InsertAxis2", True)
        call(doc, "ClearSelection2", True)
        px.fact("insert_axis2_returns", made)
        new = sc.new_features(doc, before)
        px.fact("axis_new_features", new)
        px.require(len(new) == 1, "InsertAxis2 adds exactly one feature")
        feature = sc.feature_by_name(doc, new[0][0])
        feature.Name = "Gear Axis"
        px.check(str(call(feature, "Name")) == "Gear Axis", "the axis renames and reads back")
        px.fact("axis_type_name", sc.type_name(feature))
        px.check(sc.select_feature(doc, feature), "the axis can be selected again")
        call(doc, "ClearSelection2", True)
```

That is a whole probe. The rest of this entry is the five rules that make it
trustworthy. The full harness is in
[`code/python/gear_generator/probe/`](../../code/python/gear_generator/probe/harness.py).

### 1. Work only on documents the probe made, and prove it afterwards

```python
@contextlib.contextmanager
def scratch(px: Px, doc_type: int = SW_DOC_PART, path: str = "") -> Iterator[Any]:
    """A fresh document for the length of a probe, closed afterwards unless --keep."""
    doc = new_document(px, doc_type, path)
    try:
        yield doc
    finally:
        try:
            app(px).CommandInProgress = False
        except Exception:  # noqa: BLE001 - restoring the flag must not mask the probe's own error
            pass
        try:
            close(px, doc)
        except Exception as exc:  # noqa: BLE001
            px.note(f"CLEANUP could not close the scratch document: {exc}")


def close(px: Px, doc: Any) -> None:
    """Close a document this probe made. Never one it did not."""
    title = str(call(doc, "GetTitle"))
    if title not in px.shared.get("created", []):
        raise Require(f"Refusing to close {title}: the probe did not create it.")
    if px.options.get("keep"):
        return
    call(app(px), "CloseDoc", title)
```

`new_document` records each title it makes in `px.shared["created"]`. The run
lists the open documents (`GetFirstDocument` / `GetNext`) before the first probe
and after the last, and exits with a distinct code if a scratch document was
left open or a document that was open before is gone. `CloseDoc` does not ask
about unsaved changes ([documents/02](../documents/02-save-as-and-close.md)),
which is exactly why the title check is there.

### 2. One job per probe, with a timeout

```python
def worker_executor(worker: Any) -> Executor:
    """Run each probe as one job on the COM worker thread, with a timeout."""

    def execute(body: Callable[[], None], timeout: float) -> Tuple[bool, Optional[BaseException]]:
        call = worker.submit(lambda _session: body())
        try:
            _value, error = call.wait(timeout=timeout)
        except queue.Empty:
            return False, None
        return True, error

    return execute
```

Every COM call stays on one apartment thread
([connect/06](06-one-apartment-thread.md)). A modal dialog in SolidWorks blocks
a call for as long as it is open and nothing can cancel it, so a probe that
does not answer in time is marked **hung**, the run stops, and every later
probe is reported as not run — the report is still written. Known dialogs are
switched off around the probe that would raise them (the dimension value dialog,
`swInputDimValOnCreate`, preference toggle 10, in a context manager that
restores it).

### 3. A failure is a record, with the HRESULT

```python
def attempt(self, label: str, fn: Callable[[], Any], expect_error: bool = False) -> Tuple[bool, Any]:
    """Call something that may raise; record either outcome as a fact.

    Returns ``(succeeded, value_or_error_dict)``. Nothing is swallowed
    silently: a raise becomes a line carrying the HRESULT and argerror.
    """
    try:
        value = fn()
    except Require:
        raise
    except Exception as exc:  # noqa: BLE001 - recording the failure is the point
        info = describe_error(exc)
        self.record.lines.append(Line("attempt", f"{label}: raised {one_line(exc)}", info))
        self.record.facts[f"attempt:{label}"] = {"ok": False, "error": info}
        return False, info
    shown = value if isinstance(value, (bool, int, float, str, type(None))) else type(value).__name__
    self.record.lines.append(Line("attempt", f"{label}: returned {shown!r}", shown))
    self.record.facts[f"attempt:{label}"] = {"ok": True, "value": shown}
    return True, value
```

`describe_error` unpacks a `pythoncom.com_error`: the HRESULT and its name
(`DISP_E_TYPEMISMATCH`, `DISP_E_BADPARAMCOUNT`, ...), the server's own
description, and `argerror`, the 1-based position of the argument the
dispatcher rejected — which settles a wrong argument count or type without
guessing. Try several plausible forms of a call in turn and record every one;
the forms that failed are findings too.

### 4. Judge by the outcome, not the return value

The probes in this collection check where the geometry went (a relation by the
point's new coordinates), what the part's volume became
([reading/10](../reading/10-mass-properties-as-an-oracle.md)), what the
feature tree gained (a diff of names), and whether the selection count went up
— never only that a call returned `True` or an object. Two concrete reasons
from the same run: `IModelDoc2.SaveAs3` returned `0` and the file was on disk;
`sgEQUAL` returned normally and changed nothing.

### 5. Skip what depends on a failure, and keep the evidence

```python
missing = [need for need in p.needs if status.get(need) != PASS]
if missing:
    record.status = SKIP
    record.reason = "needs " + ", ".join(
        f"{need} ({status.get(need, NOT_RUN)})" for need in missing
    )
```

A probe names the probes it `needs`; if one did not pass, it is skipped with the
reason, so one early failure shows once rather than as a cascade. Every record
carries the SolidWorks version, and the run writes a text report and
`latest.json`, which is committed. The tool's constants file names the probe
behind each value, and a test checks those constants against `latest.json`, so
the code cannot drift from the evidence.

## Why it is not obvious

**The `members` list is a claim, and it can over-claim.** Each probe declares
the members it exercises, and `--ledger` turns a passing probe's list into
"Verified" rows. In the run recorded here, four declared members were never
invoked: `IEquationMgr.EvaluateAll`, `IFeatureManager.FeatureCut3` and
`IFeatureManager.FeatureCircularPattern4` appear in no call, and
`IAssemblyDoc.FixComponent` was fetched but not called (it came back as a
Python `method` object; see [assemblies/01](../assemblies/01-new-assembly-and-insert-components.md)).
Read the generated rows against the code before copying them into a ledger.
(`FixComponent` was then called properly, as `asm.FixComponent()`, and
verified on a rerun; the other three are still never called.)

**A probe that passes may be passing for the wrong reason.** The
`FixComponent` check passed because the component it checked was already fixed
on insert. Where a probe asserts a state, make sure the state was not already
true before the call. The fix did exactly that: the probe now also fixes the
free second component, recording `IsFixed` `False` before and `True` after.

**Record what the environment was.** The first probes of the run recorded which
template the default resolved to (another year's, in inches) and made every
later part from a millimetre template. Without that record, every length after
it would have been open to question.

## What it does not do

- It cannot probe a member that needs a person to click something.
- A hung probe stops the run: the dialog has to be closed in SolidWorks by
  hand before anything else can be probed.
- The harness imports the application's own COM module (`swcom`) and, for the
  equation and end-to-end probes, its gear maths (`gears`, `spec`, `swbuild`).
  Those application modules are not copied into this repo, so
  [`code/python/gear_generator/`](../../code/python/gear_generator/probe/harness.py)
  is reference code: `harness.py`, `comerr.py` and `report.py` are
  self-contained; the `p*_*.py` probes show the calls exactly as they ran.

## Evidence

SolidWorks 2026 (revision 34.0.0), Python over pywin32, run 20260914-180426
with `--tier 5 --timeout 400`: 33 probes, 33 passed, exit code 0, no document
left open and none closed that was open before. The report and the JSON are in
[`code/python/gear_generator/probe/results/`](../../code/python/gear_generator/probe/results/probe-results-20260914-180426.txt).
Every entry in this collection verified on "SolidWorks 2026 (revision 34.0.0)"
cites a probe from that run by name.

A rerun, 20260914-183429, with `FixComponent` invoked properly and probed on a
free component, also passed 33 of 33 with exit code 0. Its report and JSON sit
beside the first run's, and `latest.json` is that rerun.

## See also

- [connect/06 — One apartment thread](06-one-apartment-thread.md)
- [connect/02 — Attach from Python](02-attach-from-python.md)
- [connect/10 — Driving from outside Windows](10-driving-from-outside-windows.md) — running the probe from WSL
- [reading/07 — Report the feature type on failure](../reading/07-report-feature-type-on-failure.md) — the same principle inside a generated script
- [reading/10 — Mass properties as an oracle](../reading/10-mass-properties-as-an-oracle.md)
- [documents/01 — New part from a template](../documents/01-new-part-from-template.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md) — why only running it counts
