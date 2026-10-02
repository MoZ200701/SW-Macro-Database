# Read-only: compare appearance-related state across open SolidWorks documents.
import re, pythoncom, win32com.client.dynamic as dyn
rot = pythoncom.GetRunningObjectTable(); ctx = pythoncom.CreateBindCtx(0)
app = None
for mk in rot.EnumRunning():
    try: name = mk.GetDisplayName(ctx, None)
    except pythoncom.com_error: continue
    if re.match(r"^SolidWorks_PID_\d+$", name, re.I):
        app = dyn.Dispatch(rot.GetObject(mk).QueryInterface(pythoncom.IID_IDispatch)); break
active = app.ActiveDoc.GetTitle
print("active:", active)
def tryp(label, fn):
    try: print(f"  {label}: {fn()}")
    except Exception as e: print(f"  {label}: ERR {str(e)[:120]}")
docs = app.GetDocuments or ()
for doc in docs:
    print("DOC", doc.GetTitle, "type", doc.GetType, "path", doc.GetPathName)
    if doc.GetType != 1: continue
    cfg = doc.ConfigurationManager.ActiveConfiguration.Name
    tryp("config", lambda: cfg)
    tryp("MaterialPropertyValues (RGB,amb,diff,spec,shin,transp,emis)", lambda: tuple(round(v,4) for v in doc.MaterialPropertyValues))
    tryp("SW material", lambda: doc.GetMaterialPropertyName2(cfg, ""))
    ext = doc.Extension
    tryp("render materials count", lambda: ext.GetRenderMaterialsCount2(1, None))
    def rms():
        out = []
        for rm in (ext.GetRenderMaterials2(1, None) or ()):
            out.append((rm.FileName, "spec", rm.Specular, "refl", rm.Reflectivity, "transp", rm.Transparency, "ents", rm.GetEntitiesCount))
        return out
    tryp("render materials", rms)
for k, v in [("RealView (toggle 223?)", None)]:
    pass
