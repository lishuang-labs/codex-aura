param([ValidateSet('normal','fallback','late','timeout','vanish','resize','maximized','closing','dropped','restarting','activation-failure','missing','splash','splash-fallback','splash-timeout')][string[]]$Cases)
$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output=Join-Path $repoRoot 'build/DragonCodexBoot'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$argsList=@('/nologo','/target:exe','/platform:x64',('/out:'+(Join-Path $output 'LaunchWindowTests.exe')),('/reference:'+(Join-Path $output 'DragonCodexBoot.exe')),'/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll','/reference:System.Drawing.dll')
foreach ($name in @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll')) { $argsList+='/reference:'+(Join-Path $framework ('WPF/'+$name)) }
$argsList+=Join-Path $repoRoot 'tests/LaunchWindowTests.cs'
$argsList+='/win32manifest:'+(Join-Path $repoRoot 'src/app.manifest')
& (Join-Path $framework 'csc.exe') @argsList
if ($LASTEXITCODE -ne 0) { throw 'Window fixture compilation failed.' }
if ($Cases) { & (Join-Path $output 'LaunchWindowTests.exe') $repoRoot ($Cases -join ',') }
else { & (Join-Path $output 'LaunchWindowTests.exe') $repoRoot }
if ($LASTEXITCODE -ne 0) { throw 'Disposable window integration tests failed.' }
