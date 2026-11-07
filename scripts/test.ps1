$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
& (Join-Path $PSScriptRoot 'build.ps1')
$outputRoot=Join-Path $repoRoot 'build/DragonCodexBoot'
$exe=Join-Path $outputRoot 'DragonCodexBoot.exe'
$process=Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw 'Launcher self-test failed. See build/DragonCodexBoot/self-test-error.txt.' }
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$argsList=@('/nologo','/target:exe','/platform:x64',('/out:'+(Join-Path $outputRoot 'GeometryTests.exe')),('/reference:'+$exe),'/reference:System.Web.Extensions.dll',('/reference:'+(Join-Path $framework 'WPF/PresentationFramework.dll')),('/reference:'+(Join-Path $framework 'WPF/WindowsBase.dll')),(Join-Path $repoRoot 'tests/GeometryTests.cs'))
& (Join-Path $framework 'csc.exe') @argsList
if ($LASTEXITCODE -ne 0) { throw 'Test harness compilation failed.' }
& (Join-Path $outputRoot 'GeometryTests.exe') (Join-Path $repoRoot 'config/launcher.example.json')
if ($LASTEXITCODE -ne 0) { throw 'Geometry or configuration tests failed.' }
# Automated unit tests never operate shortcuts or the real client.
$argsList=@('/nologo','/target:exe','/platform:x64',('/out:'+(Join-Path $outputRoot 'PreviewTests.exe')),('/reference:'+$exe),'/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll')
foreach ($name in @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll')) { $argsList += '/reference:'+(Join-Path $framework ('WPF/'+$name)) }
$argsList += Join-Path $repoRoot 'tests/PreviewTests.cs'
& (Join-Path $framework 'csc.exe') @argsList
if ($LASTEXITCODE -ne 0) { throw 'Preview test compilation failed.' }
& (Join-Path $outputRoot 'PreviewTests.exe') $repoRoot
if ($LASTEXITCODE -ne 0) { throw 'Preview tests failed.' }
$argsList=@('/nologo','/target:exe','/platform:x64',('/out:'+(Join-Path $outputRoot 'LaunchTests.exe')),('/reference:'+$exe),'/reference:System.Web.Extensions.dll',(Join-Path $repoRoot 'tests/LaunchTests.cs'))
& (Join-Path $framework 'csc.exe') @argsList
if ($LASTEXITCODE -ne 0) { throw 'Launch test compilation failed.' }
& (Join-Path $outputRoot 'LaunchTests.exe') (Join-Path $repoRoot 'config/launcher.example.json')
if ($LASTEXITCODE -ne 0) { throw 'Launch tests failed.' }
$process=Start-Process -FilePath $exe -ArgumentList '--preview-frames' -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw 'Preview frame rendering failed.' }
Get-Content -LiteralPath (Join-Path $outputRoot 'qa/self-test.json') -Raw
