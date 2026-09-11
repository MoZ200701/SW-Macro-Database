' ===========================================================================
'  ExplodeConfigsToFiles  -  one .SLDPRT per configuration
'  For the VEX CAD library.
'
'  Written for the VEX-Solidworks-Library repo (branch vex-library-2026,
'  Tools/ExplodeConfigsToFiles.swp.bas) and copied here unchanged apart from
'  this note.  See entries/files/02-explode-configurations.md for what is
'  proven and what is not.
'
'  WHAT IT DOES
'    The v1.1.1 library ships each cut-to-length metal as ONE part with one
'    configuration per hole count - C-Channel carries "1" through "35",
'    driven by a design table. This writes a standalone, single-configuration
'    file for every one of them, beside the master in a folder of its own:
'
'      Structure\C-Channel\Aluminum 2 Wide C-Channel.SLDPRT   <- master, untouched
'      Structure\C-Channel\Aluminum 2 Wide C-Channel\
'          1 Aluminum 2 Wide C-Channel.SLDPRT
'          2 Aluminum 2 Wide C-Channel.SLDPRT
'          ... 35 Aluminum 2 Wide C-Channel.SLDPRT
'
'    The "<holes> <master name>.SLDPRT" naming is the one v1.1.1 already used
'    for the single file it shipped this way, "25 Aluminum 2 Wide C-Channel".
'
'  HOW IT WORKS, AND WHY
'    SolidWorks has no "save this configuration as its own part" API, and a
'    plain Save As copies ALL 35 configurations into every output file. So
'    for each configuration the macro copies the master to the output name,
'    opens the copy, activates the wanted configuration, deletes the design
'    table, deletes every other configuration, rebuilds and saves.
'
'    The design table has to go first: it owns those configurations, so with
'    it still in place the deletions either fail or come back on the next
'    edit. Only the copy is touched - the master keeps its table and all 35.
'
'  IT IS RESUMABLE.  An output at least as new as its master is skipped, so
'  you can stop it (Esc) and re-run without redoing work. Delete an output
'  folder to force those files to be rebuilt.
'
'  HOW TO RUN
'    1. Open SolidWorks.
'    2. Tools > Macro > New...  save as ExplodeConfigsToFiles.swp
'    3. Paste this file's contents in, replacing whatever is there.
'    4. Edit LIB_ROOT and TargetFolders below if you want more than C-Channel.
'    5. Press F5.
'
'  Budget about 5 seconds per file. C-Channel alone is 5 masters x 35 = 175
'  files, roughly 15 minutes and about 700 MB on disk.
' ===========================================================================

Option Explicit

' --- edit these if you move the library -----------------------------------
Const LIB_ROOT As String = "C:\Users\M0obo\Desktop\VEX-CAD-Solidworks-1.1.1\VEX-Library-2026"
Const LOG_PATH As String = "C:\Users\M0obo\Desktop\VEX-CAD-Solidworks-1.1.1\VEX-Library-2026\_explode-log.txt"

' Only explode configurations whose name is a whole number in this range.
' The metals name their configurations after the hole count, so this is
' "1 hole to 35 holes". Set MAX_HOLES to 0 to take every configuration
' whatever it is called.
Const MIN_HOLES As Long = 1
Const MAX_HOLES As Long = 35
' -------------------------------------------------------------------------

' Every .SLDPRT sitting directly in one of these folders gets exploded.
' Paths are relative to LIB_ROOT. The two below are the ones that have been
' run; add any of the commented lines to do the rest.
'
' What is left, and what to watch for:
'
'   Structure\U-Channel      20 configurations, 1-20. Not 35.
'   Structure\Chassis Rail   25, 25 and 35, depending on the part.
'   Structure\Linear Slide   35 configurations.
'
'   Structure\Plate          124 configurations each, and the 0.25in Pitch
'   Structure\0.25in Pitch Structure   Plate has 300. These are named
'       "5x25" - width x length in holes - not a plain hole count, so they
'       need MAX_HOLES = 0 or WantConfig below skips every one of them.
'       The 0.25in pitch parts are also on a 0.25in hole pitch, not 0.5in,
'       so their configuration N is N x 0.25in long.
Private Function TargetFolders() As Variant
    TargetFolders = Array( _
        "Structure\C-Channel" _
    ,   "Structure\Angle" _
    )
'   ,   "Structure\U-Channel" _
'   ,   "Structure\Chassis Rail" _
'   ,   "Structure\Linear Slide" _
'   ,   "Structure\Plate" _
'   ,   "Structure\0.25in Pitch Structure" _
End Function

Dim swApp     As Object
Dim fso       As Object
Dim logStream As Object
Dim nDone     As Long
Dim nSkipped  As Long
Dim nFailed   As Long
Dim tStart    As Double

Sub main()

    Set swApp = Application.SldWorks
    Set fso = CreateObject("Scripting.FileSystemObject")

    If Not fso.FolderExists(LIB_ROOT) Then
        MsgBox "Library root not found:" & vbCrLf & LIB_ROOT, vbCritical
        Exit Sub
    End If

    Set logStream = fso.OpenTextFile(LOG_PATH, 8, True)
    tStart = Timer
    nDone = 0: nSkipped = 0: nFailed = 0

    WriteLog "=== run started " & Now & " ==="
    swApp.Visible = True

    Dim rel As Variant, folderPath As String
    For Each rel In TargetFolders()
        folderPath = LIB_ROOT & "\" & rel
        If fso.FolderExists(folderPath) Then
            ExplodeFolder folderPath
        Else
            nFailed = nFailed + 1
            WriteLog "MISSING FOLDER  " & folderPath
        End If
    Next rel

    Dim mins As Double
    mins = (Timer - tStart) / 60#

    WriteLog "=== finished: " & nDone & " written, " & nSkipped & _
             " skipped, " & nFailed & " failed, " & Format(mins, "0.0") & " min ==="
    logStream.Close

    MsgBox "Configuration explode finished." & vbCrLf & vbCrLf & _
           "Written: " & nDone & vbCrLf & _
           "Skipped (already done): " & nSkipped & vbCrLf & _
           "Failed: " & nFailed & vbCrLf & vbCrLf & _
           "Elapsed: " & Format(mins, "0.0") & " minutes" & vbCrLf & vbCrLf & _
           "Log: " & LOG_PATH, vbInformation

End Sub


' Every part directly inside this folder, masters only - the output
' subfolders are not walked into.
Private Sub ExplodeFolder(ByVal folderPath As String)

    Dim f As Object
    For Each f In fso.GetFolder(folderPath).Files
        If LCase(fso.GetExtensionName(f.Name)) = "sldprt" Then
            ExplodeMaster f.Path
        End If
    Next f

End Sub


Private Sub ExplodeMaster(ByVal masterPath As String)

    Dim baseName As String
    Dim outDir   As String
    Dim cfgNames As Variant
    Dim i        As Long

    baseName = fso.GetBaseName(masterPath)
    outDir = fso.GetParentFolderName(masterPath) & "\" & baseName

    ' Configuration names can be read straight off the file, unopened.
    cfgNames = swApp.GetConfigurationNames(masterPath)

    If IsEmpty(cfgNames) Then
        WriteLog "NO CONFIGS  " & baseName
        Exit Sub
    End If

    WriteLog ""
    WriteLog "######## " & baseName & "  (" & (UBound(cfgNames) + 1) & " configurations) ########"

    EnsureFolder outDir

    For i = 0 To UBound(cfgNames)
        If WantConfig(CStr(cfgNames(i))) Then
            ExplodeOne masterPath, baseName, outDir, CStr(cfgNames(i))
        End If
    Next i

End Sub


Private Sub ExplodeOne(ByVal masterPath As String, ByVal baseName As String, _
                       ByVal outDir As String, ByVal cfg As String)

    Dim destPath As String
    Dim swModel  As Object
    Dim nErr     As Long, nWarn As Long
    Dim names    As Variant
    Dim j        As Long, pass As Long

    destPath = outDir & "\" & cfg & " " & baseName & ".SLDPRT"

    ' Resumability: an output at least as new as the master is already done.
    If fso.FileExists(destPath) Then
        If fso.GetFile(destPath).DateLastModified >= fso.GetFile(masterPath).DateLastModified Then
            nSkipped = nSkipped + 1
            Exit Sub
        End If
    End If

    On Error GoTo Failed

    fso.CopyFile masterPath, destPath, True
    fso.GetFile(destPath).Attributes = 0      ' master may be read-only

    ' 1 = swDocPART, 1 = swOpenDocOptions_Silent
    Set swModel = swApp.OpenDoc6(destPath, 1, 1, "", nErr, nWarn)
    If swModel Is Nothing Then
        WriteLog "  OPENFAIL (err=" & nErr & ") " & cfg & " " & baseName
        GoTo Failed
    End If

    ' ShowConfiguration2 returns False when the configuration is already the
    ' active one - which is exactly the case for whichever config the master
    ' was last saved in ("35" for the C-Channels). Only a name mismatch
    ' afterwards is a real failure.
    swModel.ShowConfiguration2 cfg
    If swModel.ConfigurationManager.ActiveConfiguration.Name <> cfg Then
        WriteLog "  CFGFAIL  cannot activate '" & cfg & "' in " & baseName
        GoTo Failed
    End If

    ' Must go before the deletions - see the header. Not every configurable
    ' part has a design table, so a failure here is fine.
    On Error Resume Next
    swModel.DeleteDesignTable
    On Error GoTo Failed

    ' Re-read the list each pass: deleting a parent configuration takes its
    ' derived children with it, so a single pass can try to delete a name
    ' that is already gone.
    For pass = 1 To 4
        names = swModel.GetConfigurationNames
        If UBound(names) < 1 Then Exit For
        For j = 0 To UBound(names)
            If CStr(names(j)) <> cfg Then
                On Error Resume Next
                swModel.DeleteConfiguration2 CStr(names(j))
                On Error GoTo Failed
            End If
        Next j
    Next pass

    names = swModel.GetConfigurationNames
    If UBound(names) <> 0 Or CStr(names(0)) <> cfg Then
        WriteLog "  PRUNEFAIL " & cfg & " " & baseName & " - left " & (UBound(names) + 1) & " configs"
        GoTo Failed
    End If

    swModel.EditRebuild3

    ' 1 = swSaveAsOptions_Silent
    If Not swModel.Save3(1, nErr, nWarn) Then
        WriteLog "  SAVEFAIL (err=" & nErr & ") " & cfg & " " & baseName
        GoTo Failed
    End If

    swApp.CloseDoc swModel.GetTitle
    Set swModel = Nothing

    nDone = nDone + 1
    WriteLog "  ok  " & cfg & " " & baseName & ".SLDPRT"
    Exit Sub

Failed:
    On Error Resume Next
    If Not swModel Is Nothing Then
        swApp.CloseDoc swModel.GetTitle
        Set swModel = Nothing
    End If
    ' Leave no half-written file behind, so a re-run retries it.
    If fso.FileExists(destPath) Then fso.DeleteFile destPath, True
    nFailed = nFailed + 1
    WriteLog "  FAIL " & cfg & " " & baseName & " - " & Err.Description
    Err.Clear

End Sub


' Configuration names in this library are hole counts. Keep the ones inside
' the wanted range; MAX_HOLES = 0 means take everything.
Private Function WantConfig(ByVal cfg As String) As Boolean

    If MAX_HOLES = 0 Then
        WantConfig = True
        Exit Function
    End If

    Dim n As Long
    If Not IsNumeric(cfg) Then
        WantConfig = False
        Exit Function
    End If

    n = CLng(cfg)
    WantConfig = (n >= MIN_HOLES And n <= MAX_HOLES)

End Function


' mkdir -p
Private Sub EnsureFolder(ByVal path As String)
    If path = "" Then Exit Sub
    If fso.FolderExists(path) Then Exit Sub
    EnsureFolder fso.GetParentFolderName(path)
    On Error Resume Next
    fso.CreateFolder path
    On Error GoTo 0
End Sub


Private Sub WriteLog(ByVal msg As String)
    On Error Resume Next
    logStream.WriteLine msg
End Sub
