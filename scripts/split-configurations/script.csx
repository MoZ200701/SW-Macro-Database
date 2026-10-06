// split-configurations — one standalone part per configuration.
//
// The calls and their order come from code/csharp/SwExplodeConfigs.cs, which
// split a real library on SolidWorks 2026 SP1.1
// (entries/files/02-explode-configurations.md). This version is reworked to run
// inside a host that already holds the SolidWorks connection (`ctx.Sw`), and has
// NOT been run as a script yet.
//
// Method, unchanged: copy the master to the output name, open the copy, activate
// the wanted configuration, drop the design table, delete every other
// configuration, rebuild, save. There is no "save configuration as part" API,
// and a plain Save As would carry every configuration into every output.
//
// Differences from SwExplodeConfigs.cs:
//  - Output folder and file naming are parameters. The VEX-specific naming and
//    the 0.5in hole-pitch bounding-box check are gone.
//  - It never closes documents it did not open (the original closed every open
//    document, discarding changes, before starting and after a failure).
//  - Masters are never opened or written; only the copies are.

var masters = ctx.Params.GetFiles("masters");
string outRoot = Path.GetFullPath(ctx.Params.GetFolder("outputFolder"));
string nameFormat = ctx.Params.GetString("nameFormat");

int CompareConfig(string a, string b)
{
    bool na = int.TryParse(a, out int ia), nb = int.TryParse(b, out int ib);
    if (na && nb) return ia.CompareTo(ib);
    if (na) return -1;
    if (nb) return 1;
    return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
}

string OutputName(string cfg, string baseName)
{
    string name = nameFormat.Replace("{config}", cfg).Replace("{name}", baseName);
    foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
    return name + ".SLDPRT";
}

void WriteOne(string master, string dest, string cfg)
{
    File.Copy(master, dest, true);
    File.SetAttributes(dest, FileAttributes.Normal);   // the master may be read-only

    int e = 0, w = 0;
    ModelDoc2 doc = (ModelDoc2)ctx.Sw.OpenDoc6(dest, (int)swDocumentTypes_e.swDocPART,
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
    if (doc == null) throw new Exception("open failed, err=" + e + " warn=" + w);

    try
    {
        // ShowConfiguration2 returns False when the configuration is already
        // active, so read the active name back instead of trusting the result.
        doc.ShowConfiguration2(cfg);
        string active = doc.ConfigurationManager.ActiveConfiguration.Name;
        if (active != cfg)
            throw new Exception("could not activate configuration " + cfg + " (active is " + active + ")");

        // The design table owns the configurations; it must go before any
        // deletion. Not every configurable part has one.
        try { doc.DeleteDesignTable(); } catch { }

        // Re-read each pass: deleting a parent takes its derived configurations.
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
    }
    finally
    {
        // Closes the copy this script opened, by its own title.
        try { ctx.Sw.CloseDoc(doc.GetTitle()); } catch { }
    }
}

foreach (string master in masters)
{
    if (ctx.Cancel.IsCancellationRequested) { ctx.Log("cancelled"); break; }

    string baseName = Path.GetFileNameWithoutExtension(master);
    string outDir = Path.Combine(outRoot, baseName);

    string[] cfgs;
    try { cfgs = (string[])ctx.Sw.GetConfigurationNames(master); }   // reads the closed file
    catch (Exception ex) { ctx.Failed(master, "could not read configurations: " + ex.Message); continue; }
    if (cfgs == null || cfgs.Length == 0) { ctx.Failed(master, "no configurations found"); continue; }
    Array.Sort(cfgs, CompareConfig);

    ctx.Log(baseName + ": " + cfgs.Length + " configurations -> " + outDir);
    if (cfgs.Length == 1) { ctx.Skipped(master, "only one configuration"); continue; }
    if (!ctx.DryRun) Directory.CreateDirectory(outDir);

    DateTime masterStamp = File.GetLastWriteTimeUtc(master);

    foreach (string cfg in cfgs)
    {
        if (ctx.Cancel.IsCancellationRequested) break;

        string dest = Path.Combine(outDir, OutputName(cfg, baseName));

        // Resumable: an output newer than its master is left alone.
        if (File.Exists(dest) && File.GetLastWriteTimeUtc(dest) >= masterStamp)
        { ctx.Skipped(dest, "newer than its master"); continue; }

        if (ctx.DryRun) { ctx.Log("would write " + dest); continue; }

        try
        {
            WriteOne(master, dest, cfg);
            ctx.Wrote(dest);
        }
        catch (Exception ex)
        {
            ctx.Failed(dest, ex.Message);
            try { if (File.Exists(dest)) File.Delete(dest); } catch { }   // only the copy this run made
        }
    }
}
