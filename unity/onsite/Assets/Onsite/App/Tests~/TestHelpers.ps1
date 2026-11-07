param([string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.18f1')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$assets = Split-Path -Parent $taskRoot
$editorData = Join-Path $UnityEditor 'Editor\Data'
$runtime = Join-Path $editorData 'NetCoreRuntime\dotnet.exe'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn\csc.dll'
$framework = Join-Path $editorData 'UnityReferenceAssemblies\unity-4.8-api'
$mono = Join-Path $editorData 'MonoBleedingEdge\bin\mono.exe'
$refs = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Web.Extensions.dll', 'System.Runtime.Serialization.dll') | ForEach-Object { Join-Path $framework $_ }
$sources = @((Join-Path $taskRoot 'TrailStore.cs'), (Join-Path $taskRoot 'AppFormat.cs'), (Join-Path $assets 'Net\Models.cs'), (Join-Path $assets 'Runtime\Map\GeoMath.cs'), (Join-Path $PSScriptRoot 'HelperContracts.cs'), (Join-Path $PSScriptRoot 'HelperTestStorage.cs'))
$output = Join-Path $PSScriptRoot 'HelperContracts.exe'
$args = @('-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib+', ('-out:' + $output))
$args += $refs | ForEach-Object { '-r:' + $_ }
$args += $sources
& $runtime $compiler @args
if ($LASTEXITCODE -ne 0) { throw 'Helper compile failed.' }
& $mono $output
if ($LASTEXITCODE -ne 0) { throw 'Helper contracts failed.' }
