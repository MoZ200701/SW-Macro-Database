"""How long a new sketch point takes to become selectable, through the tool's own Session."""
import sys, time
sys.path.insert(0, sys.argv[1])
from gear_generator import swcom
from gear_generator.swcom import call
s = swcom.connect()
app = s._app
titles_before = [d.title for d in s.documents()]
print("open before:", titles_before)
results = []
for trial in range(8):
    suppressed = trial % 2 == 1
    s.begin_build()
    info = s.new_part("")
    try:
        s.set_rebuild_suppressed(suppressed)
        s.open_sketch("sk", 1)
        s.sketch_circle("od", (0.0, 0.0), 20.0)
        point = s._entity("od.centre")
        doc = s._doc()
        mgr = call(doc, "SelectionManager")
        start = time.time()
        attempts, first, ok_at = 0, None, None
        while time.time() - start < 6.0:
            call(doc, "ClearSelection2", True)
            returned = call(point, "Select4", False, swcom._null_dispatch())
            count = int(call(mgr, "GetSelectedObjectCount2", -1))
            attempts += 1
            if first is None:
                first = (returned, count)
            if returned and count == 1:
                ok_at = round(time.time() - start, 3)
                break
            time.sleep(0.1)
        active = call(app, "ActiveDoc")
        by_id = None
        if ok_at is None:
            call(doc, "ClearSelection2", True)
            by_id = (call(call(doc, "Extension"), "SelectByID2", "", "SKETCHPOINT", 0.0, 0.0, 0.0, False, 0,
                          swcom._null_dispatch(), 0), int(call(mgr, "GetSelectedObjectCount2", -1)))
        call(doc, "ClearSelection2", True)
        origin = s._origin_point()
        origin_first = call(origin, "Select4", False, swcom._null_dispatch())
        origin_count = int(call(mgr, "GetSelectedObjectCount2", -1))
        call(doc, "ClearSelection2", True)
        row = {"trial": trial, "suppressed": suppressed, "first": first, "ok_after_s": ok_at, "attempts": attempts,
               "active_is_build": active is not None and call(active, "GetTitle") == info.title,
               "by_id_when_failed": by_id, "origin_select4": (origin_first, origin_count)}
        print(row, flush=True)
        results.append(row)
        s.close_sketch("sk", "Probe Timing Sketch")
    finally:
        s.set_rebuild_suppressed(False)
        s.end_build()
        call(app, "CloseDoc", info.title)
print("open after:", [d.title for d in s.documents()])
