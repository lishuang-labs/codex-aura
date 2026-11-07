$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
[void][Reflection.Assembly]::LoadFrom((Join-Path $repoRoot 'build/DragonCodexBoot/DragonCodexBoot.exe'))
$image = [DragonCodexBoot.PreviewScene]::Placeholder()
$encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($image))
$output = [IO.File]::Create((Join-Path $repoRoot 'assets/character-placeholder.png'))
try { $encoder.Save($output) } finally { $output.Dispose() }
