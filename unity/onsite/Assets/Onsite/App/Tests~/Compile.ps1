param([string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.18f1', [switch]$ControlsOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$assets = Split-Path -Parent $taskRoot
$editorData = Join-Path $UnityEditor 'Editor\Data'
$runtime = Join-Path $editorData 'NetCoreRuntime\dotnet.exe'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$framework = Join-Path $editorData 'UnityReferenceAssemblies\unity-4.8-api'
$engine = Join-Path $editorData 'Managed\UnityEngine'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Unity 6000.3 Roslyn が見つかりません。-UnityEditor で指定してください。' }
$refs = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Runtime.Serialization.dll', 'System.Numerics.dll') | ForEach-Object { Join-Path $framework $_ }
$refs += Get-ChildItem -LiteralPath (Join-Path $framework 'Facades') -Filter '*.dll' | ForEach-Object FullName
$refs += Get-ChildItem -LiteralPath $engine -Filter 'UnityEngine*.dll' | ForEach-Object FullName
$sources = @(Get-ChildItem -LiteralPath $taskRoot -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notlike '*\Tests~\*' } | ForEach-Object FullName)
if ($ControlsOnly) { $sources = @($sources | Where-Object { $_ -like '*\Ui\*' -or $_ -like '*\AppFormat.cs' -or $_ -like '*\TrailStore.cs' }) }
$sources += Get-ChildItem -LiteralPath (Join-Path $assets 'Net') -Filter '*.cs' | ForEach-Object FullName
$sources += Get-ChildItem -LiteralPath (Join-Path $assets 'Runtime\Map') -Filter '*.cs' | Where-Object { $_.Name -ne 'MapWorld.cs' } | ForEach-Object FullName
$sources += Join-Path $PSScriptRoot 'MapWorldContract.cs'
$response = @('-nologo', '-target:library', '-langversion:9', '-nostdlib+', ('-out:"' + (Join-Path $PSScriptRoot 'UiCompileCheck.dll') + '"'))
$response += $refs | ForEach-Object { '-r:"' + $_ + '"' }
$response += $sources | ForEach-Object { '"' + $_ + '"' }
$responsePath = Join-Path $PSScriptRoot 'compile.rsp'
[System.IO.File]::WriteAllLines($responsePath, $response, [System.Text.UTF8Encoding]::new($false))
& $runtime $compiler ('@' + $responsePath)
if ($LASTEXITCODE -ne 0) { throw 'UI compile check failed.' }
Write-Output ('PASS: Unity 6000.3 runtime/API compilation (' + $sources.Count + ' sources). MapWorld uses a contract shim because Cesium packages have not been imported. Unity was not launched.')


