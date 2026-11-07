[CmdletBinding()]
param([switch]$SourceOnly)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'public-files.ps1')
$files=@(Get-PublicSourceFiles $repoRoot)
Assert-PublicSourceFiles $repoRoot $files
Write-Output ('PASS: '+$files.Count+' public source files; no detected private media, output, credential markers or personal absolute paths.')
if ($SourceOnly) { return }
$zip=Join-Path $repoRoot 'dist/Codex-Aura-Preview-Stage1.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zip)
try {
    $expectedFiles=@('DragonCodexBoot.exe','launcher.json','Preview.cmd','LICENSE','README.md','THIRD_PARTY_NOTICES.md','assets/character-placeholder.png','docs/PREVIEW.md','docs/PREVIEW-TESTS.md')
    $actualFiles=@($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\','/') })
    if (@(Compare-Object $expectedFiles $actualFiles).Count) { throw 'Unexpected or missing public package entry.' }
    $reader=New-Object IO.StreamReader($archive.GetEntry('launcher.json').Open())
    try { $packedConfig=$reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($packedConfig.AutoReplaceEntrypoints -ne $false -or $packedConfig.ScanAllLocalDrives -ne $false -or $packedConfig.Volume -ne 0 -or
        $packedConfig.Preview.CharacterPath -ne 'assets/character-placeholder.png' -or $packedConfig.Preview.CharacterIsPlaceholder -ne $true) {
        throw 'Public package must be a muted placeholder preview.'
    }
    $stream=$archive.GetEntry('DragonCodexBoot.exe').Open()
    try { $exeHash=(Get-FileHash -InputStream $stream).Hash } finally { $stream.Dispose() }
    if ($exeHash -ne (Get-FileHash -LiteralPath (Join-Path $repoRoot 'build/DragonCodexBoot/DragonCodexBoot.exe')).Hash) { throw 'Packaged binary mismatch.' }
    $stream=$archive.GetEntry('assets/character-placeholder.png').Open()
    try { $imageHash=(Get-FileHash -InputStream $stream).Hash } finally { $stream.Dispose() }
    if ($imageHash -ne (Get-FileHash -LiteralPath (Join-Path $repoRoot 'assets/character-placeholder.png')).Hash) { throw 'Packaged placeholder mismatch.' }
} finally { $archive.Dispose() }
$expected=(Get-Content -LiteralPath ($zip+'.sha256') -Raw).Trim().Split(' ')[0]
if ($expected -ine (Get-FileHash -LiteralPath $zip).Hash) { throw 'Archive checksum mismatch.' }
Write-Output 'PASS: package allowlist, configuration, executable, placeholder and ZIP checksums.'
