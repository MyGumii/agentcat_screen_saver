Option Explicit

Dim shell, fso, baseDirectory, powerShellPath, updateScript, screenSaverExe
Dim outputDirectory, command, exitCode

Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

baseDirectory = fso.GetParentFolderName(WScript.ScriptFullName)
powerShellPath = shell.ExpandEnvironmentStrings("%SystemRoot%") & _
    "\System32\WindowsPowerShell\v1.0\powershell.exe"
updateScript = fso.BuildPath(baseDirectory, "update-lockscreen.ps1")
screenSaverExe = fso.BuildPath(baseDirectory, "AgentCatScreenSaver.exe")
outputDirectory = fso.BuildPath(baseDirectory, "LockScreen")

command = Quote(powerShellPath) & _
    " -NoProfile -NonInteractive -WindowStyle Hidden" & _
    " -ExecutionPolicy Bypass -File " & Quote(updateScript) & _
    " -ScreenSaverExe " & Quote(screenSaverExe) & _
    " -OutputDirectory " & Quote(outputDirectory)

' Window style 0 hides the child from process creation onward, preventing
' the brief console flash that -WindowStyle Hidden alone can still allow.
exitCode = shell.Run(command, 0, True)
WScript.Quit exitCode

Function Quote(value)
    Quote = Chr(34) & value & Chr(34)
End Function
