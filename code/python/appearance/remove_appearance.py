# Remove the single part-level color.p2m appearance from v0 Master Geometry via explicit DISPATCH_METHOD. Does not save.
import re, pythoncom, win32com.client.dynamic as dyn
from win32com.client import VARIANT
rot = pythoncom.GetRunningObjectTable(); ctx = pythoncom.CreateBindCtx(0)
for mk in rot.EnumRunning():
    try: name = mk.GetDisplayName(ctx, None)
    except pythoncom.com_error: continue
    if re.match(r"^SolidWorks_PID_\d+$", name, re.I):
        app = dyn.Dispatch(rot.GetObject(mk).QueryInterface(pythoncom.IID_IDispatch)); break
doc = next(d for d in app.GetDocuments if d.GetTitle.startswith("v0 Master Geometry"))
ext = doc.Extension
rms = ext.GetRenderMaterials2(1, None) or ()
if len(rms) != 1 or not rms[0].FileName.lower().endswith("\\color.p2m"):
    print("ABORT: not the expected single color.p2m appearance"); raise SystemExit(2)
id1 = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0); id2 = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
rms[0].GetMaterialIds(id1, id2)
ids = [id1.value, id2.value]
dispid = ext._oleobj_.GetIDsOfNames("DeleteDisplayStateSpecificRenderMaterial")
ok = ext._oleobj_.Invoke(dispid, 0, pythoncom.DISPATCH_METHOD, True, VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_I4, [ids[0]]), VARIANT(pythoncom.VT_ARRAY | pythoncom.VT_I4, [ids[1]]))
print("Delete(", ids, ") ->", ok)
doc.GraphicsRedraw2()
rms = ext.GetRenderMaterials2(1, None) or ()
print("after: render materials", len(rms), [r.FileName.split("\\")[-1] for r in rms])
print("after MPV", tuple(round(v, 3) for v in doc.MaterialPropertyValues))
