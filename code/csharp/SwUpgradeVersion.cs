// From the VEX-Solidworks-Library repo, branch vex-library-2026,
// Tools/src/SwUpgradeVersion.cs.  Written up in
// entries/files/03-upgrade-file-version.md.
//
// Build:  csc -out:SwUpgradeVersion.exe -r:SolidWorks.Interop.sldworks.dll
//             -r:SolidWorks.Interop.swconst.dll SwUpgradeVersion.cs
//
// Re-saves every SolidWorks file in a tree with the running SolidWorks, which
// rewrites it in that version's file format.
//
// ORDER MATTERS. Parts are done before assemblies: opening an assembly pulls
// in its components, and saving it can rewrite them too. Doing parts first
// means each part is upgraded exactly once, by itself.
//
// THIS IS ONE-WAY. SolidWorks cannot open a file newer than itself, so once a
// part is written by 2026 nobody on an older seat can open it again. There is
// no downgrade.
//
// Resumable: a file already listed as done in the log is skipped, so an
// interrupted run can be restarted.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class SwUpgradeVersion
{
    static StreamWriter log;
    static ISldWorks sw;
    static int done, skipped, failed, alreadyCurrent;
    static int latestVersion;
    static HashSet<string> alreadyDone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    static void L(string m)
    {
        Console.WriteLine(m);
        if (log != null) { log.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + m); log.Flush(); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("usage: SwUpgradeVersion <root> <logFile> [--dry-run]"); return 2; }
        string root = args[0].TrimEnd('\\');
        string logPath = args[1];
        bool dry = Array.IndexOf(args, "--dry-run") >= 0;

        // Resume: anything previously logged "ok" is left alone.
        if (File.Exists(logPath))
            foreach (string line in File.ReadAllLines(logPath))
            {
                int k = line.IndexOf("  ok  ");
                if (k >= 0) alreadyDone.Add(line.Substring(k + 6).Trim());
            }

        log = new StreamWriter(logPath, true);
        L("=== upgrade run started " + DateTime.Now + (dry ? " (DRY RUN)" : "") + " ===");
        L("already done from a previous run: " + alreadyDone.Count);

        if (!Connect()) { log.Close(); return 1; }

        // Parts first, then assemblies, then drawings.
        var parts = new List<string>();
        var asms  = new List<string>();
        var drws  = new List<string>();
        foreach (string f in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(f).StartsWith("~$")) continue;
            switch (Path.GetExtension(f).ToLowerInvariant())
            {
                case ".sldprt": parts.Add(f); break;
                case ".sldasm": asms.Add(f);  break;
                case ".slddrw": drws.Add(f);  break;
            }
        }
        parts.Sort(); asms.Sort(); drws.Sort();
        L("found " + parts.Count + " parts, " + asms.Count + " assemblies, " + drws.Count + " drawings");

        Run(parts, (int)swDocumentTypes_e.swDocPART,     root, dry);
        Run(asms,  (int)swDocumentTypes_e.swDocASSEMBLY, root, dry);
        Run(drws,  (int)swDocumentTypes_e.swDocDRAWING,  root, dry);

        L("=== finished: " + done + " upgraded, " + alreadyCurrent + " already current, "
          + skipped + " skipped (earlier run), " + failed + " failed ===");
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
        latestVersion = sw.GetLatestSupportedFileVersion();
        L("latest supported file version: " + latestVersion);
        CloseLeftovers();
        return true;
    }

    static void CloseLeftovers()
    {
        for (int i = 0; i < 50; i++)
        {
            ModelDoc2 m = (ModelDoc2)sw.ActiveDoc;
            if (m == null) break;
            sw.CloseDoc(m.GetTitle());
        }
    }

    // "11000[2018/134] | 14000[2021/85]" -> 14000
    static bool IsCurrent(string path)
    {
        try
        {
            string[] vh = (string[])sw.VersionHistory(path);
            if (vh == null || vh.Length == 0) return false;
            string last = vh[vh.Length - 1];
            int br = last.IndexOf('[');
            if (br > 0) last = last.Substring(0, br);
            int v;
            return int.TryParse(last.Trim(), out v) && v >= latestVersion;
        }
        catch { return false; }
    }

    static void Run(List<string> files, int docType, string root, bool dry)
    {
        foreach (string f in files)
        {
            string rel = f.Substring(root.Length).TrimStart('\\');
            if (alreadyDone.Contains(rel)) { skipped++; continue; }

            // VersionHistory reads the saved-version list straight off the file,
            // without opening it. The last entry is the version it was last
            // written by; if that is already current there is nothing to do, and
            // re-saving would only churn the bytes for no gain.
            if (IsCurrent(f)) { alreadyCurrent++; L("   cur " + rel); continue; }

            if (dry) { L("   would upgrade  " + rel); continue; }

            ModelDoc2 doc = null;
            try
            {
                File.SetAttributes(f, FileAttributes.Normal);
                int e = 0, w = 0;
                doc = (ModelDoc2)sw.OpenDoc6(f, docType,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
                if (doc == null) throw new Exception("open failed err=" + e + " warn=" + w);

                // SaveAs over the same path rewrites in the running version even
                // when the model itself is unchanged; a plain Save can no-op.
                int se = 0, sw2 = 0;
                bool ok = doc.Extension.SaveAs(f, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref se, ref sw2);
                if (!ok) throw new Exception("save failed err=" + se + " warn=" + sw2);

                done++;
                L("   ok  " + rel);
            }
            catch (Exception ex)
            {
                failed++;
                L("   !! FAILED " + rel + " : " + ex.Message);
            }
            finally
            {
                try { if (doc != null) sw.CloseDoc(doc.GetTitle()); } catch { CloseLeftovers(); }
            }
        }
    }
}
