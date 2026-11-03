"""Read a sketch's segments back, in model space, from the sketch feature alone.

Copied verbatim from the Plane-Builder probe ``swbridge/probes/p1_sketch_read.py``
(branch ``probes``), the functions ``xform``, ``rot``, ``get_end_params`` and
``read_sketch``, as run on SolidWorks 2026 (34.0.0) on 2026-11-03, result record
``p1_sketch_read-20261103-041058``. Explained in
entries/reading/16-read-a-sketch-as-one-ordered-loop.md.

Not standalone: it expects the probe harness's ``call`` (the late-bound helper of
entries/connect/02), ``harness.as_dispatch`` (wrap a COM object from an array for
late binding), ``harness.describe_error``, ``harness.VARIANT`` / ``harness.pythoncom``
(pywin32's), ``harness.out_bool()`` (``VARIANT(VT_BYREF | VT_BOOL, False)``),
``harness.Doc.feature(name)``, and ``MM = 1000.0``. The ordering of the segments
into one loop (the probe's ``chain``) is application logic and is not included.
"""

from __future__ import annotations

import math
from typing import Any, Dict, List, Optional, Sequence, Tuple

from . import harness
from .harness import call

MM = 1000.0


def xform(data: Sequence[float], p: Sequence[float]) -> Tuple[float, float, float]:
    """By columns (GOTCHAS §10)."""
    r = [float(v) for v in data[:9]]
    tx, ty, tz = (float(v) for v in data[9:12])
    s = float(data[12]) if len(data) > 12 else 1.0
    x, y, z = p
    return (s * (r[0] * x + r[3] * y + r[6] * z) + tx, s * (r[1] * x + r[4] * y + r[7] * z) + ty,
            s * (r[2] * x + r[5] * y + r[8] * z) + tz)


def rot(data: Sequence[float], d: Sequence[float]) -> List[float]:
    """A direction through the transform's rotation (by columns), normalised."""
    r = [float(v) for v in data[:9]]
    x, y, z = d
    v = [r[0] * x + r[3] * y + r[6] * z, r[1] * x + r[4] * y + r[7] * z, r[2] * x + r[5] * y + r[8] * z]
    n = math.sqrt(sum(c * c for c in v))
    return [c / n for c in v] if n else v




def get_end_params(curve: Any) -> Optional[Tuple[float, float, bool, bool]]:
    outs = [harness.VARIANT(harness.pythoncom.VT_BYREF | harness.pythoncom.VT_R8, 0.0),
            harness.VARIANT(harness.pythoncom.VT_BYREF | harness.pythoncom.VT_R8, 0.0),
            harness.out_bool(), harness.out_bool()]
    res = call(curve, "GetEndParams", *outs)
    if isinstance(res, tuple) and len(res) == 5:
        return float(res[1]), float(res[2]), bool(res[3]), bool(res[4])
    return float(outs[0].value), float(outs[1].value), bool(outs[2].value), bool(outs[3].value)


def read_sketch(report: harness.Report, doc: harness.Doc, sketch_name: str, n: int = 60) -> Dict[str, Any]:
    """Everything P1 asks for, from the closed sketch feature alone."""
    feat = doc.feature(sketch_name)
    sk = call(feat, "GetSpecificFeature2")
    m2s_t = call(sk, "ModelToSketchTransform")
    m2s = [float(v) for v in call(m2s_t, "ArrayData")]
    s2m = [float(v) for v in call(call(m2s_t, "Inverse"), "ArrayData")]
    out: Dict[str, Any] = {"model_to_sketch": [round(v, 9) for v in m2s], "sketch_to_model": [round(v, 9) for v in s2m]}
    o = xform(s2m, (0, 0, 0))
    out["sketch_x_in_model"] = [round(a - b, 9) for a, b in zip(xform(s2m, (1, 0, 0)), o)]
    out["sketch_y_in_model"] = [round(a - b, 9) for a, b in zip(xform(s2m, (0, 1, 0)), o)]
    out["sketch_normal_in_model"] = [round(a - b, 9) for a, b in zip(xform(s2m, (0, 0, 1)), o)]
    out["origin_in_model_mm"] = [round(v * MM, 9) for v in o]
    # by rows instead of columns: the same on a Front-parallel plane, whose rotation is the identity (GOTCHAS §10)
    out["rows_vs_columns_max_mm"] = max(math.dist([c * MM for c in xform(s2m, p)], [c * MM for c in xform_rows(s2m, p)])
                                        for p in ((0.03, 0.02, 0.0), (-0.04, 0.01, 0.0)))
    segs_raw = [harness.as_dispatch(s) for s in (call(sk, "GetSketchSegments") or ())]
    out["segments_total"] = len(segs_raw)
    kept, dropped = [], []

    def to_model(p: Sequence[float]) -> Tuple[float, float, float]:
        q = xform(s2m, p)
        return (q[0] * MM, q[1] * MM, q[2] * MM)

    for s in segs_raw:
        name = str(call(s, "GetName"))
        typ = int(call(s, "GetType"))
        constr = bool(call(s, "ConstructionGeometry"))
        if constr:
            dropped.append({"name": name, "type": typ})
            continue
        tname = {0: "line", 1: "arc", 2: "ellipse", 3: "spline"}.get(typ, f"type{typ}")
        rec: Dict[str, Any] = {"name": name, "type": tname}
        # the ends, sketch space → model
        if tname in ("line", "arc"):
            a = harness.as_dispatch(call(s, "GetStartPoint2"))
            b = harness.as_dispatch(call(s, "GetEndPoint2"))
            sa = (float(call(a, "X")), float(call(a, "Y")), float(call(a, "Z")))
            sb = (float(call(b, "X")), float(call(b, "Y")), float(call(b, "Z")))
            if tname == "arc":
                c = harness.as_dispatch(call(s, "GetCenterPoint2"))
                rec["centre_mm"] = [round(v, 9) for v in to_model((float(call(c, "X")), float(call(c, "Y")), float(call(c, "Z"))))]
                rec["radius_mm"] = float(call(s, "GetRadius")) * MM
                try:
                    rec["rotation_dir"] = int(call(s, "GetRotationDir"))
                except Exception as exc:  # noqa: BLE001
                    rec["rotation_dir"] = harness.describe_error(exc)
        else:
            fps = [harness.as_dispatch(p) for p in (call(s, "GetPoints2") or ())]
            fit = [(float(call(p, "X")), float(call(p, "Y")), float(call(p, "Z"))) for p in fps]
            rec["fit_pts"] = [to_model(p) for p in fit]
            sa, sb = fit[0], fit[-1]
        rec["start_mm"], rec["end_mm"] = to_model(sa), to_model(sb)
        # the curve, sampled two ways
        curve = call(s, "GetCurve")
        ep = None
        try:
            ep = get_end_params(curve)
        except Exception as exc:  # noqa: BLE001
            rec["GetEndParams"] = harness.describe_error(exc)
        rec["end_params"] = ep
        ev: List[Tuple[float, float, float]] = []
        if ep is not None:
            t0, t1 = ep[0], ep[1]
            if tname == "line" and (abs(t0) > 1e3 or abs(t1) > 1e3):
                t0 = t1 = None  # an unbounded line: fall back to the ends
            if tname == "arc" or t0 is None:
                # a sketch arc's curve is its whole circle: the arc's own ends by ReverseEvaluate
                try:
                    ra = call(curve, "ReverseEvaluate", *sa)
                    rb = call(curve, "ReverseEvaluate", *sb)
                    rec["reverse_evaluate"] = (ra, rb)
                    t0, t1 = float(ra), float(rb)
                    if tname == "arc" and isinstance(rec.get("rotation_dir"), int):
                        pass
                except Exception as exc:  # noqa: BLE001
                    rec["ReverseEvaluate"] = harness.describe_error(exc)
            if t0 is not None:
                cand = []
                spans = [(t0, t1)]
                if tname == "arc":
                    per = 2 * math.pi
                    spans = [(t0, t0 + ((t1 - t0) % per)), (t0, t0 - ((t0 - t1) % per))]
                for a_, b_ in spans:
                    pts = []
                    tans = []
                    for i in range(n + 1):
                        r = call(curve, "Evaluate2", a_ + (b_ - a_) * i / n, 1 if i in (0, n) else 0)
                        pts.append(to_model((float(r[0]), float(r[1]), float(r[2]))))
                        if i in (0, n):
                            sgn = 1.0 if b_ >= a_ else -1.0
                            d = rot(s2m, (sgn * float(r[3]), sgn * float(r[4]), sgn * float(r[5])))
                            tans.append(d)
                    cand.append(pts)
                    rec.setdefault("_tans", []).append(tans)
                if tname == "arc":
                    # the span that passes the arc's mid (the arc lies on the side the sketch drew it)
                    via_err = []
                    for pts in cand:
                        mid = pts[n // 2]
                        via_err.append(abs(math.hypot(mid[0] - rec["centre_mm"][0], mid[1] - rec["centre_mm"][1]) - rec["radius_mm"]))
                    rec["arc_span_choice"] = "both spans lie on the circle; chosen by comparing with GetTessPts below"
                ev = cand[0]
                rec["_cands"] = cand
        # GetTessPts with the ends in sketch space
        try:
            tess = call(curve, "GetTessPts", 1e-7, 1e-8,
                        harness.VARIANT(harness.pythoncom.VT_ARRAY | harness.pythoncom.VT_R8, list(sa)),
                        harness.VARIANT(harness.pythoncom.VT_ARRAY | harness.pythoncom.VT_R8, list(sb)))
            tv = [float(v) for v in (tess or ())]
            tp = [to_model(tuple(tv[i:i + 3])) for i in range(0, len(tv) - 2, 3)]
            rec["tess_n"] = len(tp)
            rec["tess_pts"] = tp
        except Exception as exc:  # noqa: BLE001
            rec["GetTessPts"] = harness.describe_error(exc)
            rec["tess_pts"] = []
        if tname == "arc" and rec.get("_cands") and rec["tess_pts"]:
            # pick the evaluated span nearest the tessellation's middle point
            tm = rec["tess_pts"][len(rec["tess_pts"]) // 2]
            best = min(rec["_cands"], key=lambda pts: min(math.dist(tm, p) for p in pts))
            ev = best
            rec["arc_span"] = rec["_cands"].index(best)
        if rec.get("_tans"):
            k = rec.get("arc_span", 0)
            rec["tan_start"], rec["tan_end"] = rec["_tans"][k]
        rec.pop("_cands", None)
        rec.pop("_tans", None)
        rec["eval_pts"] = ev
        if ev:
            rec["eval_ends_vs_segment_ends_mm"] = [math.dist(ev[0], rec["start_mm"]), math.dist(ev[-1], rec["end_mm"])]
        if rec["tess_pts"]:
            rec["tess_ends_vs_segment_ends_mm"] = [math.dist(rec["tess_pts"][0], rec["start_mm"]),
                                                   math.dist(rec["tess_pts"][-1], rec["end_mm"])]
        kept.append(rec)
    out["dropped_construction"] = dropped
    out["kept"] = kept
    return out

