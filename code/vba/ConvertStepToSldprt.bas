' ===========================================================================
'  ConvertStepToSldprt  -  batch STEP -> SolidWorks converter
'  For the VEX CAD library.  Tested against the folder layout produced by
'  the 2026 catalog refresh.
'
'  Written for the VEX-Solidworks-Library repo (branch vex-library-2026,
'  Tools/ConvertStepToSldprt.swp.bas) and copied here unchanged apart from
'  this note and the pointer below.  See entries/files/01-batch-convert-step.md
'  for what is proven and what is not.
'
'  WHAT IT DOES
'    Walks every .step / .stp file under SRC_ROOT, opens it in SolidWorks,
'    and saves it as .SLDPRT (or .SLDASM if SolidWorks decides the file is
'    an assembly) into OUT_ROOT, mirroring the folder structure.
'
'  IT IS RESUMABLE.  Files that already have an up-to-date output are
'  skipped, so you can stop it (Esc) and re-run it later without redoing
'  work.  Progress is written to OUT_ROOT\_conversion-log.txt.
'
'  HOW TO RUN
'    1. Open SolidWorks.
'    2. Set the STEP import options first - see the entry, "Set the import
'       options first".  This matters a lot for speed; skipping it can make
'       the run 10x slower.
'    3. Tools > Macro > New...  save as ConvertStepToSldprt.swp
'    4. Paste this file's contents in, replacing whatever is there.
'    5. Press F5.
'
'  Expect this to take a while.  A few of the electronics STEP files are
'  15+ MB and can take several minutes each on their own.
' ===========================================================================

Option Explicit

' --- edit these two if you move the library -------------------------------
Const SRC_ROOT As String = "C:\Users\M0obo\Desktop\VEX-CAD-Solidworks-1.1.1\VEX-STEP-2026"
Const OUT_ROOT As String = "C:\Users\M0obo\Desktop\VEX-CAD-Solidworks-1.1.1\VEX-SLDPRT-2026"
' -------------------------------------------------------------------------

Dim swApp        As Object
Dim fso          As Object
Dim logStream    As Object
Dim nDone        As Long
Dim nSkipped     As Long
Dim nFailed      As Long
Dim tStart       As Double

Sub main()

    Set swApp = Application.SldWorks
    Set fso = CreateObject("Scripting.FileSystemObject")

    If Not fso.FolderExists(SRC_ROOT) Then
        MsgBox "Source folder not found:" & vbCrLf & SRC_ROOT, vbCritical
        Exit Sub
    End If

    EnsureFolder OUT_ROOT
    Set logStream = fso.OpenTextFile(OUT_ROOT & "\_conversion-log.txt", 8, True)

    tStart = Timer
    nDone = 0: nSkipped = 0: nFailed = 0

    WriteLog "=== run started " & Now & " ==="
    swApp.Visible = True

    WalkFolder SRC_ROOT

    Dim mins As Double
    mins = (Timer - tStart) / 60#

    WriteLog "=== finished: " & nDone & " converted, " & nSkipped & _
             " skipped, " & nFailed & " failed, " & Format(mins, "0.0") & " min ==="
    logStream.Close

    MsgBox "STEP conversion finished." & vbCrLf & vbCrLf & _
           "Converted: " & nDone & vbCrLf & _
           "Skipped (already done): " & nSkipped & vbCrLf & _
           "Failed: " & nFailed & vbCrLf & vbCrLf & _
           "Elapsed: " & Format(mins, "0.0") & " minutes" & vbCrLf & vbCrLf & _
           "Log: " & OUT_ROOT & "\_conversion-log.txt", vbInformation

End Sub


' Recurse through the source tree.
Private Sub WalkFolder(ByVal folderPath As String)

    Dim f As Object, sub_ As Object
    Dim ext As String

    For Each f In fso.GetFolder(folderPath).Files
        ext = LCase(fso.GetExtensionName(f.Name))
        If ext = "step" Or ext = "stp" Then
            ConvertOne f.Path
        End If
    Next f

    For Each sub_ In fso.GetFolder(folderPath).SubFolders
        WalkFolder sub_.Path
    Next sub_

End Sub


Private Sub ConvertOne(ByVal stepPath As String)

    Dim relPath   As String
    Dim outPath   As String
    Dim outDir    As String
    Dim baseName  As String
    Dim swModel   As Object
    Dim nErr      As Long, nWarn As Long
    Dim ok        As Boolean

    ' Mirror the relative path from SRC_ROOT into OUT_ROOT.
    relPath = Mid(stepPath, Len(SRC_ROOT) + 2)
    baseName = fso.GetBaseName(stepPath)
    outDir = OUT_ROOT & "\" & PathParent(relPath)
    outPath = outDir & "\" & baseName & ".SLDPRT"

    ' Resumability: skip if we already produced a part or assembly for it.
    If fso.FileExists(outPath) Or fso.FileExists(outDir & "\" & baseName & ".SLDASM") Then
        nSkipped = nSkipped + 1
        Exit Sub
    End If

    EnsureFolder outDir

    ' Use LoadFile4, NOT OpenDoc6. On these VEX STEP files OpenDoc6 fails
    ' with swFileRequiresRepairError (2097152) whatever open options and
    ' import-diagnostics preferences you set; LoadFile4 imports them
    ' cleanly. Verified against SOLIDWORKS 2026 SP1.1.
    Set swModel = swApp.LoadFile4(stepPath, "r", Nothing, nErr)

    If swModel Is Nothing Then
        nFailed = nFailed + 1
        WriteLog "FAIL  (err=" & nErr & ") " & relPath
        Exit Sub
    End If

    ' SolidWorks decides part vs assembly for neutral formats regardless of
    ' the type we asked for, so save to whatever it actually gave us.
    ' GetType: 1 = part, 2 = assembly
    If swModel.GetType = 2 Then
        outPath = outDir & "\" & baseName & ".SLDASM"
    End If

    ok = swModel.SaveAs3(outPath, 0, 2)   ' 0 = current version, 2 = silent

    swApp.CloseDoc swModel.GetTitle
    Set swModel = Nothing

    If ok Then
        nDone = nDone + 1
        WriteLog "OK    " & relPath
    Else
        nFailed = nFailed + 1
        WriteLog "SAVEFAIL " & relPath
    End If

End Sub


' Everything before the last backslash, or "" if there is none.
Private Function PathParent(ByVal rel As String) As String
    Dim p As Long
    p = InStrRev(rel, "\")
    If p = 0 Then
        PathParent = ""
    Else
        PathParent = Left(rel, p - 1)
    End If
End Function


' mkdir -p
Private Sub EnsureFolder(ByVal path As String)
    If path = "" Then Exit Sub
    If fso.FolderExists(path) Then Exit Sub
    EnsureFolder PathParentAbs(path)
    On Error Resume Next
    fso.CreateFolder path
    On Error GoTo 0
End Sub

Private Function PathParentAbs(ByVal path As String) As String
    Dim p As Long
    p = InStrRev(path, "\")
    If p <= 3 Then
        PathParentAbs = ""
    Else
        PathParentAbs = Left(path, p - 1)
    End If
End Function


Private Sub WriteLog(ByVal msg As String)
    On Error Resume Next
    logStream.WriteLine msg
End Sub
