// From the VEX-Solidworks-Library repo, branch vex-library-2026,
// Tools/src/SwInspect.cs.  Written up in
// entries/reading/08-dimensions-and-equations.md.
//
// Build:  csc -out:SwInspect.exe -r:SolidWorks.Interop.sldworks.dll
//             -r:SolidWorks.Interop.swconst.dll SwInspect.cs
//
// Dumps the internals of configurable VEX library parts: configuration names,
// design-table presence, equations/global variables, and every feature's
// display dimensions. Used to design the length-explode macro.
//
// Early binding through the interop assemblies, [STAThread], same as SwConvert.

using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class SwInspect
{
    static StreamWriter log;
    static ISldWorks sw;

    static void L(string m)
    {
        Console.WriteLine(m);
        if (log != null) { log.WriteLine(m); log.Flush(); }
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("usage: SwInspect <folderOrFile> <logFile>"); return 2; }
        log = new StreamWriter(args[1], false);

        try { sw = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application.34"); L("attached to running instance"); }
        catch
        {
            try
            {
                Type t = Type.GetTypeFromProgID("SldWorks.Application.34");
                if (t == null) t = Type.GetTypeFromProgID("SldWorks.Application");
                sw = (ISldWorks)Activator.CreateInstance(t);
                sw.Visible = true;
                L("started a new instance");
            }
            catch (Exception ex) { L("CONNECT FAILED: " + ex.Message); log.Close(); return 1; }
        }

        L("SolidWorks " + sw.RevisionNumber());

        string[] files = Directory.Exists(args[0])
            ? Directory.GetFiles(args[0], "*.SLDPRT", SearchOption.AllDirectories)
            : new string[] { args[0] };
        Array.Sort(files);

        foreach (string f in files)
        {
            L("");
            L("################ " + Path.GetFileName(f) + " ################");
            int e = 0, w = 0;
            ModelDoc2 doc = null;
            try
            {
                doc = (ModelDoc2)sw.OpenDoc6(f, (int)swDocumentTypes_e.swDocPART,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent |
                        (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly, "", ref e, ref w);
            }
            catch (Exception ex) { L("OpenDoc6 threw: " + ex.Message); }
            if (doc == null) { L("*** COULD NOT OPEN (err=" + e + " warn=" + w + ") ***"); continue; }
            L("opened  err=" + e + " warn=" + w);

            try { L("active config: " + doc.ConfigurationManager.ActiveConfiguration.Name); } catch { }

            try
            {
                string[] names = (string[])doc.GetConfigurationNames();
                L("config count: " + names.Length);
                foreach (string n in names)
                {
                    string extra = "";
                    try
                    {
                        Configuration c = (Configuration)doc.GetConfigurationByName(n);
                        Configuration p = (Configuration)c.GetParent();
                        extra = "  desc='" + c.Description + "'" + (p != null ? "  parent=" + p.Name : "");
                    }
                    catch { }
                    L("   CFG  " + n + extra);
                }
            }
            catch (Exception ex) { L("config enum failed: " + ex.Message); }

            try { L(doc.GetDesignTable() != null ? "DesignTable: PRESENT" : "DesignTable: none"); }
            catch { L("DesignTable: query failed"); }

            // Sample the first, middle and last configuration so the size of the
            // thing is visible without opening it. GetPartBox(true) is metres.
            try
            {
                string[] cn = (string[])doc.GetConfigurationNames();
                Array.Reverse(cn);
                int[] pick = { 0, cn.Length / 2, cn.Length - 1 };
                foreach (int i in pick)
                {
                    if (i < 0 || i >= cn.Length) continue;
                    doc.ShowConfiguration2(cn[i]);
                    doc.EditRebuild3();
                    double[] bb = (double[])((PartDoc)doc).GetPartBox(true);
                    double[] e2 = { (bb[3]-bb[0])/0.0254, (bb[4]-bb[1])/0.0254, (bb[5]-bb[2])/0.0254 };
                    Array.Sort(e2);
                    L("   SIZE cfg '" + cn[i] + "' = " + e2[2].ToString("0.000") + " x "
                      + e2[1].ToString("0.000") + " x " + e2[0].ToString("0.000") + " in");
                }
            }
            catch (Exception ex) { L("size sample failed: " + ex.Message); }

            try
            {
                EquationMgr em = doc.GetEquationMgr();
                if (em != null)
                {
                    L("equations: " + em.GetCount());
                    for (int i = 0; i < em.GetCount(); i++)
                        L("   EQ[" + i + "] " + em.Equation[i]);
                }
            }
            catch (Exception ex) { L("equation dump failed: " + ex.Message); }

            try
            {
                L("features:");
                Feature feat = (Feature)doc.FirstFeature();
                int i = 0;
                while (feat != null && i < 400)
                {
                    string tn = ""; try { tn = feat.GetTypeName2(); } catch { }
                    L("   FEAT " + feat.Name + "  [" + tn + "]");
                    try
                    {
                        DisplayDimension dd = (DisplayDimension)feat.GetFirstDisplayDimension();
                        while (dd != null)
                        {
                            Dimension d = (Dimension)dd.GetDimension();
                            double v = 0; try { v = d.GetSystemValue2(""); } catch { }
                            L("        DIM " + d.FullName + " = " + Math.Round(v * 1000.0, 4) + " mm");
                            dd = (DisplayDimension)feat.GetNextDisplayDimension(dd);
                        }
                    }
                    catch { }
                    feat = (Feature)feat.GetNextFeature();
                    i++;
                }
            }
            catch (Exception ex) { L("feature walk failed: " + ex.Message); }

            try { sw.CloseDoc(doc.GetTitle()); } catch { }
        }

        L("");
        L("=== done ===");
        log.Close();
        return 0;
    }
}
