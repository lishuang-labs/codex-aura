[CmdletBinding()]
param([ValidateSet('Install','Verify','Restore')][string]$Action='Install',[string]$AppRoot)
$ErrorActionPreference='Stop'
if (!$AppRoot) {
    $parent=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    $AppRoot=if (Test-Path -LiteralPath (Join-Path $parent 'DragonCodexBoot.exe')) { $parent } else { Join-Path $parent 'build/DragonCodexBoot' }
}
$AppRoot=[IO.Path]::GetFullPath($AppRoot)
$integration=Join-Path $PSScriptRoot 'integrate-entrypoints.ps1'
if ($Action -ne 'Install') { & $integration -Action $Action -AppRoot $AppRoot;return }
$cfg=Get-Content -LiteralPath (Join-Path $AppRoot 'launcher.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($cfg.AutoReplaceEntrypoints -ne $false -or $cfg.ScanAllLocalDrives -ne $false) { throw 'Keep automatic integration and drive scanning disabled.' }
if ($cfg.AppLaunch -notmatch '^shell:AppsFolder\\([A-Za-z0-9_.-]+![A-Za-z0-9_.-]+)$') { throw 'Invalid registered application ID.' }
$appId=$Matches[1]
$installed=Get-StartApps | Where-Object { $_.AppID -ieq $appId } | Select-Object -First 1
if (!$installed) { throw 'Configured Codex application is not installed.' }
$family,$application=$appId.Split('!')
$official=@(foreach ($package in Get-AppxPackage | Where-Object { $_.PackageFamilyName -ieq $family }) {
    $manifest=Get-AppxPackageManifest -Package $package.PackageFullName
    foreach ($app in @($manifest.Package.Applications.Application | Where-Object { $_.Id -ieq $application })) {
        if ($app.Executable) { Join-Path $package.InstallLocation $app.Executable }
    }
})
$official=@($official | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
if (!$official.Count) { throw 'Unable to resolve installed Codex icon; no shortcuts changed.' }
$paths=@(Join-Path ([Environment]::GetFolderPath('Desktop')) 'Codex.lnk'; Join-Path ([Environment]::GetFolderPath('Programs')) 'Codex.lnk')
& $integration -Action Install -AppRoot $AppRoot -ShortcutPaths $paths -CreateMissingLinks -OfficialExecutablePaths $official
$shell=New-Object -ComObject WScript.Shell
try {
    foreach ($path in $paths) {
        if (!(Test-Path -LiteralPath $path)) { throw ('Shortcut could not be created: '+$path) }
        $link=$shell.CreateShortcut($path)
        if ($link.TargetPath -ine (Join-Path $AppRoot 'DragonCodexBoot.exe') -or $link.Arguments -ne '--launch') { throw ('Existing unrelated shortcut preserved: '+$path) }
        [pscustomobject]@{Shortcut=$path;Target=$link.TargetPath;Arguments=$link.Arguments;Icon=$link.IconLocation}
    }
} finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) }
Write-Output 'Codex shortcuts are ready. Existing registered Start pins are unchanged; pin this shortcut in their place.'
