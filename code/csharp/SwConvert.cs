// Batch STEP -> SolidWorks converter.
//
// Runs as its own process but uses EARLY binding through the SolidWorks
// interop assemblies, so the CLR marshals OpenDoc6's ByRef Long
// out-parameters correctly. Late-bound hosts (VBScript, PowerShell) cannot
// do this and fail with "Type mismatch" before the call executes.
//
// Main is [STAThread]: SolidWorks is an STA COM server, and calling it from
// an MTA thread deadlocks.

using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class SwConvert
{
    static StreamWriter log;
    static ISldWorks sw;
    static int done, skipped, failed;

    static void L(string m)
    {
        Console.WriteLine(m);
        if (log != null) { log.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + m); log.Flush(); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length < 3) { Console.WriteLine("usage: SwConvert <srcRoot> <outRoot> <logFile>"); return 2; }
        string srcRoot = args[0].TrimEnd('\\');
        string outRoot = args[1].TrimEnd('\\');

        Directory.CreateDirectory(outRoot);
        log = new StreamWriter(args[2], false);

        try { sw = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application.34"); }
        catch (Exception ex) { L("ATTACH FAILED: " + ex.Message); log.Close(); return 1; }

        L("attached to SolidWorks " + sw.RevisionNumber());

        // OpenDoc6 returns swFileRequiresRepairError (2097152) on these STEP
        // files while import diagnostics / full entity repair are enabled:
        // in silent mode SolidWorks cannot raise the repair prompt, so it
        // fails the load instead. Turn that machinery off.
        SetToggle(swUserPreferenceToggle_e.swImportAutoRunImportDiagnostics, false);
        SetToggle(swUserPreferenceToggle_e.swImportAutoRunImportDiagnosticsPersist, false);
        SetToggle(swUserPreferenceToggle_e.swImportNeutralRunDiagnostics, false);
        SetToggle(swUserPreferenceToggle_e.swForceEnableImportDiagnosis, false);
        SetToggle(swUserPreferenceToggle_e.swImportSolidSurface, true);
        SetToggle(swUserPreferenceToggle_e.swImportNeutral_SolidandSurface, true);
        SetToggle(swUserPreferenceToggle_e.swImportMultBodyAsPartData, false);
        SetInt(swUserPreferenceIntegerValue_e.swImportCheckAndRepair, 0);

        // Close anything left open, discarding changes, so nothing can be
        // mistaken for a fresh import and no save prompt can appear.
        for (int i = 0; i < 50; i++)
        {
            ModelDoc2 open = (ModelDoc2)sw.ActiveDoc;
            if (open == null) break;
            string t = open.GetTitle();
            L("closing leftover document: " + t);
            sw.CloseDoc(t);
        }

        var files = Directory.GetFiles(srcRoot, "*.*", SearchOption.AllDirectories);
        Array.Sort(files);
        int total = 0;
        foreach (var f in files)
        {
            string e = Path.GetExtension(f).ToLowerInvariant();
            if (e == ".step" || e == ".stp") total++;
        }
        L(total + " STEP files to process");

        var start = DateTime.Now;
        int n = 0;
        foreach (var f in files)
        {
            string ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext != ".step" && ext != ".stp") continue;
            n++;
            ConvertOne(f, srcRoot, outRoot, n, total);
        }

        L(string.Format("FINISHED converted={0} skipped={1} failed={2} in {3:N1} min",
            done, skipped, failed, (DateTime.Now - start).TotalMinutes));
        log.Close();
        return failed > 0 ? 1 : 0;
    }

    static void SetToggle(swUserPreferenceToggle_e p, bool v)
    {
        try { sw.SetUserPreferenceToggle((int)p, v); L("  pref " + p + " = " + v); }
        catch (Exception ex) { L("  pref " + p + " FAILED: " + ex.Message); }
    }

    static void SetInt(swUserPreferenceIntegerValue_e p, int v)
    {
        try { sw.SetUserPreferenceIntegerValue((int)p, v); L("  pref " + p + " = " + v); }
        catch (Exception ex) { L("  pref " + p + " FAILED: " + ex.Message); }
    }

    static void ConvertOne(string src, string srcRoot, string outRoot, int n, int total)
    {
        string rel     = Path.GetDirectoryName(src).Substring(srcRoot.Length).TrimStart('\\');
        string outDir  = string.IsNullOrEmpty(rel) ? outRoot : Path.Combine(outRoot, rel);
        string baseName = Path.GetFileNameWithoutExtension(src);

        if (File.Exists(Path.Combine(outDir, baseName + ".SLDPRT")) ||
            File.Exists(Path.Combine(outDir, baseName + ".SLDASM"))) { skipped++; return; }

        Directory.CreateDirectory(outDir);
        var t0 = DateTime.Now;

        // LoadFile4 is the working route for neutral formats. OpenDoc6
        // fails every one of these files with swFileRequiresRepairError
        // (2097152) regardless of the open options or the import
        // diagnostics preferences.
        int err = 0;
        ModelDoc2 m = null;
        try { m = (ModelDoc2)sw.LoadFile4(src, "r", null, ref err); }
        catch (Exception ex) { L(string.Format("[{0}/{1}] EXCEPTION open: {2}  <- {3}", n, total, ex.Message, baseName)); failed++; return; }

        if (m == null)
        {
            L(string.Format("[{0}/{1}] FAIL open err={2}  <- {3}", n, total, err, baseName));
            failed++;
            return;
        }

        string dst = Path.Combine(outDir,
            baseName + (m.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY ? ".SLDASM" : ".SLDPRT"));

        int e2 = 0, w2 = 0;
        bool ok = false;
        try { ok = m.Extension.SaveAs(dst, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                                      (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref e2, ref w2); }
        catch (Exception ex) { L("  saveas threw: " + ex.Message); }

        try { sw.CloseDoc(m.GetTitle()); } catch { }
        m = null;

        if (File.Exists(dst))
        {
            done++;
            L(string.Format("[{0}/{1}] OK {2,5:N1}s {3,9:N0}b  {4}", n, total,
                (DateTime.Now - t0).TotalSeconds, new FileInfo(dst).Length, baseName));
        }
        else { failed++; L(string.Format("[{0}/{1}] FAIL save ok={2} err={3}  <- {4}", n, total, ok, e2, baseName)); }
    }
}
