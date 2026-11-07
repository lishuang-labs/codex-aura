$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appRoot=Join-Path $repoRoot 'build/DragonCodexBoot'
$stage=Join-Path $repoRoot ('.local/preview-package-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage,(Join-Path $stage 'assets'),(Join-Path $stage 'docs'),(Join-Path $stage 'scripts'),(Join-Path $repoRoot 'dist') | Out-Null
Copy-Item -LiteralPath (Join-Path $appRoot 'DragonCodexBoot.exe') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'config/launcher.example.json') -Destination (Join-Path $stage 'launcher.json')
Copy-Item -LiteralPath (Join-Path $repoRoot 'Preview.cmd'),(Join-Path $repoRoot 'Launch.cmd'),(Join-Path $repoRoot 'LICENSE'),(Join-Path $repoRoot 'README.md'),(Join-Path $repoRoot 'THIRD_PARTY_NOTICES.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $repoRoot 'assets/character-placeholder.png') -Destination (Join-Path $stage 'assets')
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/PREVIEW.md'),(Join-Path $repoRoot 'docs/PREVIEW-TESTS.md'),(Join-Path $repoRoot 'docs/LAUNCH.md'),(Join-Path $repoRoot 'docs/ENTRYPOINTS.md') -Destination (Join-Path $stage 'docs')
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts/create-launch-shortcut.ps1'),(Join-Path $repoRoot 'scripts/install-codex-entry.ps1'),(Join-Path $repoRoot 'scripts/integrate-entrypoints.ps1') -Destination (Join-Path $stage 'scripts')
$zip=Join-Path $repoRoot 'dist/Codex-Aura-Stage2.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
($hash+'  '+[IO.Path]::GetFileName($zip)) | Set-Content -LiteralPath ($zip+'.sha256') -Encoding ASCII
Write-Output $zip
