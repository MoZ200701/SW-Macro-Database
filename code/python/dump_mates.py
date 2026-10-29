"""Dump every mate, component and equation of an open assembly to JSON.

Read-only: nothing is rebuilt, saved, suppressed or switched. Run with the
Windows Python beside SolidWorks (from WSL, see connect/10). Attaches through
swcom.py beside this file (Running Object Table, late-bound ``call``).
"""

from __future__ import annotations

import argparse
import json
import os
import sys
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import pythoncom  # noqa: E402
from win32com.client import VARIANT  # noqa: E402

import swcom  # noqa: E402
from swcom import call  # noqa: E402

# Members tried on whatever IFeature.GetDefinition hands back for a mate.
# Gear: numerator/denominator/reverse. The rest cover rack-pinion, screw,
# linear coupler, limits and widths; anything that does not answer is skipped.
DEFINITION_FIELDS = [
    "GearRatioNumerator", "GearRatioDenominator", "Reverse",
    "PinionPitchDiameter", "DistancePerRevolution", "PinionPitchDiameterMate",
    "Distance", "RevolutionsPerDistance", "DistancePerRevolution",
    "Ratio", "RatioNumerator", "RatioDenominator", "Offset",
    "MateAlignment", "Angle", "MaximumVariation", "MinimumVariation",
    "LimitMate", "Constraint", "Type",
]
IMATE_FIELDS = ["Type", "Alignment", "Flipped", "MaximumVariation", "MinimumVariation"]


def _try(obj, name, *args):
    try:
        return call(obj, name, *args)
    except Exception:
        return None


def _plain(v):
    """COM values to JSON-safe ones; anything COM-shaped becomes its repr."""
    if v is None or isinstance(v, (bool, int, float, str)):
        return v
    if isinstance(v, (tuple, list)):
        return [_plain(x) for x in v]
    return repr(v)


def _sub_features(feature):
    child = _try(feature, "GetFirstSubFeature")
    while child is not None:
        yield child
        child = _try(child, "GetNextSubFeature")


def _top_features(doc):
    f = _try(doc, "FirstFeature")
    while f is not None:
        yield f
        f = _try(f, "GetNextFeature")


def _dimensions(feature):
    out = []
    dd = _try(feature, "GetFirstDisplayDimension")
    while dd is not None:
        dim = _try(dd, "GetDimension2", 0)
        if dim is not None:
            out.append({"name": _plain(_try(dim, "FullName")),
                        "system_value": _plain(_try(dim, "SystemValue"))})
        dd = _try(feature, "GetNextDisplayDimension", dd)
    return out


def _entity(me):
    comp = _try(me, "ReferenceComponent")
    return {
        "component": _plain(_try(comp, "Name2")) if comp is not None else None,
        "reference_type": _plain(_try(me, "ReferenceType2")),
        # assembly coords, metres: point(0-2), direction(3-5), radius(6)
        "entity_params": _plain(_try(me, "EntityParams")),
    }


def _mate(feature, path):
    rec = {
        "name": _plain(_try(feature, "Name")),
        "type_name": _plain(_try(feature, "GetTypeName2")),
        "folder_path": path,
        "suppressed": _plain(_try(feature, "IsSuppressed")),
    }
    try:
        flag = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_BOOL, False)
        rec["error_code"] = _plain(feature.GetErrorCode2(flag))
    except Exception:
        rec["error_code"] = None
    mate = _try(feature, "GetSpecificFeature2")
    if mate is not None:
        rec["imate"] = {k: _plain(_try(mate, k)) for k in IMATE_FIELDS}
        n = _try(mate, "GetMateEntityCount")
        ents = []
        for i in range(int(n) if isinstance(n, int) else 0):
            me = _try(mate, "MateEntity", i)
            if me is not None:
                ents.append(_entity(me))
        rec["entities"] = ents
    definition = _try(feature, "GetDefinition")
    if definition is not None:
        data = {}
        for k in DEFINITION_FIELDS:
            v = _try(definition, k)
            if v is not None and not callable(v):
                data[k] = _plain(v)
        rec["definition"] = data
    rec["dimensions"] = _dimensions(feature)
    return rec


def _mates(doc):
    out = []
    for f in _top_features(doc):
        if _try(f, "GetTypeName2") != "MateGroup":
            continue

        def walk(parent, path):
            for child in _sub_features(parent):
                t = _try(child, "GetTypeName2")
                if t == "FtrFolder":
                    out.append({"name": _plain(_try(child, "Name")), "type_name": t, "folder_path": path})
                    walk(child, path + [_try(child, "Name")])
                else:
                    out.append(_mate(child, path))

        walk(f, [])
    return out


def _equations(doc):
    mgr = _try(doc, "GetEquationMgr")
    if mgr is None:
        return []
    n = _try(mgr, "GetCount")
    eqs = []
    for i in range(int(n) if isinstance(n, int) else 0):
        eqs.append({"equation": _plain(_try(mgr, "Equation", i)),
                    "value": _plain(_try(mgr, "Value", i)),
                    "global": _plain(_try(mgr, "GlobalVariable", i))})
    return eqs


def _doc_summary(doc):
    return {
        "title": _plain(_try(doc, "GetTitle")),
        "path": _plain(_try(doc, "GetPathName")),
        "type": _plain(_try(doc, "GetType")),
        "configurations": _plain(_try(doc, "GetConfigurationNames")),
        "active_configuration": _plain(_try(_try(doc, "GetActiveConfiguration"), "Name")),
        "equations": _equations(doc),
    }


def _components(asm):
    comps = _try(asm, "GetComponents", False) or []
    out = []
    for c in comps:
        parent = _try(c, "GetParent")
        xf = _try(c, "Transform2")
        out.append({
            "name": _plain(_try(c, "Name2")),
            "path": _plain(_try(c, "GetPathName")),
            "parent": _plain(_try(parent, "Name2")) if parent is not None else None,
            "suppressed": _plain(_try(c, "IsSuppressed")),
            "fixed": _plain(_try(c, "IsFixed")),
            "suppression_state": _plain(_try(c, "GetSuppression2")),
            "referenced_configuration": _plain(_try(c, "ReferencedConfiguration")),
            # 16 doubles: rotation 0-8 (by columns, GOTCHAS 10), translation 9-11 m, scale 12
            "transform": _plain(_try(xf, "ArrayData")) if xf is not None else None,
        })
    return comps, out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--title", required=True, help="substring of the top assembly's title")
    args = ap.parse_args()

    # swcom.connect() gates on SW 2026 (an Airfoil Converter minimum); the
    # attach itself is version-independent, and this machine runs 2024 SP5.
    candidates = swcom.find_candidates()
    if not candidates:
        print("No running SolidWorks found in the Running Object Table")
        return 3
    app = sorted(candidates, key=lambda c: (c.has_active_doc, c.revision), reverse=True)[0].app
    docs = _try(app, "GetDocuments") or []
    top = None
    for d in docs:
        title = _try(d, "GetTitle") or ""
        if args.title.lower() in title.lower() and _try(d, "GetType") == 2:
            top = d
            break
    if top is None:
        top = _try(app, "ActiveDoc")
    if top is None or _try(top, "GetType") != 2:
        print("No open assembly matched; open docs:", [_try(d, "GetTitle") for d in docs])
        return 3

    result = {
        "solidworks_revision": _plain(_try(app, "RevisionNumber")),
        "open_documents": [_try(d, "GetTitle") for d in docs],
        "top": _doc_summary(top),
        "assemblies": [],
        "parts": {},
    }
    comps, result["components"] = _components(top)
    result["assemblies"].append({"owner": None, **_doc_summary(top), "mates": _mates(top)})

    seen = {result["top"]["path"]}
    for c in comps:
        model = _try(c, "GetModelDoc2")
        if model is None:
            continue
        path = _try(model, "GetPathName")
        if path in seen:
            continue
        seen.add(path)
        if _try(model, "GetType") == 2:
            result["assemblies"].append({"owner": _try(c, "Name2"), **_doc_summary(model), "mates": _mates(model)})
        else:
            s = _doc_summary(model)
            if s["equations"] or (s["configurations"] and len(s["configurations"]) > 1):
                result["parts"][path] = s

    os.makedirs(args.out, exist_ok=True)
    target = os.path.join(args.out, "mates.json")
    with open(target, "w", encoding="utf-8") as fh:
        json.dump(result, fh, indent=1)
    n_mates = sum(len(a["mates"]) for a in result["assemblies"])
    print(f"SolidWorks {result['solidworks_revision']}; top: {result['top']['title']}")
    print(f"{len(result['components'])} components, {len(result['assemblies'])} assemblies, {n_mates} mate records")
    for a in result["assemblies"]:
        kinds = {}
        for m in a["mates"]:
            kinds[m.get("type_name")] = kinds.get(m.get("type_name"), 0) + 1
        print(f"  {a['title']}: {kinds}  configs={a['configurations']}")
    print("wrote", target)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception:
        traceback.print_exc()
        raise SystemExit(5)
