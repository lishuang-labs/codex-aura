[CmdletBinding()]
param([string]$AppRoot,[string]$Destination)
$ErrorActionPreference='Stop'
if (!$AppRoot) {
    $parent=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    if (Test-Path -LiteralPath (Join-Path $parent 'DragonCodexBoot.exe')) { $AppRoot=$parent }
    else { $AppRoot=Join-Path $parent 'build/DragonCodexBoot' }
}
$AppRoot=[IO.Path]::GetFullPath($AppRoot)
if (!$Destination) { $Destination=$AppRoot }
$Destination=[IO.Path]::GetFullPath($Destination)
$exe=Join-Path $AppRoot 'DragonCodexBoot.exe'
if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Build or extract Codex Aura first.' }
if (!(Test-Path -LiteralPath $Destination -PathType Container)) { throw 'Shortcut destination must already exist.' }
$path=Join-Path $Destination 'Codex Aura.lnk'
$shell=New-Object -ComObject WScript.Shell
try {
    if (Test-Path -LiteralPath $path) {
        $existing=$shell.CreateShortcut($path)
        if ($existing.TargetPath -ine $exe -or $existing.Arguments -ne '--launch' -or $existing.WorkingDirectory -ine $AppRoot) {
            throw 'A different Codex Aura shortcut exists; preserved without overwriting.'
        }
        Write-Output ('Existing launch shortcut verified: '+$path)
        return
    }
    $link=$shell.CreateShortcut($path)
    $link.TargetPath=$exe;$link.Arguments='--launch';$link.WorkingDirectory=$AppRoot
    $link.IconLocation=$exe+',0';$link.Description='Codex Aura - play animation and open Codex'
    $link.Save()
    $check=$shell.CreateShortcut($path)
    if ($check.TargetPath -ine $exe -or $check.Arguments -ne '--launch') { throw 'Created shortcut verification failed.' }
    Write-Output ('Created independent launch shortcut: '+$path)
} finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
