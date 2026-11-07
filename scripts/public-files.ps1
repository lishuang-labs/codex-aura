# Shared read-only inventory; temporary Git metadata is confined to .local.
function Get-PublicSourceFiles([string]$RepoRoot) {
    $scratch=Join-Path $RepoRoot ('.local/source-audit-'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $scratch | Out-Null
    & git -c init.defaultBranch=main init --quiet $scratch
    if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize isolated inventory metadata.' }
    $gitDir=Join-Path $scratch '.git'
    $files=@(& git -c core.quotePath=false -c core.excludesFile=NUL --git-dir=$gitDir --work-tree=$RepoRoot ls-files --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to list public source candidates.' }
    if (Test-Path -LiteralPath (Join-Path $RepoRoot '.git')) {
        $files+=@(& git -c core.quotePath=false -C $RepoRoot ls-files --cached)
        if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect already tracked files.' }
    }
    @($files | Sort-Object -Unique)
}

function Assert-PublicSourceFiles([string]$RepoRoot,[string[]]$Files) {
    foreach ($name in $Files) {
        if ($name -match '(^|/)(build|dist|\.local|references|logs|qa|\.integration|bin|obj|\.build)/' -or
            $name -match '\.(mp4|mov|webm|mp3|wav|m4a|aac|ogg|flac|gif|jpe?g|ico|lnk|exe|dll|pdb|zip|7z|log|pem|key|p12|pfx)$' -or
            $name -match '(^|/)(launcher\.json|\.env(?:\..*)?|id_rsa.*|id_ed25519.*)$' -or
            ($name.StartsWith('assets/') -and $name -notin @('assets/README.md','assets/character-placeholder.png'))) {
            throw ('Excluded local or media file in public source: '+$name)
        }
        $path=Join-Path $RepoRoot $name
        if ((Get-Item -LiteralPath $path).Length -gt 2MB) { throw ('Review large source file: '+$name) }
        if ($name -eq 'assets/character-placeholder.png') { continue }
        if ($name.EndsWith('.png')) { throw ('Unreviewed image: '+$name) }
        $content=Get-Content -LiteralPath $path -Raw -Encoding UTF8
        # Report filenames only, never print matching secrets.
        if ($content -match 'gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{50,}|sk-(?:proj-)?[A-Za-z0-9_-]{30,}|AKIA[0-9A-Z]{16}' -or
            $content -match '-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----' -or
            $content -match '[A-Z]:[\\/]Users[\\/]|[A-Z]:[\\/]workspace[\\/]') {
            throw ('Potential credential or personal absolute path in: '+$name)
        }
    }
    $config=Get-Content -LiteralPath (Join-Path $RepoRoot 'config/launcher.example.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($config.AutoReplaceEntrypoints -ne $false -or $config.ScanAllLocalDrives -ne $false -or $config.Volume -ne 0 -or
        $config.Preview.CharacterPath -ne 'assets/character-placeholder.png' -or $config.Preview.CharacterIsPlaceholder -ne $true -or $config.Preview.AudioPath) {
        throw 'Public template must use a muted placeholder preview with integration disabled.'
    }
}
