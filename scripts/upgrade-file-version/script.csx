// upgrade-file-version — re-save files in the running SolidWorks' file format.
//
// The calls and their order come from code/csharp/SwUpgradeVersion.cs, which
// upgraded a real library on SolidWorks 2026 SP1.1
// (entries/files/03-upgrade-file-version.md). This version is reworked to run
// inside a host that already holds the SolidWorks connection (`ctx.Sw`), and has
// NOT been run as a script yet.
//
// THIS IS ONE-WAY. SolidWorks cannot open a file newer than itself, so once a
// file is written by this version nobody on an older seat can open it again.
//
// ORDER MATTERS. Parts first, then assemblies, then drawings: opening an
// assembly pulls in its components and saving it can rewrite them too, so doing
// parts first means each part is upgraded exactly once, by itself.
//
// Differences from SwUpgradeVersion.cs:
//  - It works on the files it is given, not a whole folder tree.
//  - It never closes documents it did not open, and never attaches to or starts
//    SolidWorks itself.
//  - The log-file resume is gone: files already current are skipped by
//    VersionHistory, which makes a rerun safe anyway.

var targets = ctx.Params.GetFiles("targets");

int latest = ctx.Sw.GetLatestSupportedFileVersion();
ctx.Log("SolidWorks " + ctx.Sw.RevisionNumber() + " writes file version " + latest);

// "11000[2018/134] | 14000[2021/85]" -> the last entry's number.
bool IsCurrent(string path)
{
    try
    {
        string[] vh = (string[])ctx.Sw.VersionHistory(path);
        if (vh == null || vh.Length == 0) return false;
        string last = vh[vh.Length - 1];
        int br = last.IndexOf('[');
        if (br > 0) last = last.Substring(0, br);
        return int.TryParse(last.Trim(), out int v) && v >= latest;
    }
    catch { return false; }
}

int TypeOf(string path)
{
    switch (Path.GetExtension(path).ToLowerInvariant())
    {
        case ".sldprt": return (int)swDocumentTypes_e.swDocPART;
        case ".sldasm": return (int)swDocumentTypes_e.swDocASSEMBLY;
        case ".slddrw": return (int)swDocumentTypes_e.swDocDRAWING;
        default: return -1;
    }
}

int Order(string path)
{
    int t = TypeOf(path);
    return t == (int)swDocumentTypes_e.swDocPART ? 0 : t == (int)swDocumentTypes_e.swDocASSEMBLY ? 1 : 2;
}

var ordered = targets
    .Where(f => !Path.GetFileName(f).StartsWith("~$"))
    .OrderBy(Order)
    .ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
    .ToList();

foreach (string f in ordered)
{
    if (ctx.Cancel.IsCancellationRequested) { ctx.Log("cancelled"); break; }

    int docType = TypeOf(f);
    if (docType < 0) { ctx.Skipped(f, "not a SolidWorks part, assembly or drawing"); continue; }

    // VersionHistory reads the saved-version list off the closed file. If the
    // last writer is already current, re-saving would only churn the bytes.
    if (IsCurrent(f)) { ctx.Skipped(f, "already in the current file version"); continue; }

    if (ctx.DryRun) { ctx.Log("would upgrade " + f); continue; }

    ModelDoc2 doc = null;
    try
    {
        File.SetAttributes(f, FileAttributes.Normal);
        int e = 0, w = 0;
        doc = (ModelDoc2)ctx.Sw.OpenDoc6(f, docType,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
        if (doc == null) throw new Exception("open failed, err=" + e + " warn=" + w);

        // SaveAs over the same path rewrites in the running version even when
        // the model is unchanged; a plain Save can be a no-op.
        int se = 0, sw2 = 0;
        bool ok = doc.Extension.SaveAs(f, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref se, ref sw2);
        if (!ok) throw new Exception("save failed, err=" + se + " warn=" + sw2);

        ctx.Modified(f);
    }
    catch (Exception ex)
    {
        ctx.Failed(f, ex.Message);
    }
    finally
    {
        // Closes only the document this script opened.
        try { if (doc != null) ctx.Sw.CloseDoc(doc.GetTitle()); } catch { }
    }
}
