// From the VEX-Solidworks-Library repo, branch vex-library-2026,
// Tools/src/SwExplodeConfigs.cs.  Written up in
// entries/files/02-explode-configurations.md.
//
// Build:  csc -out:SwExplodeConfigs.exe -r:SolidWorks.Interop.sldworks.dll
//             -r:SolidWorks.Interop.swconst.dll SwExplodeConfigs.cs
// The interop assemblies ship with SolidWorks; copy them out of the install.
//
// Explodes a configurable VEX library part into one .SLDPRT per configuration.
//
// The v1.1.1 library ships each cut-to-length metal as a single part with one
// configuration per hole count (C-Channel: "1".."35", design-table driven).
// This writes a standalone single-configuration file for each of them:
//
//     Structure\C-Channel\Aluminum 2 Wide C-Channel.SLDPRT      <- master, untouched
//     Structure\C-Channel\Aluminum 2 Wide C-Channel\
//         1 Aluminum 2 Wide C-Channel.SLDPRT
//         2 Aluminum 2 Wide C-Channel.SLDPRT
//         ...
//
// naming taken from the one file v1.1.1 already shipped this way,
// "25 Aluminum 2 Wide C-Channel.SLDPRT".
//
// Method: copy the master to the destination name, open the copy, activate the
// wanted configuration, drop the design table, delete every other
// configuration, rebuild, save. There is no "save configuration as part" API,
// and a plain Save As would carry all 35 configurations into every output file.
//
// Resumable: an output newer than its master is left alone, so an interrupted
// run can be restarted. Every file is logged and its bounding box checked
// against the expected N x 0.5in length.
//
// Early binding through the interop assemblies, [STAThread] - see SwConvert.cs.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class SwExplodeConfigs
{
    const double HOLE_PITCH_IN = 0.5;   // VEX structure is on a 0.5in hole pitch
    const double TOL_IN        = 0.03;  // bounding-box slack before we call it suspect

    static StreamWriter log;
    static ISldWorks sw;
    static int done, skipped, failed, suspect;
    static bool dryRun, verify, checkOnly;

    static void L(string m)
    {
        Console.WriteLine(m);
        if (log != null) { log.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + m); log.Flush(); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        var positional = new List<string>();
        foreach (string a in args)
        {
            if (a == "--dry-run") dryRun = true;
            else if (a == "--verify") verify = true;
            else if (a == "--check") { checkOnly = true; verify = true; }
            else positional.Add(a);
        }
        if (positional.Count < 2)
        {
            Console.WriteLine("usage: SwExplodeConfigs <fileOrFolder> <logFile> [--dry-run] [--verify] [--check]");
            return 2;
        }

        string target = positional[0];
        log = new StreamWriter(positional[1], false);

        string[] masters = Directory.Exists(target)
            ? Directory.GetFiles(target, "*.SLDPRT", SearchOption.TopDirectoryOnly)
            : new string[] { target };
        Array.Sort(masters);

        L("=== explode run started " + DateTime.Now + (dryRun ? "  (DRY RUN)" : checkOnly ? "  (CHECK ONLY)" : "") + " ===");
        L("masters: " + masters.Length);

        if (!Connect()) { log.Close(); return 1; }

        foreach (string master in masters)
        {
            try { if (checkOnly) CheckExisting(master); else Explode(master); }
            catch (Exception ex) { failed++; L("!! " + Path.GetFileName(master) + " FAILED: " + ex.Message); }
        }

        L("");
        L("=== finished: " + done + " written, " + skipped + " skipped, "
          + failed + " failed, " + suspect + " suspect ===");
        log.Close();
        return failed > 0 ? 1 : 0;
    }

    static bool Connect()
    {
        try { sw = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application.34"); L("attached to running SolidWorks"); }
        catch
        {
            try
            {
                Type t = Type.GetTypeFromProgID("SldWorks.Application.34") ?? Type.GetTypeFromProgID("SldWorks.Application");
                sw = (ISldWorks)Activator.CreateInstance(t);
                sw.Visible = true;
                L("started SolidWorks");
            }
            catch (Exception ex) { L("CONNECT FAILED: " + ex.Message); return false; }
        }
        L("SolidWorks " + sw.RevisionNumber());
        CloseLeftovers();
        return true;
    }

    static void CloseLeftovers()
    {
        for (int i = 0; i < 50; i++)
        {
            ModelDoc2 open = (ModelDoc2)sw.ActiveDoc;
            if (open == null) break;
            L("closing leftover document: " + open.GetTitle());
            sw.CloseDoc(open.GetTitle());
        }
    }

    static void Explode(string master)
    {
        string baseName = Path.GetFileNameWithoutExtension(master);
        string outDir   = Path.Combine(Path.GetDirectoryName(master), baseName);

        // Config names come straight off the file - no need to open it.
        string[] cfgs = (string[])sw.GetConfigurationNames(master);
        Array.Sort(cfgs, CompareConfig);

        L("");
        L("######## " + baseName + "  (" + cfgs.Length + " configurations) ########");
        L("   -> " + outDir);

        if (!dryRun) Directory.CreateDirectory(outDir);
        DateTime masterStamp = File.GetLastWriteTimeUtc(master);

        foreach (string cfg in cfgs)
        {
            string dest = Path.Combine(outDir, cfg + " " + baseName + ".SLDPRT");

            if (File.Exists(dest) && File.GetLastWriteTimeUtc(dest) >= masterStamp)
            { skipped++; L("   skip  " + Path.GetFileName(dest)); continue; }

            if (dryRun) { L("   would write  " + Path.GetFileName(dest)); continue; }

            try
            {
                WriteOne(master, dest, cfg, baseName);
                done++;
            }
            catch (Exception ex)
            {
                failed++;
                L("   !! " + Path.GetFileName(dest) + " FAILED: " + ex.Message);
                try { CloseLeftovers(); } catch { }
                try { if (File.Exists(dest)) File.Delete(dest); } catch { }
            }
        }
    }

    static void WriteOne(string master, string dest, string cfg, string baseName)
    {
        File.Copy(master, dest, true);
        File.SetAttributes(dest, FileAttributes.Normal);   // master may be read-only

        int e = 0, w = 0;
        ModelDoc2 doc = (ModelDoc2)sw.OpenDoc6(dest, (int)swDocumentTypes_e.swDocPART,
                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
        if (doc == null) throw new Exception("open failed, err=" + e + " warn=" + w);

        try
        {
            // ShowConfiguration2 returns False when the configuration is already
            // the active one, which is exactly the case for whichever config the
            // master was last saved in ("35" for the C-Channels). Only a name
            // mismatch afterwards is a real failure.
            doc.ShowConfiguration2(cfg);
            string active = doc.ConfigurationManager.ActiveConfiguration.Name;
            if (active != cfg)
                throw new Exception("could not activate configuration " + cfg + " (active is " + active + ")");

            // The design table owns these configurations; leaving it in place
            // makes the deletions below either fail or come back on next edit.
            // Not every configurable part has one, so a failure here is fine.
            try { doc.DeleteDesignTable(); } catch { }

            // Delete everything except cfg. Re-read the list each pass because
            // deleting a parent takes its derived configurations with it.
            for (int pass = 0; pass < 4; pass++)
            {
                string[] names = (string[])doc.GetConfigurationNames();
                if (names.Length <= 1) break;
                foreach (string n in names)
                {
                    if (n == cfg) continue;
                    try { doc.DeleteConfiguration2(n); } catch { }
                }
            }

            string[] left = (string[])doc.GetConfigurationNames();
            if (left.Length != 1 || left[0] != cfg)
                throw new Exception("expected only '" + cfg + "', got: " + string.Join(",", left));

            doc.EditRebuild3();

            if (!doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w))
                throw new Exception("save failed, err=" + e + " warn=" + w);

            string note = "";
            if (verify) note = "  " + CheckBox(doc, cfg);

            L("   ok    " + Path.GetFileName(dest) + note);
        }
        finally
        {
            try { sw.CloseDoc(doc.GetTitle()); } catch { CloseLeftovers(); }
        }
    }

    // One bounding-box edge should be holes x 0.5in. For short pieces that is
    // not the longest edge - a 2-wide channel is 1in across, so anything under
    // two holes is wider than it is long - so every edge is a candidate.
    //
    // GetPartBox(false) converts to the document unit system (inches in this
    // library); GetPartBox(true) skips that and returns metres. Use metres so
    // the check does not silently depend on the part's unit setting.
    static string CheckBox(ModelDoc2 doc, string cfg)
    {
        try
        {
            double[] b = (double[])((PartDoc)doc).GetPartBox(true);
            double[] d = { (b[3] - b[0]) / 0.0254, (b[4] - b[1]) / 0.0254, (b[5] - b[2]) / 0.0254 };
            Array.Sort(d);
            string dims = d[2].ToString("0.000") + " x " + d[1].ToString("0.000")
                        + " x " + d[0].ToString("0.000") + "in";

            int holes;
            if (!int.TryParse(cfg, NumberStyles.Integer, CultureInfo.InvariantCulture, out holes))
                return dims;

            double expect = holes * HOLE_PITCH_IN;
            double best = Math.Min(Math.Abs(d[0] - expect), Math.Min(Math.Abs(d[1] - expect), Math.Abs(d[2] - expect)));
            string s = dims + "  expect " + expect.ToString("0.000") + "in";
            if (best > TOL_IN) { suspect++; s += "  <-- SUSPECT"; }
            return s;
        }
        catch (Exception ex) { return "bbox check failed: " + ex.Message; }
    }

    // Re-open already written outputs and confirm each is a single-configuration
    // part of the right length. Writes nothing.
    static void CheckExisting(string master)
    {
        string baseName = Path.GetFileNameWithoutExtension(master);
        string outDir   = Path.Combine(Path.GetDirectoryName(master), baseName);
        L("");
        L("######## checking " + baseName + " ########");
        if (!Directory.Exists(outDir)) { L("   no output folder"); return; }

        string[] cfgs = (string[])sw.GetConfigurationNames(master);
        Array.Sort(cfgs, CompareConfig);

        foreach (string cfg in cfgs)
        {
            string dest = Path.Combine(outDir, cfg + " " + baseName + ".SLDPRT");
            if (!File.Exists(dest)) { failed++; L("   !! MISSING " + Path.GetFileName(dest)); continue; }

            int e = 0, w = 0;
            ModelDoc2 doc = (ModelDoc2)sw.OpenDoc6(dest, (int)swDocumentTypes_e.swDocPART,
                                (int)swOpenDocOptions_e.swOpenDocOptions_Silent
                              | (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly, "", ref e, ref w);
            if (doc == null) { failed++; L("   !! WILL NOT OPEN " + Path.GetFileName(dest) + " err=" + e); continue; }

            try
            {
                string[] names = (string[])doc.GetConfigurationNames();
                string cfgNote = names.Length == 1 && names[0] == cfg
                               ? "1 cfg '" + names[0] + "'"
                               : "!! " + names.Length + " cfgs: " + string.Join(",", names);
                if (names.Length != 1 || names[0] != cfg) failed++;
                L("   " + Path.GetFileName(dest) + "  " + cfgNote + "  " + CheckBox(doc, cfg));
                done++;
            }
            finally { try { sw.CloseDoc(doc.GetTitle()); } catch { CloseLeftovers(); } }
        }
    }

    static int CompareConfig(string a, string b)
    {
        int ia, ib;
        bool na = int.TryParse(a, out ia), nb = int.TryParse(b, out ib);
        if (na && nb) return ia.CompareTo(ib);
        if (na) return -1;
        if (nb) return 1;
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
