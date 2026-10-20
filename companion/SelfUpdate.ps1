param(
    [Parameter(Mandatory=$true)][int]$ParentProcessId,
    [Parameter(Mandatory=$true)][long]$ParentStarted,
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [Parameter(Mandatory=$true)][string]$TargetPath,
    [Parameter(Mandatory=$true)][string]$ExpectedHash,
    [Parameter(Mandatory=$true)][string]$ReadyPath,
    [switch]$NoErrorDialog
)
$ErrorActionPreference = 'Stop'
$replaced = $false
$parentExited = $false
$folder = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($TargetPath))
$candidate = Join-Path $folder ('.realmly-next-' + [guid]::NewGuid().ToString('N'))
$backup = Join-Path $folder ('.realmly-previous-' + [guid]::NewGuid().ToString('N'))
try {
    if ((Get-FileHash -LiteralPath $SourcePath -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'The staged update failed verification.' }
    $parent = [Diagnostics.Process]::GetProcessById($ParentProcessId)
    if ($parent.StartTime.ToUniversalTime().Ticks -ne $ParentStarted) { throw 'The original Companion process has changed.' }
    [IO.File]::WriteAllText($ReadyPath, 'ready')
    if (-not $parent.WaitForExit(60000)) { throw 'The Companion did not close. No files were changed.' }
    $parentExited = $true
    # Copy into the target directory first, so replacement is on the same volume.
    Copy-Item -LiteralPath $SourcePath -Destination $candidate
    if ((Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash -ne $ExpectedHash) { throw 'The copied update failed verification.' }
    [IO.File]::Replace($candidate, $TargetPath, $backup, $true)
    $replaced = $true
    Start-Process -FilePath $TargetPath -WorkingDirectory $folder
    Remove-Item -LiteralPath $backup -Force -ErrorAction SilentlyContinue
    # Only remove our own GUID staging directory beneath Realmly/Updates.
    $stage = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($SourcePath))
    $updates = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Realmly\Updates'
    if ([IO.Path]::GetDirectoryName($stage) -eq $updates -and [IO.Path]::GetFileName($stage) -match '^[a-f0-9]{32}$') {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
    }
} catch {
    $failure = $_.Exception.Message
    if ($replaced -and (Test-Path -LiteralPath $backup)) {
        try {
            # PowerShell 5.1 binds a null backup argument as an empty path. Use
            # our concrete candidate path to retain the failed EXE during rollback.
            [IO.File]::Replace($backup, $TargetPath, $candidate, $true)
            Start-Process -FilePath $TargetPath -WorkingDirectory $folder
        } catch { $failure += "`nRecovery copy: $backup`n" + $_.Exception.Message }
    } elseif ($parentExited -and (Test-Path -LiteralPath $TargetPath)) {
        try { Start-Process -FilePath $TargetPath -WorkingDirectory $folder }
        catch { $failure += "`nCould not reopen the unchanged Companion: " + $_.Exception.Message }
    }
    [IO.File]::WriteAllText((Join-Path ([IO.Path]::GetDirectoryName($ReadyPath)) 'update-error.txt'), $failure)
    if (-not $NoErrorDialog) {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show("The Companion update could not finish.`n`n$failure", 'Realmly update', 'OK', 'Error') | Out-Null
    }
} finally {
    if (Test-Path -LiteralPath $candidate) { Remove-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue }
}
