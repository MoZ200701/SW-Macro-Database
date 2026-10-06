// convert-step — STEP files to SolidWorks parts and assemblies.
//
// The calls and their order come from code/csharp/SwConvert.cs, which converted
// a real library on SolidWorks 2026 SP1.1 (entries/files/01-batch-convert-step.md).
// This version is reworked to run inside a host that already holds the
// SolidWorks connection (`ctx.Sw`), and has NOT been run as a script yet.
//
// Differences from SwConvert.cs, all for running inside someone's session:
//  - It does not close documents it did not open. SwConvert.cs closed every open
//    document first, discarding changes; that is not acceptable next to a user's
//    work.
//  - The import preferences it changes are read first and restored in `finally`.
//    SwConvert.cs left them changed. If a preference cannot be read, it is not
//    changed either, so nothing is ever left in a state we cannot undo.
//  - Inputs come from parameters, output files are reported through ctx.Wrote,
//    and a dry run lists what would happen without loading anything.

string srcRoot = Path.GetFullPath(ctx.Params.GetFolder("sourceFolder")).TrimEnd('\\');
string outRoot = Path.GetFullPath(ctx.Params.GetFolder("outputFolder")).TrimEnd('\\');
bool recurse = ctx.Params.GetBool("recurse");

string OutDirFor(string src)
{
    string rel = Path.GetRelativePath(srcRoot, Path.GetDirectoryName(src));
    return rel == "." ? outRoot : Path.Combine(outRoot, rel);
}

string AlreadyConverted(string outDir, string baseName)
{
    foreach (string ext in new[] { ".SLDPRT", ".SLDASM" })
    {
        string p = Path.Combine(outDir, baseName + ext);
        if (File.Exists(p)) return p;
    }
    return null;
}

var files = Directory.GetFiles(srcRoot, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
    .Where(f =>
    {
        string e = Path.GetExtension(f).ToLowerInvariant();
        return e == ".step" || e == ".stp";
    })
    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
    .ToList();

ctx.Log(files.Count + " STEP files under " + srcRoot);

if (ctx.DryRun)
{
    foreach (string f in files)
    {
        string outDir = OutDirFor(f);
        string existing = AlreadyConverted(outDir, Path.GetFileNameWithoutExtension(f));
        if (existing != null) ctx.Skipped(f, "already converted: " + existing);
        else ctx.Log("would convert " + f + "  ->  " + outDir);
    }
}
else
{
    // OpenDoc6 / LoadFile4 fail these files with swFileRequiresRepairError
    // (2097152) while import diagnostics and entity repair are on: in silent
    // mode SolidWorks cannot raise the repair prompt, so it fails the load.
    var toggles = new (swUserPreferenceToggle_e Pref, bool Value)[]
    {
        (swUserPreferenceToggle_e.swImportAutoRunImportDiagnostics, false),
        (swUserPreferenceToggle_e.swImportAutoRunImportDiagnosticsPersist, false),
        (swUserPreferenceToggle_e.swImportNeutralRunDiagnostics, false),
        (swUserPreferenceToggle_e.swForceEnableImportDiagnosis, false),
        (swUserPreferenceToggle_e.swImportSolidSurface, true),
        (swUserPreferenceToggle_e.swImportNeutral_SolidandSurface, true),
        (swUserPreferenceToggle_e.swImportMultBodyAsPartData, false),
    };
    var restoreToggles = new List<(swUserPreferenceToggle_e Pref, bool Value)>();
    bool restoreRepair = false;
    int oldRepair = 0;

    try
    {
        foreach (var t in toggles)
        {
            bool old;
            try { old = ctx.Sw.GetUserPreferenceToggle((int)t.Pref); }
            catch (Exception ex) { ctx.Log("pref " + t.Pref + " unreadable, left alone: " + ex.Message); continue; }
            try
            {
                ctx.Sw.SetUserPreferenceToggle((int)t.Pref, t.Value);
                restoreToggles.Add((t.Pref, old));
            }
            catch (Exception ex) { ctx.Log("pref " + t.Pref + " could not be set: " + ex.Message); }
        }
        try
        {
            oldRepair = ctx.Sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swImportCheckAndRepair);
            ctx.Sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swImportCheckAndRepair, 0);
            restoreRepair = true;
        }
        catch (Exception ex) { ctx.Log("pref swImportCheckAndRepair left alone: " + ex.Message); }

        int n = 0;
        foreach (string src in files)
        {
            if (ctx.Cancel.IsCancellationRequested) { ctx.Log("cancelled after " + n + " of " + files.Count); break; }
            n++;

            string outDir = OutDirFor(src);
            string baseName = Path.GetFileNameWithoutExtension(src);
            string existing = AlreadyConverted(outDir, baseName);
            if (existing != null) { ctx.Skipped(src, "already converted: " + existing); continue; }

            Directory.CreateDirectory(outDir);

            // LoadFile4 is the working route for neutral formats; OpenDoc6 fails
            // every one of these files with 2097152 whatever the options.
            int err = 0;
            ModelDoc2 m = null;
            try { m = (ModelDoc2)ctx.Sw.LoadFile4(src, "r", null, ref err); }
            catch (Exception ex) { ctx.Failed(src, "load threw: " + ex.Message); continue; }
            if (m == null) { ctx.Failed(src, "load failed, err=" + err); continue; }

            string dst = Path.Combine(outDir,
                baseName + (m.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY ? ".SLDASM" : ".SLDPRT"));

            int e2 = 0, w2 = 0;
            bool ok = false;
            try
            {
                ok = m.Extension.SaveAs(dst, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                                        (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref e2, ref w2);
            }
            catch (Exception ex) { ctx.Log("save threw for " + baseName + ": " + ex.Message); }
            finally
            {
                // Closes the document this script just loaded, by its own title.
                try { ctx.Sw.CloseDoc(m.GetTitle()); } catch { }
            }

            if (File.Exists(dst)) { ctx.Wrote(dst); ctx.Log("[" + n + "/" + files.Count + "] ok " + baseName); }
            else ctx.Failed(src, "save failed, ok=" + ok + " err=" + e2 + " warn=" + w2);
        }
    }
    finally
    {
        foreach (var t in restoreToggles)
        {
            try { ctx.Sw.SetUserPreferenceToggle((int)t.Pref, t.Value); }
            catch (Exception ex) { ctx.Log("could not restore " + t.Pref + ": " + ex.Message); }
        }
        if (restoreRepair)
        {
            try { ctx.Sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swImportCheckAndRepair, oldRepair); }
            catch (Exception ex) { ctx.Log("could not restore swImportCheckAndRepair: " + ex.Message); }
        }
    }
}
