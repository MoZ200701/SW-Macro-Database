---
id: connect-05-choosing-among-versions
title: Choosing among several installed versions
status: verified
verified_on: a machine carrying SolidWorks 2024, 2025 and 2026
language: [python]
api: [ISldWorks.RevisionNumber]
keywords: [version, revision number, major 32 33 34, 2024 2025 2026, minimum version, choose session, multiple installs]
answers: "Several SolidWorks versions are installed. Which one should my tool drive?"
---

# Choosing among several installed versions

**Status:** Verified, on a machine carrying SolidWorks 2024, 2025 and 2026
**Language:** Python shown; the logic is language-independent

## Why there is a choice to make

Engineering machines accumulate versions. Once more than one is installed, "the
SolidWorks" is not a well-defined thing, and both of the obvious approaches are
wrong:

- Taking whatever the unversioned ProgID resolves to picks by registration
  order, not by what is running. See [GOTCHAS §1](../../GOTCHAS.md).
- Taking the first session found in the Running Object Table picks by
  enumeration order, which is arbitrary.

So enumerate them all, ask each its version, and choose deliberately.

## Version numbering

Majors advance by one a year:

| Major | Release |
|---|---|
| 32 | 2024 |
| 33 | 2025 |
| 34 | 2026 |

`RevisionNumber` gives a string like `"34.0.0"`, or `"34.1.1"` for a service
pack.

```python
_YEAR_OFFSET = 1992


def parse_revision(text):
    parts = str(text).strip().split(".")
    numbers = tuple(int(p) for p in parts if p != "")
    if not numbers:
        raise SolidWorksError(f"Could not read a version out of {text!r}.")
    return numbers


def release_year(major):
    """Display only, never a decision."""
    return major + _YEAR_OFFSET
```

Keep the year mapping display-only. It is a convention, not a deduction, and
if it ever breaks you want the cost to be one wrong word in a message.

## The choice

```python
def choose(candidates):
    """Pick the session to drive: the newest one that clears the floor."""
    if not candidates:
        raise NotRunning(
            "No running SolidWorks was found. Open the part you want the curves in."
        )

    usable = [c for c in candidates if meets_minimum(c.revision)]
    if not usable:
        found = ", ".join(sorted({version_label(c.revision) for c in candidates}))
        raise WrongVersion(
            f"Found {found}. This needs SolidWorks {release_year(MINIMUM_MAJOR)} "
            f"(revision {MINIMUM_MAJOR}) or newer."
        )

    # Newest first; a session with a document open wins a tie, because that is
    # almost certainly the window the user is looking at. PID last, only so the
    # choice is deterministic when nothing else separates them.
    return sorted(usable,
                  key=lambda c: (c.revision, c.has_active_doc, -c.pid),
                  reverse=True)[0]
```

Three ranking keys, in order: version, then whether it has a document open,
then process id. The middle one is the one that makes this feel right in
practice, because the session with something open is the window the user is
looking at.

## Never refuse a version for being too new

```python
MINIMUM_MAJOR = 34
HIGHEST_TESTED_MAJOR = 34


def is_newer_than_tested(revision):
    return bool(revision) and revision[0] > HIGHEST_TESTED_MAJOR
```

`is_newer_than_tested` exists to soften a message, not to block. A 2027 install
reports 35 and gets used exactly as 2026 does. The API this kind of tool leans
on has been stable across many releases, so the realistic failure on a new
version is changed behaviour, not a missing member, and locking the user out
would cost everything and buy nothing.

## Report the ones you could not reach

```python
try:
    app = _dispatch(table.GetObject(moniker))
    revision = parse_revision(call(app, "RevisionNumber"))
    has_doc = call(app, "ActiveDoc") is not None
except (pythoncom.com_error, SolidWorksError, AttributeError) as exc:
    unreachable.append((name, str(exc)))
    continue
```

A session sitting on a modal dialog will not answer, and must not sink the
whole scan. But skipping it silently produces the hardest class of bug to place
later: "it says SolidWorks isn't running, and it is." Collect the failures and
show them when the scan comes up empty.

## The equivalent for ProgIDs

If you are in VBScript and stuck with ProgIDs, do the same thing by generating
the candidate list from the registry rather than hard-coding it: enumerate
`HKEY_CLASSES_ROOT\SldWorks.Application.*`, sort descending by the numeric
suffix, and append the unversioned ProgID last.

## See also

- [connect/02 — Attach from Python](02-attach-from-python.md)
- [GOTCHAS §1](../../GOTCHAS.md)
