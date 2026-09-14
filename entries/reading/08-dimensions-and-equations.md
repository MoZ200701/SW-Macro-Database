---
id: reading-08-dimensions-and-equations
title: Read every dimension, equation and configuration out of a part
status: partly-verified
verified_on: SolidWorks 2026 SP1.1 for the open, configuration and size passes; the dimension and equation walks ran in the same tool but their output was not retained
language: [csharp]
api: [IFeature.GetFirstDisplayDimension, IFeature.GetNextDisplayDimension, IDisplayDimension.GetDimension, IDimension.FullName, IDimension.GetSystemValue2, IModelDoc2.GetEquationMgr, IEquationMgr.GetCount, IEquationMgr.Equation, IModelDoc2.GetDesignTable, IModelDoc2.GetConfigurationByName, IConfiguration.GetParent]
keywords: [display dimension, GetFirstDisplayDimension, GetSystemValue2, FullName, equation manager, global variable, design table, derived configuration, GetParent, inspect part, dump]
answers: "How do I read the dimensions and equations of an existing part from code?"
---

# Read every dimension, equation and configuration out of a part

## What this is for

Before automating anything against an unfamiliar part, you need to know what is
actually in it: which configurations, whether a design table drives them,
whether equations link dimensions together, and what each feature's dimensions
are called and currently hold. Guessing any of that produces a macro that
works on one part and quietly does the wrong thing on the next.

This is the reconnaissance pass. It opens each part read-only and writes
everything out to a log.

## Dimensions hang off features, through display dimensions

There is no "all dimensions" collection. You walk the feature tree, and each
feature hands you a chain of display dimensions, each of which wraps the real
dimension.

```csharp
Feature feat = (Feature)doc.FirstFeature();
int i = 0;
while (feat != null && i < 400)
{
    string tn = ""; try { tn = feat.GetTypeName2(); } catch { }
    L("   FEAT " + feat.Name + "  [" + tn + "]");
    try
    {
        DisplayDimension dd = (DisplayDimension)feat.GetFirstDisplayDimension();
        while (dd != null)
        {
            Dimension d = (Dimension)dd.GetDimension();
            double v = 0; try { v = d.GetSystemValue2(""); } catch { }
            L("        DIM " + d.FullName + " = " + Math.Round(v * 1000.0, 4) + " mm");
            dd = (DisplayDimension)feat.GetNextDisplayDimension(dd);
        }
    }
    catch { }
    feat = (Feature)feat.GetNextFeature();
    i++;
}
```

`GetFirstDisplayDimension` and `GetNextDisplayDimension` are on `IFeature`;
`GetDimension` is on `IDisplayDimension`; `FullName` and `GetSystemValue2` are
on `IDimension`.

**`GetSystemValue2` is in metres**, like the rest of the API, whatever the
document's own units say. The multiply by 1000 above is the conversion to
millimetres for the log, not something the call does. Its string argument is
the configuration to read; `""` means the active one.

`FullName` is the qualified name, of the form the equation editor uses, which is
what lets a dimension found this way be matched against an equation found below.

## Equations and global variables

```csharp
EquationMgr em = doc.GetEquationMgr();
if (em != null)
{
    L("equations: " + em.GetCount());
    for (int i = 0; i < em.GetCount(); i++)
        L("   EQ[" + i + "] " + em.Equation[i]);
}
```

`GetEquationMgr` is on `IModelDoc2`. `Equation[i]` is the raw text, global
variables included, in the order the editor shows them. A part whose dimensions
are driven this way will not respond to setting a dimension directly, which is
the reason to look before automating.

## Configurations, and which are derived

```csharp
string[] names = (string[])doc.GetConfigurationNames();
foreach (string n in names)
{
    Configuration c = (Configuration)doc.GetConfigurationByName(n);
    Configuration p = (Configuration)c.GetParent();
    extra = "  desc='" + c.Description + "'" + (p != null ? "  parent=" + p.Name : "");
}
```

`GetParent` returning non-null means the configuration is derived from another,
which matters as soon as you delete or modify one: a derived configuration goes
when its parent goes. See [files/02](../files/02-explode-configurations.md).

`IModelDoc2.GetDesignTable()` returning non-null says the configurations are
table-driven, so editing them through the API is the wrong route.

## Why it is not obvious

**A feature walk needs a bound.** The loop above stops at 400 features. A
malformed walk is an infinite loop rather than an error, and a bounded walk that
truncates is easier to notice than a hung process.

**Every one of these calls is wrapped.** Not every feature has dimensions, not
every part has equations or a table, and a `Configuration` can refuse
`Description`. On a reconnaissance pass over an unknown library, one part that
throws should cost you that one line of output, not the run.

**Open the part read-only**, with
`swOpenDocOptions_Silent | swOpenDocOptions_ReadOnly`. A pass that only reads
should be unable to write, and it keeps the close from ever offering to save
([GOTCHAS §22](../../GOTCHAS.md)).

## What it does not do

- It reads. Nothing here sets a dimension. Writing one back is
  `IDimension.SetSystemValue3`, which this entry does not cover and which was
  not tried.
- The walk is the top-level feature chain only, so dimensions on absorbed
  sub-features are not reached. That is the same constraint as
  [GOTCHAS §19](../../GOTCHAS.md), for the same reason.
- Open question: whether `FullName` is stable enough to key on across a rebuild
  that renames a feature. [curves/10](../curves/10-persistent-references.md) is
  the reliable answer for tracking a feature; whether a dimension has an
  equivalent was not tested.

## Evidence

SolidWorks 2026 SP1.1. `SwInspect.exe` ran over a library of configurable metal
parts, opening each read-only, and its output was what the explode macro in
[files/02](../files/02-explode-configurations.md) was designed against.

**Verified**, in that the results were used and are on the record: the
read-only open of each part, the configuration enumeration, design-table
presence, and the per-configuration size sample. The configuration counts it
reported drove the work that followed — 20 for U-Channel, 35 for Linear Slide,
124 for each Plate, 300 for the 0.25 in pitch Plate — and the parts named as
table-driven were the ones that later needed `DeleteDesignTable`.

**Not separately attested**: the dimension walk and the equation dump. They ran
in the same pass over the same parts, and nothing in the logs suggested they
failed, but their output was not kept and no downstream artifact depends on it.
The calls are documented and the code is the code that ran; treat the values
they produce as unconfirmed until someone reads a log.

A defect the reconnaissance surrounded is worth recording either way.
Configuration `33` of two Angle parts is 16.990 in where 33 holes at 0.5 in is
16.500 in, with `32` at 16.000 in and `34` at 17.000 in. That was caught by
measuring geometry, not by reading dimensions — see
[reading/09](09-bounding-box.md) — and it sits in the source design table.

## Full source

[`code/csharp/SwInspect.cs`](../../code/csharp/SwInspect.cs). Run as
`SwInspect <folderOrFile> <logFile>`.

## See also

- [connect/08 — Find a feature by name](../connect/08-find-a-feature-by-name.md)
- [reading/07 — Report the feature type on failure](07-report-feature-type-on-failure.md)
- [reading/09 — Bounding box as a check](09-bounding-box.md)
- [sketches/04 — Dimensions](../sketches/04-dimensions.md) — the writing side,
  still unverified
- [files/02 — Explode configurations](../files/02-explode-configurations.md)
- [equations/01 — Global variables from code](../equations/01-global-variables-from-code.md) —
  adding and changing the equations this reads
- [equations/02 — Link a dimension to a global](../equations/02-link-a-dimension-to-a-global.md) —
  the `Dim@Owner` names an equation uses
- [features/04 — Read a feature's dimensions](../features/04-read-a-features-dimensions.md) —
  the same display-dimension walk, used to find one dimension by value
