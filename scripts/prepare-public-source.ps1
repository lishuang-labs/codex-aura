$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'public-files.ps1')
$files=@(Get-PublicSourceFiles $repoRoot)
Assert-PublicSourceFiles $repoRoot $files
$destination=Join-Path $repoRoot ('.local/public-source-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$manifest=@(foreach ($name in $files) {
    $target=Join-Path $destination $name
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $target
    [pscustomobject]@{Path=$name;Bytes=(Get-Item -LiteralPath $target).Length;SHA256=(Get-FileHash -LiteralPath $target).Hash}
})
$manifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $repoRoot '.local/public-source-manifest.json') -Encoding UTF8
$destination | Set-Content -LiteralPath (Join-Path $repoRoot '.local/public-source-path.txt') -Encoding UTF8
Write-Output ('Prepared '+$files.Count+' source files: '+$destination)
Write-Output 'No commit, remote operation or upload was performed.'
